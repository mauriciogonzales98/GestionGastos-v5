using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El índice único de `categoria` tiene que dejar convivir la unicidad con la baja lógica (D-01).
///
/// Se escribe contra el `DbContext` y no contra endpoints a propósito: lo que se verifica es el
/// **esquema**, no la validación de la aplicación. La validación de FR-005 rechaza el duplicado con
/// un mensaje legible y vive en `ValidacionDeLaCategoria`; esto de acá es la red de abajo, la que
/// sigue estando el día que alguien escriba una consulta que no pase por ella.
///
/// Los tres casos son los de la tabla de data-model.md, y los dos últimos fallan mientras el índice
/// siga siendo `(usuario_id, nombre, tipo)`: una fila dada de baja sigue ocupando su nombre y la
/// persona no puede volver a usarlo (FR-009).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class UnicidadDeCategoriasTests(BaseDeDatosFixture baseDeDatos)
{
    /// <summary>
    /// Ids fijos y altos, fuera del alcance del autoincremental y de las diez sembradas.
    ///
    /// Se fijan en vez de dejarlos crecer para que la limpieza pueda ser exacta: la base la comparte
    /// toda la suite y borrar "lo que haya quedado" es como no borrar nada.
    /// </summary>
    private const long Cuenta = 9910;

    private const int PrimeraId = 9911;
    private const int SegundaId = 9912;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Dos activas de la misma cuenta, con el mismo nombre y el mismo tipo, chocan.
    ///
    /// Es la mitad del índice que ya funcionaba y que la columna nueva no puede aflojar: si esto se
    /// pusiera en verde por el lado equivocado —el índice deja pasar todo—, los otros dos tests
    /// también pasarían y nadie lo notaría.
    /// </summary>
    [Fact]
    public async Task Dos_Activas_Homonimas_De_La_Misma_Cuenta_Chocan()
    {
        await PrepararAsync();

        try
        {
            await GuardarAsync(Categoria(PrimeraId, activa: true));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => GuardarAsync(Categoria(SegundaId, activa: true)));
        }
        finally
        {
            await LimpiarAsync();
        }
    }

    /// <summary>
    /// Una activa y una dada de baja con el mismo nombre y tipo conviven. Es FR-009: dar de baja
    /// "Gimnasio" y volver a crearlo tiene que poder hacerse.
    /// </summary>
    [Fact]
    public async Task Una_Dada_De_Baja_No_Le_Ocupa_El_Nombre_A_Una_Activa()
    {
        await PrepararAsync();

        try
        {
            await GuardarAsync(Categoria(PrimeraId, activa: false, discriminador: PrimeraId));
            await GuardarAsync(Categoria(SegundaId, activa: true));

            await using var contexto = _baseDeDatos.CrearContexto();
            Assert.Equal(2, await contexto.Categorias.CountAsync(c => c.UsuarioId == Cuenta));
        }
        finally
        {
            await LimpiarAsync();
        }
    }

    /// <summary>
    /// Varias dadas de baja homónimas conviven entre sí. Es el caso que obliga a que el
    /// discriminador sea el `Id` y no un simple `0/1`: con un booleano, la segunda baja chocaría
    /// contra la primera y la persona quedaría sin poder dar de baja dos veces el mismo nombre.
    /// </summary>
    [Fact]
    public async Task Dos_Dadas_De_Baja_Homonimas_Conviven()
    {
        await PrepararAsync();

        try
        {
            await GuardarAsync(Categoria(PrimeraId, activa: false, discriminador: PrimeraId));
            await GuardarAsync(Categoria(SegundaId, activa: false, discriminador: SegundaId));

            await using var contexto = _baseDeDatos.CrearContexto();
            Assert.Equal(2, await contexto.Categorias.CountAsync(c => c.UsuarioId == Cuenta));
        }
        finally
        {
            await LimpiarAsync();
        }
    }

    /// <summary>
    /// SC-005: la migración no le movió nada a las diez sembradas.
    ///
    /// Se compara el catálogo entero contra la lista literal —id, nombre y tipo, en orden— y no
    /// sólo la cantidad: diez filas siguen siendo diez aunque una haya cambiado de nombre, y ése es
    /// justo el daño que este test tiene que ver. El `discriminador` en 0 es lo que las deja
    /// compartiendo el casillero de las activas, que es donde tienen que estar.
    ///
    /// Es la comprobación de D-10: si algún día una migración toca la semilla, esto se pone en rojo
    /// antes de que alguien lo descubra mirando su propio selector.
    /// </summary>
    [Fact]
    public async Task Las_Diez_Predefinidas_Sobreviven_A_La_Migracion_SC005()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        var predefinidas = await contexto.Categorias
            .Where(c => c.UsuarioId == null)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Nombre, c.Tipo, c.Activa, c.Discriminador })
            .ToListAsync();

        Assert.Equal(
            [
                (1, "Comida", TipoMovimiento.Gasto),
                (2, "Transporte", TipoMovimiento.Gasto),
                (3, "Vivienda", TipoMovimiento.Gasto),
                (4, "Servicios", TipoMovimiento.Gasto),
                (5, "Salud", TipoMovimiento.Gasto),
                (6, "Ocio", TipoMovimiento.Gasto),
                (7, "Otros", TipoMovimiento.Gasto),
                (8, "Sueldo", TipoMovimiento.Ingreso),
                (9, "Ingreso extra", TipoMovimiento.Ingreso),
                (10, "Otros", TipoMovimiento.Ingreso),
            ],
            predefinidas.Select(c => (c.Id, c.Nombre, c.Tipo)));

        Assert.All(predefinidas, c =>
        {
            Assert.True(c.Activa, $"La predefinida {c.Id} ({c.Nombre}) quedó dada de baja.");
            Assert.Equal(0, c.Discriminador);
        });
    }

    private static Categoria Categoria(int id, bool activa, long discriminador = 0) => new()
    {
        Id = id,
        Nombre = "Gimnasio",
        Tipo = TipoMovimiento.Gasto,
        UsuarioId = Cuenta,
        Activa = activa,
        Discriminador = discriminador,
    };

    /// <summary>
    /// Deja la cuenta de prueba creada y sin categorías. Limpia ANTES de crear: una corrida
    /// interrumpida deja las filas puestas y el test siguiente fallaría por eso y no por el código.
    /// </summary>
    private async Task PrepararAsync()
    {
        await LimpiarAsync();

        await using var contexto = _baseDeDatos.CrearContexto();
        contexto.Usuarios.Add(new Usuario { Id = Cuenta, Email = "unicidad@gestiongastos.local" });
        await contexto.SaveChangesAsync();
    }

    private async Task LimpiarAsync()
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        await contexto.Categorias.Where(c => c.UsuarioId == Cuenta).ExecuteDeleteAsync();
        await contexto.Usuarios.Where(u => u.Id == Cuenta).ExecuteDeleteAsync();
    }

    private async Task GuardarAsync(Categoria categoria)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        contexto.Categorias.Add(categoria);
        await contexto.SaveChangesAsync();
    }
}

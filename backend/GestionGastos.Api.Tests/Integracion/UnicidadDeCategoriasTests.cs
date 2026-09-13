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
    /// SC-005, reformulado por la feature 013: **el catálogo que recibe una cuenta nueva** son las
    /// diez de siempre, activas y con el discriminador en cero.
    ///
    /// Hasta esta feature esto se comprobaba sobre las diez filas sembradas por la migración, que
    /// eran de todo el mundo. Ya no existen: cada cuenta recibe su copia al registrarse, así que lo
    /// que hay que vigilar se mudó del esquema al alta — y el daño que evita es el mismo, porque la
    /// migración de esta feature es justamente la que pudo haberlas tocado.
    ///
    /// Se compara el catálogo entero contra la lista literal —nombre y tipo, en orden— y no sólo la
    /// cantidad: diez filas siguen siendo diez aunque una haya cambiado de nombre, y ése es justo el
    /// daño que este test tiene que ver. El identificador queda afuera de la comparación porque
    /// ahora depende de cuántas cuentas se registraron antes en esa base.
    ///
    /// El `discriminador` en 0 es lo que las deja compartiendo el casillero de las activas, que es
    /// donde tienen que estar.
    /// </summary>
    [Fact]
    public async Task El_Catalogo_De_Una_Cuenta_Nueva_Son_Las_Diez_Activas_SC005()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await using var contexto = _baseDeDatos.CrearContexto();

        var suyas = await contexto.Categorias
            .Where(c => c.UsuarioId == cuenta.Id)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Nombre, c.Tipo, c.Activa, c.Discriminador })
            .ToListAsync();

        Assert.Equal(
            [
                ("Comida", TipoMovimiento.Gasto),
                ("Transporte", TipoMovimiento.Gasto),
                ("Vivienda", TipoMovimiento.Gasto),
                ("Servicios", TipoMovimiento.Gasto),
                ("Salud", TipoMovimiento.Gasto),
                ("Ocio", TipoMovimiento.Gasto),
                ("Otros", TipoMovimiento.Gasto),
                ("Sueldo", TipoMovimiento.Ingreso),
                ("Ingreso extra", TipoMovimiento.Ingreso),
                ("Otros", TipoMovimiento.Ingreso),
            ],
            suyas.Select(c => (c.Nombre, c.Tipo)));

        Assert.All(suyas, c =>
        {
            Assert.True(c.Activa, $"La categoría {c.Id} ({c.Nombre}) nació dada de baja.");
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

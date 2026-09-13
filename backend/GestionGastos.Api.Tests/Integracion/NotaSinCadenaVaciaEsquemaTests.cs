using System.Data.Common;
using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El esquema de `movimiento.nota` admite **una sola** forma de "sin nota": la ausencia de valor
/// (`FR-014` de 012-nota-del-movimiento).
///
/// Es la deuda **D12-08**, la única que la feature 012 creó y la única que nació aceptada a
/// sabiendas: sus *Clarifications* decidieron que el esquema no eligiera entre `NULL` y `''`, y
/// dejaron la invariante de `FR-005` sostenida por la lectura —`FR-011`, en `MovimientoDto`— en vez
/// de por el almacenamiento. La anotaron para el día que apareciera un camino de lectura nuevo.
///
/// **Lo que faltaba no era normalizar al escribir: eso ya se hacía.**
/// `ValidacionDelMovimiento.NotaNormalizada` convierte la cadena vacía y los espacios en ausencia de
/// valor desde la feature 012, así que la aplicación ya escribía una sola forma. Lo que no existía
/// era algo que lo **garantizara**: un `INSERT` con SQL puro, o el próximo camino de escritura que
/// se olvidara de llamar a esa normalización, metía un `''` sin protesta y creaba el segundo estado
/// que `FR-005` dice que no existe.
///
/// Va contra SQL directo y no contra la entidad de EF a propósito, igual que
/// <see cref="MonedaCodigoEsquemaTests"/> y <see cref="IntentoDeAccesoEsquemaTests"/>: lo que se
/// verifica es lo que la migración dejó en MySQL, no lo que el modelo de EF cree. Y es además el
/// único camino por el que el daño podía entrar, porque el de la aplicación ya estaba tapado.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class NotaSinCadenaVaciaEsquemaTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Un `INSERT` con la nota en cadena vacía **no entra**.
    ///
    /// Es la ruta por la que la deuda podía materializarse sin que ninguna validación de aplicación
    /// se enterara: la que no pasa por `NotaNormalizada`.
    /// </summary>
    [Fact]
    public async Task Un_Insert_Con_La_Nota_En_Cadena_Vacia_Es_Rechazado_FR014()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 11));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var gasto = await CatalogoDeCategorias.UnGastoAsync(_baseDeDatos, cuenta.Id);

        await using var contexto = _baseDeDatos.CrearContexto();

        // El `finally` limpia **aunque la aserción falle**, y no es ceremonia: mientras este test
        // esté en rojo —o sea, antes de que exista la restricción— el INSERT ENTRA, y la fila
        // inválida queda en la base compartida. Es la cicatriz que `MonedaCodigoEsquemaTests` ya
        // documentó y que se descubrió corriéndolo.
        try
        {
            await Assert.ThrowsAnyAsync<DbException>(() =>
                contexto.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha, nota)
                    VALUES ({cuenta.Id}, 0, 100.00, 1, {gasto}, '2026-09-11', '')"));
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM movimiento WHERE usuario_id = {cuenta.Id}");
        }
    }

    /// <summary>
    /// Un `UPDATE` que deja la nota en cadena vacía **no entra** tampoco.
    ///
    /// Sin este caso, la restricción podría existir y aun así dejar que una fila válida se volviera
    /// inválida después, que es el camino más fácil de pasar por alto: la nota se edita.
    /// </summary>
    [Fact]
    public async Task Un_Update_Que_Deja_La_Nota_En_Cadena_Vacia_Es_Rechazado_FR014()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 11));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var gasto = await CatalogoDeCategorias.UnGastoAsync(_baseDeDatos, cuenta.Id);

        await using var contexto = _baseDeDatos.CrearContexto();
        var movimiento = new Movimiento
        {
            UsuarioId = cuenta.Id,
            Tipo = TipoMovimiento.Gasto,
            Monto = 100m,
            MonedaId = 1,
            CategoriaId = gasto,
            Fecha = new DateOnly(2026, 9, 11),
            Nota = "una nota que existe",
        };
        contexto.Movimientos.Add(movimiento);
        await contexto.SaveChangesAsync();

        try
        {
            await Assert.ThrowsAnyAsync<DbException>(() =>
                contexto.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE movimiento SET nota = '' WHERE id = {movimiento.Id}"));
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM movimiento WHERE usuario_id = {cuenta.Id}");
        }
    }

    /// <summary>
    /// Las dos formas que SÍ son válidas siguen entrando: la ausencia de valor —que es "sin nota", y
    /// la única representación que queda— y una nota con texto.
    ///
    /// Sin este caso la restricción podría ser de más y nadie se enteraría hasta que un alta
    /// normal empezara a fallar. Una restricción que rechaza lo que tiene que rechazar **y** admite
    /// lo que tiene que admitir es lo único que prueba que está bien escrita.
    /// </summary>
    [Fact]
    public async Task La_Ausencia_De_Valor_Y_Una_Nota_Con_Texto_Siguen_Entrando_FR014()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 11));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var gasto = await CatalogoDeCategorias.UnGastoAsync(_baseDeDatos, cuenta.Id);

        await using var contexto = _baseDeDatos.CrearContexto();

        try
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha, nota)
                VALUES ({cuenta.Id}, 0, 100.00, 1, {gasto}, '2026-09-11', NULL)");
            await contexto.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha, nota)
                VALUES ({cuenta.Id}, 0, 100.00, 1, {gasto}, '2026-09-11', 'una nota que existe')");

            var cuantos = await contexto.Movimientos
                .CountAsync(m => m.UsuarioId == cuenta.Id);

            Assert.Equal(2, cuantos);
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM movimiento WHERE usuario_id = {cuenta.Id}");
        }
    }
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **La base rechaza por sí misma un movimiento clasificado con la categoría de otra cuenta**
/// (`FR-006`, `FR-009`, `SC-002` de la feature 013). Es la deuda **D7-07** saldada.
///
/// Va contra SQL directo y no contra la API, igual que <see cref="MonedaCodigoEsquemaTests"/> y
/// <see cref="NotaSinCadenaVaciaEsquemaTests"/>, y por el motivo que le da sentido a la feature: lo
/// que la aplicación rechaza ya está probado —`AislamientoDeCategoriasTests` lo cubre desde la 007—
/// y lo que faltaba era que la regla siguiera siendo cierta para la escritura que **no** pasa por la
/// aplicación: un script de mantenimiento, una importación, un arreglo a mano en la base.
///
/// **Por qué esta restricción no se podía poner antes.** Mientras "predefinida" significó
/// `usuario_id IS NULL`, la foránea compuesta rechazaba las diez predefinidas con un `ERROR 1452`:
/// una fila padre con `NULL` en la clave referenciada no puede ser referenciada por nadie. No rompía
/// el ataque, rompía el uso normal. Lo que la hizo posible no fue la restricción sino que toda
/// categoría pasara a tener dueño.
///
/// Las dos comprobaciones de `MovimientosEndpoints` **se conservan** (`FR-007`): la foránea es el
/// piso, no el reemplazo. Quien usa la aplicación tiene que seguir recibiendo el error de validación
/// con su campo, y no el fallo crudo de una restricción.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class AmbitoDeCategoriaEsquemaTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Fecha = new(2026, 9, 13);

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// `FR-006` y `SC-002`: un `INSERT` directo de un movimiento de A con una categoría de B **no
    /// entra**.
    /// </summary>
    [Fact]
    public async Task Un_Insert_Con_Una_Categoria_Ajena_Es_Rechazado_FR006_SC002()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Fecha);
        using var a = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var b = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deB = await CatalogoDeCategorias.UnGastoAsync(_baseDeDatos, b.Id);

        await using var contexto = _baseDeDatos.CrearContexto();

        // El `finally` limpia **aunque la aserción falle**, y no es ceremonia: mientras este test
        // esté en rojo —o sea, antes de que exista la restricción— el INSERT ENTRA, y la fila
        // inválida queda en la base que comparte toda la suite. Es la cicatriz que
        // `NotaSinCadenaVaciaEsquemaTests` ya documentó.
        try
        {
            await Assert.ThrowsAnyAsync<DbException>(() =>
                contexto.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha)
                    VALUES ({a.Id}, 0, 100.00, 1, {deB}, {Fecha:yyyy-MM-dd})
                    """));
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM movimiento WHERE usuario_id = {a.Id}");
        }
    }

    /// <summary>
    /// `FR-006` en el `UPDATE`: mover un movimiento existente a la categoría de otra cuenta tampoco
    /// entra.
    ///
    /// Sin este caso, la restricción podría existir y aun así dejar que una fila válida se volviera
    /// inválida después — el camino más fácil de pasar por alto, y el que un script de mantenimiento
    /// toma con más naturalidad que un `INSERT`.
    /// </summary>
    [Fact]
    public async Task Un_Update_A_Una_Categoria_Ajena_Es_Rechazado_FR006_SC002()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Fecha);
        using var a = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var b = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deA = await CatalogoDeCategorias.UnGastoAsync(_baseDeDatos, a.Id);
        var deB = await CatalogoDeCategorias.UnGastoAsync(_baseDeDatos, b.Id);

        await using var contexto = _baseDeDatos.CrearContexto();

        try
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha)
                VALUES ({a.Id}, 0, 100.00, 1, {deA}, {Fecha:yyyy-MM-dd})
                """);

            await Assert.ThrowsAnyAsync<DbException>(() =>
                contexto.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE movimiento SET categoria_id = {deB} WHERE usuario_id = {a.Id}"));

            // Y siguió con la suya: un UPDATE rechazado que igual movió algo sería peor que ninguno.
            var categoria = await contexto.Movimientos
                .Where(m => m.UsuarioId == a.Id)
                .Select(m => m.CategoriaId)
                .SingleAsync();

            Assert.Equal(deA, categoria);
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM movimiento WHERE usuario_id = {a.Id}");
        }
    }

    /// <summary>
    /// `FR-009`, la otra mitad: un `INSERT` con una categoría **propia** sí entra.
    ///
    /// **Hoy ya pasa, y por eso está.** Una restricción que rechaza lo que tiene que rechazar y
    /// además rechaza lo que tiene que admitir no se distingue de una correcta mirando sólo los
    /// casos que fallan. El rojo de esta tanda tiene que venir de los dos tests de arriba; si viene
    /// de éste, la restricción está de más.
    /// </summary>
    [Fact]
    public async Task Un_Insert_Con_Una_Categoria_Propia_Es_Aceptado_FR009_SC002()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Fecha);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var suya = await CatalogoDeCategorias.UnGastoAsync(_baseDeDatos, cuenta.Id);

        await using var contexto = _baseDeDatos.CrearContexto();

        try
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha)
                VALUES ({cuenta.Id}, 0, 100.00, 1, {suya}, {Fecha:yyyy-MM-dd})
                """);

            Assert.Equal(1, await contexto.Movimientos.CountAsync(m => m.UsuarioId == cuenta.Id));
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM movimiento WHERE usuario_id = {cuenta.Id}");
        }
    }

    /// <summary>
    /// `movimiento.usuario_id` participa de **dos** claves foráneas a la vez: la que ya tenía contra
    /// `usuario` y la nueva contra `categoria`.
    ///
    /// **Research D-01 lo marcó como "hay que probarlo, no suponerlo"**, y por eso este test existe:
    /// el diseño entero se apoya en que MySQL admita que una columna sea parte de dos foráneas, y
    /// darlo por hecho sería exactamente la clase de premisa sin medir que este proyecto ya se
    /// encontró tres veces.
    ///
    /// Se lee del catálogo del motor y no del modelo de EF: lo que importa es lo que la migración
    /// dejó puesto en MySQL.
    /// </summary>
    [Fact]
    public async Task El_Usuario_Del_Movimiento_Participa_De_Dos_Foraneas_D01()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        var destinos = await ForaneasDeUsuarioIdAsync(contexto);

        Assert.Contains("usuario", destinos);
        Assert.Contains("categoria", destinos);
    }

    /// <summary>
    /// Las tablas a las que apunta alguna foránea que incluya <c>movimiento.usuario_id</c>.
    /// </summary>
    private static async Task<List<string>> ForaneasDeUsuarioIdAsync(DbContext contexto)
    {
        await contexto.Database.OpenConnectionAsync();
        await using var comando = contexto.Database.GetDbConnection().CreateCommand();

        comando.CommandText = """
            SELECT DISTINCT k.REFERENCED_TABLE_NAME
            FROM information_schema.KEY_COLUMN_USAGE k
            WHERE k.TABLE_SCHEMA = DATABASE()
              AND k.TABLE_NAME = 'movimiento'
              AND k.COLUMN_NAME = 'usuario_id'
              AND k.REFERENCED_TABLE_NAME IS NOT NULL
            """;

        await using var lector = await comando.ExecuteReaderAsync();

        var destinos = new List<string>();
        while (await lector.ReadAsync())
        {
            destinos.Add(lector.GetString(0));
        }

        return destinos;
    }
}

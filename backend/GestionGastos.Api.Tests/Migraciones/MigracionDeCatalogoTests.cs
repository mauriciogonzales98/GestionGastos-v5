using System.Data.Common;
using System.Globalization;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GestionGastos.Api.Tests.Migraciones;

/// <summary>
/// **La migración de datos de la feature 013** (FR-010 a FR-014, SC-003, SC-004): las diez
/// categorías compartidas se convierten en una copia por cuenta, y los movimientos que las usaban
/// pasan a apuntar a la copia de su propio dueño.
///
/// Es la única parte irreversible de la feature, así que el test **fabrica el estado anterior** en
/// vez de suponerlo: baja el esquema a la migración previa, siembra con SQL crudo, y lo vuelve a
/// subir. Es la misma técnica que <see cref="MigracionDeCuentasTests"/>, y por el mismo motivo — una
/// vez migrada la base no queda estado inicial que reproduzca el escenario.
///
/// **SQL crudo y no el modelo de EF**: el modelo de hoy dice que <c>categoria.usuario_id</c> es
/// obligatorio, y el esquema al que se baja admite el nulo que justamente hay que sembrar. Usar el
/// modelo fallaría por una razón que no es la que se está verificando.
///
/// Va en la colección compartida a propósito: mover el esquema mientras otro test lo usa es la
/// interferencia que el Principio IV prohíbe, y la colección serializa. Corre contra
/// <c>gestiongastos_test</c> como el resto de la suite: la segunda base de la lista blanca del
/// fixture no la usa nadie porque el usuario de MySQL del proyecto no puede crear una tercera.
///
/// **El caso que de verdad importa es la propia dada de baja homónima de una predefinida.** Es el
/// que un reapuntado por nombre rompería en silencio: el <c>JOIN</c> encontraría dos filas
/// candidatas y mandaría el movimiento a la equivocada, sin error y sin que ningún total avisara
/// (research D-03). Por eso la migración empareja por identidad y por eso este test siembra ese
/// caso.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class MigracionDeCatalogoTests(BaseDeDatosFixture baseDeDatos)
{
    /// <summary>La migración justo anterior a la de esta feature.</summary>
    private const string MigracionAnterior = "NotaSinCadenaVacia";

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// FR-010, FR-013 y FR-014: cada cuenta queda con sus diez copias, las propias quedan intactas
    /// y ninguna categoría queda sin dueño.
    /// </summary>
    [Fact]
    public async Task Cada_Cuenta_Queda_Con_Sus_Diez_Y_Ninguna_Sin_Dueno_FR010_FR013_FR014_SC004()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await using var contexto = _baseDeDatos.CrearContexto();
        var migrador = contexto.Database.GetService<IMigrator>();

        try
        {
            var sembrado = await SembrarElEstadoAnteriorAsync(contexto, migrador);

            // El hecho que se mide.
            await migrador.MigrateAsync();

            // FR-014 / SC-004: ninguna categoría sin dueño.
            Assert.Equal(0, await EscalarAsync(
                contexto, "SELECT COUNT(*) FROM categoria WHERE usuario_id IS NULL"));

            // FR-010: las diez, por cuenta. La cuenta A además conserva sus dos propias.
            Assert.Equal(12, await ContarCategoriasDeAsync(contexto, sembrado.CuentaA));
            Assert.Equal(10, await ContarCategoriasDeAsync(contexto, sembrado.CuentaB));

            // Y son las diez del catálogo, no diez filas cualesquiera: siete de gasto y tres de
            // ingreso entre las que la cuenta B recibió.
            Assert.Equal(7, await EscalarAsync(
                contexto,
                $"SELECT COUNT(*) FROM categoria WHERE usuario_id = {sembrado.CuentaB} AND tipo = 0"));
            Assert.Equal(3, await EscalarAsync(
                contexto,
                $"SELECT COUNT(*) FROM categoria WHERE usuario_id = {sembrado.CuentaB} AND tipo = 1"));

            // FR-013: las propias que ya existían, intactas — mismo id, mismo nombre, mismo tipo,
            // mismo estado de baja. Se compara fila por fila y no sólo "siguen estando".
            Assert.Equal(
                sembrado.Propias,
                await FilasAsync(
                    contexto,
                    $"""
                    SELECT CONCAT_WS('|', id, nombre, tipo, activa + 0)
                    FROM categoria
                    WHERE id IN ({sembrado.GimnasioDeA}, {sembrado.ComidaDadaDeBajaDeA})
                    ORDER BY id
                    """));

            // FR-014: ningún movimiento quedó sin categoría ni fuera de su ámbito.
            Assert.Equal(0, await EscalarAsync(
                contexto,
                """
                SELECT COUNT(*)
                FROM movimiento m JOIN categoria c ON c.id = m.categoria_id
                WHERE c.usuario_id <> m.usuario_id
                """));
        }
        finally
        {
            await RestaurarAsync(migrador);
        }
    }

    /// <summary>
    /// FR-011: un movimiento que apuntaba a una compartida queda apuntando a la copia **de su
    /// propio dueño**, con el mismo nombre y el mismo tipo.
    ///
    /// **Y no a la propia dada de baja homónima.** Ése es el caso que separa un reapuntado por
    /// identidad de uno por nombre, y el que este test existe para fijar.
    /// </summary>
    [Fact]
    public async Task Cada_Movimiento_Queda_Con_La_Copia_De_Su_Dueno_FR011()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await using var contexto = _baseDeDatos.CrearContexto();
        var migrador = contexto.Database.GetService<IMigrator>();

        try
        {
            var sembrado = await SembrarElEstadoAnteriorAsync(contexto, migrador);

            await migrador.MigrateAsync();

            // Cada movimiento, con el dueño de su categoría y el nombre de esa categoría.
            Assert.Equal(
                [
                    // Los dos que apuntaban a la "Comida" compartida quedaron en la copia de SU dueño.
                    $"{sembrado.CuentaA}|Comida|0|1",
                    // El que ya apuntaba a una propia activa no se movió.
                    $"{sembrado.CuentaA}|Gimnasio|0|1",
                    // El que apuntaba a la propia DADA DE BAJA sigue apuntando a ella: no se lo
                    // llevó la copia homónima. Es el caso de research D-03.
                    $"{sembrado.CuentaA}|Comida|0|0",
                    $"{sembrado.CuentaB}|Comida|0|1",
                    $"{sembrado.CuentaB}|Sueldo|1|1",
                ],
                await FilasAsync(
                    contexto,
                    """
                    SELECT CONCAT_WS('|', c.usuario_id, c.nombre, c.tipo, c.activa + 0)
                    FROM movimiento m JOIN categoria c ON c.id = m.categoria_id
                    ORDER BY m.id
                    """));
        }
        finally
        {
            await RestaurarAsync(migrador);
        }
    }

    /// <summary>
    /// FR-012 y SC-003: ni un total, ni un balance, ni una línea del desglose se mueven.
    ///
    /// Se compara la **misma foto** antes y después, sobre la misma base. El desglose se agrupa por
    /// nombre y tipo de categoría y no por su identificador: la migración cambia los identificadores
    /// a propósito, así que una foto por id daría distinto por construcción sin que ningún número se
    /// hubiera movido. Lo que FR-012 promete que no cambia es la plata que suma cada categoría.
    /// </summary>
    [Fact]
    public async Task La_Migracion_No_Mueve_Un_Solo_Numero_FR012_SC003()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await using var contexto = _baseDeDatos.CrearContexto();
        var migrador = contexto.Database.GetService<IMigrator>();

        try
        {
            await SembrarElEstadoAnteriorAsync(contexto, migrador);

            var antes = await FotoDeTotalesAsync(contexto);
            var desgloseAntes = await FotoDelDesgloseAsync(contexto);

            // Que la foto no esté vacía: comparar dos listas vacías pasa en verde sin verificar nada.
            Assert.NotEmpty(antes);
            Assert.NotEmpty(desgloseAntes);

            await migrador.MigrateAsync();

            Assert.Equal(antes, await FotoDeTotalesAsync(contexto));
            Assert.Equal(desgloseAntes, await FotoDelDesgloseAsync(contexto));
        }
        finally
        {
            await RestaurarAsync(migrador);
        }
    }

    /// <summary>
    /// **Un fallo de la migración no deja la base a medias, y se puede reintentar** (`FR-014`).
    ///
    /// El fallo se fabrica con el único caso que puede producirlo de verdad: una cuenta con una
    /// categoría propia **activa** homónima de una predefinida. El esquema anterior lo permitía
    /// —para MySQL `usuario_id NULL` y `usuario_id 7` son claves distintas, así que el índice único
    /// las dejaba convivir (D-02 de la 007)— y la copia que la migración crea choca contra ella.
    ///
    /// **Lo que este test fija no es que falle, sino cómo queda la base después.** Un fallo tiene
    /// que dejarla exactamente como estaba: sin copias a medias, sin columnas de trabajo puestas y
    /// —sobre todo— en condiciones de volver a intentarlo una vez corregido el dato. Es lo que
    /// separa "falla" de "se rompe".
    ///
    /// **Y no se puede dar por supuesto por estar dentro de una migración.** Medido el 2026-09-13:
    /// un `ALTER TABLE` confirma la transacción de forma implícita y la termina, así que todo lo que
    /// venga después queda fuera de ella y un `ROLLBACK` posterior no lo deshace. Una migración que
    /// mezcle DDL con pasos de datos **no es atómica**, diga lo que diga el comentario que tenga
    /// encima.
    /// </summary>
    [Fact]
    public async Task Un_Fallo_No_Deja_La_Base_A_Medias_Y_Se_Puede_Reintentar_FR014()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await using var contexto = _baseDeDatos.CrearContexto();
        var migrador = contexto.Database.GetService<IMigrator>();

        try
        {
            var sembrado = await SembrarElEstadoAnteriorAsync(contexto, migrador);

            // La propia ACTIVA homónima de una predefinida: el caso que hace fallar el alta de las
            // copias. Se agrega sobre el estado que el sembrado ya dejó.
            await EjecutarAsync(contexto, $"""
                INSERT INTO categoria (nombre, tipo, usuario_id, activa, discriminador)
                VALUES ('Transporte', 0, {sembrado.CuentaA}, 1, 0)
                """);

            var compartidasAntes = await EscalarAsync(
                contexto, "SELECT COUNT(*) FROM categoria WHERE usuario_id IS NULL");
            var deLaCuentaAntes = await ContarCategoriasDeAsync(contexto, sembrado.CuentaA);

            // 1 · La migración falla. Que falle está bien: FR-014 pide fallar antes que completarse
            //     a medias.
            await Assert.ThrowsAnyAsync<Exception>(() => migrador.MigrateAsync());

            // 2 · Y la base quedó como estaba. Las tres cosas que un fallo no puede dejar puestas:
            Assert.Equal(
                compartidasAntes,
                await EscalarAsync(contexto, "SELECT COUNT(*) FROM categoria WHERE usuario_id IS NULL"));

            Assert.Equal(deLaCuentaAntes, await ContarCategoriasDeAsync(contexto, sembrado.CuentaA));

            Assert.Equal(
                0,
                await EscalarAsync(contexto, """
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'categoria'
                      AND COLUMN_NAME = 'migracion_origen_id'
                    """));

            // 3 · Corregido el dato que la hacía fallar, el reintento entra.
            await EjecutarAsync(
                contexto,
                $"DELETE FROM categoria WHERE usuario_id = {sembrado.CuentaA} AND nombre = 'Transporte'");

            await migrador.MigrateAsync();

            Assert.Equal(0, await EscalarAsync(
                contexto, "SELECT COUNT(*) FROM categoria WHERE usuario_id IS NULL"));
        }
        finally
        {
            await RestaurarAsync(migrador);
        }
    }

    /// <summary>
    /// Baja el esquema a la migración anterior y siembra el estado de antes de la feature.
    ///
    /// Lo que fabrica: dos cuentas, las diez compartidas —que vuelve a poner el <c>Down()</c>—, una
    /// propia activa, una propia dada de baja **homónima de una predefinida**, y movimientos
    /// apuntando a las dos clases.
    /// </summary>
    private static async Task<EstadoSembrado> SembrarElEstadoAnteriorAsync(
        DbContext contexto, IMigrator migrador)
    {
        await migrador.MigrateAsync(MigracionAnterior);

        // Las diez compartidas tienen que estar: es el estado del que parte la migración. Si el
        // `Down()` no las repuso, este test no está verificando lo que dice.
        Assert.Equal(10, await EscalarAsync(
            contexto, "SELECT COUNT(*) FROM categoria WHERE usuario_id IS NULL"));

        await EjecutarAsync(contexto, """
            INSERT INTO usuario (email, contrasena_hash)
            VALUES ('migracion-a@ejemplo.local', 'x'), ('migracion-b@ejemplo.local', 'x')
            """);

        var cuentaA = await EscalarAsync(
            contexto, "SELECT id FROM usuario WHERE email = 'migracion-a@ejemplo.local'");
        var cuentaB = await EscalarAsync(
            contexto, "SELECT id FROM usuario WHERE email = 'migracion-b@ejemplo.local'");

        await EjecutarAsync(contexto, $"""
            INSERT INTO categoria (nombre, tipo, usuario_id, activa, discriminador)
            VALUES ('Gimnasio', 0, {cuentaA}, 1, 0)
            """);
        var gimnasio = await EscalarAsync(
            contexto, $"SELECT id FROM categoria WHERE usuario_id = {cuentaA} AND nombre = 'Gimnasio'");

        // La propia DADA DE BAJA homónima de una predefinida. El discriminador lleva su propio id,
        // que es lo que deja convivir la baja con la activa del mismo nombre (FR-009 de la 007).
        await EjecutarAsync(contexto, $"""
            INSERT INTO categoria (nombre, tipo, usuario_id, activa, discriminador)
            VALUES ('Comida', 0, {cuentaA}, 0, 0)
            """);
        var comidaDeBaja = await EscalarAsync(
            contexto,
            $"SELECT id FROM categoria WHERE usuario_id = {cuentaA} AND nombre = 'Comida' AND activa = 0");
        await EjecutarAsync(
            contexto,
            $"UPDATE categoria SET discriminador = {comidaDeBaja} WHERE id = {comidaDeBaja}");

        // Las compartidas por nombre y tipo, no por número: el id de "Comida" es un detalle de la
        // semilla y no algo que este test tenga que saber.
        var comidaCompartida = await EscalarAsync(
            contexto,
            "SELECT id FROM categoria WHERE usuario_id IS NULL AND nombre = 'Comida' AND tipo = 0");
        var sueldoCompartido = await EscalarAsync(
            contexto,
            "SELECT id FROM categoria WHERE usuario_id IS NULL AND nombre = 'Sueldo' AND tipo = 1");

        await EjecutarAsync(contexto, $"""
            INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha)
            VALUES ({cuentaA}, 0, 1000.00, 1, {comidaCompartida}, '2026-08-10'),
                   ({cuentaA}, 0,  500.00, 1, {gimnasio},         '2026-08-11'),
                   ({cuentaA}, 0,  250.00, 1, {comidaDeBaja},     '2026-08-12'),
                   ({cuentaB}, 0,  700.00, 1, {comidaCompartida}, '2026-08-13'),
                   ({cuentaB}, 1, 3000.00, 1, {sueldoCompartido}, '2026-08-14')
            """);

        var propias = await FilasAsync(
            contexto,
            $"""
            SELECT CONCAT_WS('|', id, nombre, tipo, activa + 0)
            FROM categoria
            WHERE id IN ({gimnasio}, {comidaDeBaja})
            ORDER BY id
            """);

        return new EstadoSembrado(cuentaA, cuentaB, gimnasio, comidaDeBaja, propias);
    }

    /// <summary>
    /// Deja el esquema al día y la base limpia, pase lo que pase.
    ///
    /// Si este test muriera dejando la base una migración atrás, todos los que corren después
    /// fallarían por su culpa y no por la suya — la peor forma de romper una suite.
    /// </summary>
    private async Task RestaurarAsync(IMigrator migrador)
    {
        // **Se limpia ANTES de migrar, y ése es todo el punto del orden.**
        //
        // Limpiar después parece igual y no lo es: si el test murió con un dato que hace fallar la
        // migración —que es justamente lo que uno de estos tests fabrica a propósito—, el
        // `MigrateAsync` de la restauración falla por el mismo motivo, la base queda en el esquema
        // viejo, y **los cien tests siguientes fallan por algo que no es suyo**. Pasó de verdad
        // mientras se escribía esto.
        //
        // Lo mismo con la columna de trabajo: una migración que mezcla DDL con pasos de datos puede
        // dejarla puesta al fallar —el `ALTER TABLE` confirma la transacción y la termina—, y
        // entonces el reintento muere con `Duplicate column name`. Con la migración escrita como
        // corresponde esto no encuentra nada; está por si vuelve a escribirse mal.
        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            var quedo = await EscalarAsync(contexto, """
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'categoria'
                  AND COLUMN_NAME = 'migracion_origen_id'
                """);

            if (quedo > 0)
            {
                await EjecutarAsync(
                    contexto, "ALTER TABLE categoria DROP COLUMN migracion_origen_id");
            }

            // Con SQL crudo y no por el modelo: el esquema al que se puede haber quedado la base
            // admite el `usuario_id` nulo que el modelo de hoy dice que no existe.
            await EjecutarAsync(contexto, "DELETE FROM movimiento");
            await EjecutarAsync(contexto, "DELETE FROM categoria WHERE usuario_id IS NOT NULL");
            await EjecutarAsync(contexto, "DELETE FROM usuario");
        }

        await migrador.MigrateAsync();
        await _baseDeDatos.LimpiarCuentasAsync();
    }

    /// <summary>
    /// Total ingresado, gastado y balance por cuenta, período y moneda.
    ///
    /// El agregado se arma adentro y el texto comparable afuera: con el <c>CONCAT_WS</c> en el mismo
    /// <c>SELECT</c> del <c>GROUP BY</c>, MySQL no reconoce el <c>DATE_FORMAT</c> como la misma
    /// expresión por la que se agrupa y <c>only_full_group_by</c> lo rechaza.
    /// </summary>
    private static Task<List<string>> FotoDeTotalesAsync(DbContext contexto) =>
        FilasAsync(contexto, """
            SELECT CONCAT_WS('|', t.cuenta, t.periodo, t.moneda, t.ingresado, t.gastado, t.balance)
            FROM (
                SELECT
                    m.usuario_id AS cuenta,
                    DATE_FORMAT(m.fecha, '%Y-%m') AS periodo,
                    mo.codigo AS moneda,
                    SUM(CASE WHEN m.tipo = 1 THEN m.monto ELSE 0 END) AS ingresado,
                    SUM(CASE WHEN m.tipo = 0 THEN m.monto ELSE 0 END) AS gastado,
                    SUM(CASE WHEN m.tipo = 1 THEN m.monto ELSE -m.monto END) AS balance
                FROM movimiento m JOIN moneda mo ON mo.id = m.moneda_id
                GROUP BY m.usuario_id, DATE_FORMAT(m.fecha, '%Y-%m'), mo.codigo
            ) t
            ORDER BY t.cuenta, t.periodo, t.moneda
            """);

    /// <summary>El desglose de gastos, por nombre y tipo de categoría.</summary>
    private static Task<List<string>> FotoDelDesgloseAsync(DbContext contexto) =>
        FilasAsync(contexto, """
            SELECT CONCAT_WS('|', t.cuenta, t.periodo, t.moneda, t.categoria, t.tipo, t.total)
            FROM (
                SELECT
                    m.usuario_id AS cuenta,
                    DATE_FORMAT(m.fecha, '%Y-%m') AS periodo,
                    mo.codigo AS moneda,
                    c.nombre AS categoria,
                    c.tipo AS tipo,
                    SUM(m.monto) AS total
                FROM movimiento m
                JOIN moneda mo ON mo.id = m.moneda_id
                JOIN categoria c ON c.id = m.categoria_id
                WHERE m.tipo = 0
                GROUP BY m.usuario_id, DATE_FORMAT(m.fecha, '%Y-%m'), mo.codigo, c.nombre, c.tipo
            ) t
            ORDER BY t.cuenta, t.periodo, t.moneda, t.categoria, t.tipo
            """);

    private static Task<long> ContarCategoriasDeAsync(DbContext contexto, long usuarioId) =>
        EscalarAsync(contexto, $"SELECT COUNT(*) FROM categoria WHERE usuario_id = {usuarioId}");

    // El SQL de estos tres helpers se arma en tiempo de ejecución y CA2100 lo marca con razón como
    // regla general. Acá los únicos valores que se interpolan son identificadores que la propia
    // clase acaba de leer de la base —nunca entrada externa— y el texto sale de literales de este
    // archivo. Se silencia en el punto exacto, con el motivo, en vez de apagar la regla.
    private static async Task<long> EscalarAsync(DbContext contexto, string sql)
    {
        await using var comando = await CrearComandoAsync(contexto, sql);

        return Convert.ToInt64(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<List<string>> FilasAsync(DbContext contexto, string sql)
    {
        await using var comando = await CrearComandoAsync(contexto, sql);
        await using var lector = await comando.ExecuteReaderAsync();

        var filas = new List<string>();
        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetString(0));
        }

        return filas;
    }

    private static async Task EjecutarAsync(DbContext contexto, string sql)
    {
        await using var comando = await CrearComandoAsync(contexto, sql);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<DbCommand> CrearComandoAsync(DbContext contexto, string sql)
    {
        await contexto.Database.OpenConnectionAsync();
        var comando = contexto.Database.GetDbConnection().CreateCommand();
#pragma warning disable CA2100 // Sólo identificadores leídos de la base; el texto es literal de esta clase.
        comando.CommandText = sql;
#pragma warning restore CA2100

        return comando;
    }

    /// <summary>Lo que el sembrado dejó puesto, para que los asertos no lo vuelvan a buscar.</summary>
    private sealed record EstadoSembrado(
        long CuentaA,
        long CuentaB,
        long GimnasioDeA,
        long ComidaDadaDeBajaDeA,
        List<string> Propias);
}

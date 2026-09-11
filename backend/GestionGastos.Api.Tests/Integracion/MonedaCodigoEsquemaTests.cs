using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El esquema de `moneda.codigo` exige **tres letras** (`FR-010` de 012-nota-del-movimiento).
///
/// Es la deuda **D11-02**, que nació como D9-09 en la feature 009 y pasó por D10-03 y D11-02
/// esperando siempre lo mismo: un ticket que abriera una migración por otro motivo. La 012 la abre
/// para la nota del movimiento, y con el plan DISC-001 terminándose ahí **ya no había un próximo
/// ticket al que apuntarle la deuda**.
///
/// Va contra SQL directo y no contra la entidad de EF a propósito, igual que
/// <see cref="IntentoDeAccesoEsquemaTests"/>: lo que se verifica es lo que la migración dejó en
/// MySQL, no lo que el modelo de EF cree. Y además es el único camino por el que el daño podía
/// entrar — **el catálogo se administra como dato** (`PRD:RF-32`), así que nadie lo escribe desde la
/// aplicación y no hay ninguna validación de aplicación que pudiera atraparlo. Un test que pasara
/// por el `DbSet` estaría probando una ruta que en producción no existe.
///
/// Por eso tampoco se agrega validación en el código: darle a la aplicación una responsabilidad
/// sobre una tabla que no escribe sería inventar un guardarraíl sin llamador.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class MonedaCodigoEsquemaTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Un código que no son tres letras **no entra**.
    ///
    /// Los tres casos cubren las tres formas de incumplir: un dígito en el medio, un dígito al final
    /// y un largo menor. El `char(3)` que existe desde la migración `Inicial` ya acotaba el largo
    /// —de ahí que `A1` falle por dos motivos a la vez—, así que **lo que esta restricción agrega es
    /// que sean letras**, que es lo que los dos primeros casos verifican.
    ///
    /// Sin esto, un `1X2` metido con SQL puro entraba sin protesta, viajaba en el contrato y llegaba
    /// hasta el formateo del monto, que es el cuarto lugar donde esta deuda se podía cruzar y la
    /// razón por la que `formatearMonto` tiene un `try/catch` que la nombra.
    /// </summary>
    [Theory]
    [InlineData("1X2")]
    [InlineData("ab1")]
    [InlineData("A1")]
    public async Task Un_Codigo_Que_No_Son_Tres_Letras_Es_Rechazado_FR010(string codigo)
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        // El `finally` limpia **aunque la aserción falle**, y eso no es ceremonia: mientras este test
        // esté en rojo —o sea, antes de que exista la restricción— el INSERT ENTRA, y la fila
        // inválida queda en la base.
        //
        // **La comparación es sensible a mayúsculas, y eso no es detalle: sin el CAST este DELETE
        // destruye el catálogo.** `codigo` usa `utf8mb4_0900_ai_ci`, así que `codigo = 'ars'` alcanza
        // al `ARS` REAL de la semilla. Pasó de verdad durante la revisión del PR #29: un caso de
        // prueba con un código en minúsculas borró `ARS`, el alta se quedó sin moneda predeterminada
        // y cuatro tests de contrato sin relación con esto empezaron a dar 500. El síntoma apareció
        // lejísimos de la causa. Sin esta limpieza, el rojo de este test contamina al de abajo,
        // que enumera el catálogo entero, y encima deja filas que la propia migración no podría
        // aplicar. Se descubrió corriéndolo: el Principio IV dice que ningún test puede depender de
        // lo que dejó otro, y un test que sólo limpia cuando pasa lo incumple justo cuando importa.
        try
        {
            await Assert.ThrowsAnyAsync<DbException>(async () =>
                await contexto.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO moneda (codigo, nombre, simbolo, decimales, es_predeterminada) VALUES ({codigo}, 'Invalida', '¤', 2, 0)"));

            var filas = await contexto.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM moneda WHERE CAST(codigo AS BINARY) = CAST({codigo} AS BINARY)")
                .SingleAsync();

            Assert.Equal(0, filas);
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM moneda WHERE CAST(codigo AS BINARY) = CAST({codigo} AS BINARY)");
        }
    }

    /// <summary>
    /// Un código válido entra, y **el catálogo que ya está sembrado sobrevive a la migración**
    /// (`SC-010`).
    ///
    /// **Este test nace en verde**, y eso es información y no un defecto: `ARS` y `USD` ya son tres
    /// letras, así que la migración se aplica sobre datos válidos. Vale por el día que alguien
    /// agregue una moneda al catálogo con un código que no lo sea — y por el día que alguien afloje
    /// la restricción pensando que no protege nada.
    ///
    /// Se verifica sobre **todas** las filas del catálogo y no sobre una lista escrita acá: es la
    /// regla D-10 de la feature 009, que prohíbe que un test fije un número sobre el tamaño del
    /// catálogo. Ya se rompió una vez por eso.
    /// </summary>
    [Fact]
    public async Task Un_Codigo_De_Tres_Letras_Entra_Y_El_Catalogo_Sembrado_Sobrevive_SC010()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        // Ninguna fila del catálogo, cualquiera sea su tamaño, incumple la restricción.
        var queIncumplen = await contexto.Database
            .SqlQuery<string>($"SELECT codigo AS Value FROM moneda WHERE codigo NOT REGEXP '^[A-Za-z]{{3}}$'")
            .ToListAsync();

        Assert.Empty(queIncumplen);

        // **Y la semilla está intacta: exactamente una predeterminada** (`RF-25`).
        //
        // Se agregó tras la revisión del PR #29, donde una limpieza de test insensible a mayúsculas
        // borró `ARS` del catálogo. El daño no se vio acá: se vio como cuatro 500 en tests de contrato
        // que no tienen nada que ver, porque el alta hace `SingleAsync(m => m.EsPredeterminada)` y sin
        // predeterminada lanza. Esta comprobación existe para que ese daño se vea **donde ocurre**.
        var predeterminadas = await contexto.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM moneda WHERE es_predeterminada = 1")
            .SingleAsync();

        Assert.Equal(1, predeterminadas);

        // Y una moneda nueva con un código válido entra. `XCD` es del rango que ISO 4217 deja para
        // usos no monetarios, así que no colisiona con una moneda real.
        const string Codigo = "XCD";
        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM moneda WHERE CAST(codigo AS BINARY) = CAST({Codigo} AS BINARY)");

        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO moneda (codigo, nombre, simbolo, decimales, es_predeterminada) VALUES ({Codigo}, 'Moneda valida', '¤', 2, 0)");

        var filas = await contexto.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM moneda WHERE CAST(codigo AS BINARY) = CAST({Codigo} AS BINARY)")
            .SingleAsync();

        Assert.Equal(1, filas);

        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM moneda WHERE CAST(codigo AS BINARY) = CAST({Codigo} AS BINARY)");
    }

    /// <summary>
    /// Un código en **minúsculas se acepta**, y es una decisión y no un descuido.
    ///
    /// La revisión del PR #29 propuso apretar la restricción a mayúsculas, porque ISO 4217 define los
    /// códigos así. Se descartó con la evidencia: el motivo por el que esta restricción existe —D11-02,
    /// antes D9-09— es que un código que `Intl` no entiende llegue hasta `formatearMonto`, e **`Intl`
    /// interpreta los códigos sin distinguir mayúsculas**: `ars` y `ARS` dan los dos el mismo formato.
    /// Un código en minúsculas no es el caso que la restricción viene a atrapar.
    ///
    /// Y expresarlo costaría caro: `codigo` usa una colación insensible a mayúsculas, así que
    /// `REGEXP '^[A-Z]{3}$'` aceptaría `ars` igual — haría falta un `COLLATE` explícito dentro del
    /// CHECK, o sea una dependencia del nombre de una colación a cambio de nada.
    ///
    /// Este test existe para que la decisión quede medida y no haya que volver a discutirla: si alguien
    /// aprieta la restricción, esto se pone en rojo y lo manda a leer el porqué.
    /// </summary>
    [Fact]
    public async Task Un_Codigo_En_Minusculas_Se_Acepta_Porque_Intl_No_Distingue_FR010()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        const string Codigo = "xcz";
        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM moneda WHERE CAST(codigo AS BINARY) = CAST({Codigo} AS BINARY)");

        try
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO moneda (codigo, nombre, simbolo, decimales, es_predeterminada) VALUES ({Codigo}, 'Minusculas', '¤', 2, 0)");

            var filas = await contexto.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM moneda WHERE CAST(codigo AS BINARY) = CAST({Codigo} AS BINARY)")
                .SingleAsync();

            Assert.Equal(1, filas);
        }
        finally
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM moneda WHERE CAST(codigo AS BINARY) = CAST({Codigo} AS BINARY)");
        }
    }
}

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El desglose de gastos por categoría (RF-19, historia 2 de FEAT-001c).
///
/// Es lo que responde *en qué se me va la plata*, y lo que el gráfico del ticket 5 va a consumir.
///
/// **La propiedad central de este archivo es INV-02**: la suma del desglose es el total gastado.
/// El diseño la hace estructural —los dos salen de la misma consulta agregada (D-04)—, así que
/// estos tests no están para descubrir un error de hoy: están para que se note el día que alguien
/// separe esa consulta en dos y las dos empiecen a discrepar en silencio.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class DesglosePorCategoriaTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 15);
    private static readonly DateOnly Temprano = new(2026, 8, 5);
    private static readonly DateOnly Tarde = new(2026, 8, 25);

    private const int Comida = 1;
    private const int Transporte = 2;
    private const int Vivienda = 3;
    private const int Salud = 5;
    private const int Sueldo = 8;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-27 (RF-19): el total de cada categoría es la suma de los montos de sus gastos.
    ///
    /// Tres categorías, con dos movimientos en una de ellas: si el agrupado estuviera mal, una
    /// categoría con un solo movimiento igual daría bien y taparía el error.
    /// </summary>
    [Fact]
    public async Task El_Total_De_Cada_Categoria_Es_La_Suma_De_Sus_Gastos_AC27()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 1000m, Comida, Temprano);
        await RegistrarAsync(cuenta, "gasto", 2000m, Comida, Tarde);
        await RegistrarAsync(cuenta, "gasto", 500m, Transporte, Tarde);
        await RegistrarAsync(cuenta, "gasto", 750m, Vivienda, Temprano);

        var desglose = await DesgloseAsync(cuenta);

        Assert.Equal(3, desglose.Count);
        Assert.Equal(3000m, desglose[Comida]);
        Assert.Equal(500m, desglose[Transporte]);
        Assert.Equal(750m, desglose[Vivienda]);
    }

    /// <summary>
    /// INV-02 (FR-009): la suma del desglose es **exactamente** el total gastado de esa moneda.
    ///
    /// Se compara contra el total que devuelve la propia respuesta, no contra un número escrito a
    /// mano: lo que se verifica acá no es cuánto suma, sino que las dos cifras no puedan separarse.
    /// </summary>
    [Fact]
    public async Task La_Suma_Del_Desglose_Es_El_Total_Gastado_INV02()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 1234.56m, Comida, Temprano);
        await RegistrarAsync(cuenta, "gasto", 78.90m, Transporte, Tarde);
        await RegistrarAsync(cuenta, "gasto", 1000m, Salud, Tarde);
        await RegistrarAsync(cuenta, "ingreso", 9999m, Sueldo, Temprano);

        using var resumen = await ResumenAsync(cuenta);
        var ars = Moneda(resumen, "ARS");

        var suma = ars.GetProperty("gastosPorCategoria").EnumerateArray()
            .Sum(c => c.GetProperty("total").GetDecimal());

        Assert.Equal(ars.GetProperty("totalGastado").GetDecimal(), suma);
    }

    /// <summary>
    /// INV-07 (FR-008): **ninguna categoría de ingreso aparece en el desglose**, y esos montos sí
    /// suman en el total ingresado y en el balance.
    ///
    /// RF-19 desglosa gastos. Un ingreso colado ahí adentro no rompería ningún total —el total
    /// gastado seguiría bien— pero haría que el gráfico de "en qué se me va la plata" muestre una
    /// barra de plata que entró. Y de paso rompería INV-02, porque la suma del desglose dejaría de
    /// dar el total gastado.
    /// </summary>
    [Fact]
    public async Task Ninguna_Categoria_De_Ingreso_Aparece_En_El_Desglose_INV07()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 300m, Comida, Temprano);
        await RegistrarAsync(cuenta, "ingreso", 5000m, Sueldo, Tarde);

        using var resumen = await ResumenAsync(cuenta);
        var ars = Moneda(resumen, "ARS");

        var categorias = ars.GetProperty("gastosPorCategoria").EnumerateArray()
            .Select(c => c.GetProperty("categoriaId").GetInt32())
            .ToList();

        Assert.DoesNotContain(Sueldo, categorias);
        Assert.Equal([Comida], categorias);

        // Y el ingreso no se perdió: está donde tiene que estar.
        Assert.Equal(5000m, ars.GetProperty("totalIngresado").GetDecimal());
        Assert.Equal(4700m, ars.GetProperty("balance").GetDecimal());
    }

    /// <summary>
    /// FR-009: una categoría sin movimientos en el período **no aparece**, en lugar de aparecer en
    /// cero.
    ///
    /// El desglose describe lo que pasó, no el catálogo. Diez categorías en cero al lado de dos con
    /// datos no informan nada y arruinan cualquier gráfico de proporciones.
    /// </summary>
    [Fact]
    public async Task Una_Categoria_Sin_Gastos_No_Aparece_En_El_Desglose_FR009()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 100m, Comida, Temprano);

        var desglose = await DesgloseAsync(cuenta);

        Assert.Equal([Comida], desglose.Keys);
        Assert.DoesNotContain(Transporte, desglose.Keys);
    }

    /// <summary>
    /// AC-20: un gasto que cambia de categoría deja de sumar en la anterior y suma en la nueva.
    ///
    /// Es la prueba de que el desglose se **deriva**. Un total precalculado necesitaría que alguien
    /// se acordara de invalidarlo acá, y ése es el olvido que este diseño hace imposible.
    /// </summary>
    [Fact]
    public async Task Un_Gasto_Que_Cambia_De_Categoria_Se_Muda_De_Total_AC20()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var id = await RegistrarAsync(cuenta, "gasto", 900m, Comida, Temprano);

        Assert.Equal(900m, (await DesgloseAsync(cuenta))[Comida]);

        using (var edicion = await cuenta.Cliente.PutAsJsonAsync(
            new Uri($"/api/movimientos/{id}", UriKind.Relative),
            new
            {
                tipo = "gasto",
                monto = 900m,
                categoriaId = Transporte,
                fecha = Temprano.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            }))
        {
            Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
        }

        var despues = await DesgloseAsync(cuenta);

        Assert.DoesNotContain(Comida, despues.Keys);
        Assert.Equal(900m, despues[Transporte]);
    }

    /// <summary>AC-21: un gasto eliminado deja de sumar en todos lados.</summary>
    [Fact]
    public async Task Un_Gasto_Eliminado_No_Suma_En_Ningun_Total_AC21()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 100m, Comida, Temprano);
        var condenado = await RegistrarAsync(cuenta, "gasto", 5000m, Transporte, Tarde);

        using (var baja = await cuenta.Cliente.DeleteAsync(
            new Uri($"/api/movimientos/{condenado}", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        }

        using var resumen = await ResumenAsync(cuenta);
        var ars = Moneda(resumen, "ARS");

        Assert.Equal(100m, ars.GetProperty("totalGastado").GetDecimal());
        Assert.DoesNotContain(
            Transporte,
            ars.GetProperty("gastosPorCategoria").EnumerateArray()
                .Select(c => c.GetProperty("categoriaId").GetInt32()));
    }

    /// <summary>
    /// El desglose viene ordenado de mayor a menor, y **dos categorías empatadas desempatan por
    /// identificador**.
    ///
    /// El orden es parte del contrato porque el gráfico del ticket 5 lo va a consumir: sin decirlo,
    /// quien lo dibuje no sabe si puede confiar en él o tiene que reordenar, y las dos decisiones
    /// son igual de razonables.
    ///
    /// **El desempate no es prolijidad.** La consulta agregada no lleva `ORDER BY` a propósito
    /// (`MovimientosConsulta.Agrupado`), así que el orden en que llegan las filas empatadas es el
    /// que el motor elija ese día. Sin desempate, dos categorías con el mismo total se intercambian
    /// entre dos pedidos idénticos y las barras del gráfico saltan solas — que es exactamente el
    /// motivo por el que el catálogo de monedas ya se ordena por id.
    ///
    /// Dos empatadas y una tercera arriba: sin la tercera, un orden invertido entero también
    /// pasaría.
    /// </summary>
    [Fact]
    public async Task El_Desglose_Ordena_Por_Total_Y_Desempata_Por_Categoria()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        // Las dos empatadas se cargan con el id MAYOR primero y con la fecha más nueva, para que el
        // orden natural de la consulta —si lo hubiera— sea el contrario al que se espera.
        await RegistrarAsync(cuenta, "gasto", 500m, Salud, Tarde);
        await RegistrarAsync(cuenta, "gasto", 500m, Transporte, Temprano);
        await RegistrarAsync(cuenta, "gasto", 1000m, Comida, Temprano);

        using var resumen = await ResumenAsync(cuenta);

        Assert.Equal(
            [Comida, Transporte, Salud],
            Moneda(resumen, "ARS").GetProperty("gastosPorCategoria").EnumerateArray()
                .Select(c => c.GetProperty("categoriaId").GetInt32()));
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>El desglose de la moneda predeterminada, como categoría → total.</summary>
    private static async Task<Dictionary<int, decimal>> DesgloseAsync(CuentaDePrueba cuenta)
    {
        using var resumen = await ResumenAsync(cuenta);

        return Moneda(resumen, "ARS").GetProperty("gastosPorCategoria").EnumerateArray()
            .ToDictionary(
                c => c.GetProperty("categoriaId").GetInt32(),
                c => c.GetProperty("total").GetDecimal());
    }

    private static JsonElement Moneda(JsonDocument resumen, string codigo) =>
        resumen.RootElement.GetProperty("monedas").EnumerateArray()
            .Single(m => m.GetProperty("monedaCodigo").GetString() == codigo);

    private static async Task<JsonDocument> ResumenAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/resumen", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<long> RegistrarAsync(
        CuentaDePrueba cuenta, string tipo, decimal monto, int categoriaId, DateOnly fecha)
    {
        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new
            {
                tipo,
                monto,
                categoriaId,
                fecha = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt64();
    }
}

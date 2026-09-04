using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El resumen del período (RF-19, RF-20, RF-21, RF-22 — historias 1 y 3 de FEAT-001c).
///
/// **Acá se verifica algo que no se ve.** Hasta esta feature, un fallo de lectura aparecía como una
/// fila de más en una lista. Un total contaminado, en cambio, se ve idéntico a uno correcto: nadie
/// puede mirar un número y darse cuenta de que adentro hay plata de otra cuenta. Por eso todos los
/// tests de acá comparan contra un valor **escrito a mano**, y ninguno contra una suma recalculada
/// en el propio test — recalcularla sería repetir el mismo error que se quiere atrapar.
///
/// El reloj va clavado sin excepción: el período por omisión es el mes en curso **del servidor**, y
/// un test que no lo fije pasa once meses al año (Principio IV).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ResumenDelPeriodoTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 15);

    /// <summary>Dos fechas del mes en curso, y una de otro mes.</summary>
    private static readonly DateOnly Temprano = new(2026, 8, 5);
    private static readonly DateOnly Tarde = new(2026, 8, 25);

    /// <summary>
    /// El mes anterior, con aritmética de MESES y no restando días.
    ///
    /// Restar 30 le erra en febrero y en los meses de 31, y le erra en silencio: el movimiento cae
    /// dentro del mes en curso y el total no cierra, que se lee como un error de cálculo y no como
    /// un test mal escrito.
    /// </summary>
    private static readonly DateOnly MesAnterior = new DateOnly(Hoy.Year, Hoy.Month, 10).AddMonths(-1);

    private const int Comida = 1;
    private const int Transporte = 2;
    private const int Sueldo = 8;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-30 (RF-22): el total ingresado y el total gastado del mes son las sumas por tipo.
    ///
    /// Los montos son distintos entre sí y de distinto orden de magnitud a propósito: con montos
    /// iguales, un total que sume el conjunto equivocado puede dar por casualidad el número
    /// correcto.
    /// </summary>
    [Fact]
    public async Task Los_Totales_Del_Mes_Son_Las_Sumas_Por_Tipo_AC30()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 1000m, Comida, Temprano);
        await RegistrarAsync(cuenta, "gasto", 2000m, Comida, Tarde);
        await RegistrarAsync(cuenta, "gasto", 500m, Transporte, Tarde);
        await RegistrarAsync(cuenta, "ingreso", 8000m, Sueldo, Temprano);

        using var resumen = await ResumenAsync(cuenta);
        var ars = Moneda(resumen, "ARS");

        Assert.Equal(8000m, ars.GetProperty("totalIngresado").GetDecimal());
        Assert.Equal(3500m, ars.GetProperty("totalGastado").GetDecimal());
    }

    /// <summary>
    /// AC-31 (INV-06): sin ningún movimiento, los totales van en CERO y la respuesta conserva su
    /// forma. No es una lista vacía.
    ///
    /// La diferencia importa porque decide quién resuelve el caso vacío. Con una lista vacía, cada
    /// cliente inventa sus propios ceros —y ahí es donde termina apareciendo un "—" donde tenía que
    /// ir un "0"—. Con una entrada por moneda del catálogo, el dato ya viene completo.
    ///
    /// El catálogo tiene dos monedas desde la migración inicial, así que esto verifica algo real:
    /// **dos** entradas, no una.
    /// </summary>
    [Fact]
    public async Task Sin_Movimientos_Devuelve_Una_Entrada_Por_Moneda_En_Cero_AC31()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var resumen = await ResumenAsync(cuenta);
        var monedas = resumen.RootElement.GetProperty("monedas");

        Assert.Equal(2, monedas.GetArrayLength());

        foreach (var moneda in monedas.EnumerateArray())
        {
            Assert.Equal(0m, moneda.GetProperty("totalIngresado").GetDecimal());
            Assert.Equal(0m, moneda.GetProperty("totalGastado").GetDecimal());
            Assert.Equal(0m, moneda.GetProperty("balance").GetDecimal());
            Assert.Empty(moneda.GetProperty("gastosPorCategoria").EnumerateArray());
        }
    }

    /// <summary>
    /// FR-002: sin parámetros, el período es el mes en curso **del servidor**, y lo de afuera no
    /// suma.
    ///
    /// El movimiento de control va en el mes anterior con un monto enorme: si el recorte fallara,
    /// el total no se parecería ni de casualidad al esperado.
    /// </summary>
    [Fact]
    public async Task Los_Movimientos_De_Otro_Mes_No_Suman_FR002()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 1000m, Comida, Temprano);
        await RegistrarAsync(cuenta, "gasto", 777_777m, Comida, MesAnterior);

        using var resumen = await ResumenAsync(cuenta);

        Assert.Equal(1000m, Moneda(resumen, "ARS").GetProperty("totalGastado").GetDecimal());
    }

    /// <summary>
    /// INV-01 (RF-20): el balance es lo ingresado menos lo gastado, **también cuando da negativo**.
    ///
    /// Un mes en rojo es un resultado, no un error: es exactamente la información que alguien
    /// necesita ver. Un balance recortado en cero mentiría justo cuando más importa.
    /// </summary>
    [Fact]
    public async Task El_Balance_Es_Ingresos_Menos_Gastos_Y_Puede_Ser_Negativo_INV01()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "ingreso", 1000m, Sueldo, Temprano);
        await RegistrarAsync(cuenta, "gasto", 2500m, Comida, Tarde);

        using var resumen = await ResumenAsync(cuenta);

        Assert.Equal(-1500m, Moneda(resumen, "ARS").GetProperty("balance").GetDecimal());
    }

    /// <summary>
    /// AC-15 y AC-16: lo que se acaba de registrar ya suma en el resumen siguiente.
    ///
    /// Es la prueba de que el resumen se **deriva** y no se guarda: no hay nada que invalidar, así
    /// que no hay forma de que quede viejo.
    /// </summary>
    [Fact]
    public async Task Un_Alta_Se_Refleja_En_El_Total_De_Su_Tipo_AC15_AC16()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 100m, Comida, Temprano);

        using (var antes = await ResumenAsync(cuenta))
        {
            Assert.Equal(100m, Moneda(antes, "ARS").GetProperty("totalGastado").GetDecimal());
            Assert.Equal(0m, Moneda(antes, "ARS").GetProperty("totalIngresado").GetDecimal());
        }

        await RegistrarAsync(cuenta, "gasto", 400m, Comida, Tarde);
        await RegistrarAsync(cuenta, "ingreso", 900m, Sueldo, Tarde);

        using var despues = await ResumenAsync(cuenta);

        Assert.Equal(500m, Moneda(despues, "ARS").GetProperty("totalGastado").GetDecimal());
        Assert.Equal(900m, Moneda(despues, "ARS").GetProperty("totalIngresado").GetDecimal());
    }

    /// <summary>
    /// **AC-02 del PRD** (INV-04), la última fila de la *Deuda registrada* de la feature 004: el
    /// resumen se calcula sólo sobre los movimientos propios.
    ///
    /// Ese AC quedó anotado allá porque el endpoint no existía. Nace cubierto, que era la
    /// condición.
    ///
    /// **El monto de la otra cuenta es dos órdenes de magnitud mayor a propósito.** Si el
    /// aislamiento fallara, el total no sería "un poco distinto" —sería 1.003.500 en lugar de
    /// 3.500—, y eso es lo que hace que el test diga algo. Con montos parecidos, una contaminación
    /// se confunde con un redondeo.
    ///
    /// Se comprueban las DOS cuentas, no sólo la primera: un aislamiento que filtre en una sola
    /// dirección pasaría mirando únicamente a Ana.
    /// </summary>
    [Fact]
    public async Task El_Resumen_No_Suma_Ningun_Monto_De_Otra_Cuenta_AC02()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var ana = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var beto = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(ana, "gasto", 1000m, Comida, Temprano);
        await RegistrarAsync(ana, "gasto", 2500m, Transporte, Tarde);
        await RegistrarAsync(ana, "ingreso", 4000m, Sueldo, Temprano);

        await RegistrarAsync(beto, "gasto", 1_000_000m, Comida, Temprano);
        await RegistrarAsync(beto, "ingreso", 500_000m, Sueldo, Tarde);

        using (var deAna = await ResumenAsync(ana))
        {
            var ars = Moneda(deAna, "ARS");
            Assert.Equal(3500m, ars.GetProperty("totalGastado").GetDecimal());
            Assert.Equal(4000m, ars.GetProperty("totalIngresado").GetDecimal());
            Assert.Equal(500m, ars.GetProperty("balance").GetDecimal());
        }

        using var deBeto = await ResumenAsync(beto);
        var suyo = Moneda(deBeto, "ARS");
        Assert.Equal(1_000_000m, suyo.GetProperty("totalGastado").GetDecimal());
        Assert.Equal(500_000m, suyo.GetProperty("totalIngresado").GetDecimal());
    }

    /// <summary>
    /// RF-03: sin sesión, 401.
    ///
    /// Un resumen no es un agregado inocuo por ser agregado: es la foto financiera de una cuenta.
    /// La barrera de autorización ya lo exige por su lado, y esto lo fija desde el comportamiento.
    /// </summary>
    [Fact]
    public async Task Sin_Sesion_Responde_401_RF03()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        using var anonimo = factoria.CreateClient();

        using var respuesta = await anonimo.GetAsync(new Uri("/api/resumen", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// AC-29 (RF-21): el rango acota, y **incluye sus dos extremos**.
    ///
    /// Los dos movimientos de control van fechados **exactamente** en los extremos. Es donde se
    /// esconde un `&gt;` puesto donde iba un `&gt;=`: con un rango holgado, una implementación que
    /// excluyera los bordes pasaría en verde igual.
    /// </summary>
    [Fact]
    public async Task El_Rango_Acota_E_Incluye_Sus_Dos_Extremos_AC29()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 100m, Comida, Temprano);
        await RegistrarAsync(cuenta, "gasto", 200m, Comida, Tarde);
        await RegistrarAsync(cuenta, "gasto", 50_000m, Comida, new DateOnly(2026, 8, 26));

        using var resumen = await ResumenAsync(cuenta, Temprano, Tarde);

        Assert.Equal(300m, Moneda(resumen, "ARS").GetProperty("totalGastado").GetDecimal());
    }

    /// <summary>
    /// Un rango de un solo día es válido y contiene lo de ese día.
    ///
    /// Que el resultado NO sea cero es lo que prueba que los extremos entran: `desde == hasta` es
    /// el caso donde un rango exclusivo devolvería vacío sin que nada avise.
    /// </summary>
    [Fact]
    public async Task Un_Rango_De_Un_Solo_Dia_Contiene_Lo_De_Ese_Dia()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 640m, Comida, Temprano);
        await RegistrarAsync(cuenta, "gasto", 999m, Comida, Tarde);

        using var resumen = await ResumenAsync(cuenta, Temprano, Temprano);

        Assert.Equal(640m, Moneda(resumen, "ARS").GetProperty("totalGastado").GetDecimal());
    }

    /// <summary>
    /// FR-004: el rango invertido y el medio rango se rechazan con 400.
    ///
    /// Devolver todo en cero sería peor que un error: se lee como "no gastaste nada" y esconde que
    /// la pregunta estaba mal formada. En un resumen eso es más grave que en un listado — una lista
    /// vacía llama la atención, un cero parece un dato.
    /// </summary>
    [Theory]
    [InlineData("?desde=2026-08-31&hasta=2026-08-01")]
    [InlineData("?desde=2026-08-01")]
    [InlineData("?hasta=2026-08-31")]
    public async Task Un_Periodo_Mal_Formado_Se_Rechaza_FR004(string consulta)
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var (estado, cuerpo) = await CrudoAsync(cuenta, consulta);

        Assert.Equal(HttpStatusCode.BadRequest, estado);

        using var json = JsonDocument.Parse(cuerpo);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("rango", out _));
    }

    /// <summary>
    /// INV-03 (FR-005): para un mismo período, el resumen y el listado hablan del **mismo
    /// conjunto**.
    ///
    /// El total se compara contra la suma del listado filtrado con ese rango. Si difieren, una de
    /// las dos vistas miente y quien mira la pantalla no tiene forma de saber cuál. Es el test que
    /// se rompería si algún día alguien le agregara una condición a una sola de las dos consultas.
    /// </summary>
    [Fact]
    public async Task El_Resumen_Y_El_Listado_Hablan_Del_Mismo_Conjunto_INV03()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 123.45m, Comida, Temprano);
        await RegistrarAsync(cuenta, "gasto", 67.89m, Transporte, Tarde);
        await RegistrarAsync(cuenta, "ingreso", 500m, Sueldo, Tarde);
        await RegistrarAsync(cuenta, "gasto", 9_000m, Comida, MesAnterior);

        using var listado = await ListadoAsync(cuenta, Temprano, Tarde);
        var gastadoSegunElListado = listado.RootElement.EnumerateArray()
            .Where(m => m.GetProperty("tipo").GetString() == "gasto")
            .Sum(m => m.GetProperty("monto").GetDecimal());

        using var resumen = await ResumenAsync(cuenta, Temprano, Tarde);

        Assert.Equal(
            gastadoSegunElListado,
            Moneda(resumen, "ARS").GetProperty("totalGastado").GetDecimal());
    }

    /// <summary>
    /// FR-014 con rango explícito: un período sin movimientos devuelve la forma completa en cero.
    ///
    /// Es AC-31 otra vez, pero por el camino del filtro: el caso vacío no es una rareza del arranque
    /// —es lo que va a ver cualquiera que mire un mes en el que no cargó nada.
    /// </summary>
    [Fact]
    public async Task Un_Rango_Sin_Movimientos_Devuelve_La_Forma_Completa_En_Cero_FR014()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, "gasto", 1000m, Comida, Temprano);

        using var resumen = await ResumenAsync(
            cuenta, new DateOnly(1999, 1, 1), new DateOnly(1999, 1, 31));

        var monedas = resumen.RootElement.GetProperty("monedas");
        Assert.Equal(2, monedas.GetArrayLength());

        foreach (var moneda in monedas.EnumerateArray())
        {
            Assert.Equal(0m, moneda.GetProperty("totalGastado").GetDecimal());
            Assert.Empty(moneda.GetProperty("gastosPorCategoria").EnumerateArray());
        }

        // Y el período que se pidió vuelve en la respuesta, aunque no haya nada adentro.
        Assert.Equal("1999-01-01", resumen.RootElement.GetProperty("desde").GetString());
        Assert.Equal("1999-01-31", resumen.RootElement.GetProperty("hasta").GetString());
    }

    /// <summary>
    /// D-06: el período que decidió el SERVIDOR viaja en la respuesta, aunque no se haya pedido.
    ///
    /// Sin esto, el cliente que quiera titular el mes tendría que calcularlo con el reloj del
    /// navegador, y ahí vuelven a existir dos criterios de "hoy" — que es lo que FR-002 evita.
    /// </summary>
    [Fact]
    public async Task El_Periodo_Por_Omision_Viaja_En_La_Respuesta_D06()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var resumen = await ResumenAsync(cuenta);

        Assert.Equal("2026-08-01", resumen.RootElement.GetProperty("desde").GetString());
        Assert.Equal("2026-08-31", resumen.RootElement.GetProperty("hasta").GetString());
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>La entrada de una moneda del resumen, por su código.</summary>
    private static JsonElement Moneda(JsonDocument resumen, string codigo) =>
        resumen.RootElement.GetProperty("monedas").EnumerateArray()
            .Single(m => m.GetProperty("monedaCodigo").GetString() == codigo);

    /// <summary>
    /// **El guardarraíl de D-05: el resumen NO hereda el acotado por moneda del listado.**
    ///
    /// La feature 009 le agrega un `monedaId` opcional a `MovimientosConsulta.Filtrado`, y esa
    /// consulta comparte con el resumen el método privado que acota por cuenta. `Agrupado` le pasa
    /// `null` explícito, y este test es lo que hace que ese `null` sea una afirmación verificada en
    /// vez de un comentario.
    ///
    /// **El daño que evita es silencioso**, y es el mismo que `verificar-desglose.sh` vigila para
    /// `categoria.activa`: si el resumen empezara a filtrar por moneda, los totales de un período
    /// ya cerrado darían otro número sin que nadie tocara un movimiento. El resumen informa sobre
    /// **todas** las monedas del catálogo, siempre (`006:AC-31`, FR-009 de la 006).
    ///
    /// Por eso el caso exige que **las dos** monedas traigan su total, y no sólo que la lista tenga
    /// dos entradas: un resumen filtrado devolvería igual las dos entradas —una de ellas en cero—,
    /// porque la lista sale del catálogo y no de los movimientos.
    /// </summary>
    [Fact]
    public async Task El_Resumen_No_Hereda_El_Acotado_Por_Moneda_Del_Listado()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await CatalogoDeMonedas.ConLaMonedaAsync(_baseDeDatos, "XR1", async moneda =>
        {
            using var factoria = new FactoriaConReloj(Hoy);
            using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

            await RegistrarAsync(cuenta, "gasto", 100m, Comida, Hoy, moneda.Id);
            await RegistrarAsync(cuenta, "gasto", 250m, Comida, Hoy);

            string predeterminada;
            await using (var contexto = _baseDeDatos.CrearContexto())
            {
                predeterminada = await contexto.Monedas
                    .Where(m => m.EsPredeterminada)
                    .Select(m => m.Codigo)
                    .SingleAsync();
            }

            using var resumen = await ResumenAsync(cuenta);

            var totales = resumen.RootElement.GetProperty("monedas")
                .EnumerateArray()
                .ToDictionary(
                    e => e.GetProperty("monedaCodigo").GetString()!,
                    e => e.GetProperty("totalGastado").GetDecimal());

            Assert.Equal(100m, totales[moneda.Codigo]);
            Assert.Equal(250m, totales[predeterminada]);
        });
    }

    private static async Task<JsonDocument> ResumenAsync(
        CuentaDePrueba cuenta, DateOnly? desde = null, DateOnly? hasta = null)
    {
        var (estado, cuerpo) = await CrudoAsync(cuenta, Consulta(desde, hasta));

        Assert.Equal(HttpStatusCode.OK, estado);
        return JsonDocument.Parse(cuerpo);
    }

    /// <summary>El listado filtrado con el mismo período, para poder comparar las dos vistas.</summary>
    private static async Task<JsonDocument> ListadoAsync(
        CuentaDePrueba cuenta, DateOnly desde, DateOnly hasta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/movimientos" + Consulta(desde, hasta), UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpStatusCode Estado, string Cuerpo)> CrudoAsync(
        CuentaDePrueba cuenta, string consulta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/resumen" + consulta, UriKind.Relative));

        return (respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
    }

    private static string Consulta(DateOnly? desde, DateOnly? hasta)
    {
        var partes = new List<string>();
        if (desde is { } d)
        {
            partes.Add("desde=" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (hasta is { } h)
        {
            partes.Add("hasta=" + h.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return partes.Count == 0 ? string.Empty : "?" + string.Join("&", partes);
    }

    private static async Task RegistrarAsync(
        CuentaDePrueba cuenta,
        string tipo,
        decimal monto,
        int categoriaId,
        DateOnly fecha,
        int? monedaId = null)
    {
        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new
            {
                tipo,
                monto,
                categoriaId,
                monedaId,
                fecha = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }
}

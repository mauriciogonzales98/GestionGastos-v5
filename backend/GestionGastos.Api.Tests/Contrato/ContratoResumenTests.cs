using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionGastos.Api.Tests.Integracion;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// La barrera del contrato para los tres tipos del resumen (D-09 de la feature 001).
///
/// Se compara en las dos direcciones, como en movimientos y categorías: un campo que falta hace
/// llegar `undefined` a la pantalla; uno que sobra es un dato que salió a la red sin que nadie lo
/// decidiera.
///
/// **Son tres comparaciones y no una porque la respuesta tiene tres niveles.** Cada uno se compara
/// contra su propio nodo del JSON. Por eso también los tres son `export interface` con nombre en
/// `tipos.ts` y ninguno es un objeto anidado inline: `TiposDelFrontend.CamposDeInterfaz` cuenta los
/// campos de todo el cuerpo de la interfaz, así que un objeto anidado haría aparecer los campos del
/// hijo como campos del padre y la comparación fallaría por un motivo que no es el real (D-07).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ContratoResumenTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 23);

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Los tres niveles del resumen coinciden con el contrato, en las dos direcciones.
    ///
    /// Se carga un gasto de verdad antes de pedirlo: sin ningún movimiento, `gastosPorCategoria`
    /// vendría vacío y `TotalPorCategoria` quedaría sin comparar contra nada — una barrera que no
    /// mira no es una barrera.
    /// </summary>
    [Fact]
    public async Task Los_Campos_Del_Resumen_Coinciden_En_Las_Dos_Direcciones()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using (var creacion = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 10m, categoriaId = 1, fecha = "2026-08-23" }))
        {
            Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);
        }

        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/resumen", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        CompararEnLasDosDirecciones(
            TiposDelFrontend.CamposDeInterfaz("Resumen"),
            [.. json.RootElement.EnumerateObject().Select(p => p.Name)],
            "Resumen");

        var moneda = json.RootElement.GetProperty("monedas").EnumerateArray().First();

        CompararEnLasDosDirecciones(
            TiposDelFrontend.CamposDeInterfaz("ResumenPorMoneda"),
            [.. moneda.EnumerateObject().Select(p => p.Name)],
            "ResumenPorMoneda");

        // La moneda del gasto que se cargó recién: es la única con desglose, y es la que permite
        // comparar el tercer nivel contra algo real.
        var conDesglose = json.RootElement.GetProperty("monedas").EnumerateArray()
            .Single(m => m.GetProperty("gastosPorCategoria").GetArrayLength() > 0);

        CompararEnLasDosDirecciones(
            TiposDelFrontend.CamposDeInterfaz("TotalPorCategoria"),
            [.. conDesglose.GetProperty("gastosPorCategoria").EnumerateArray().First()
                .EnumerateObject().Select(p => p.Name)],
            "TotalPorCategoria");
    }

    /// <summary>
    /// El error del período tiene la forma que el contrato ya declara para los errores.
    ///
    /// Va acá y no en el archivo de movimientos porque es el mismo intérprete: si el resumen
    /// devolviera otra clave, la pantalla no sabría al lado de qué control poner el mensaje.
    /// </summary>
    [Fact]
    public async Task El_Error_Del_Periodo_Usa_La_Misma_Clave_Que_El_Listado()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var claves = new List<string>();

        foreach (var ruta in new[] { "/api/movimientos", "/api/resumen" })
        {
            using var respuesta = await cuenta.Cliente.GetAsync(
                new Uri(ruta + "?desde=2026-08-31&hasta=2026-08-01", UriKind.Relative));

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            claves.Add(string.Join(
                ",",
                json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name)));
        }

        Assert.Equal(claves[0], claves[1]);
    }

    private static void CompararEnLasDosDirecciones(
        IReadOnlyList<string> delContrato, IReadOnlyList<string> delJson, string tipo)
    {
        var faltan = delContrato.Except(delJson, StringComparer.Ordinal).ToList();
        var sobran = delJson.Except(delContrato, StringComparer.Ordinal).ToList();

        Assert.True(
            faltan.Count == 0,
            $"{tipo}: el contrato declara campos que la API no devuelve: {string.Join(", ", faltan)}. " +
            "La pantalla los va a leer como `undefined`.");

        Assert.True(
            sobran.Count == 0,
            $"{tipo}: la API devuelve campos que el contrato no declara: {string.Join(", ", sobran)}. " +
            "Son datos que salieron a la red sin que nadie lo decidiera.");
    }
}

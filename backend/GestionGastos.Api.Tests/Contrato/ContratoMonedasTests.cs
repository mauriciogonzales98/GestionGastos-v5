using System.Net;
using System.Text.Json;
using GestionGastos.Api.Tests.Integracion;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// La barrera del contrato frontend↔backend para `GET /api/monedas` (D-09, Principio V).
///
/// Compara en las DOS direcciones, igual que las otras tres: ningún campo del contrato falta en el
/// JSON, y ninguno del JSON sobra frente al contrato. La segunda dirección es la que acá tiene el
/// caso concreto: la tabla `moneda` tiene una columna `decimales` que el contrato **decidió no
/// declarar**, porque hoy no la consume nadie —el formato regional del monto es el ticket 6—. Si
/// alguien la agrega al DTO "por completitud", este test se pone en rojo, que es exactamente lo que
/// se quiere: un campo que nadie usa es un dato que salió a la red sin que nadie lo decidiera.
///
/// Que estos tests pasen no prueba que la barrera sirva: prueba que hoy están alineados.
/// `backend/verificar-contrato.sh` es lo que comprueba que sabe ponerse en rojo.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ContratoMonedasTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Los_Campos_De_Moneda_Coinciden_En_Las_Dos_Direcciones()
    {
        var delContrato = TiposDelFrontend.CamposDeInterfaz("Moneda");

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 4));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        using var respuesta = await cliente.GetAsync(new Uri("/api/monedas", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        // El catálogo nunca viene vacío: la migración siembra al menos una moneda. Si viniera
        // vacío no habría nada que comparar y el test pasaría sin comparar nada, que es la forma
        // más silenciosa de que una barrera deje de proteger.
        var primera = Assert.Single(json.RootElement.EnumerateArray().Take(1));
        var delJson = primera.EnumerateObject().Select(p => p.Name).ToList();

        var faltanEnLaApi = delContrato.Except(delJson, StringComparer.Ordinal).ToList();
        var sobranEnLaApi = delJson.Except(delContrato, StringComparer.Ordinal).ToList();

        Assert.True(
            faltanEnLaApi.Count == 0,
            $"El contrato declara campos que la API no emite: {string.Join(", ", faltanEnLaApi)}. " +
            "El frontend va a leer undefined.");

        Assert.True(
            sobranEnLaApi.Count == 0,
            $"La API emite campos que el contrato no declara: {string.Join(", ", sobranEnLaApi)}. " +
            "O el contrato quedó viejo, o se filtró a la red un dato que nadie decidió exponer.");
    }
}

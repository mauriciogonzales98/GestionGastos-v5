using System.Text.Json;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// La barrera del contrato frontend↔backend para `GET /api/categorias` (D-09, Principio V).
///
/// Compara en las DOS direcciones: ningún campo del contrato falta en el JSON, y ningún campo del
/// JSON sobra frente al contrato. Una sola dirección no alcanza — un rename del backend deja en
/// verde el build, `tsc`, ESLint y toda la suite, y hace llegar `undefined` a la pantalla.
///
/// Que estos tests pasen no prueba que la barrera sirva: prueba que hoy están alineados.
/// `backend/verificar-contrato.sh` es lo que comprueba que sabe ponerse en rojo.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ContratoCategoriasTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Los_Campos_De_Categoria_Coinciden_En_Las_Dos_Direcciones()
    {
        Assert.NotNull(_baseDeDatos);
        var delContrato = TiposDelFrontend.CamposDeInterfaz("Categoria");
        var delJson = await CamposDelPrimerElementoAsync();

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

    [Fact]
    public async Task Los_Valores_De_TipoMovimiento_Coinciden_Con_Los_Que_Emite_La_Api()
    {
        Assert.NotNull(_baseDeDatos);
        var delContrato = TiposDelFrontend.LiteralesDeUnion("TipoMovimiento");

        using var factoria = new WebApplicationFactory<Program>();
        using var cliente = factoria.CreateClient();
        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        var delJson = json.RootElement.EnumerateArray()
            .Select(c => c.GetProperty("tipo").GetString() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // El catálogo tiene las dos mitades, así que la API ejercita los dos literales de la unión.
        Assert.Equal(
            delContrato.OrderBy(v => v, StringComparer.Ordinal),
            delJson.OrderBy(v => v, StringComparer.Ordinal));
    }

    private static async Task<IReadOnlyList<string>> CamposDelPrimerElementoAsync()
    {
        using var factoria = new WebApplicationFactory<Program>();
        using var cliente = factoria.CreateClient();
        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        var primero = json.RootElement.EnumerateArray().FirstOrDefault();
        Assert.Equal(JsonValueKind.Object, primero.ValueKind);

        return primero.EnumerateObject().Select(p => p.Name).ToList();
    }
}

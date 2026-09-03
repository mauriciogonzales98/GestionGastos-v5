using System.Net;
using System.Net.Http.Json;
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

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;
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

    /// <summary>
    /// Los campos de `NuevaCategoria` son los que la API acepta de verdad, y el `201` devuelve la
    /// misma forma que el listado.
    ///
    /// El cuerpo se arma con los nombres QUE DECLARA EL CONTRATO, no con los del DTO: si la API
    /// dejara de aceptar alguno —un rename, un cambio de política de nombres— este POST deja de
    /// llegar completo y el valor no vuelve en la respuesta.
    /// </summary>
    [Fact]
    public async Task Los_Campos_De_NuevaCategoria_Son_Los_Que_La_Api_Acepta_De_Verdad()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        var delContrato = TiposDelFrontend.CamposDeInterfaz("NuevaCategoria");

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var cuerpo = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var campo in delContrato)
        {
            cuerpo[campo] = campo switch
            {
                "nombre" => "Gimnasio",
                "tipo" => "gasto",
                _ => throw new InvalidOperationException(
                    $"El contrato declara el campo `{campo}` de NuevaCategoria y este test no sabe " +
                    "con qué valor ejercitarlo. Agregalo acá: un campo del contrato sin ejercitar " +
                    "es un campo sin barrera."),
            };
        }

        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/categorias", UriKind.Relative), cuerpo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        // Cada valor mandado tiene que haber llegado. Un campo que la API ignora se guardaría con
        // su valor por defecto y el 201 saldría igual.
        Assert.Equal("Gimnasio", json.RootElement.GetProperty("nombre").GetString());
        Assert.Equal("gasto", json.RootElement.GetProperty("tipo").GetString());

        // El 201 devuelve la MISMA forma que el listado. Si divergieran, la pantalla no podría
        // insertar la creada en el catálogo que ya tiene sin volver a pedirlo (FR-019).
        var delJson = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        CompararEnLasDosDirecciones(TiposDelFrontend.CamposDeInterfaz("Categoria"), delJson, "Categoria (alta)");
    }

    /// <summary>
    /// Los campos de `CategoriaEditada` son los que el renombre acepta de verdad, y el `200`
    /// devuelve la misma forma que el listado.
    ///
    /// **Y `tipo` no está entre ellos.** Que el contrato no lo declare es lo que impide que la
    /// pantalla ofrezca cambiarlo; que el DTO tampoco lo tenga es lo que impide que llegue igual.
    /// Este test junta las dos mitades: arma el cuerpo con lo que el contrato declara y comprueba
    /// que la categoría conservó su tipo.
    /// </summary>
    [Fact]
    public async Task Los_Campos_De_CategoriaEditada_Son_Los_Que_La_Api_Acepta_De_Verdad()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        var delContrato = TiposDelFrontend.CamposDeInterfaz("CategoriaEditada");

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var alta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/categorias", UriKind.Relative), new { nombre = "Gimnasio", tipo = "gasto" });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var creada = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var id = creada.RootElement.GetProperty("id").GetInt32();

        var cuerpo = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var campo in delContrato)
        {
            cuerpo[campo] = campo switch
            {
                "nombre" => "Gimnasio y pileta",
                _ => throw new InvalidOperationException(
                    $"El contrato declara el campo `{campo}` de CategoriaEditada y este test no sabe " +
                    "con qué valor ejercitarlo. Agregalo acá: un campo del contrato sin ejercitar " +
                    "es un campo sin barrera."),
            };
        }

        using var respuesta = await cuenta.Cliente.PutAsJsonAsync(
            new Uri($"/api/categorias/{id}", UriKind.Relative), cuerpo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("Gimnasio y pileta", json.RootElement.GetProperty("nombre").GetString());
        Assert.Equal("gasto", json.RootElement.GetProperty("tipo").GetString());

        var delJson = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        CompararEnLasDosDirecciones(
            TiposDelFrontend.CamposDeInterfaz("Categoria"), delJson, "Categoria (renombre)");
    }

    /// <summary>
    /// La baja responde `204` **sin cuerpo**, y después la categoría ya no está en el catálogo.
    ///
    /// El contrato no tiene una interfaz que comparar acá —un `204` no lleva JSON— así que lo que
    /// se verifica es justamente eso: que no lleve. `darDeBajaCategoria` está tipada como
    /// `Promise&lt;void&gt;` y va por `pedirSinCuerpo`, que no intenta parsear; un cuerpo que
    /// apareciera sería un contrato nuevo que nadie decidió.
    /// </summary>
    [Fact]
    public async Task La_Baja_Responde_204_Sin_Cuerpo()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var alta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/categorias", UriKind.Relative), new { nombre = "Gimnasio", tipo = "gasto" });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var creada = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var id = creada.RootElement.GetProperty("id").GetInt32();

        using var baja = await cuenta.Cliente.DeleteAsync(new Uri($"/api/categorias/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        Assert.Equal(string.Empty, await baja.Content.ReadAsStringAsync());

        using var catalogo = await cuenta.Cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        using var json = JsonDocument.Parse(await catalogo.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            json.RootElement.EnumerateArray(),
            c => c.GetProperty("id").GetInt32() == id);
    }

    private static void CompararEnLasDosDirecciones(
        IReadOnlyList<string> delContrato,
        IReadOnlyList<string> delApi,
        string nombre)
    {
        var faltanEnLaApi = delContrato.Except(delApi, StringComparer.Ordinal).ToList();
        var sobranEnLaApi = delApi.Except(delContrato, StringComparer.Ordinal).ToList();

        Assert.True(
            faltanEnLaApi.Count == 0,
            $"{nombre}: el contrato declara campos que la API no emite: {string.Join(", ", faltanEnLaApi)}. " +
            "El frontend va a leer undefined.");

        Assert.True(
            sobranEnLaApi.Count == 0,
            $"{nombre}: la API emite campos que el contrato no declara: {string.Join(", ", sobranEnLaApi)}. " +
            "O el contrato quedó viejo, o se filtró a la red un dato que nadie decidió exponer.");
    }

    private async Task<IReadOnlyList<string>> CamposDelPrimerElementoAsync()
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;
        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        var primero = json.RootElement.EnumerateArray().FirstOrDefault();
        Assert.Equal(JsonValueKind.Object, primero.ValueKind);

        return primero.EnumerateObject().Select(p => p.Name).ToList();
    }
}

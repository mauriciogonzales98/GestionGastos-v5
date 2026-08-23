using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Tests.Integracion;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// La barrera del contrato para los tres tipos que faltaban: `Movimiento`, `NuevoMovimiento` y
/// `ProblemDetails` (D-09).
///
/// Igual que en categorías, se compara en las dos direcciones. Un campo que falta hace llegar
/// `undefined` a la pantalla; uno que sobra es un dato que salió a la red sin que nadie lo
/// decidiera.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ContratoMovimientosTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Los_Campos_De_Movimiento_Coinciden_En_Las_Dos_Direcciones()
    {
        await _baseDeDatos.LimpiarMovimientosAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 23));
        using var cliente = factoria.CreateClient();

        using var creacion = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 10m, categoriaId = 1, fecha = "2026-08-23" });
        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);

        using var json = JsonDocument.Parse(await creacion.Content.ReadAsStringAsync());
        var delJson = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        var delContrato = TiposDelFrontend.CamposDeInterfaz("Movimiento");

        CompararEnLasDosDirecciones(delContrato, delJson, "Movimiento");

        // El listado devuelve la MISMA forma que el alta. Si divergieran, la pantalla podría
        // insertar el creado sin volver a pedirlo (FR-014) y quedarse con una fila distinta a las
        // demás.
        using var listado = await cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));
        using var jsonListado = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        var delListado = jsonListado.RootElement.EnumerateArray().First()
            .EnumerateObject().Select(p => p.Name).ToList();

        CompararEnLasDosDirecciones(delContrato, delListado, "Movimiento (listado)");
    }

    [Fact]
    public void Los_Campos_De_NuevoMovimiento_Coinciden_Con_Los_Que_Acepta_La_Api()
    {
        // La "forma real" de la petición es el DTO que el endpoint deserializa: es lo que la API
        // acepta de verdad, no lo que la documentación dice que acepta.
        var delApi = typeof(NuevoMovimientoDto)
            .GetProperties()
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..])
            .ToList();

        var delContrato = TiposDelFrontend.CamposDeInterfaz("NuevoMovimiento");

        CompararEnLasDosDirecciones(delContrato, delApi, "NuevoMovimiento");
    }

    [Fact]
    public async Task Los_Campos_De_ProblemDetails_Coinciden_Con_Un_Error_Real()
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 23));
        using var cliente = factoria.CreateClient();

        // Un tipo inválido: el error se produce de verdad, no se construye a mano en el test.
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "invalido", monto = 10m, categoriaId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var delJson = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        var delContrato = TiposDelFrontend.CamposDeInterfaz("ProblemDetails");

        // Acá la comparación es en una dirección: `errors` es opcional en el contrato y
        // ProblemDetails admite miembros de extensión, así que exigir igualdad exacta rompería
        // ante un campo que RFC 9457 permite. Lo que no se admite es que falte lo declarado.
        var faltan = delContrato.Except(delJson, StringComparer.Ordinal).ToList();
        Assert.True(
            faltan.Count == 0,
            $"El contrato declara campos de ProblemDetails que la API no emite: {string.Join(", ", faltan)}");
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
            $"{nombre}: el contrato declara campos que la API no tiene: {string.Join(", ", faltanEnLaApi)}");

        Assert.True(
            sobranEnLaApi.Count == 0,
            $"{nombre}: la API tiene campos que el contrato no declara: {string.Join(", ", sobranEnLaApi)}");
    }
}

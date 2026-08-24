using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 23));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

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
    public async Task Los_Campos_De_NuevoMovimiento_Son_Los_Que_La_Api_Acepta_De_Verdad()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        var delContrato = TiposDelFrontend.CamposDeInterfaz("NuevoMovimiento");

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 23));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        // Se manda un cuerpo armado con los nombres QUE DECLARA EL CONTRATO, no con los del DTO.
        // Si la API dejara de aceptar alguno —un cambio de política de nombres, un rename— este
        // POST deja de llegar completo y el 201 no aparece o llega con valores por defecto.
        var cuerpo = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var campo in delContrato)
        {
            cuerpo[campo] = campo switch
            {
                "tipo" => "gasto",
                "monto" => 77.77m,
                "categoriaId" => 1,
                "fecha" => "2026-08-23",
                _ => throw new InvalidOperationException(
                    $"El contrato declara el campo `{campo}` de NuevoMovimiento y este test no sabe " +
                    "con qué valor ejercitarlo. Agregalo acá: un campo del contrato sin ejercitar " +
                    "es un campo sin barrera."),
            };
        }

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative), cuerpo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        // Cada valor mandado tiene que haber llegado. Un campo que la API ignora se guardaría con
        // su valor por defecto y el 201 saldría igual: verificar sólo el código de estado dejaría
        // pasar exactamente el error que esta barrera existe para atrapar.
        Assert.Equal("gasto", json.RootElement.GetProperty("tipo").GetString());
        Assert.Equal(77.77m, json.RootElement.GetProperty("monto").GetDecimal());
        Assert.Equal(1, json.RootElement.GetProperty("categoriaId").GetInt32());
        Assert.Equal("2026-08-23", json.RootElement.GetProperty("fecha").GetString());
    }

    [Fact]
    public async Task Los_Campos_De_ProblemDetails_Coinciden_Con_Un_Error_Real()
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 23));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

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

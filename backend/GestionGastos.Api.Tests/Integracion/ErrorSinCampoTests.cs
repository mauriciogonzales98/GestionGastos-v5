using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El contrato (contracts/api-http.md, *Formato de error*) promete: "Un error que no corresponda a
/// ningún campo —un fallo al persistir— sale como 500 con ProblemDetails sin `errors`".
///
/// Hasta ahora eso no pasaba y nada lo verificaba: sin manejador de excepciones registrado, la
/// respuesta era la página de excepción del framework —con stack trace y SQL— o un cuerpo vacío.
/// </summary>
public class ErrorSinCampoTests
{
    [Fact]
    public async Task Un_Fallo_Al_Leer_Sale_Como_ProblemDetails_Y_No_Filtra_El_Stack()
    {
        using var factoria = new FactoriaConBaseRota();
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(cuerpo);

        Assert.True(json.RootElement.TryGetProperty("title", out _));
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());

        // Sin `errors`: no hay ningún campo al que culpar, y el frontend usa esa ausencia para
        // mandar el mensaje a la región del formulario en vez de a un control.
        Assert.False(json.RootElement.TryGetProperty("errors", out _));

        // Y nada de detalles internos en la respuesta.
        Assert.DoesNotContain("MySqlConnector", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at GestionGastos", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_Fallo_Al_Persistir_Tambien_Sale_Como_ProblemDetails()
    {
        using var factoria = new FactoriaConBaseRota();
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 100m, categoriaId = 1, fecha = "2026-08-23" });

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }
}

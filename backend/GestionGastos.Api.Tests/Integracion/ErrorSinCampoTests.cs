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

        // Se ejercita el inicio de sesión, que lee la tabla de cuentas y es uno de los dos
        // endpoints anónimos. Los demás exigen sesión, así que con la base rota responderían 401
        // antes de llegar a fallar — y este test dejaría de verificar lo que dice verificar.
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email = "quien@ejemplo.com", contrasena = "una frase larga y buena" });

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

        // El alta de cuenta escribe, y también es anónima.
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email = "quien@ejemplo.com", contrasena = "una frase larga y buena" });

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }
}

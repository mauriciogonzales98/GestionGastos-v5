using System.Net;
using System.Net.Http.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// AC-03 (FR-03): con email y contraseña correctos, la sesión queda iniciada.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class InicioDeSesionTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Credenciales_Correctas_Inician_La_Sesion_AC03()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = $"acceso-{Guid.NewGuid():N}@ejemplo.com";
        const string Contrasena = "una frase larga y buena";

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        using var sesion = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);

        // La cookie tiene que venir en la respuesta: es lo que sostiene la sesión.
        Assert.True(
            sesion.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(),
            "El inicio de sesión no devolvió ninguna cookie.");

        // Y a partir de acá el servidor reconoce a esa cuenta.
        using var actual = await cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
    }
    /// <summary>
    /// AC-04 (FR-03) y NFR-03: email inexistente y contraseña incorrecta se rechazan con la MISMA
    /// respuesta. Si difirieran, probar emails contra el login publicaría cuáles están registrados.
    /// </summary>
    [Fact]
    public async Task Email_Inexistente_Y_Contrasena_Incorrecta_Dan_La_Misma_Respuesta_AC04_NFR03()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = $"acceso-{Guid.NewGuid():N}@ejemplo.com";
        const string Contrasena = "una frase larga y buena";

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        // Existe la cuenta, pero la contraseña está mal.
        using var contrasenaMal = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena = "la que no es" });

        // No existe ninguna cuenta con ese email.
        using var emailInexistente = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email = $"nadie-{Guid.NewGuid():N}@ejemplo.com", contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.Unauthorized, contrasenaMal.StatusCode);
        Assert.Equal(emailInexistente.StatusCode, contrasenaMal.StatusCode);

        // Se comparan todos los campos MENOS `traceId`, que es distinto en cada petición por
        // definición y no dice nada sobre si la cuenta existe. Compararlo también haría fallar el
        // test por ruido y taparía lo que sí importa.
        Assert.Equal(
            await SinTraceIdAsync(emailInexistente),
            await SinTraceIdAsync(contrasenaMal));

        // Y ninguna de las dos dejó sesión iniciada.
        using var sesion = await cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, sesion.StatusCode);
    }

    /// <summary>
    /// El login verifica un hash aunque la cuenta no exista (research.md D-04).
    ///
    /// Se comprueba la CONDUCTA, no el tiempo: un test que midiera milisegundos sería intermitente
    /// y el Principio IV lo prohíbe. Lo que se mide es que el rechazo por email inexistente tarda
    /// un orden de magnitud parecido al rechazo por contraseña incorrecta — con un margen amplio,
    /// suficiente para detectar la ausencia total del hash (2 ms contra 100 ms) sin volverse
    /// sensible al ruido de la máquina.
    /// </summary>
    [Fact]
    public async Task El_Rechazo_Por_Email_Inexistente_Tambien_Paga_El_Costo_Del_Hash()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = $"acceso-{Guid.NewGuid():N}@ejemplo.com";
        const string Contrasena = "una frase larga y buena";

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        var conCuenta = await MedirAsync(cliente, email, "la que no es");
        var sinCuenta = await MedirAsync(cliente, $"nadie-{Guid.NewGuid():N}@ejemplo.com", Contrasena);

        // Margen deliberadamente ancho: lo que se detecta es la ausencia del hash, no una
        // diferencia fina. Un umbral ajustado convertiría esto en un test intermitente.
        Assert.True(
            sinCuenta > conCuenta / 5,
            $"El rechazo sin cuenta tardó {sinCuenta:F0} ms y el rechazo con cuenta {conCuenta:F0} ms. " +
            "Una diferencia así indica que no se está verificando ningún hash cuando el email no " +
            "existe, y eso permite distinguir las cuentas registradas con un cronómetro.");
    }

    /// <summary>El cuerpo del error, campo por campo, sin el `traceId`.</summary>
    private static async Task<Dictionary<string, string>> SinTraceIdAsync(HttpResponseMessage respuesta)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return json.RootElement.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "traceId", StringComparison.Ordinal))
            .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.Ordinal);
    }

    private static async Task<double> MedirAsync(HttpClient cliente, string email, string contrasena)
    {
        // Dos corridas y se toma la mayor: la primera paga la compilación del pipeline.
        double mayor = 0;

        for (var i = 0; i < 2; i++)
        {
            var cronometro = System.Diagnostics.Stopwatch.StartNew();
            using var respuesta = await cliente.PostAsJsonAsync(
                new Uri("/api/sesion", UriKind.Relative), new { email, contrasena });
            cronometro.Stop();
            mayor = Math.Max(mayor, cronometro.Elapsed.TotalMilliseconds);
        }

        return mayor;
    }

    /// <summary>AC-06 (FR-05): al cerrar sesión, la sesión deja de valer.</summary>
    [Fact]
    public async Task Cerrar_Sesion_La_Invalida_Y_Es_Idempotente_AC06()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using (var antes = await cuenta.Cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        using (var cierre = await cuenta.Cliente.DeleteAsync(new Uri("/api/sesion", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        }

        using (var despues = await cuenta.Cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
        }

        // Cerrar una sesión que ya no existe no es un error: alguien con una cookie vencida tiene
        // que poder limpiarla igual.
        using var otraVez = await cuenta.Cliente.DeleteAsync(new Uri("/api/sesion", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, otraVez.StatusCode);
    }
}

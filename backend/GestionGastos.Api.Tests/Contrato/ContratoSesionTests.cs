using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionGastos.Api.Tests.Integracion;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// La barrera del contrato para los tres tipos que trae este ticket: `NuevaCuenta`, `Credenciales`
/// y `SesionActual` (D-09, Principio V).
///
/// Los dos primeros son cuerpos de **petición**: ahí la comparación de nombres contra el JSON de
/// respuesta no aplica, porque no hay respuesta que enumerar. Lo que se verifica es más fuerte que
/// una lista de nombres — se arma la petición con los nombres QUE DECLARA EL CONTRATO y se
/// comprueba que la API los usó de verdad: la cuenta creada con esos nombres permite entrar, y las
/// credenciales mandadas con esos nombres abren sesión. Un campo que la API ignorara se guardaría
/// con su valor por defecto y el código de estado saldría igual.
///
/// `SesionActual` sí es una respuesta, y se compara en las dos direcciones como el resto.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ContratoSesionTests(BaseDeDatosFixture baseDeDatos)
{
    private const string ContrasenaDePrueba = "una frase larga de contrato";

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Los_Campos_De_NuevaCuenta_Son_Los_Que_La_Api_Acepta_De_Verdad()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        var email = Unico();
        var cuerpo = CuerpoSegunElContrato("NuevaCuenta", email);

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), cuerpo);

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        // Que el alta responda 201 no prueba nada por sí solo: responde 201 igual cuando el email
        // ya existía (NFR-03), y respondería 201 aunque hubiera ignorado los dos campos. Lo que lo
        // prueba es que esas mismas credenciales abran sesión.
        using var sesion = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email, contrasena = ContrasenaDePrueba });

        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
    }

    [Fact]
    public async Task Los_Campos_De_Credenciales_Son_Los_Que_La_Api_Acepta_De_Verdad()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        var email = Unico();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email, contrasena = ContrasenaDePrueba }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        var cuerpo = CuerpoSegunElContrato("Credenciales", email);

        using var sesion = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), cuerpo);

        // Un campo del contrato que la API no leyera llegaría como null y el login respondería 401
        // —el mismo 401 indistinguible de siempre—, así que el 200 es la prueba de que los dos
        // nombres son los que la API espera.
        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
    }

    [Fact]
    public async Task Los_Campos_De_SesionActual_Coinciden_En_Las_Dos_Direcciones()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        var delContrato = TiposDelFrontend.CamposDeInterfaz("SesionActual");

        var email = Unico();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        // El alta y el login se hacen acá y no con `CuentaDePrueba` porque este test necesita la
        // contraseña para volver a entrar, y tomarla prestada de ese helper ataría esta barrera a
        // una constante privada suya.
        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email, contrasena = ContrasenaDePrueba }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        using (var entrada = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email, contrasena = ContrasenaDePrueba }))
        {
            Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
        }

        using var consulta = await cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);

        using var json = JsonDocument.Parse(await consulta.Content.ReadAsStringAsync());
        var delJson = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        CompararEnLasDosDirecciones(delContrato, delJson, "SesionActual");

        // El inicio de sesión devuelve la MISMA forma que la consulta. Si divergieran, la pantalla
        // tendría que tratar distinto a la cuenta recién entrada y a la que vuelve de una recarga,
        // que son el mismo estado.
        using var inicio = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email, contrasena = ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, inicio.StatusCode);

        using var jsonInicio = JsonDocument.Parse(await inicio.Content.ReadAsStringAsync());
        var delInicio = jsonInicio.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        CompararEnLasDosDirecciones(delContrato, delInicio, "SesionActual (inicio de sesión)");
    }

    /// <summary>
    /// El 401 del login trae los campos declarados de `ProblemDetails` y **no** trae `errors`.
    ///
    /// La ausencia no es un detalle: el formulario usa justamente eso para mandar el mensaje a la
    /// región del formulario en vez de ponerlo al lado de un campo. Un `errors` con la clave
    /// `email` haría que la pantalla señalara ese campo y dijera, sin querer, cuál de los dos
    /// estaba bien (NFR-03).
    /// </summary>
    [Fact]
    public async Task El_401_Del_Login_Es_ProblemDetails_Y_No_Trae_Errors()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email = Unico(), contrasena = ContrasenaDePrueba });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var delJson = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        var delContrato = TiposDelFrontend.CamposDeInterfaz("ProblemDetails");

        var faltan = delContrato
            .Except(delJson, StringComparer.Ordinal)
            .Where(c => !string.Equals(c, "errors", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            faltan.Count == 0,
            $"El contrato declara campos de ProblemDetails que el 401 no emite: {string.Join(", ", faltan)}");

        Assert.DoesNotContain("errors", delJson, StringComparer.Ordinal);
    }

    /// <summary>
    /// Arma el cuerpo de una petición con los nombres de campo que declara el contrato. Un campo
    /// nuevo sin valor conocido lanza en vez de omitirse: un campo del contrato sin ejercitar es
    /// un campo sin barrera.
    /// </summary>
    private static Dictionary<string, object?> CuerpoSegunElContrato(string tipo, string email)
    {
        var cuerpo = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var campo in TiposDelFrontend.CamposDeInterfaz(tipo))
        {
            cuerpo[campo] = campo switch
            {
                "email" => email,
                "contrasena" => ContrasenaDePrueba,
                _ => throw new InvalidOperationException(
                    $"El contrato declara el campo `{campo}` de {tipo} y este test no sabe con qué " +
                    "valor ejercitarlo. Agregalo acá: un campo del contrato sin ejercitar es un " +
                    "campo sin barrera."),
            };
        }

        return cuerpo;
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

    private static string Unico() => $"contrato-{Guid.NewGuid():N}@ejemplo.com";
}

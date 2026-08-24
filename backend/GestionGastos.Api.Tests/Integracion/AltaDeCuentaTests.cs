using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El alta de una cuenta (FR-001, FR-002).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class AltaDeCuentaTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-01 (FR-01): con un email no registrado, la cuenta queda creada y esas mismas credenciales
    /// permiten iniciar sesión.
    /// </summary>
    [Fact]
    public async Task Crea_La_Cuenta_Y_Permite_Iniciar_Sesion_Con_Ella_AC01()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = Unico();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email, contrasena = "una frase larga y buena" });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            Assert.Equal(1, await contexto.Usuarios.CountAsync(u => u.Email == email));
        }

        using var sesion = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email, contrasena = "una frase larga y buena" });

        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
    }

    /// <summary>
    /// AC-02 (FR-02) y NFR-03: el alta con un email ya registrado no crea una segunda cuenta ni
    /// toca la original, y responde **exactamente igual** que un alta exitosa.
    /// </summary>
    [Fact]
    public async Task Email_Ya_Registrado_No_Duplica_Ni_Cambia_Nada_Y_Responde_Igual_AC02_NFR03()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = Unico();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var primera = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email, contrasena = "la contraseña original" });
        var cuerpoPrimera = await primera.Content.ReadAsStringAsync();

        string hashOriginal;
        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            hashOriginal = (await contexto.Usuarios.SingleAsync(u => u.Email == email)).ContrasenaHash;
        }

        using var segunda = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email, contrasena = "otra contraseña distinta" });

        // Mismo código y mismo cuerpo: la respuesta no delata que la cuenta ya existía.
        Assert.Equal(primera.StatusCode, segunda.StatusCode);
        Assert.Equal(cuerpoPrimera, await segunda.Content.ReadAsStringAsync());

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            var cuentas = await contexto.Usuarios.Where(u => u.Email == email).ToListAsync();
            Assert.Single(cuentas);
            // Y sobre todo: la contraseña original quedó intacta. Si se sobrescribiera, cualquiera
            // podría apropiarse de una cuenta ajena dándose de alta con su email.
            Assert.Equal(hashOriginal, cuentas[0].ContrasenaHash);
        }
    }

    /// <summary>AC-10 y AC-11 del lado de la base: lo guardado es un hash, y dos cuentas con la
    /// misma contraseña no comparten valor.</summary>
    [Fact]
    public async Task Lo_Guardado_Es_Un_Hash_Y_Dos_Cuentas_Iguales_No_Lo_Comparten_AC10_AC11()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var unaYOtra = new[] { Unico(), Unico() };
        const string Misma = "la misma contraseña para las dos";

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        foreach (var email in unaYOtra)
        {
            using var alta = await cliente.PostAsJsonAsync(
                new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Misma });
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        await using var contexto = _baseDeDatos.CrearContexto();
        var hashes = await contexto.Usuarios
            .Where(u => unaYOtra.Contains(u.Email))
            .Select(u => u.ContrasenaHash)
            .ToListAsync();

        Assert.Equal(2, hashes.Count);
        Assert.All(hashes, h => Assert.StartsWith("$2", h, StringComparison.Ordinal));
        Assert.All(hashes, h => Assert.DoesNotContain(Misma, h, StringComparison.Ordinal));
        Assert.NotEqual(hashes[0], hashes[1]);
    }

    [Theory]
    [InlineData("null", "\"12345678901234\"", "email", "email ausente")]
    [InlineData("\"\"", "\"12345678901234\"", "email", "email vacío")]
    [InlineData("\"no-es-un-email\"", "\"12345678901234\"", "email", "email sin arroba")]
    // Con un espacio adentro: `MailAddress` lo acepta —para el RFC un espacio entrecomillado es
    // legal— y después ese email no puede escribirse en ningún cliente de correo. Es el único caso
    // donde la validación es MÁS estricta que el estándar, y por eso tiene su propio caso.
    [InlineData("\"ana perez@x.com\"", "\"12345678901234\"", "email", "email con un espacio")]
    [InlineData("\"a@b.com\"", "null", "contrasena", "contraseña ausente")]
    [InlineData("\"a@b.com\"", "\"12345678901\"", "contrasena", "once caracteres, uno menos del mínimo")]
    public async Task Rechaza_El_Alta_Invalida_Con_La_Clave_Del_Campo(
        string email, string contrasena, string campo, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var respuesta = await EnviarCrudoAsync(
            "/api/cuentas", $$"""{"email":{{email}},"contrasena":{{contrasena}}}""");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.True(
            json.RootElement.GetProperty("errors").TryGetProperty(campo, out _),
            $"[{caso}] se esperaba la clave `{campo}` en errors. Acá SÍ se dice qué está mal: " +
            "no revela nada sobre qué cuentas existen.");
    }

    /// <summary>El borde que sí pasa: exactamente el mínimo. Sin él, una validación de más quedaría
    /// indistinguible de una correcta.</summary>
    [Fact]
    public async Task Acepta_Una_Contrasena_De_Exactamente_Doce_Caracteres()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email = Unico(), contrasena = "123456789012" });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
    }

    /// <summary>
    /// El email no distingue mayúsculas. Sin esto, `Ana@x.com` y `ana@x.com` serían dos cuentas y
    /// FR-002 quedaría incumplido por una diferencia que ninguna persona percibe como distinta.
    /// </summary>
    [Fact]
    public async Task El_Email_No_Distingue_Mayusculas()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = Unico();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var primera = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email, contrasena = "una frase larga y buena" });
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        using var segunda = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email = email.ToUpperInvariant(), contrasena = "otra distinta" });

        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Single(await contexto.Usuarios.Where(u => u.Email == email).ToListAsync());
    }

    private static async Task<HttpResponseMessage> EnviarCrudoAsync(string ruta, string cuerpo)
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");

        return await cliente.PostAsync(new Uri(ruta, UriKind.Relative), contenido);
    }

    /// <summary>Un email distinto por llamada: la base es compartida y el UNIQUE no perdona.</summary>
    private static string Unico() => $"cuenta-{Guid.NewGuid():N}@ejemplo.com";
}

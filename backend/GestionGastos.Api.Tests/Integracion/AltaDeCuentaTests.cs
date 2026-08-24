using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// Los dos largos que la base y el algoritmo imponen, y que la validación tiene que atajar
    /// antes.
    ///
    /// Un email de 300 caracteres pasaba la validación y moría contra `varchar(254)` con un 500
    /// "Data too long": un error del servidor donde correspondía un 400 con la clave del campo. Una
    /// contraseña de 100 caracteres se aceptaba, y bcrypt guardaba en silencio sólo sus primeros 72
    /// bytes — con lo que otra contraseña distinta que compartiera ese prefijo abría la cuenta.
    /// </summary>
    [Theory]
    [InlineData(300, 20, "email", "email de 300 caracteres")]
    [InlineData(20, 100, "contrasena", "contraseña de 100 caracteres")]
    public async Task Rechaza_Lo_Que_No_Entra_En_La_Base_Ni_En_El_Algoritmo(
        int largoDelEmail, int largoDeLaContrasena, string campo, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        // El sufijo `@ejemplo.com` va aparte para que el email siga siendo un email: lo que se
        // verifica es el largo, no el formato.
        var email = new string('a', largoDelEmail - "@ejemplo.com".Length) + "@ejemplo.com";
        var contrasena = new string('b', largoDeLaContrasena);

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.True(
            json.RootElement.GetProperty("errors").TryGetProperty(campo, out _),
            $"[{caso}] se esperaba la clave `{campo}` en errors.");
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

    /// <summary>
    /// La carrera: entre la consulta que pregunta si el email existe y el INSERT que lo escribe,
    /// otra petición crea esa misma cuenta.
    ///
    /// El índice único frena la segunda escritura —para eso está— y sin manejarlo esa petición
    /// salía con un 500: un error para quien no hizo nada mal, y una respuesta que delata que la
    /// cuenta existe. Tiene que terminar como cualquier alta con email ya registrado.
    ///
    /// El interceptor hace la carrera exacta y repetible; dos peticiones simultáneas darían un test
    /// que falla a veces, que es lo mismo que no tenerlo.
    /// </summary>
    [Fact]
    public async Task Si_Otra_Peticion_Gana_La_Carrera_El_Alta_Responde_Igual_Y_No_Rompe()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = Unico();

        var carrera = new CreaLaCuentaAntesDeGuardar(_baseDeDatos.Cadena);
        using var factoria = new FactoriaConReloj(
            new DateOnly(2026, 8, 24),
            // `ConfigureDbContext` y no `AddSingleton<IInterceptor>`: registrarlo en el contenedor
            // a secas lo deja inerte —EF no lo levanta— y con el interceptor inerte no hay carrera
            // y este test pasa en verde sin ejercitar nada. Pasó de verdad mientras se escribía.
            servicios => servicios.ConfigureDbContext<GestionGastosDbContext>(
                o => o.AddInterceptors(carrera)));
        using var cliente = factoria.CreateClient();

        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email, contrasena = "una frase larga y buena" });

        // Primero: que la carrera haya ocurrido de verdad. Sin esto, un interceptor inerte deja
        // este test en verde sin haber ejercitado nada.
        Assert.True(carrera.Intervino, "el interceptor no llegó a crear la cuenta rival");

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        Assert.Equal(
            "Si el email no estaba registrado, la cuenta fue creada. Ya podés iniciar sesión.",
            JsonDocument.Parse(await alta.Content.ReadAsStringAsync())
                .RootElement.GetProperty("mensaje").GetString());

        // Y quedó una sola cuenta: la que ganó la carrera.
        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Single(await contexto.Usuarios.Where(u => u.Email == email).ToListAsync());
    }

    /// <summary>
    /// NFR-03 del lado del reloj: el alta con un email YA REGISTRADO también paga el costo del
    /// hash.
    ///
    /// Igualar el mensaje y el código no alcanza. Si el camino "ya existe" se saltea el hash,
    /// responde en 2 ms contra los ~100 ms del alta que sí crea la cuenta, y esa diferencia publica
    /// qué emails están registrados con un cronómetro — en un endpoint anónimo y sin límite de
    /// intentos. Es la misma medida que `El_Rechazo_Por_Email_Inexistente_Tambien_Paga_El_Costo_Del_Hash`
    /// verifica para el login (research.md D-04).
    ///
    /// Se comprueba la CONDUCTA, no el tiempo exacto: el margen es ancho a propósito, suficiente
    /// para detectar la ausencia total del hash sin volverse sensible al ruido de la máquina.
    /// </summary>
    [Fact]
    public async Task El_Alta_Con_Email_Ya_Registrado_Tambien_Paga_El_Costo_Del_Hash_NFR03()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var registrado = Unico();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email = registrado, contrasena = "una frase larga y buena" }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        var conEmailNuevo = await MedirAsync(cliente, Unico());
        var conEmailRegistrado = await MedirAsync(cliente, registrado);

        Assert.True(
            conEmailRegistrado > conEmailNuevo / 5,
            $"El alta con un email ya registrado tardó {conEmailRegistrado:F0} ms y la que creó la " +
            $"cuenta {conEmailNuevo:F0} ms. Una diferencia así indica que el camino 'ya existe' no " +
            "está hasheando nada, y eso permite distinguir los emails registrados con un cronómetro.");
    }

    /// <summary>Lo que tarda un alta con <paramref name="email"/>, en milisegundos.</summary>
    private static async Task<double> MedirAsync(HttpClient cliente, string email)
    {
        // Dos corridas y se toma la mayor: la primera paga la compilación del pipeline. El email
        // nuevo se pide una vez por llamada, así que la segunda corrida de un email nuevo ya lo
        // encuentra registrado — y eso está bien: lo que se mide es el piso de cada camino.
        double mayor = 0;

        for (var i = 0; i < 2; i++)
        {
            var cronometro = System.Diagnostics.Stopwatch.StartNew();
            using var respuesta = await cliente.PostAsJsonAsync(
                new Uri("/api/cuentas", UriKind.Relative),
                new { email, contrasena = "una frase larga y buena" });
            cronometro.Stop();
            mayor = Math.Max(mayor, cronometro.Elapsed.TotalMilliseconds);
        }

        return mayor;
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

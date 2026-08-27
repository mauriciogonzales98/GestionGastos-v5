using System.Net;
using System.Net.Http.Json;
using GestionGastos.Api.Cuentas;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Sesion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El límite de intentos fallidos contra la API (RNF-05, ticket 01b).
///
/// Ningún test de acá espera: la ventana de 15 minutos se recorre adelantando el reloj de
/// <see cref="FactoriaConReloj"/>. El Principio IV prohíbe tests que dependan del paso del tiempo
/// real.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class LimiteDeIntentosTests(BaseDeDatosFixture baseDeDatos)
{
    private const string Contrasena = "una frase larga y buena";
    private static readonly DateOnly Hoy = new(2026, 8, 26);
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-01 (FR-01, FR-02): con cinco fallos consecutivos, el sexto intento se rechaza.
    ///
    /// El sexto va **con la contraseña correcta**: con la incorrecta el test pasaría en verde sin
    /// que exista ningún límite, porque una contraseña incorrecta se rechaza igual.
    /// </summary>
    [Fact]
    public async Task El_Sexto_Intento_Se_Rechaza_AC01()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos);

        using var sexto = await IntentarAsync(cliente, email, Contrasena);
        Assert.Equal(HttpStatusCode.Unauthorized, sexto.StatusCode);

        await using var contexto = _baseDeDatos.CrearContexto();
        var fila = await contexto.IntentosDeAcceso.SingleAsync(i => i.Email == email);
        Assert.Equal(LimiteDeIntentos.MaximoDeFallos, fila.FallosConsecutivos);
    }

    /// <summary>
    /// AC-02 (FR-02): dentro de la ventana, la contraseña correcta se rechaza y **no** deja sesión
    /// iniciada. Que devuelva 401 sin iniciar sesión son dos cosas distintas: un rechazo que igual
    /// entregara la cookie cumpliría el código de estado y fallaría el requisito.
    /// </summary>
    [Fact]
    public async Task La_Contrasena_Correcta_Tampoco_Entra_Dentro_De_La_Ventana_AC02()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos);

        using var intento = await IntentarAsync(cliente, email, Contrasena);
        Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
        Assert.False(
            intento.Headers.TryGetValues("Set-Cookie", out _),
            "El intento bloqueado devolvió una cookie: el rechazo entregó sesión igual.");

        using var sesion = await cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, sesion.StatusCode);
    }

    /// <summary>
    /// AC-03 (FR-02): a los 14 minutos del quinto fallo sigue rechazando. Se adelanta el reloj, no
    /// se espera.
    /// </summary>
    [Fact]
    public async Task Dentro_De_Los_Quince_Minutos_Sigue_Rechazando_AC03()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos);

        factoria.Reloj.Avanzar(TimeSpan.FromMinutes(14));

        using var intento = await IntentarAsync(cliente, email, Contrasena);
        Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
    }

    /// <summary>
    /// AC-04 (FR-01): con cuatro fallos —uno menos que el límite— el quinto intento correcto entra.
    /// Es el borde por abajo: sin este test, un límite de 4 pasaría desapercibido.
    /// </summary>
    [Fact]
    public async Task Con_Cuatro_Fallos_El_Quinto_Correcto_Entra_AC04()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos - 1);

        using var intento = await IntentarAsync(cliente, email, Contrasena);
        Assert.Equal(HttpStatusCode.OK, intento.StatusCode);
    }

    /// <summary>
    /// AC-07 (FR-01): el bloqueo alcanza a un email, no a la aplicación. Sin este test, un límite
    /// global —o un bug que bloquee de más— pasaría por bueno.
    /// </summary>
    [Fact]
    public async Task El_Bloqueo_De_Un_Email_No_Alcanza_A_Otro_AC07()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var bloqueado = await CrearCuentaAsync(factoria);
        var otro = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, bloqueado, LimiteDeIntentos.MaximoDeFallos);

        using var intento = await IntentarAsync(cliente, otro, Contrasena);
        Assert.Equal(HttpStatusCode.OK, intento.StatusCode);
    }

    /// <summary>
    /// AC-09 (FR-01, FR-06), la mitad que es de conteo: un email **no registrado** también acumula
    /// fallos y también se bloquea. Si sólo contara los registrados, fallar seis veces y mirar si
    /// la respuesta cambia diría qué emails tienen cuenta.
    /// </summary>
    [Fact]
    public async Task Un_Email_No_Registrado_Tambien_Acumula_Y_Se_Bloquea_AC09()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = $"nadie-{Guid.NewGuid():N}@ejemplo.com";

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos);

        await using var contexto = _baseDeDatos.CrearContexto();
        var fila = await contexto.IntentosDeAcceso.SingleAsync(i => i.Email == email);
        Assert.Equal(LimiteDeIntentos.MaximoDeFallos, fila.FallosConsecutivos);
    }

    /// <summary>
    /// AC-10 (FR-06): lo que está bloqueado es el email, no quien lo intenta. Otro
    /// <see cref="HttpClient"/> es otro navegador: no comparte cookies con el primero.
    /// </summary>
    [Fact]
    public async Task El_Bloqueo_Sigue_Desde_Otro_Navegador_AC10()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = await CrearCuentaAsync(factoria);

        using var primero = factoria.CreateClient();
        await FallarAsync(primero, email, LimiteDeIntentos.MaximoDeFallos);

        using var otroNavegador = factoria.CreateClient();
        using var intento = await IntentarAsync(otroNavegador, email, Contrasena);
        Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
    }

    /// <summary>
    /// AC-05 (FR-03): un intento exitoso deja el contador en cero.
    ///
    /// Se verifican las dos mitades: que la fila desaparece, y que después hacen falta **cinco**
    /// fallos nuevos para bloquear. Sin la segunda, un reinicio parcial —que dejara el contador en
    /// 3, por ejemplo— pasaría en verde.
    /// </summary>
    [Fact]
    public async Task Un_Intento_Exitoso_Deja_El_Contador_En_Cero_AC05()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos - 1);

        using (var entrada = await IntentarAsync(cliente, email, Contrasena))
        {
            Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
        }

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            Assert.Null(await contexto.IntentosDeAcceso.FirstOrDefaultAsync(i => i.Email == email));
        }

        // Y el contador arranca de cero de verdad: cuatro fallos nuevos todavía no bloquean.
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos - 1);

        using var todaviaEntra = await IntentarAsync(cliente, email, Contrasena);
        Assert.Equal(HttpStatusCode.OK, todaviaEntra.StatusCode);
    }

    /// <summary>
    /// AC-06 (FR-04): a los 15 minutos, la contraseña correcta entra, sin que nadie haya
    /// intervenido. Y el caso complementario: un fallo posterior al vencimiento deja el contador en
    /// **1** y no en 6, así que hacen falta cuatro más para volver a bloquear (research.md D-03).
    /// </summary>
    [Fact]
    public async Task La_Ventana_Se_Levanta_Sola_A_Los_Quince_Minutos_AC06()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var email = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos);

        factoria.Reloj.Avanzar(LimiteDeIntentos.Ventana + TimeSpan.FromSeconds(1));

        // Un fallo justo después del vencimiento: el contador tiene que arrancar de nuevo en 1.
        await FallarAsync(cliente, email, 1);

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            var fila = await contexto.IntentosDeAcceso.SingleAsync(i => i.Email == email);
            Assert.Equal(1, fila.FallosConsecutivos);
        }

        using var entrada = await IntentarAsync(cliente, email, Contrasena);
        Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
    }

    /// <summary>
    /// La purga por inactividad (research.md D-03): la fila de un email sin intentos en más de 24 h
    /// desaparece cuando se registra el fallo de otro email, y ese email vuelve a foja cero.
    ///
    /// Sin este test la tabla crece una fila por cada email jamás presentado —incluida la lista
    /// entera que use un atacante— y nadie se entera hasta que alguien mira el disco.
    /// </summary>
    [Fact]
    public async Task Las_Filas_Sin_Intentos_En_Veinticuatro_Horas_Se_Purgan()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var viejo = await CrearCuentaAsync(factoria);
        var otro = $"otro-{Guid.NewGuid():N}@ejemplo.com";

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, viejo, LimiteDeIntentos.MaximoDeFallos);

        factoria.Reloj.Avanzar(LimiteDeIntentos.InactividadQueReinicia + TimeSpan.FromMinutes(1));

        // El fallo de OTRO email es el que arrastra la purga.
        await FallarAsync(cliente, otro, 1);

        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Null(await contexto.IntentosDeAcceso.FirstOrDefaultAsync(i => i.Email == viejo));
    }

    /// <summary>
    /// AC-13 (NFR-03), su mitad determinista: el rechazo de un email bloqueado **también verifica
    /// un hash**.
    ///
    /// Es la conducta que produce el tiempo. Salir temprano con un `if` respondería en ~2 ms contra
    /// los ~100 ms del rechazo por contraseña incorrecta, y ese cronómetro dice qué emails
    /// acumularon cinco fallos — justo lo que el bloqueo no puede publicar. El test de tiempo vive
    /// en `Rendimiento/` y el CI lo excluye; éste corre siempre.
    /// </summary>
    [Fact]
    public async Task El_Rechazo_Por_Bloqueo_Tambien_Paga_El_Costo_Del_Hash_AC13()
    {
        var espia = new HasherEspia();
        using var factoria = new FactoriaConReloj(
            Hoy, servicios => servicios.AddSingleton<HasherDeContrasenas>(espia));

        var email = await CrearCuentaAsync(factoria);

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, email, LimiteDeIntentos.MaximoDeFallos);

        espia.Reiniciar();

        using var bloqueado = await IntentarAsync(cliente, email, Contrasena);
        Assert.Equal(HttpStatusCode.Unauthorized, bloqueado.StatusCode);

        Assert.Equal(1, espia.Verificaciones);
    }

    /// <summary>
    /// AC-08 (FR-05) y AC-09 (FR-06): las tres respuestas de rechazo son la misma.
    ///
    /// Se comparan **el cuerpo entero** y el código, no sólo el status: un campo de más en el
    /// cuerpo del rechazo por bloqueo es la misma filtración que un código distinto. Un email
    /// bloqueado **no registrado** entra en la comparación por AC-09: si su respuesta difiriera de
    /// la de uno registrado y bloqueado, el bloqueo diría qué emails tienen cuenta.
    /// </summary>
    [Fact]
    public async Task El_Bloqueo_Responde_Igual_Que_Las_Credenciales_Incorrectas_AC08_AC09()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        var registrado = await CrearCuentaAsync(factoria);
        var conContrasenaMal = await CrearCuentaAsync(factoria);
        var noRegistrado = $"nadie-{Guid.NewGuid():N}@ejemplo.com";

        using var cliente = factoria.CreateClient();
        await FallarAsync(cliente, registrado, LimiteDeIntentos.MaximoDeFallos);
        await FallarAsync(cliente, noRegistrado, LimiteDeIntentos.MaximoDeFallos);

        using var porCredenciales = await IntentarAsync(cliente, conContrasenaMal, "la que no es");
        using var porBloqueo = await IntentarAsync(cliente, registrado, Contrasena);
        using var porBloqueoSinCuenta = await IntentarAsync(cliente, noRegistrado, Contrasena);

        Assert.Equal(HttpStatusCode.Unauthorized, porCredenciales.StatusCode);
        Assert.Equal(porCredenciales.StatusCode, porBloqueo.StatusCode);
        Assert.Equal(porCredenciales.StatusCode, porBloqueoSinCuenta.StatusCode);

        var esperado = await SinTraceIdAsync(porCredenciales);
        Assert.Equal(esperado, await SinTraceIdAsync(porBloqueo));
        Assert.Equal(esperado, await SinTraceIdAsync(porBloqueoSinCuenta));
    }

    /// <summary>
    /// Un email más largo que la columna se rechaza con el 401 de siempre, no con un 500.
    ///
    /// El login nunca validó el email porque nunca lo escribía. Ahora lo escribe: `varchar(254)` y
    /// MySQL en modo estricto convierten un email de 256 caracteres en un `Data too long` que sube
    /// sin manejar. Es la misma cicatriz que documenta `ValidacionDeLaCuenta.LargoMaximoDeEmail`,
    /// que ya se pagó una vez en el alta de cuenta.
    /// </summary>
    [Fact]
    public async Task Un_Email_Mas_Largo_Que_La_Columna_Se_Rechaza_Sin_Romper()
    {
        await _baseDeDatos.LimpiarIntentosDeAccesoAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cliente = factoria.CreateClient();

        var demasiadoLargo =
            new string('a', ValidacionDeLaCuenta.LargoMaximoDeEmail) + "@ejemplo.com";

        using var respuesta = await IntentarAsync(cliente, demasiadoLargo, Contrasena);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        // Y tampoco deja rastro: un email que no entra en la columna no puede tener cuenta, así que
        // contarle los fallos no protege nada y sólo da una forma de escribir basura en la tabla.
        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Empty(await contexto.IntentosDeAcceso.ToListAsync());
    }

    /// <summary>
    /// El email vacío no lleva contador, que es lo que dice la spec en sus casos borde.
    ///
    /// Sin esto, `{"email": null}` termina como una fila con `email = ''`: cinco intentos y el
    /// "email vacío" queda bloqueado, que no protege a nadie de nada.
    /// </summary>
    [Fact]
    public async Task El_Email_Vacio_No_Lleva_Contador()
    {
        await _baseDeDatos.LimpiarIntentosDeAccesoAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cliente = factoria.CreateClient();

        using var respuesta = await IntentarAsync(cliente, "   ", Contrasena);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Empty(await contexto.IntentosDeAcceso.ToListAsync());
    }

    /// <summary>
    /// El contador no puede desbordar el `tinyint unsigned` de la columna.
    ///
    /// Sólo incrementan los intentos que ya pasaron el chequeo de bloqueo, pero ese chequeo se lee
    /// al principio de cada petición: con cientos de intentos concurrentes sobre el mismo email
    /// —que es el perfil de un ataque de fuerza bruta, y bcrypt deja la ventana de solapamiento bien
    /// abierta— todos leen "no bloqueado" y todos incrementan. Pasado 255, MySQL corta con un
    /// `Out of range value` que llega al cliente como un 500.
    ///
    /// Se siembra el borde en vez de fabricar la concurrencia: el valor 255 es el que rompe, y
    /// llegar ahí con peticiones reales sería un test que falla a veces (Principio IV).
    /// </summary>
    [Fact]
    public async Task El_Contador_No_Desborda_El_Byte_De_La_Columna()
    {
        await _baseDeDatos.LimpiarIntentosDeAccesoAsync();

        var reloj = new RelojFijo(Hoy);
        var email = $"desborde-{Guid.NewGuid():N}@ejemplo.com";

        await using var contexto = _baseDeDatos.CrearContexto();
        contexto.IntentosDeAcceso.Add(new IntentoDeAcceso
        {
            Email = email,
            FallosConsecutivos = byte.MaxValue,
            UltimoFallo = reloj.GetUtcNow().UtcDateTime,
        });
        await contexto.SaveChangesAsync();

        var limite = new LimiteDeIntentos(contexto, reloj);
        await limite.RegistrarFalloAsync(email);

        var fila = await contexto.IntentosDeAcceso.AsNoTracking().SingleAsync(i => i.Email == email);
        Assert.Equal(LimiteDeIntentos.MaximoDeFallos, fila.FallosConsecutivos);
    }

    /// <summary>El cuerpo del error, campo por campo, sin el `traceId` —distinto en cada petición
    /// por definición, y mudo respecto de la causa del rechazo—.</summary>
    private static async Task<Dictionary<string, string>> SinTraceIdAsync(HttpResponseMessage respuesta)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return json.RootElement.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "traceId", StringComparison.Ordinal))
            .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.Ordinal);
    }

    /// <summary>Crea una cuenta por la API y devuelve su email. No deja sesión iniciada.</summary>
    private async Task<string> CrearCuentaAsync(FactoriaConReloj factoria)
    {
        var email = $"limite-{Guid.NewGuid():N}@ejemplo.com";

        using var cliente = factoria.CreateClient();
        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        await _baseDeDatos.LimpiarIntentosDeAccesoAsync();
        return email;
    }

    private static async Task FallarAsync(HttpClient cliente, string email, int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            using var intento = await IntentarAsync(cliente, email, "esta no es la contraseña");
            Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> IntentarAsync(
        HttpClient cliente, string email, string contrasena) =>
        cliente.PostAsJsonAsync(new Uri("/api/sesion", UriKind.Relative), new { email, contrasena });
}

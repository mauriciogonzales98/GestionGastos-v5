using GestionGastos.Api.Categorias;
using GestionGastos.Api.Cuentas;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Persistencia;
using GestionGastos.Api.Sesion;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// La cadena de conexión vive en user-secrets, nunca en appsettings (AGENTS.md, Code conventions).
// En el CI y en la suite de tests llega por la variable de entorno ConnectionStrings__Default.
var cadena = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión 'Default'. Va en user-secrets o en la variable de entorno " +
        "ConnectionStrings__Default. No se adivina: apuntar sin querer a la base equivocada es " +
        "peor que no arrancar.");

// Versión fija en vez de AutoDetect: AutoDetect abre una conexión al construir el modelo, así que
// haría falta un servidor vivo hasta para generar una migración.
builder.Services.AddDbContext<GestionGastosDbContext>(o =>
    o.UseMySql(cadena, new MySqlServerVersion(new Version(8, 4, 10))));

// El usuario actual sale de la sesión, no de una fila fija (D-05). Scoped porque depende de la
// petición en curso.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioDeLaSesion>();
builder.Services.AddSingleton<HasherDeContrasenas>();

// El límite de intentos fallidos (RNF-05). Scoped porque usa el DbContext de la petición.
builder.Services.AddScoped<LimiteDeIntentos>();

// Autenticación por cookie: HttpOnly la vuelve inalcanzable desde JavaScript, así que un XSS no
// puede robar la sesión; con un token en localStorage sí podría. SameSite=Strict cubre CSRF sin
// agregar un token anti-CSRF (D-01).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "gestiongastos.sesion";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Strict;
        opciones.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        // "24 h SIN ACTIVIDAD" (NFR-02), no 24 h desde el login: deslizante es literalmente eso.
        opciones.ExpireTimeSpan = TimeSpan.FromHours(24);
        opciones.SlidingExpiration = true;

        // Sin redirecciones: esto es una API. Un 302 al login rompería el cliente, que espera un
        // 401 para saber que la sesión venció (D-09).
        opciones.Events.OnRedirectToLogin = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        opciones.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Autorización GLOBAL con excepciones explícitas, y no endpoint por endpoint.
//
// La diferencia importa el día que alguien agregue un endpoint: así nace protegido y hay que
// acordarse de abrirlo, en vez de nacer abierto y haber que acordarse de cerrarlo. Un endpoint sin
// proteger es el agujero más fácil de dejar, y el que menos se nota.
//
// Las dos excepciones —alta e inicio de sesión— llevan `.AllowAnonymous()` en su propia definición.
// Si también exigieran sesión, no habría forma de obtener una.
builder.Services.AddAuthorization(opciones =>
{
    opciones.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// El reloj de la autenticación es el MISMO TimeProvider que el resto de la aplicación. Es lo que
// vuelve verificable AC-12 adelantando el reloj, en vez de esperando 24 h (Principio IV).
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((opciones, reloj) => opciones.TimeProvider = reloj);

// El formato único de error del contrato, también para lo que no corresponde a ningún campo.
// Sin esto, una excepción sin manejar salía como la página de excepción del framework —con stack
// trace y la consulta SQL— o, fuera de desarrollo, como un 500 con el cuerpo vacío. Ninguna de las
// dos es lo que el contrato promete, y el frontend usa la ausencia de `errors` para decidir que el
// mensaje va a la región del formulario y no al lado de un control.
builder.Services.AddProblemDetails();

// El reloj es un servicio para que los tests puedan clavarlo en una fecha (D-03, AC-17).
builder.Services.AddSingleton(TimeProvider.System);

// La zona horaria en la que el servidor decide qué día es hoy. Explícita y configurable, NO la del
// proceso: en un contenedor la del proceso es UTC, y con la persona usuaria en Argentina el
// servidor pasa al día siguiente a las 21:00 mientras el navegador todavía no. Ahí el recorte del
// mes del listado deja de coincidir con la fecha que el formulario propone.
builder.Services.AddSingleton(
    TimeZoneInfo.FindSystemTimeZoneById(
        builder.Configuration["Aplicacion:ZonaHoraria"] ?? "America/Argentina/Buenos_Aires"));

var app = builder.Build();

// Convierte cualquier excepción sin manejar en un ProblemDetails 500. En Development el
// framework antepone su propia página de excepción, que es más útil para depurar; lo que ve un
// cliente real es esto.
app.UseExceptionHandler();

// Aplicar las migraciones al arrancar, SÓLO en desarrollo.
//
// El quickstart promete que `dotnet run` deja la base usable, y hasta ahora esa promesa era falsa:
// la API no migraba nada y la primera petición moría con "Table doesn't exist". No se notaba porque
// el fixture de tests migra por su cuenta, así que la suite corría verde sobre una base que se
// preparaba sola y nadie ejecutaba el camino que el quickstart documenta.
//
// Fuera de desarrollo NO se migra automáticamente: aplicar un cambio de esquema es una decisión
// deliberada, con su ventana y su respaldo, no un efecto secundario de reiniciar un proceso. En
// producción va `dotnet ef database update` como paso propio del despliegue.
if (app.Environment.IsDevelopment())
{
    using var alcance = app.Services.CreateScope();
    await alcance.ServiceProvider.GetRequiredService<GestionGastosDbContext>().Database.MigrateAsync();
}

app.MapGet("/", () => "GestionGastos API");

app.UseAuthentication();
app.UseAuthorization();

app.MapCuentas();
app.MapSesion();
app.MapCategorias();
app.MapMovimientos();

await app.RunAsync();

/// <summary>
/// Visible para que los tests de integración puedan levantar la aplicación con
/// WebApplicationFactory. Con instrucciones de nivel superior, la clase generada es internal.
/// </summary>
public partial class Program;

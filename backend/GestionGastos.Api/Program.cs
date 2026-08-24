using GestionGastos.Api.Categorias;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Persistencia;
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

builder.Services.AddSingleton<IUsuarioActual, UsuarioSemilla>();

// El reloj es un servicio para que los tests puedan clavarlo en una fecha (D-03, AC-17).
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

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

app.MapCategorias();
app.MapMovimientos();

await app.RunAsync();

/// <summary>
/// Visible para que los tests de integración puedan levantar la aplicación con
/// WebApplicationFactory. Con instrucciones de nivel superior, la clase generada es internal.
/// </summary>
public partial class Program;

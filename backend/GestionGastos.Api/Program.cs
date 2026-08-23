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

app.MapGet("/", () => "GestionGastos API");

app.MapCategorias();
app.MapMovimientos();

app.Run();

/// <summary>
/// Visible para que los tests de integración puedan levantar la aplicación con
/// WebApplicationFactory. Con instrucciones de nivel superior, la clase generada es internal.
/// </summary>
public partial class Program;

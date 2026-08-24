using GestionGastos.Api.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// La aplicación apuntando a una base que no está. Sirve para ejercitar el camino que ningún test
/// tocaba: qué contesta la API cuando algo revienta y el fallo no corresponde a ningún campo.
///
/// El entorno es Production a propósito. En Development el framework antepone su página de
/// excepción —con stack y SQL— y esa es una comodidad de desarrollo, no el contrato. Lo que el
/// contrato promete es lo que ve un cliente real.
/// </summary>
public sealed class FactoriaConBaseRota : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureServices(servicios =>
        {
            servicios.AddSingleton(TimeZoneInfo.Utc);
            servicios.RemoveAll<DbContextOptions<GestionGastosDbContext>>();
            servicios.RemoveAll<GestionGastosDbContext>();

            // Puerto 1: nadie escucha, así que la conexión se rechaza de inmediato en vez de
            // esperar un timeout.
            servicios.AddDbContext<GestionGastosDbContext>(o => o.UseMySql(
                "Server=127.0.0.1;Port=1;Database=no_existe;User Id=nadie;Password=nada;",
                new MySqlServerVersion(new Version(8, 4, 10))));
        });
    }
}

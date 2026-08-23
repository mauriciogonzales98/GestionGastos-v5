using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// La aplicación de verdad, con el reloj reemplazado por uno clavado en una fecha.
///
/// Es una subclase y no <c>WithWebHostBuilder</c> porque ese método devuelve una fábrica nueva y
/// deja sin liberar la original —CA2000 lo marca, y tiene razón: son dos servidores levantados
/// para usar uno.
/// </summary>
public sealed class FactoriaConReloj(DateOnly hoy) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(servicios =>
            servicios.AddSingleton<TimeProvider>(new RelojFijo(hoy)));
    }
}

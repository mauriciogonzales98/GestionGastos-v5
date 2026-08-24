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
/// <param name="hoy">El día en que queda clavado el reloj.</param>
/// <param name="extras">
/// Servicios que este test agrega encima. Existe para poder inyectar un interceptor que fabrica una
/// condición de carrera de forma determinista: sin él, esa carrera sólo se reproduce a veces, y un
/// test que falla a veces no verifica nada (Principio IV).
/// </param>
public sealed class FactoriaConReloj(DateOnly hoy, Action<IServiceCollection>? extras = null)
    : WebApplicationFactory<Program>
{
    /// <summary>El reloj de esta aplicación, para que un test pueda adelantarlo (AC-12).</summary>
    public RelojFijo Reloj { get; } = new(hoy);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(servicios =>
        {
            servicios.AddSingleton<TimeProvider>(Reloj);

            // Zona UTC junto al reloj UTC: la fecha inyectada es la que el servidor tiene que ver,
            // sin corrimientos. La conversión de zona se verifica aparte, en DiaActualTests, con
            // los bordes donde importa.
            servicios.AddSingleton(TimeZoneInfo.Utc);

            extras?.Invoke(servicios);
        });
    }
}

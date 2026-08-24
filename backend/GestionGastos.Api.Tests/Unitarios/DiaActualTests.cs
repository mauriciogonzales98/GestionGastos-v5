using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// De qué día habla el servidor cuando dice "hoy".
///
/// El problema que esto cierra: había DOS relojes. El navegador calculaba su hoy local y el
/// servidor usaba la zona horaria del proceso, que en un contenedor es UTC. Con la persona en
/// Argentina (UTC-3), a partir de las 21:00 los dos dejaban de coincidir, y el recorte del mes del
/// listado se corría un día antes que el del formulario.
/// </summary>
public class DiaActualTests
{
    private static readonly TimeZoneInfo Argentina =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    [Fact]
    public void A_Las_23_30_De_Argentina_El_Dia_Es_El_De_Argentina_Y_No_El_De_UTC()
    {
        // 2026-09-01T02:30Z son las 23:30 del 31 de agosto en Buenos Aires.
        var reloj = new RelojEnInstante(new DateTimeOffset(2026, 9, 1, 2, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 8, 31), DiaActual.De(reloj, Argentina));

        // Y para que se vea el error que esto evita: en UTC ese mismo instante ya es septiembre.
        Assert.Equal(new DateOnly(2026, 9, 1), DiaActual.De(reloj, TimeZoneInfo.Utc));
    }

    [Fact]
    public void El_Primero_Del_Mes_Temprano_Sigue_Siendo_El_Primero()
    {
        // 2026-09-01T12:00Z son las 09:00 del 1 de septiembre en Buenos Aires.
        var reloj = new RelojEnInstante(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 9, 1), DiaActual.De(reloj, Argentina));
    }

    [Fact]
    public void Con_Zona_UTC_Devuelve_El_Dia_UTC()
    {
        var reloj = new RelojEnInstante(new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 8, 15), DiaActual.De(reloj, TimeZoneInfo.Utc));
    }

    private sealed class RelojEnInstante(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}

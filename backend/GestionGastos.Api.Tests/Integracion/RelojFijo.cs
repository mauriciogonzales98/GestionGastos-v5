namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Un <see cref="TimeProvider"/> clavado en un instante que el test puede mover.
///
/// Es lo que vuelve deterministas AC-17 y AC-12: sin esta costura, "la fecha por defecto es hoy" se
/// verifica contra sí mismo, y "la sesión expira a las 24 h" exigiría esperar un día.
///
/// El Principio IV prohíbe tests que dependan del paso del tiempo real.
/// </summary>
public sealed class RelojFijo : TimeProvider
{
    private DateTimeOffset _ahora;

    public RelojFijo(DateOnly hoy)
    {
        _ahora = new DateTimeOffset(hoy.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _ahora;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>Mueve el reloj hacia adelante. Es la única forma de verificar una expiración.</summary>
    public void Avanzar(TimeSpan cuanto) => _ahora = _ahora.Add(cuanto);
}

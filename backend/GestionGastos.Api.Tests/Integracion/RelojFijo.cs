namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Un <see cref="TimeProvider"/> clavado en una fecha. Es lo que vuelve determinista AC-17: sin
/// esta costura, el test de "la fecha por defecto es hoy" se verifica contra sí mismo y pasa
/// aunque el servidor esté poniendo cualquier cosa.
///
/// El Principio IV prohíbe tests que dependan del día en que corren.
/// </summary>
public sealed class RelojFijo(DateOnly hoy) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(hoy.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

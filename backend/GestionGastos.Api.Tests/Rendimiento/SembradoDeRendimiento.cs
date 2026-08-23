namespace GestionGastos.Api.Tests.Rendimiento;

/// <summary>
/// El sembrado de las mediciones de rendimiento.
///
/// Las fechas se generan con una función pura parametrizada por fecha y ancladas al año de esa
/// fecha, nunca a un año escrito a mano. Es la lección que el plan DISC-001 deja en FIX-004: un
/// sembrado anclado a un año fijo vence, y el día que vence el test no falla ruidosamente — mide
/// una tabla vacía y pasa en verde.
/// </summary>
public static class SembradoDeRendimiento
{
    /// <summary>
    /// Fechas repartidas por el mes de <paramref name="hoy"/>, con varias repetidas para que el
    /// desempate por id tenga sobre qué trabajar.
    /// </summary>
    public static IReadOnlyList<DateOnly> GenerarFechasSembradas(DateOnly hoy, int cantidad)
    {
        var primero = new DateOnly(hoy.Year, hoy.Month, 1);
        var diasDelMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);

        return [.. Enumerable.Range(0, cantidad).Select(i => primero.AddDays(i % diasDelMes))];
    }
}

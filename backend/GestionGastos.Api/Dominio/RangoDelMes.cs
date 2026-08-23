namespace GestionGastos.Api.Dominio;

/// <summary>
/// El mes calendario de una fecha, con los dos extremos incluidos. Es lo que FR-007 llama "el mes
/// actual": el recorte fijo del listado, que no se expone como control.
///
/// El "hoy" entra por parámetro y nunca se lee de <see cref="DateTime.Now"/> acá adentro. Esa es la
/// decisión D-03 de research.md, y es lo que vuelve verificables los bordes de mes —febrero
/// bisiesto incluido— sin esperar a que llegue la fecha.
/// </summary>
public readonly record struct RangoDelMes(DateOnly Primero, DateOnly Ultimo)
{
    /// <summary>
    /// El rango del mes al que pertenece <paramref name="hoy"/>.
    /// </summary>
    public static RangoDelMes De(DateOnly hoy) => new(
        new DateOnly(hoy.Year, hoy.Month, 1),
        new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
}

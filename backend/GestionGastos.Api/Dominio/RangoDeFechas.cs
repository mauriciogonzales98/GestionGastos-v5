namespace GestionGastos.Api.Dominio;

/// <summary>
/// Un período con **sus dos extremos incluidos**.
///
/// Generaliza a <see cref="RangoDelMes"/>, que sólo sabía construir meses calendario: eso alcanzaba
/// para el recorte fijo del listado (FR-007 de la feature 001) y no para el filtro por rango que
/// pide RF-18.
///
/// **El invariante `Desde &lt;= Hasta` vive en el tipo**, y por eso el constructor es privado. Es lo
/// que vuelve a FR-015 —rechazar un rango invertido— una validación de borde en un solo lugar, en
/// vez de una condición repetida en cada consulta que reciba fechas.
/// </summary>
public readonly record struct RangoDeFechas
{
    private RangoDeFechas(DateOnly desde, DateOnly hasta)
    {
        Desde = desde;
        Hasta = hasta;
    }

    /// <summary>El primer día del período. Incluido.</summary>
    public DateOnly Desde { get; }

    /// <summary>El último día del período. **Incluido**: un movimiento fechado acá adentro entra.</summary>
    public DateOnly Hasta { get; }

    /// <summary>
    /// El rango entre dos fechas, o <c>null</c> si están invertidas.
    ///
    /// Devuelve <c>null</c> en vez de lanzar porque quien lo llama es un endpoint con la entrada de
    /// una persona en la mano: un rango invertido es una petición mal formada, no un fallo del
    /// programa.
    /// </summary>
    public static RangoDeFechas? De(DateOnly desde, DateOnly hasta) =>
        desde <= hasta ? new RangoDeFechas(desde, hasta) : null;

    /// <summary>
    /// El rango del mes calendario al que pertenece <paramref name="dia"/>.
    ///
    /// Nunca puede quedar invertido, así que no devuelve nulo.
    /// </summary>
    public static RangoDeFechas DelMesDe(DateOnly dia) => new(
        new DateOnly(dia.Year, dia.Month, 1),
        new DateOnly(dia.Year, dia.Month, DateTime.DaysInMonth(dia.Year, dia.Month)));
}

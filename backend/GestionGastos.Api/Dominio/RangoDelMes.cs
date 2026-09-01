namespace GestionGastos.Api.Dominio;

/// <summary>
/// El mes calendario de una fecha. Es lo que FR-007 llama "el mes actual": el recorte por omisión
/// del listado, que el servidor decide y el cliente no puede cambiar.
///
/// Desde FEAT-001b devuelve un <see cref="RangoDeFechas"/> en vez de ser un tipo propio: el mes es
/// **una forma de construir** un rango, no una clase distinta de rango. El listado con filtros pide
/// uno arbitrario y la consulta no tiene por qué distinguir de dónde vino.
///
/// El "hoy" entra por parámetro y nunca se lee de <see cref="DateTime.Now"/> acá adentro. Esa es la
/// decisión D-03 de la feature 001, y es lo que vuelve verificables los bordes de mes —febrero
/// bisiesto incluido— sin esperar a que llegue la fecha.
/// </summary>
public static class RangoDelMes
{
    /// <summary>El rango del mes al que pertenece <paramref name="hoy"/>.</summary>
    public static RangoDeFechas De(DateOnly hoy) => RangoDeFechas.DelMesDe(hoy);
}

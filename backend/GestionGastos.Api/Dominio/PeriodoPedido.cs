namespace GestionGastos.Api.Dominio;

/// <summary>
/// **El único intérprete de `desde` y `hasta`** (D-03 de la feature 006).
///
/// Traduce lo que llega por la URL a un <see cref="RangoDeFechas"/>, o a un rechazo con su motivo.
/// Lo usan el listado y el resumen, y que sea uno solo no es prolijidad: FR-005 exige que las dos
/// vistas describan el mismo conjunto ante el mismo período. Con dos intérpretes, esa igualdad
/// depende de que nadie toque uno sin tocar el otro — y el día que se separen, quien mire la
/// pantalla no tiene forma de saber cuál de los dos números está mal.
///
/// Estas reglas nacieron escritas a mano adentro del endpoint del listado en FEAT-001b. Acá no
/// cambian: se mudan.
///
/// El "hoy" entra por parámetro y nunca se lee de <see cref="DateTime.Now"/>. Es la D-03 de la
/// feature 001, y es lo que vuelve verificable el mes por omisión sin esperar a que cambie el mes.
/// </summary>
public static class PeriodoPedido
{
    /// <summary>
    /// La clave con la que viajan los errores del período.
    ///
    /// Es una constante y no un literal repetido porque el frontend la usa para poner el mensaje al
    /// lado del control: si cambia acá, tiene que cambiar en un solo lugar.
    /// </summary>
    public const string Clave = "rango";

    /// <summary>
    /// Interpreta el período pedido.
    ///
    /// Devuelve el diccionario de errores —vacío si todo está bien, con la forma que espera
    /// `Results.ValidationProblem`— y deja el rango en <paramref name="rango"/>. Es la misma forma
    /// que <c>ValidacionDelMovimiento.Validar</c>, para que los endpoints traten a las dos
    /// validaciones igual.
    ///
    /// Cuando hay error, <paramref name="rango"/> queda en el mes en curso. No es un valor útil y
    /// no hay que usarlo: quien recibe errores responde con ellos. Se deja definido para que el
    /// tipo no obligue a un nullable que después haya que desenvolver en el camino feliz.
    /// </summary>
    /// <param name="desde">Primer día del período, incluido. Ausente = sin rango pedido.</param>
    /// <param name="hasta">Último día del período, incluido. Ausente = sin rango pedido.</param>
    /// <param name="hoy">El día del servidor, del que sale el mes por omisión.</param>
    /// <param name="rango">El período resultante.</param>
    public static Dictionary<string, string[]> Interpretar(
        DateOnly? desde,
        DateOnly? hasta,
        DateOnly hoy,
        out RangoDeFechas rango)
    {
        rango = RangoDelMes.De(hoy);

        // Los dos extremos van juntos o no va ninguno. Con medio rango habría que suponer un
        // extremo abierto que nadie declaró, y ese supuesto es distinto para cada quien.
        if (desde is null != hasta is null)
        {
            return Error("Indicá las dos fechas del rango, o ninguna.");
        }

        // Sin rango pedido, el recorte es el mes en curso, y lo decide el SERVIDOR (FR-002): que el
        // filtro exista no convierte al valor por omisión en algo que el cliente elige.
        //
        // Alcanza con mirar UNO de los dos: la guarda de arriba ya garantizó que o vinieron los dos
        // o no vino ninguno. Preguntar por los dos acá dejaría una rama que no se puede alcanzar, y
        // una rama inalcanzable es una que ningún test puede cubrir — se ve como cobertura faltante
        // y no lo es, que es la peor clase de hueco: el que enseña a ignorar el informe.
        if (desde is not { } d)
        {
            return [];
        }

        var h = hasta!.Value;

        // Un rango invertido se rechaza en vez de devolver un resultado vacío: el vacío se lee como
        // "no hay nada" y esconde que la pregunta estaba mal formada (FR-004).
        if (RangoDeFechas.De(d, h) is not { } pedido)
        {
            return Error("La fecha de inicio no puede ser posterior a la de fin.");
        }

        rango = pedido;
        return [];
    }

    private static Dictionary<string, string[]> Error(string mensaje) =>
        new(StringComparer.Ordinal) { [Clave] = [mensaje] };
}

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Lo que llega en el alta. `fecha` es opcional: ausente o null significa "hoy", y ese valor lo
/// pone el servidor y no el cliente (AC-17) — es la única forma de que el test sea determinista.
///
/// **`monedaId` también es opcional, y ausente significa "la predeterminada del catálogo"**
/// (FR-002, feature 009). Hasta el ticket 4b este campo no existía y la moneda la decidía siempre
/// el servidor; que siga siendo opcional es `PRD:NFR-01` —quien opera en una sola moneda no agrega
/// ni un paso— y es la compatibilidad hacia atrás del contrato: todo cliente que ya andaba sin
/// mandarlo sigue andando.
/// </summary>
/// <param name="Tipo">"gasto" o "ingreso".</param>
/// <param name="Monto">Número con hasta dos decimales.</param>
/// <param name="CategoriaId">Categoría elegida, del mismo tipo que el movimiento (FR-011).</param>
/// <param name="MonedaId">
/// Moneda del catálogo. Ausente o null = la predeterminada.
///
/// Es <c>int?</c> y no <c>short?</c> aunque <see cref="Dominio.Moneda.Id"/> sea <c>short</c>: con
/// <c>short?</c>, un número fuera de rango falla al deserializar y responde un 400 genérico del
/// framework, sin decir qué campo está mal. Con <c>int?</c> llega hasta la validación y se rechaza
/// con la clave <c>monedaId</c>, que es lo que le permite al frontend poner el mensaje al lado del
/// selector. Es el mismo motivo por el que todos los campos de este DTO son anulables.
/// </param>
/// <param name="Fecha">Día del movimiento. Ausente o null = hoy.</param>
public record NuevoMovimientoDto(
    string? Tipo,
    decimal? Monto,
    int? CategoriaId,
    int? MonedaId,
    DateOnly? Fecha);

/// <summary>
/// Un movimiento como lo ve el cliente. Es la misma forma en el alta y en el listado: devolver el
/// movimiento entero al crearlo es lo que permite a la pantalla insertarlo sin volver a pedirlo
/// (FR-014).
///
/// `categoriaNombre` viaja junto al id para que el listado no cruce contra el catálogo. Es además
/// lo que hará funcionar RF-09 en el ticket 3: el nombre que se conserva en los movimientos ya
/// registrados es el que devuelve esta lectura.
/// </summary>
public record MovimientoDto(
    long Id,
    string Tipo,
    decimal Monto,
    int CategoriaId,
    string CategoriaNombre,
    string MonedaCodigo,
    DateOnly Fecha);

/// <summary>
/// Lo que llega al modificar un movimiento (RF-14).
///
/// **`Fecha` es obligatoria acá y opcional en el alta, y la diferencia es a propósito.** Ausente
/// significa "hoy" al registrar, que es lo correcto; en una edición sería una trampa — quien mande
/// una modificación sin fecha vería su movimiento saltar a hoy en silencio. Un movimiento editado
/// conserva su fecha salvo que se pida cambiarla, y exigirla es la forma más simple de garantizarlo.
///
/// **`MonedaId` es opcional, y acá ausente significa "la que ya tenía"** — no "la predeterminada",
/// que es lo que significa en el alta. Parecen dos reglas y son una sola: **ausente nunca produce
/// un cambio que nadie pidió**. Es la misma regla que hace obligatoria a `Fecha`, porque ahí
/// ausente sí significaría un valor nuevo.
///
/// Con esto RF-14 queda entero: hasta la feature 009 se podía corregir todo de un movimiento menos
/// su moneda, y esa mitad faltante estaba anotada como deuda esperando el catálogo del ticket 4a.
///
/// No lleva propietario, y si llegara igual en el JSON se descarta al deserializar: el dueño lo
/// decide la sesión (INV-01).
/// </summary>
/// <param name="Tipo">"gasto" o "ingreso". Se valida contra el tipo de la categoría elegida.</param>
/// <param name="Monto">Número con hasta dos decimales.</param>
/// <param name="CategoriaId">Categoría elegida, del mismo tipo que el movimiento.</param>
/// <param name="MonedaId">Moneda del catálogo. Ausente o null = la que el movimiento ya tenía.</param>
/// <param name="Fecha">Día del movimiento. Obligatoria.</param>
public record MovimientoEditadoDto(
    string? Tipo,
    decimal? Monto,
    int? CategoriaId,
    int? MonedaId,
    DateOnly? Fecha);

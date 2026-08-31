namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Lo que llega en el alta. `fecha` es opcional: ausente o null significa "hoy", y ese valor lo
/// pone el servidor y no el cliente (AC-17) — es la única forma de que el test sea determinista.
///
/// No hay campo de moneda: se registra en la predeterminada del catálogo (FR-009). El selector
/// llega en el ticket 4b.
/// </summary>
/// <param name="Tipo">"gasto" o "ingreso".</param>
/// <param name="Monto">Número con hasta dos decimales.</param>
/// <param name="CategoriaId">Categoría elegida, del mismo tipo que el movimiento (FR-011).</param>
/// <param name="Fecha">Día del movimiento. Ausente o null = hoy.</param>
public record NuevoMovimientoDto(string? Tipo, decimal? Monto, int? CategoriaId, DateOnly? Fecha);

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
/// Tampoco lleva moneda: no se elige al registrar y tampoco al editar. La mitad faltante de RF-14
/// está en la Deuda registrada de la spec, esperando el catálogo de monedas del ticket 4a.
///
/// No lleva propietario, y si llegara igual en el JSON se descarta al deserializar: el dueño lo
/// decide la sesión (INV-01).
/// </summary>
/// <param name="Tipo">"gasto" o "ingreso". Se valida contra el tipo de la categoría elegida.</param>
/// <param name="Monto">Número con hasta dos decimales.</param>
/// <param name="CategoriaId">Categoría elegida, del mismo tipo que el movimiento.</param>
/// <param name="Fecha">Día del movimiento. Obligatoria.</param>
public record MovimientoEditadoDto(string? Tipo, decimal? Monto, int? CategoriaId, DateOnly? Fecha);

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

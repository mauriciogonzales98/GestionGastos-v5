namespace GestionGastos.Api.Resumenes;

/// <summary>
/// El resumen de un período, tal como lo ve el cliente (RF-19, RF-20, RF-22).
///
/// **No se persiste nada de esto.** Se deriva de los movimientos cada vez que se pide, y por eso
/// editar o borrar un movimiento se refleja sin que haya que invalidar nada: no hay nada que
/// invalidar (AC-19, AC-20, AC-21).
/// </summary>
/// <param name="Desde">
/// Primer día del período, incluido. **Viaja siempre**, también cuando el cliente no lo mandó.
///
/// Sin esto, quien quiera titular "Agosto 2026" tendría que calcular el mes por su cuenta, en la
/// zona horaria del navegador — o sea, un segundo criterio de "hoy" conviviendo con el del
/// servidor, que es justo lo que FR-002 evita (D-06).
/// </param>
/// <param name="Hasta">Último día del período, incluido.</param>
/// <param name="Monedas">
/// Una entrada por **cada moneda del catálogo**, tenga o no movimientos. Nunca viene vacío.
/// </param>
public record Resumen(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<ResumenPorMoneda> Monedas);

/// <summary>
/// Lo que pasó en una moneda durante el período.
///
/// Es la unidad que RF-29 vuelve indivisible: dos de éstos son dos universos separados y **nada se
/// suma nunca a través de ellos**. No hay conversión en ningún lado, ni la va a haber: el producto
/// decidió totales separados, no una moneda de referencia.
/// </summary>
/// <param name="MonedaId">Identificador de la moneda en el catálogo.</param>
/// <param name="MonedaCodigo">Su código ISO 4217, junto al id para no cruzar contra el catálogo.</param>
/// <param name="TotalIngresado">Suma de los ingresos de esta moneda. Cero si no hubo.</param>
/// <param name="TotalGastado">Suma de los gastos de esta moneda. Cero si no hubo.</param>
/// <param name="Balance">
/// Lo ingresado menos lo gastado. **Puede ser negativo**, y eso es un resultado, no un error: un
/// mes en rojo es exactamente la información que alguien necesita ver.
/// </param>
/// <param name="GastosPorCategoria">
/// El desglose, con las categorías que tuvieron al menos un movimiento. Vacío es normal.
/// </param>
public record ResumenPorMoneda(
    short MonedaId,
    string MonedaCodigo,
    decimal TotalIngresado,
    decimal TotalGastado,
    decimal Balance,
    IReadOnlyList<TotalPorCategoria> GastosPorCategoria);

/// <summary>
/// Cuánto suma una categoría dentro de una moneda y un período.
///
/// El nombre viaja junto al id, igual que en el listado: es lo que permite mostrar el desglose sin
/// cruzarlo contra el catálogo, y es el nombre **vigente** — por eso renombrar una categoría se ve
/// reflejado acá sin hacer nada (AC-13).
/// </summary>
public record TotalPorCategoria(
    int CategoriaId,
    string CategoriaNombre,
    decimal Total);

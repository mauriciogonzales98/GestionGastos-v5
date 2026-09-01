using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Una fila del agregado: cuánto suma un grupo de movimientos.
///
/// **No es lo que se devuelve por HTTP.** Es la materia intermedia de la que se componen el total
/// ingresado, el total gastado, el balance y el desglose por categoría — los cuatro, de las mismas
/// filas. Ésa es toda la gracia: FR-005 y FR-009 son igualdades entre esos números, y sacarlos de
/// una sola consulta las vuelve estructurales en vez de coincidencias que hay que verificar (D-04).
/// </summary>
/// <param name="MonedaId">La moneda del grupo. Nada se suma nunca a través de dos monedas.</param>
/// <param name="MonedaCodigo">Su código ISO, para no volver a cruzar contra el catálogo.</param>
/// <param name="Tipo">Gasto o ingreso.</param>
/// <param name="CategoriaId">La categoría del grupo.</param>
/// <param name="CategoriaNombre">Su nombre **vigente**, no una copia del momento del alta (AC-13).</param>
/// <param name="Total">
/// La suma de los montos del grupo.
///
/// **No es un `decimal(11,2)`.** El techo de un movimiento —999.999.999,99, D-01 de la feature
/// 001— no es el techo de una suma de movimientos: mil movimientos en el máximo lo pasan. `SUM`
/// amplía la precisión del lado del motor y `decimal` tiene rango de sobra de este lado, así que no
/// hay nada que hacer. Está escrito porque el reflejo es tipar el total igual que el monto, y ahí
/// sí habría un desborde silencioso (D-11).
/// </param>
public record MontoAgrupado(
    short MonedaId,
    string MonedaCodigo,
    TipoMovimiento Tipo,
    int CategoriaId,
    string CategoriaNombre,
    decimal Total);

using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// **El canal único de lectura de movimientos.** Ninguna otra parte del código de producción puede
/// leer `contexto.Movimientos`, y `BarreraDeAislamientoTests` se pone en rojo si aparece una que lo
/// haga.
///
/// Hasta el ticket 01c eso se cumplía sin que nadie lo hubiera dicho: era una coincidencia, no una
/// regla. El motivo de convertirla en regla es que el acotado por cuenta se escribe a mano, así que
/// una consulta nueva nace sin él salvo que alguien se acuerde — y nadie va a estar mirando esa
/// consulta el día que se escriba. Los tests cruzados tampoco: no saben que existe.
///
/// Desde FEAT-001c el acotado ya no se escribe en cada consulta: sale de `DeLaCuenta`, que es
/// privado y por donde pasan las tres. La regla del canal no se ablanda por eso —la barrera sigue
/// exigiendo el `usuario_id` en el SQL de cada una—, pero el olvido que había que temer ahora
/// requiere saltearse el único método que lo escribe.
///
/// Si hace falta una lectura nueva, va acá adentro y acotada. Agregar una excepción a la barrera es
/// desarmar la barrera.
///
/// La lectura vive en un método propio también para que un test pueda inspeccionar el SQL que
/// genera.
///
/// No es una separación decorativa: el índice (usuario_id, fecha DESC, id DESC) hace que MySQL
/// devuelva las filas ya ordenadas aunque la consulta no lo pida, así que un test que sólo mire el
/// resultado pasa en verde con el OrderBy borrado. D-04 exige verificarlo en doble capa, y la
/// segunda capa necesita alcanzar la consulta antes de que se ejecute.
/// </summary>
public static class MovimientosConsulta
{
    /// <summary>
    /// El listado de una cuenta, acotado por período y —si se pide— por categoría.
    ///
    /// Se llamaba `DelMes` hasta FEAT-001b, cuando sólo sabía de meses calendario. Ahora recibe un
    /// rango cualquiera y el nombre habría quedado mintiendo.
    ///
    /// La categoría es opcional y se combina con **y**: un movimiento sale si cumple todo lo que se
    /// pidió. Una categoría que no existe no es un error — simplemente no deja pasar nada, y eso es
    /// deliberado: rechazarla confirmaría cuáles existen.
    ///
    /// El orden se pide explícitamente aunque el índice `(usuario_id, fecha DESC, id DESC)` ya lo
    /// devuelva así. Es la D-04 de la feature 001 y sigue vigente: heredarlo del índice deja el
    /// listado a merced de un cambio de plan del motor.
    /// </summary>
    public static IQueryable<Movimiento> Filtrado(
        GestionGastosDbContext contexto,
        long usuarioId,
        RangoDeFechas rango,
        int? categoriaId = null) =>
        DeLaCuenta(contexto, usuarioId, rango, categoriaId)
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id);

    /// <summary>
    /// Lo que suma cada grupo de movimientos de una cuenta, dentro de un período: la consulta que
    /// alimenta el resumen (FEAT-001c).
    ///
    /// **Es una sola consulta y agrupa por los tres ejes a la vez** —moneda, tipo y categoría— y no
    /// una por cada número que hay que informar. El total gastado, el balance y el desglose por
    /// categoría se componen después de estas mismas filas, así que la suma del desglose no
    /// **puede** diferir del total: no son dos cuentas que casualmente dan igual, son la misma
    /// cuenta mirada de dos maneras (D-04). Dos consultas separadas darían lo mismo hoy y dejarían
    /// de darlo el día que alguien toque el WHERE de una sola.
    ///
    /// No lleva `OrderByDescending`: el orden es un requisito del listado (D-04 de la feature 001),
    /// no del acotado, y un `ORDER BY` que el `GROUP BY` descarta es trabajo que se le pide al motor
    /// para nada.
    ///
    /// El acotado por cuenta no se escribe acá: sale de <see cref="DeLaCuenta"/>, igual que el del
    /// listado. Y aun así `BarreraDeAislamientoTests` inspecciona su SQL y le exige el `usuario_id`
    /// — construcción y vigilancia, que son las dos mitades del Principio V y no una redundancia.
    ///
    /// **Y NO filtra por `categoria.activa`. Nunca puede empezar a hacerlo** (FR-011, AC-06, D-05).
    /// Es el reflejo natural de quien acaba de sumar la baja lógica al modelo y sería un daño
    /// silencioso: los movimientos de una categoría dada de baja dejarían de sumar, y el resumen de
    /// un mes cerrado hace dos años pasaría a dar otro número sin que nadie tocara un movimiento.
    /// La baja lógica apaga una categoría en el SELECTOR, no en la historia. `BarreraDelDesgloseTests`
    /// inspecciona este SQL y se pone en rojo si aparece `activa`; el filtro va en
    /// `CategoriasConsulta`, que es donde se decide qué se OFRECE.
    /// </summary>
    public static IQueryable<MontoAgrupado> Agrupado(
        GestionGastosDbContext contexto,
        long usuarioId,
        RangoDeFechas rango) =>
        DeLaCuenta(contexto, usuarioId, rango, categoriaId: null)
            .GroupBy(m => new
            {
                m.MonedaId,
                MonedaCodigo = m.Moneda!.Codigo,
                m.Tipo,
                m.CategoriaId,
                CategoriaNombre = m.Categoria!.Nombre,
            })
            .Select(g => new MontoAgrupado(
                g.Key.MonedaId,
                g.Key.MonedaCodigo,
                g.Key.Tipo,
                g.Key.CategoriaId,
                g.Key.CategoriaNombre,
                g.Sum(m => m.Monto)));

    /// <summary>
    /// **El acotado por cuenta, escrito una sola vez.**
    ///
    /// Es privado a propósito: no es una consulta que alguien pida, es la condición que toda
    /// consulta de movimientos tiene que llevar. Que el listado y el resumen salgan de acá hace que
    /// el aislamiento se herede por construcción en vez de depender de que cada consulta nueva se
    /// acuerde de escribirlo — que es exactamente el olvido que la barrera existe para atrapar.
    /// </summary>
    private static IQueryable<Movimiento> DeLaCuenta(
        GestionGastosDbContext contexto,
        long usuarioId,
        RangoDeFechas rango,
        int? categoriaId) =>
        contexto.Movimientos
            .Where(m => m.UsuarioId == usuarioId && m.Fecha >= rango.Desde && m.Fecha <= rango.Hasta)
            .Where(m => categoriaId == null || m.CategoriaId == categoriaId);

    /// <summary>
    /// Un movimiento propio, por identificador. Lo usan la consulta individual, la edición y la
    /// eliminación: las tres necesitan encontrar antes de responder o de tocar.
    ///
    /// **El acotado por cuenta va en la consulta, no después.** Traer la fila por `Id` y comprobar
    /// el dueño en memoria daría el mismo `404` visible y dejaría el `WHERE` sin `usuario_id` — o
    /// sea, `BarreraDeAislamientoTests` en rojo. Es a propósito: se quiere que el aislamiento esté
    /// en la consulta, donde no depende de que alguien se acuerde del `if`.
    ///
    /// Devuelve `IQueryable` y no el movimiento para que la barrera pueda inspeccionar su SQL, igual
    /// que con <see cref="DelMes"/>.
    /// </summary>
    public static IQueryable<Movimiento> PropioPorId(
        GestionGastosDbContext contexto,
        long usuarioId,
        long id) =>
        contexto.Movimientos.Where(m => m.UsuarioId == usuarioId && m.Id == id);
}

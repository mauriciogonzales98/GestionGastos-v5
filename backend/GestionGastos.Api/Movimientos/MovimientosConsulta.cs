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
        contexto.Movimientos
            .Where(m => m.UsuarioId == usuarioId && m.Fecha >= rango.Desde && m.Fecha <= rango.Hasta)
            .Where(m => categoriaId == null || m.CategoriaId == categoriaId)
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id);

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

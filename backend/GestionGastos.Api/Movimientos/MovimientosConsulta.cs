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
    public static IQueryable<Movimiento> DelMes(
        GestionGastosDbContext contexto,
        long usuarioId,
        RangoDelMes rango) =>
        contexto.Movimientos
            .Where(m => m.UsuarioId == usuarioId && m.Fecha >= rango.Desde && m.Fecha <= rango.Hasta)
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

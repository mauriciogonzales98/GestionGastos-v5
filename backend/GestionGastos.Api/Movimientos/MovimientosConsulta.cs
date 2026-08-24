using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// La lectura del listado, en un método propio para que un test pueda inspeccionar el SQL que
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
}

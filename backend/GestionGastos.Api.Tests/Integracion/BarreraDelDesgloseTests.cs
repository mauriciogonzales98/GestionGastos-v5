using GestionGastos.Api.Dominio;
using GestionGastos.Api.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **El desglose del resumen no filtra por `categoria.activa`, y nunca puede empezar a hacerlo**
/// (FR-011, AC-06, D-05). Salda la deuda D6-04 que dejó anotada la feature 006.
///
/// El daño que evita no es un error visible: es que los totales históricos cambien solos. El
/// desglose sale de un `JOIN` contra `categorias`, y agregarle `WHERE activa` —el reflejo natural
/// de quien acaba de sumar la baja lógica al modelo— hace que los movimientos de una categoría dada
/// de baja desaparezcan de la suma. El resumen de un mes cerrado hace dos años pasa a dar otro
/// número, sin que nadie haya tocado un movimiento. La baja lógica existe justamente para que eso
/// no ocurra: se apaga la categoría en el selector, no en la historia.
///
/// **Por qué hace falta una barrera y no alcanza con los tests del resumen.** Hasta esta feature
/// todas las categorías tenían `activa = true`, así que el filtro no cambiaba ningún número y la
/// suite entera quedaba en verde con él puesto — 195 de 195, comprobado antes de escribir esto
/// (T014). Ese verde es la deuda D6-04: no había forma de que un test de resultado viera la
/// diferencia, porque no había ninguna categoría inactiva con la que producirla.
///
/// `AC-06` sí produce la diferencia y la mide, y es la primera capa. Ésta es la segunda, y mira el
/// SQL en vez del resultado: un test de resultado sólo protege el escenario que arma, y el filtro
/// puede volver a aparecer por un camino que ese escenario no toca.
///
/// `backend/verificar-desglose.sh` es lo que le prueba a esta barrera que sabe ponerse en rojo.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class BarreraDelDesgloseTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public void El_Desglose_No_Filtra_Por_Categoria_Activa()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var sql = MovimientosConsulta
            // Los valores no importan: se mira el SQL que se genera, no filas.
            .Agrupado(contexto, usuarioId: 1, RangoDelMes.De(new DateOnly(2026, 8, 15)))
            .ToQueryString();

        // Que el JOIN esté es lo que vuelve significativa la comprobación de abajo. Sin él, el SQL
        // no nombraría `activa` por el motivo equivocado —no hay categoría a la vista— y la barrera
        // pasaría en verde sin vigilar nada.
        Assert.True(
            sql.Contains("categoria", StringComparison.OrdinalIgnoreCase),
            "El SQL del desglose ya ni siquiera nombra `categoria`: la consulta cambió de forma y " +
            "esta barrera quedó mirando algo que no existe.\n\nSQL:\n" + sql);

        Assert.False(
            sql.Contains("activa", StringComparison.OrdinalIgnoreCase),
            "El desglose del resumen empezó a filtrar por `categoria.activa`.\n\n" +
            "Parece inofensivo y no lo es: los movimientos de una categoría dada de baja dejan de " +
            "sumar, así que el resumen de un mes ya cerrado pasa a dar otro número sin que nadie " +
            "haya tocado un movimiento. La historia se reescribe sola. La baja lógica existe para " +
            "que una categoría desaparezca del SELECTOR (FR-010) y siga nombrando lo que ya nombró " +
            "(FR-011, AC-06).\n\n" +
            "Es la deuda D6-04 de la feature 006, y esta barrera es lo que la salda: hasta la " +
            "feature 007 todas las categorías estaban activas, así que el filtro no cambiaba ningún " +
            "número y la suite entera quedaba en verde con él puesto.\n\n" +
            "El filtro por `activa` va donde se OFRECE una categoría —`CategoriasConsulta`—, nunca " +
            "donde se suma lo que ya se registró.\n\nSQL:\n" + sql);
    }
}

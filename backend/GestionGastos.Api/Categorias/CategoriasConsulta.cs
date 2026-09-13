using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;

namespace GestionGastos.Api.Categorias;

/// <summary>
/// **El canal único de lectura de categorías**, espejo de <c>MovimientosConsulta</c> y por el mismo
/// motivo (D-03).
///
/// Hasta esta feature las diez categorías eran de todo el mundo: no había nada que aislar, así que
/// ninguna consulta podía nacer mal acotada. Desde acá cada cuenta tiene las suyas, y el acotado
/// por ámbito pasa a ser una condición que hay que acordarse de escribir — que es exactamente el
/// olvido que un canal con barrera existe para atrapar.
///
/// **Lo que se comparte con movimientos es la vigilancia, no el acotado.** Una categoría puede ser
/// de nadie —`usuario_id IS NULL` son las predefinidas del sistema—, así que su predicado no es
/// `usuario_id = @yo` a secas y no se puede reusar el de movimientos. Lo que sí se reusa es la
/// regla: toda lectura vive acá adentro, devuelve `IQueryable` para que la barrera pueda
/// inspeccionar su SQL antes de que se ejecute, y `BarreraDeAislamientoTests` se pone en rojo si
/// alguna deja de nombrar `usuario_id`.
///
/// Si hace falta una lectura nueva, va acá adentro y acotada. Agregar una excepción a la barrera es
/// desarmar la barrera.
/// </summary>
public static class CategoriasConsulta
{
    /// <summary>
    /// El catálogo que una cuenta puede usar (FR-002): las predefinidas del sistema más las propias
    /// de esa cuenta, todas activas, ordenadas por tipo y después por identificador.
    ///
    /// El orden se pide explícitamente aunque hoy el motor lo devuelva parecido: es parte del
    /// contrato, y heredarlo del plan de ejecución lo deja a merced de que el plan cambie.
    /// </summary>
    public static IQueryable<Categoria> Ofrecibles(GestionGastosDbContext contexto, long usuarioId) =>
        DelAmbito(contexto, usuarioId)
            .Where(c => c.Activa)
            .OrderBy(c => c.Tipo)
            .ThenBy(c => c.Id);

    /// <summary>
    /// Las categorías **activas** del ámbito que ya usan ese nombre y ese tipo. Es la consulta con
    /// la que se comprueba FR-005, en el alta y en el renombre.
    ///
    /// **Existe para que el rechazo tenga forma de error de validación, no de choque de índice.**
    /// Su razón de ser cambió con la feature 013 y conviene decirlo, porque el comentario anterior
    /// pasó a ser falso: decía que el índice no alcanzaba porque para MySQL `usuario_id NULL` y
    /// `usuario_id 7` son claves distintas, así que dejaba pasar una propia homónima de una
    /// predefinida (D-02 de la 007). Sin `NULL`, el índice cubre el caso entero.
    ///
    /// La comprobación **se conserva igual**: el índice devolvería un `1062` que termina en un `500`
    /// sin decir qué campo está mal, y lo que la persona necesita es un `400` con la clave `nombre`
    /// al lado de su control. La red de abajo sigue estando; ésta es la que se ve.
    ///
    /// La comparación de nombre no normaliza mayúsculas ni acentos: la collation
    /// `utf8mb4_0900_ai_ci` de la columna ya los ignora, y hacerlo a mano acá además apagaría el
    /// índice. Los espacios al borde sí se recortan, pero antes de llegar: quien llama manda el
    /// nombre ya recortado.
    ///
    /// Sólo mira las activas: una dada de baja no le ocupa el nombre a nadie (FR-009).
    /// </summary>
    public static IQueryable<Categoria> Homonimas(
        GestionGastosDbContext contexto,
        long usuarioId,
        string nombre,
        TipoMovimiento tipo) =>
        DelAmbito(contexto, usuarioId)
            .Where(c => c.Activa && c.Nombre == nombre && c.Tipo == tipo);

    /// <summary>
    /// Una categoría del ámbito por identificador, **activa o no**.
    ///
    /// **La usan cuatro llamadores, y conviene saberlo antes de tocarla**: el renombre y la baja de
    /// categorías, y —desde que se saldó D7-05— el alta y la edición de movimientos, que buscan por
    /// acá la categoría con la que clasificar. Los dos primeros deciden qué responder; los dos
    /// segundos le agregan su propia condición sobre `activa` en el sitio donde llaman.
    ///
    /// No filtra por `activa` ella misma, y por eso: la baja es idempotente (D-06), así que darle
    /// de baja a algo ya dado de baja tiene que encontrarlo para poder responder `204` en vez de
    /// `404`. Quien necesite sólo las activas lo pide donde llama.
    ///
    /// **Todo lo que devuelve se puede tocar.** Hasta la feature 013 devolvía también las diez
    /// predefinidas, que la cuenta veía y no poseía, y quien llamaba tenía que distinguir "no se
    /// puede tocar" —`403`— de "no existe" —`404`—. Esa distinción se quedó sin casos: lo que no es
    /// tuyo no lo ves, y lo que ves es tuyo (FR-016). Lo que el ámbito deja afuera —las de otras
    /// cuentas— cae en el mismo `404` que un id inexistente, y **eso no cambió**: confirmar su
    /// existencia sería la filtración que FR-013 de la 007 evita.
    /// </summary>
    public static IQueryable<Categoria> DelAmbitoPorId(
        GestionGastosDbContext contexto,
        long usuarioId,
        int id) =>
        DelAmbito(contexto, usuarioId).Where(c => c.Id == id);

    /// <summary>
    /// **El acotado por ámbito, escrito una sola vez.**
    ///
    /// Privado a propósito, igual que `DeLaCuenta` en `MovimientosConsulta`: no es una consulta que
    /// alguien pida, es la condición que toda lectura de categorías tiene que llevar. Que salga de
    /// acá hace que el aislamiento se herede por construcción en vez de depender de que cada
    /// consulta nueva se acuerde de escribirlo.
    ///
    /// **Desde la feature 013 es idéntico al predicado de movimientos.** Antes llevaba además
    /// `usuario_id IS NULL`, que eran las diez predefinidas: se veían desde todas las cuentas y no
    /// eran de ninguna. Esa mitad se fue con ellas — ahora cada cuenta tiene sus diez.
    /// </summary>
    private static IQueryable<Categoria> DelAmbito(GestionGastosDbContext contexto, long usuarioId) =>
        contexto.Categorias.Where(c => c.UsuarioId == usuarioId);
}

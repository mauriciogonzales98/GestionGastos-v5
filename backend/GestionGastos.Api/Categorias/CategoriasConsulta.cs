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
    /// **La unicidad la comprueba la aplicación y no puede quedarse en el índice** (D-02): para
    /// MySQL, `usuario_id NULL` y `usuario_id 7` son claves distintas, así que el índice deja pasar
    /// una propia homónima de una predefinida. El índice cubre el choque dentro del mismo ámbito;
    /// esto cubre el choque ENTRE los dos ámbitos que una cuenta ve como uno solo.
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
    /// Es la que usan el renombre y la baja para decidir qué responder, y por eso no filtra por
    /// `activa`: la baja es idempotente (D-06), así que darle de baja a algo ya dado de baja tiene
    /// que encontrarlo para poder responder `204` en vez de `404`.
    ///
    /// Devuelve también las predefinidas, que la cuenta VE. Distinguir "no se puede tocar" de "no
    /// existe" es justamente lo que separa el `403` del `404` (FR-008, FR-013, D-06), y esa
    /// distinción necesita encontrar la fila primero. Quien llama decide: sin dueño es predefinida
    /// y va `403`; con dueño es propia de esta cuenta y se puede tocar. Lo que el ámbito ya dejó
    /// afuera —las propias de otras cuentas— cae en el mismo `404` que un id inexistente.
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
    /// `usuario_id IS NULL` son las diez predefinidas: se ven desde todas las cuentas y no son de
    /// ninguna. Que se **vean** no significa que se puedan tocar — eso lo decide otra consulta, y
    /// esa diferencia es exactamente FR-008.
    /// </summary>
    private static IQueryable<Categoria> DelAmbito(GestionGastosDbContext contexto, long usuarioId) =>
        contexto.Categorias.Where(c => c.UsuarioId == null || c.UsuarioId == usuarioId);
}

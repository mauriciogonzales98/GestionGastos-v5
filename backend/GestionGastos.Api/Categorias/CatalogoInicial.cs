using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;

namespace GestionGastos.Api.Categorias;

/// <summary>
/// **El catálogo que cada cuenta recibe al registrarse** (FR-002), y el único lugar del código que
/// conoce esos diez nombres.
///
/// Hasta la feature 013 las diez vivían en <c>GestionGastosDbContext.Sembrar</c> como filas sin
/// dueño, compartidas por todas las cuentas. Dejaron de poder vivir ahí por una razón concreta y no
/// por gusto: <c>HasData</c> siembra hechos del esquema, y estas filas dependen de un
/// <c>usuario_id</c> que no existe hasta que alguien se registra. El catálogo dejó de ser una
/// propiedad de la base y pasó a ser una consecuencia del alta de una cuenta (D-05).
///
/// **Es un archivo aparte, y no un método dentro de <c>CuentasEndpoints</c>.** La barrera de
/// aislamiento nombra uno por uno los archivos que pueden escribir el conjunto de categorías, y
/// declarar a <c>CuentasEndpoints</c> le abriría ese conjunto a un archivo que hace muchas otras
/// cosas — una autorización que quedaría vigente para todo lo que ese archivo haga en el futuro.
/// Un archivo de una sola responsabilidad mantiene la excepción del tamaño de lo que hace falta
/// (D-06).
///
/// (La barrera escanea **texto**, así que este comentario evita a propósito escribir el nombre del
/// conjunto tal como aparece en el código: lo leería como un uso más y no puede distinguir una
/// mención en prosa de una consulta.)
///
/// **La migración no pasa por acá, y eso también es deliberado** (D-02). Las copias de las cuentas
/// que ya existían salen de las filas que había en la base, no de esta lista. Una migración es un
/// hecho histórico: si leyera de acá, el día que estos diez nombres cambien una migración vieja
/// cambiaría de significado y migraría a un catálogo que quizá nunca estuvo en esa base.
/// </summary>
public static class CatalogoInicial
{
    /// <summary>
    /// Los siete de gasto de FR-006 de la feature 001, en su orden.
    ///
    /// "Otros" está también en <see cref="Ingresos"/>: son dos categorías distintas que comparten
    /// nombre y difieren en tipo, y el índice único las admite justamente porque el tipo entra en la
    /// clave.
    /// </summary>
    private static readonly string[] Gastos =
        ["Comida", "Transporte", "Vivienda", "Servicios", "Salud", "Ocio", "Otros"];

    /// <summary>Los tres de ingreso, en su orden.</summary>
    private static readonly string[] Ingresos =
        ["Sueldo", "Ingreso extra", "Otros"];

    /// <summary>
    /// Las diez categorías de una cuenta, recién construidas y enlazadas a ella.
    ///
    /// El enlace va por la propiedad de navegación y no por <c>UsuarioId</c> porque
    /// <paramref name="dueno"/> llega **sin identificador**: lo genera su propio <c>INSERT</c>. Con
    /// la navegación puesta, EF ordena las once escrituras y completa el <c>usuario_id</c> de las
    /// diez filas dentro del mismo <c>SaveChanges</c> (D-07).
    ///
    /// Devuelve filas nuevas en cada llamada, nunca instancias compartidas: son las diez **de esa**
    /// cuenta.
    /// </summary>
    public static IReadOnlyList<Categoria> Nuevas(Usuario dueno) =>
    [
        .. Gastos.Select(nombre => De(nombre, TipoMovimiento.Gasto, dueno)),
        .. Ingresos.Select(nombre => De(nombre, TipoMovimiento.Ingreso, dueno)),
    ];

    /// <summary>
    /// Deja las diez listas para guardarse junto con la cuenta.
    ///
    /// No guarda: el <c>SaveChangesAsync</c> lo hace quien llama, y es **uno solo**. Ésa es toda la
    /// transacción que FR-003 pide — la cuenta y su catálogo quedan las dos o no queda ninguna — y
    /// por eso no hace falta abrir una explícita.
    /// </summary>
    public static void EntregarA(GestionGastosDbContext contexto, Usuario dueno) =>
        contexto.Categorias.AddRange(Nuevas(dueno));

    private static Categoria De(string nombre, TipoMovimiento tipo, Usuario dueno) =>
        new() { Nombre = nombre, Tipo = tipo, Usuario = dueno, Activa = true };
}

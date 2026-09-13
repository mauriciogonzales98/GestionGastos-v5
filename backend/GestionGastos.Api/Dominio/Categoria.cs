namespace GestionGastos.Api.Dominio;

/// <summary>
/// Una categoría del catálogo (FR-006). El formulario ofrece sólo las del tipo que se está
/// cargando, y por eso <see cref="Tipo"/> es parte de la identidad de la fila.
/// </summary>
public class Categoria
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public TipoMovimiento Tipo { get; set; }

    /// <summary>
    /// La cuenta dueña. **Obligatoria**: no existe la categoría sin dueño (FR-001).
    ///
    /// Nació anulable, y el <c>null</c> significaba "predefinida del sistema": diez filas que todas
    /// las cuentas veían y ninguna poseía. Esa clase de fila desapareció con la feature 013, que le
    /// entrega a cada cuenta su propia copia de las diez al registrarse.
    ///
    /// **No es un detalle de tipos.** Mientras el <c>null</c> existió, la regla de que un movimiento
    /// no puede apuntar a la categoría de otra cuenta no se podía escribir como restricción: la
    /// condición real era "el dueño coincide **o la categoría no es de nadie**", y una clave foránea
    /// no sabe decir "o nula". Sacarlo es lo que dejó poner la foránea compuesta que hoy sostiene
    /// esa invariante — la deuda D7-07.
    /// </summary>
    public long UsuarioId { get; set; }

    /// <summary>
    /// La cuenta dueña, como objeto. **Existe para el alta**: cuando una cuenta se registra, sus
    /// diez categorías iniciales se enlazan por acá y no por <see cref="UsuarioId"/>, porque en ese
    /// momento la cuenta todavía no tiene identificador — lo genera su propio <c>INSERT</c>. Con la
    /// navegación puesta, EF ordena las once escrituras y propaga el identificador dentro del mismo
    /// <c>SaveChanges</c>, que es lo que hace que FR-003 sea una sola transacción.
    ///
    /// **No se lee por acá.** Toda lectura de categorías pasa por `CategoriasConsulta`, y el
    /// aislamiento se acota con `UsuarioId`, no navegando.
    /// </summary>
    public Usuario? Usuario { get; set; }

    /// <summary>Baja lógica de RF-09. Igual que <see cref="UsuarioId"/>, anticipo del ticket 3.</summary>
    public bool Activa { get; set; } = true;

    /// <summary>
    /// <c>0</c> mientras la categoría está activa; su propio <see cref="Id"/> cuando se da de baja.
    ///
    /// **Existe sólo para el índice único** (D-01). Sin él, `UNIQUE (usuario_id, nombre, tipo)`
    /// haría que una categoría dada de baja siguiera ocupando su nombre para siempre, y FR-009
    /// —volver a crear una con el mismo nombre— sería imposible. Con él, la fila de baja se lleva
    /// una clave que nadie más puede repetir y deja el casillero `0` libre.
    ///
    /// El valor es el `Id` y no un `1`: dos bajas homónimas también tienen que poder convivir, y
    /// con un booleano la segunda chocaría contra la primera.
    ///
    /// No viaja en el contrato: es un detalle del esquema, no algo que el cliente deba conocer.
    /// </summary>
    public long Discriminador { get; set; }
}

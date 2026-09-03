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
    /// <c>null</c> = predefinida del sistema; con valor = propia de esa cuenta.
    ///
    /// Anticipo deliberado del ticket 3 (D-06): en esta feature todas las filas nacen en
    /// <c>null</c>, pero la columna está desde el principio para no migrar la tabla después ni
    /// reescribir las consultas que la tocan.
    /// </summary>
    public long? UsuarioId { get; set; }

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

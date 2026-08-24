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
}

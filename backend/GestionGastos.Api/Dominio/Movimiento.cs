namespace GestionGastos.Api.Dominio;

/// <summary>
/// El hecho registrado: dinero que salió (gasto) o que entró (ingreso) de la cuenta.
///
/// Se crea y no cambia. La edición y la baja llegan en FEAT-001b.
/// </summary>
public class Movimiento
{
    /// <summary>PK, y además el desempate del orden del listado cuando dos comparten fecha (D-04).</summary>
    public long Id { get; set; }

    /// <summary>
    /// Se asigna a mano desde <c>IUsuarioActual</c> en cada alta (FR-010). No sale de un default
    /// del esquema: el dueño de un movimiento es una decisión de la aplicación.
    /// </summary>
    public long UsuarioId { get; set; }

    public TipoMovimiento Tipo { get; set; }

    /// <summary>
    /// <c>decimal(11,2)</c> en el esquema, con <c>CHECK (monto &gt; 0)</c>. El techo de FR-004b
    /// —999.999.999,99— entra exactamente en esa precisión (D-01).
    /// </summary>
    public decimal Monto { get; set; }

    public short MonedaId { get; set; }

    /// <summary><c>NOT NULL</c>: es lo que hace imposible un movimiento sin categoría (FR-005).</summary>
    public int CategoriaId { get; set; }

    /// <summary>Sin hora ni zona horaria (FR-003, D-02).</summary>
    public DateOnly Fecha { get; set; }

    public Categoria? Categoria { get; set; }

    public Moneda? Moneda { get; set; }
}

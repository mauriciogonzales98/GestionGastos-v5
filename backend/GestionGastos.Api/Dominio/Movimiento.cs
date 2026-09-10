namespace GestionGastos.Api.Dominio;

/// <summary>
/// El hecho registrado: dinero que salió (gasto) o que entró (ingreso) de la cuenta.
///
/// Desde FEAT-001b tiene ciclo de vida: se crea, se modifica y se elimina. La eliminación es
/// definitiva —no hay baja lógica ni deshacer— y por eso es la única transición irreversible del
/// modelo. Lo que NO cambia nunca al editar es su propietario (INV-01).
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

    /// <summary>
    /// La nota descriptiva, de hasta 120 caracteres Unicode (RF-33, feature 012).
    ///
    /// **Es descriptiva, no clasificatoria**: no se busca, no se filtra, no se agrupa y no entra en
    /// ningún total ni en el desglose. Por eso su columna **no tiene índice** — un índice acá sólo
    /// serviría para buscar por ella, y dejarlo puesto "por si acaso" serviría el camino que
    /// `FR-007` evita. Lo que ese requisito protege es que la categoría siga siendo el único eje de
    /// análisis: una nota libre que se pudiera filtrar se vuelve una segunda taxonomía informal
    /// —"alquiler", "Alquiler", "alq"— que el sistema no entiende.
    ///
    /// Anulable, y **es la primera columna de texto anulable del modelo**. La ausencia de valor y la
    /// cadena vacía significan las dos "sin nota" y el esquema admite las dos: quien normaliza es la
    /// lectura, en `MovimientoDto`, que es el único punto por el que pasan todas (`FR-011`).
    /// </summary>
    public string? Nota { get; set; }

    public Categoria? Categoria { get; set; }

    public Moneda? Moneda { get; set; }
}

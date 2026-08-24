namespace GestionGastos.Api.Dominio;

/// <summary>
/// Las dos mitades del dominio (FR-001, FR-002). Los valores son explícitos porque se persisten
/// como <c>tinyint</c>: cambiarlos reinterpretaría las filas ya guardadas.
/// </summary>
public enum TipoMovimiento
{
    /// <summary>Dinero que salió de la cuenta.</summary>
    Gasto = 0,

    /// <summary>Dinero que entró a la cuenta.</summary>
    Ingreso = 1,
}

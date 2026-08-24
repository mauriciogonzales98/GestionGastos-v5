namespace GestionGastos.Api.Dominio;

/// <summary>
/// Una moneda del catálogo (RF-31). Es tabla y no enum a propósito: es lo que permite sumar una
/// moneda sin tocar el código (RF-32), que es lo que el ticket 4a va a explotar.
///
/// En esta feature el catálogo existe pero no se expone: todo movimiento se registra en la
/// predeterminada (FR-009).
/// </summary>
public class Moneda
{
    public short Id { get; set; }

    /// <summary>Código ISO 4217: <c>ARS</c>, <c>USD</c>.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Simbolo { get; set; } = string.Empty;

    /// <summary>
    /// Cuántos decimales admite. Es dato de la moneda y no una constante del código, porque no
    /// todas usan dos.
    /// </summary>
    public byte Decimales { get; set; } = 2;

    /// <summary>Exactamente una fila del catálogo la tiene en <c>true</c> (RF-25).</summary>
    public bool EsPredeterminada { get; set; }
}

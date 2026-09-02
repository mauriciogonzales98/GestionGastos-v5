namespace GestionGastos.Api.Dominio;

/// <summary>
/// Una moneda del catálogo (RF-31). Es tabla y no enum a propósito: es lo que permite sumar una
/// moneda sin tocar el código (RF-32), que es lo que el ticket 4a va a explotar.
///
/// El catálogo todavía no se **elige**: todo movimiento se registra en la predeterminada (FR-009) y
/// el selector llega con el ticket 4b. Pero desde FEAT-001c sí se expone: el resumen devuelve una
/// entrada por cada fila de esta tabla, tenga o no movimientos, y por eso hoy ya se ven dos —ARS
/// con datos y USD en cero—. Agregar una moneda acá agrega una entrada al resumen sin tocar código,
/// que es exactamente lo que RF-32 quería.
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

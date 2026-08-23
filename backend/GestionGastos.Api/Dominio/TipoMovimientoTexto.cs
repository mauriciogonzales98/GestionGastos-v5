namespace GestionGastos.Api.Dominio;

/// <summary>
/// La traducción entre el <c>tinyint</c> que guarda la base y la cadena que viaja por la red.
///
/// Vive en un solo lugar a propósito: el contrato dice que `tipo` es "gasto" o "ingreso", y si
/// cada endpoint escribiera su propio mapeo, uno solo que se desalinee rompe el frontend en
/// silencio.
/// </summary>
public static class TipoMovimientoTexto
{
    public const string Gasto = "gasto";

    public const string Ingreso = "ingreso";

    public static string ATexto(this TipoMovimiento tipo) => tipo switch
    {
        TipoMovimiento.Gasto => Gasto,
        TipoMovimiento.Ingreso => Ingreso,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de movimiento desconocido."),
    };

    /// <summary>
    /// Convierte el texto recibido en la petición. Devuelve <c>false</c> en vez de lanzar: un
    /// `tipo` inválido es un error de validación del cliente (400), no una falla del servidor.
    /// </summary>
    public static bool TryDesdeTexto(string? texto, out TipoMovimiento tipo)
    {
        switch (texto)
        {
            case Gasto:
                tipo = TipoMovimiento.Gasto;
                return true;
            case Ingreso:
                tipo = TipoMovimiento.Ingreso;
                return true;
            default:
                tipo = default;
                return false;
        }
    }
}

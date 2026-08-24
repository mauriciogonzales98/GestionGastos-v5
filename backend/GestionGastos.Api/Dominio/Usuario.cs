namespace GestionGastos.Api.Dominio;

/// <summary>
/// La cuenta dueña de los movimientos. Existe desde el primer día para que
/// <see cref="Movimiento.UsuarioId"/> sea una clave foránea real y no un campo suelto.
///
/// El hash de contraseña llegó con el ticket 1a. El contador de intentos fallidos y su ventana de
/// bloqueo NO van acá: son del ticket 1b (RNF-05).
/// </summary>
public class Usuario
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// El verificador de la contraseña: hash bcrypt completo, con su algoritmo, su factor de
    /// trabajo y su sal adentro. Nunca la contraseña (NFR-01).
    /// </summary>
    public string ContrasenaHash { get; set; } = string.Empty;
}

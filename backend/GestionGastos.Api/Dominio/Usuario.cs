namespace GestionGastos.Api.Dominio;

/// <summary>
/// La cuenta dueña de los movimientos. Existe desde el primer día para que
/// <see cref="Movimiento.UsuarioId"/> sea una clave foránea real y no un campo suelto.
///
/// Las columnas de autenticación —hash de contraseña, intentos fallidos— NO van acá: son del
/// ticket 1a. Ver data-model.md.
/// </summary>
public class Usuario
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;
}

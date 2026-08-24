namespace GestionGastos.Api.Cuentas;

/// <param name="Email">Email de la cuenta. Se compara sin distinguir mayúsculas.</param>
/// <param name="Contrasena">Contraseña en claro. Sólo viaja en esta petición y nunca se guarda.</param>
public record NuevaCuentaDto(string? Email, string? Contrasena);

/// <param name="Credenciales">Email y contraseña de una cuenta existente.</param>
public record CredencialesDto(string? Email, string? Contrasena);

/// <summary>
/// La respuesta del alta. Es **la misma** exista o no la cuenta: decir "ese email ya está
/// registrado" sería mucho más amable y publicaría la lista de emails registrados (NFR-03).
/// </summary>
public record AltaDeCuentaDto(string Mensaje);

/// <summary>La cuenta en sesión.</summary>
public record SesionActualDto(string Email);

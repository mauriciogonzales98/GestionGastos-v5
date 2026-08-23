namespace GestionGastos.Api.Persistencia;

/// <summary>
/// De dónde sale la cuenta propietaria de un movimiento (FR-010).
///
/// Es una abstracción con una sola implementación a propósito. El ticket 1a la va a *reemplazar*
/// por una que lea la sesión, en vez de migrar datos: por eso el propietario se asigna desde acá
/// desde el primer día, aunque hoy sea siempre el mismo (D-05).
/// </summary>
public interface IUsuarioActual
{
    /// <summary>El id de la cuenta en cuyo nombre se está operando.</summary>
    long Id { get; }
}

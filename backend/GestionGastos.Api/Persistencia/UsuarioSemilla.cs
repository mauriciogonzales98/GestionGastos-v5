namespace GestionGastos.Api.Persistencia;

/// <summary>
/// La única implementación de <see cref="IUsuarioActual"/> mientras no haya autenticación: siempre
/// la fila semilla que crea la migración inicial.
///
/// El plan DISC-001 deja escrita la trampa que esto evita: el filtro global de lectura del ticket
/// 1c NO aplica al INSERT. Si la escritura no asigna el propietario explícitamente, el aislamiento
/// entre cuentas nace roto y nadie se entera hasta que hay dos cuentas.
/// </summary>
public class UsuarioSemilla : IUsuarioActual
{
    /// <summary>
    /// El id de la fila semilla. Es fijo porque la migración inicial lo siembra con ese valor
    /// explícito, no con el autoincremental.
    /// </summary>
    public const long IdSemilla = 1;

    public long Id => IdSemilla;
}

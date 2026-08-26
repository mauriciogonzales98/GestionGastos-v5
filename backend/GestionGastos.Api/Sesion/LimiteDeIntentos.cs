using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Sesion;

/// <summary>
/// El límite de intentos fallidos de inicio de sesión (RNF-05, FR-01..FR-06 del ticket 01b).
///
/// Tras <see cref="MaximoDeFallos"/> fallos consecutivos sobre un email, todo intento nuevo sobre
/// ese email se rechaza durante <see cref="Ventana"/>, **incluido el que traiga la contraseña
/// correcta**. La ventana se cuenta desde el último fallo contado y se levanta sola.
///
/// La decisión es una función pura a propósito: así los bordes de la ventana se verifican sin base
/// de datos ni servidor.
/// </summary>
public sealed class LimiteDeIntentos(GestionGastosDbContext contexto, TimeProvider reloj)
{
    /// <summary>Los cinco fallos consecutivos que bloquean (RNF-05).</summary>
    public const int MaximoDeFallos = 5;

    /// <summary>Cuánto dura el bloqueo, contado desde el quinto fallo (RNF-05).</summary>
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Cuánta inactividad devuelve a un email a foja cero.
    ///
    /// Es también el criterio de purga: la fila deja de existir porque el email ya no cuenta, no
    /// porque una tarea de limpieza pasó por ahí. El precio está aceptado en research.md D-03:
    /// permite probar 4 contraseñas por día indefinidamente, que frente a bcrypt es ruido.
    /// </summary>
    public static readonly TimeSpan InactividadQueReinicia = TimeSpan.FromHours(24);

    /// <summary>
    /// Si un email con <paramref name="fallos"/> fallos y último fallo en
    /// <paramref name="ultimoFallo"/> está bloqueado en <paramref name="ahora"/>.
    ///
    /// El borde de los 15 minutos clavados cae del lado de "ya no bloquea": el PRD pide **al menos**
    /// 15 minutos, y a los 15 cumplidos ya se cumplieron.
    /// </summary>
    public static bool EstaBloqueado(byte fallos, DateTime ultimoFallo, DateTime ahora) =>
        fallos >= MaximoDeFallos && ahora - ultimoFallo < Ventana;

    /// <summary>Si el email está dentro de su ventana de bloqueo ahora mismo.</summary>
    public async Task<bool> EstaBloqueadoAsync(string email)
    {
        var fila = await contexto.IntentosDeAcceso.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Email == email);

        return fila is not null
            && EstaBloqueado(fila.FallosConsecutivos, fila.UltimoFallo, reloj.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Suma un fallo al email, creando su fila si no la tenía.
    ///
    /// Va como UPSERT atómico y no como leer-modificar-guardar: cinco peticiones en paralelo que
    /// leyeran 0 y guardaran 1 dejarían el email a un fallo del límite después de cinco fallos.
    /// Acá el que cuenta es MySQL, sobre la fila bloqueada, y el resultado no depende del
    /// intercalado. Este repositorio ya se comió esa clase de bug una vez, con dos altas
    /// simultáneas del mismo email.
    ///
    /// El mismo UPDATE resuelve el reinicio del contador sin leer antes: si la ventana de bloqueo
    /// venció, o si el email lleva más de <see cref="InactividadQueReinicia"/> sin intentos, el
    /// contador arranca de nuevo en 1 en vez de seguir sumando.
    /// </summary>
    public async Task RegistrarFalloAsync(string email)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var finDeVentana = ahora - Ventana;
        var finDeInactividad = ahora - InactividadQueReinicia;

        await contexto.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO intento_de_acceso (email, fallos_consecutivos, ultimo_fallo)
            VALUES ({email}, 1, {ahora})
            ON DUPLICATE KEY UPDATE
                fallos_consecutivos = IF(
                    (fallos_consecutivos >= {MaximoDeFallos} AND ultimo_fallo <= {finDeVentana})
                        OR ultimo_fallo <= {finDeInactividad},
                    1,
                    fallos_consecutivos + 1),
                ultimo_fallo = {ahora}");

        // La purga viaja pegada al fallo, que es el camino que YA escribe. En el de lectura correría
        // en todos los inicios de sesión, incluidos los exitosos, y ahí se gasta el presupuesto de
        // NFR-02 en el camino más frecuente. Borra por el índice de `ultimo_fallo`.
        await contexto.IntentosDeAcceso
            .Where(i => i.UltimoFallo <= finDeInactividad)
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// Devuelve el email a foja cero borrando su fila (FR-03).
    ///
    /// Borrar y no poner el contador en 0: una fila en cero no significa nada distinto de no tener
    /// fila, y dejarla haría crecer la tabla con el estado más común de todos.
    /// </summary>
    public async Task ReiniciarAsync(string email) =>
        await contexto.IntentosDeAcceso.Where(i => i.Email == email).ExecuteDeleteAsync();
}

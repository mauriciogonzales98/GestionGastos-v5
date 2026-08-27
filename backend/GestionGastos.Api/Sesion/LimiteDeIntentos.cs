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
    /// Cuántas filas vencidas se lleva como máximo una sola purga.
    ///
    /// La purga viaja dentro de una petición, así que tiene que costar lo mismo siempre. Sin cota,
    /// el primer inicio de sesión fallido posterior a un barrido de emails paga el borrado de todo
    /// lo que ese barrido dejó, y ahí se va entero el presupuesto de NFR-02. Acotada sigue
    /// convergiendo: cada fallo se lleva un lote, y los fallos sobran justamente cuando hay algo
    /// que purgar.
    ///
    /// Medido contra MySQL 8.4 con 50.000 filas vencidas: borrarlas de una tarda 2,89 s; un lote de
    /// 100, 10 ms. El presupuesto de NFR-02 son 50 ms, así que el lote entra con aire y el borrado
    /// de una sola vez no entra ni cerca.
    /// </summary>
    public const int TamanoDeLaPurga = 100;

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
    ///
    /// El <c>LEAST</c> es el techo de la columna, que es `tinyint unsigned`. Sólo incrementan los
    /// intentos que ya pasaron el chequeo de bloqueo, pero ese chequeo se lee al principio de cada
    /// petición: con cientos de intentos concurrentes sobre el mismo email —el perfil de un ataque
    /// de fuerza bruta, y bcrypt deja la ventana de solapamiento bien abierta— todos leen "no
    /// bloqueado" y todos incrementan. Pasado 255, MySQL corta con "Out of range value" y eso llega
    /// al cliente como un 500. Por encima de <see cref="MaximoDeFallos"/> el valor no cambia
    /// ninguna decisión, así que clavarlo ahí no pierde nada y saca el desborde del mapa.
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
                    LEAST(fallos_consecutivos + 1, {MaximoDeFallos})),
                ultimo_fallo = {ahora}");

        // La purga viaja pegada al fallo, que es el camino que YA escribe. En el de lectura correría
        // en todos los inicios de sesión, incluidos los exitosos, y ahí se gasta el presupuesto de
        // NFR-02 en el camino más frecuente. Borra por el índice de `ultimo_fallo`.
        //
        // Y va POR LOTES: ver TamanoDeLaPurga. Es SQL a mano y no `ExecuteDeleteAsync` porque EF no
        // sabe traducir un `LIMIT` en un borrado.
        await contexto.Database.ExecuteSqlInterpolatedAsync($@"
            DELETE FROM intento_de_acceso
            WHERE ultimo_fallo <= {finDeInactividad}
            LIMIT {TamanoDeLaPurga}");
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

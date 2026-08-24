using System.Globalization;
using System.Security.Claims;

namespace GestionGastos.Api.Persistencia;

/// <summary>
/// La cuenta en cuyo nombre se está operando: la de la sesión iniciada.
///
/// Reemplaza a `UsuarioSemilla`, que devolvía una fila fija. La interfaz no cambió, así que
/// `MovimientosEndpoints` no se tocó: sigue asignando el propietario a mano en el INSERT y
/// acotando la lectura. Esa es exactamente la costura que FEAT-001a dejó preparada (D-05).
/// </summary>
public class UsuarioDeLaSesion(IHttpContextAccessor acceso) : IUsuarioActual
{
    public long Id
    {
        get
        {
            var claim = acceso.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Lanza en vez de devolver un valor por omisión. Si algún endpoint quedara sin
            // proteger, un 0 o un null escribiría filas huérfanas en silencio; así el descuido se
            // convierte en un error visible. La protección real es la autorización: esto es la red
            // debajo.
            if (!long.TryParse(claim, CultureInfo.InvariantCulture, out var id))
            {
                throw new InvalidOperationException(
                    "Se pidió el usuario actual sin una sesión iniciada. Algún endpoint quedó sin " +
                    "exigir autorización: revisá Program.cs y la barrera de autorización.");
            }

            return id;
        }
    }
}

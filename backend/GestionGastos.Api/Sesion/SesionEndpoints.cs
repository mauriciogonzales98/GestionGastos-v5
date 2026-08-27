using System.Security.Claims;
using GestionGastos.Api.Cuentas;
using GestionGastos.Api.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Sesion;

/// <summary>
/// Iniciar, consultar y cerrar sesión (FR-003, FR-004, FR-005).
/// </summary>
public static class SesionEndpoints
{
    /// <summary>
    /// Un hash descartable contra el que verificar cuando la cuenta no existe.
    ///
    /// Sin esto, el login respondería en 2 ms para un email inexistente y en ~100 ms para una
    /// contraseña incorrecta, y esa diferencia distingue las cuentas registradas con un cronómetro.
    /// Igualar el mensaje sin igualar el tiempo deja el canal abierto (research.md D-04).
    /// </summary>
    private static readonly string HashDescartable =
        new HasherDeContrasenas().Hashear("una contraseña que no es de nadie");

    /// <summary>
    /// La única respuesta de rechazo del inicio de sesión, para las tres causas: email inexistente,
    /// contraseña incorrecta y email bloqueado por el límite de intentos. Que sea una sola función
    /// es lo que impide que una de las tres se diferencie de las otras sin que nadie lo note
    /// (AC-04, AC-08, NFR-03).
    /// </summary>
    private static IResult Rechazo() =>
        Results.Problem(
            title: "Email o contraseña incorrectos.",
            statusCode: StatusCodes.Status401Unauthorized);

    public static void MapSesion(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/sesion", async (
            CredencialesDto peticion,
            GestionGastosDbContext contexto,
            HasherDeContrasenas hasher,
            LimiteDeIntentos limite,
            HttpContext http) =>
        {
            var email = peticion.Email?.Trim() ?? string.Empty;

            // Un email más largo que la columna NO va al contador.
            //
            // Hasta el ticket 01b el login no escribía el email en ningún lado, así que no validarlo
            // no costaba nada. Ahora lo escribe: `intento_de_acceso.email` es `varchar(254)` y MySQL
            // en modo estricto corta con "Data too long", y esa excepción sale como un 500 donde
            // corresponde el 401 de siempre. Es la misma cicatriz que el alta ya pagó, y que
            // `ValidacionDeLaCuenta.LargoMaximoDeEmail` documenta.
            //
            // Saltear el contador no abre ningún hueco: un email que no entra en la columna tampoco
            // entra en `usuario.email`, así que no hay cuenta que proteger detrás de él.
            var cuentaElFallo = email.Length <= ValidacionDeLaCuenta.LargoMaximoDeEmail;

            // Cinco fallos consecutivos bloquean este email por 15 minutos, con contraseña correcta
            // incluida (RNF-05, AC-01, AC-02).
            var bloqueado = cuentaElFallo && await limite.EstaBloqueadoAsync(email);

            var cuenta = await contexto.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

            // Se verifica SIEMPRE: exista o no la cuenta, y ESTÉ O NO BLOQUEADO EL EMAIL.
            //
            // Cuando está bloqueado, el resultado se descarta. Parece trabajo al pedo y es el
            // requisito: sin él, el rechazo por bloqueo vuelve en ~2 ms contra los ~100 ms del
            // rechazo por contraseña incorrecta, y esa diferencia dice con un cronómetro qué emails
            // acumularon cinco fallos — que es exactamente lo que el bloqueo no puede publicar
            // (AC-13, research.md D-04). Medido: 2 ms contra 142 ms antes de este cambio.
            //
            // Si alguna vez esto parece código muerto y se "optimiza", AC-13 se rompe y ningún test
            // funcional se entera. Por eso hay además un test que cuenta las verificaciones.
            var hashAVerificar = cuenta?.ContrasenaHash ?? HashDescartable;
            var correcta = hasher.Verificar(peticion.Contrasena ?? string.Empty, hashAVerificar);

            if (bloqueado)
            {
                // El intento rechazado por el bloqueo NO suma al contador ni mueve la marca: la
                // ventana se cuenta desde el quinto fallo y es fija. Si se moviera, cualquiera
                // dejaría a otra persona afuera para siempre golpeando su email cada 14 minutos.
                return Rechazo();
            }

            if (cuenta is null || !correcta)
            {
                if (cuentaElFallo)
                {
                    await limite.RegistrarFalloAsync(email);
                }

                return Rechazo();
            }

            // El acierto borra los fallos previos: "consecutivos" quiere decir exactamente esto
            // (FR-03, AC-05).
            await limite.ReiniciarAsync(email);

            var identidad = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, cuenta.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new Claim(ClaimTypes.Email, cuenta.Email),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidad));

            return Results.Ok(new SesionActualDto(cuenta.Email));
        })
        .AllowAnonymous();

        rutas.MapGet("/api/sesion", (ClaimsPrincipal quien) =>
            Results.Ok(new SesionActualDto(quien.FindFirstValue(ClaimTypes.Email) ?? string.Empty)));

        rutas.MapDelete("/api/sesion", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        // Cerrar una sesión que ya no existe también responde 204: no es un error, y exigir sesión
        // para poder cerrarla dejaría a alguien con una cookie vencida sin forma de limpiarla.
        .AllowAnonymous();
    }
}

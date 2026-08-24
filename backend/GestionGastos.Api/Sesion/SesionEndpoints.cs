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

    public static void MapSesion(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/sesion", async (
            CredencialesDto peticion,
            GestionGastosDbContext contexto,
            HasherDeContrasenas hasher,
            HttpContext http) =>
        {
            var email = peticion.Email?.Trim() ?? string.Empty;
            var cuenta = await contexto.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

            // Se verifica SIEMPRE, exista o no la cuenta.
            var hashAVerificar = cuenta?.ContrasenaHash ?? HashDescartable;
            var correcta = hasher.Verificar(peticion.Contrasena ?? string.Empty, hashAVerificar);

            if (cuenta is null || !correcta)
            {
                // Una sola respuesta para las dos causas (AC-04, NFR-03).
                return Results.Problem(
                    title: "Email o contraseña incorrectos.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

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

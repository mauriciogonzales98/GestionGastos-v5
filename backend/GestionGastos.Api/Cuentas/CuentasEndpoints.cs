using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Cuentas;

/// <summary>
/// El alta de cuenta (FR-001, FR-002).
/// </summary>
public static class CuentasEndpoints
{
    public static void MapCuentas(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/cuentas", async (
            NuevaCuentaDto peticion,
            GestionGastosDbContext contexto,
            HasherDeContrasenas hasher) =>
        {
            var errores = ValidacionDeLaCuenta.Validar(peticion.Email, peticion.Contrasena);
            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

            var email = peticion.Email!.Trim();

            // Se hashea SIEMPRE, antes de saber si hace falta, y si el email ya estaba el resultado
            // se tira. Es trabajo desperdiciado a propósito: hashear sólo cuando la cuenta se crea
            // dejaba el alta respondiendo en 2 ms para un email registrado y en ~100 ms para uno
            // nuevo, y esa diferencia publica el padrón de emails con un cronómetro aunque el
            // mensaje y el código sean idénticos. Es la misma medida que el login toma con
            // `HashDescartable`, y sin ella igualar la respuesta es decorativo (research.md D-04).
            var hash = hasher.Hashear(peticion.Contrasena!);

            // La comparación no distingue mayúsculas porque la colación de la columna es
            // `utf8mb4_0900_ai_ci`: `Ana@x.com` y `ana@x.com` son la misma cuenta.
            var yaExiste = await contexto.Usuarios.AnyAsync(u => u.Email == email);

            if (!yaExiste)
            {
                contexto.Usuarios.Add(new Usuario
                {
                    Email = email,
                    ContrasenaHash = hash,
                });

                await contexto.SaveChangesAsync();
            }

            // La MISMA respuesta en los dos casos (NFR-03). Si el email ya estaba, no se creó nada
            // y la cuenta original quedó intacta (AC-02) — pero eso no se dice, porque decirlo
            // publicaría qué emails están registrados.
            return Results.Created((string?)null, new AltaDeCuentaDto(
                "Si el email no estaba registrado, la cuenta fue creada. Ya podés iniciar sesión."));
        })
        .AllowAnonymous();
    }
}

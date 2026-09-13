using GestionGastos.Api.Categorias;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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
            // Se normaliza ANTES de validar, no después. Al revés el `Trim` no podía hacer nada:
            // `EsUnEmail` rechaza cualquier valor con un espacio, incluidos los de los extremos, así
            // que ` ana@x.com ` moría con "ese email no parece válido" — y el login, que sí trimea,
            // aceptaba ese mismo texto. La misma persona escribiendo lo mismo entraba y no podía
            // darse de alta.
            var email = peticion.Email?.Trim();

            var errores = ValidacionDeLaCuenta.Validar(email, peticion.Contrasena);
            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

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
                var cuenta = new Usuario
                {
                    // `email` no es null acá: la validación de arriba ya rechazó el vacío.
                    Email = email!,
                    ContrasenaHash = hash,
                };

                contexto.Usuarios.Add(cuenta);

                // Las diez categorías iniciales de esta cuenta (FR-002). Quedan enlazadas al usuario
                // recién construido, que todavía no tiene identificador: lo propaga EF dentro del
                // mismo `SaveChanges`.
                //
                // **Un solo `SaveChangesAsync`, el que ya había.** Es una sola transacción implícita,
                // y eso es exactamente lo que FR-003 pide: la cuenta y su catálogo quedan las dos o
                // no queda ninguna. No hace falta abrir una explícita.
                //
                // La escritura vive dentro de `CatalogoInicial` y no acá: la barrera de aislamiento
                // nombra archivo por archivo quién puede escribir categorías, y este archivo hace
                // muchas otras cosas (D-06).
                CatalogoInicial.EntregarA(contexto, cuenta);

                try
                {
                    await contexto.SaveChangesAsync();
                }
                catch (DbUpdateException excepcion) when (EsEmailDuplicado(excepcion))
                {
                    // Entre la consulta y el INSERT, otra petición creó esa misma cuenta. El índice
                    // único la frenó —que es su trabajo— y acá termina como termina cualquier alta
                    // con un email ya registrado: sin crear nada y con la respuesta de siempre.
                    //
                    // No es un catch silencioso: se atrapa UN error concreto, el 1062 del email, y
                    // el resultado es el que corresponde. Dejarlo salir devolvía un 500 que además
                    // delataba la cuenta existente, que es lo que NFR-03 evita.
                }
            }

            // La MISMA respuesta en los dos casos (NFR-03). Si el email ya estaba, no se creó nada
            // y la cuenta original quedó intacta (AC-02) — pero eso no se dice, porque decirlo
            // publicaría qué emails están registrados.
            return Results.Created((string?)null, new AltaDeCuentaDto(
                "Si el email no estaba registrado, la cuenta fue creada. Ya podés iniciar sesión."));
        })
        .AllowAnonymous();
    }

    /// <summary>
    /// <c>true</c> si el fallo es el índice único del email, y no cualquier otro problema al
    /// guardar.
    ///
    /// Se mira el número de error de MySQL —1062, clave duplicada— y no el texto del mensaje, que
    /// cambia con la versión y con el idioma del servidor. `MySqlConnector` llega con Pomelo; no es
    /// una dependencia nueva.
    ///
    /// **El 1062 solo dejó de alcanzar con la feature 013** (research D-07). Hasta entonces este
    /// `SaveChanges` escribía una fila y el único índice único que podía saltar era el del email.
    /// Ahora escribe once —la cuenta y sus diez categorías—, y `ux_categoria_ambito_nombre_tipo`
    /// también contesta 1062. Un `catch` que se quedara en el número atraparía un fallo del catálogo
    /// y lo haría pasar por "email ya registrado": la cuenta no se crearía, la respuesta diría que
    /// quizá sí, y no quedaría rastro en ningún lado.
    ///
    /// Por eso se mira **sobre qué entidad** falló, que es información que EF da estructurada. Es
    /// más preciso que el nombre del índice dentro del mensaje, que sí depende del texto.
    ///
    /// **Y se exige que haya al menos una entrada, que no es una redundancia.** `All` sobre una lista
    /// vacía devuelve `true` —"todas son un `Usuario`" es cierto cuando no hay ninguna—, así que un
    /// `1062` que EF no pudiera atribuir a ninguna entidad volvería a caer en este `catch` y el alta
    /// respondería como si la cuenta se hubiera creado sin haberla creado. Es el mismo desenlace que
    /// esta comprobación viene a impedir, entrando por la puerta de al lado.
    /// </summary>
    private static bool EsEmailDuplicado(DbUpdateException excepcion) =>
        excepcion.InnerException is MySqlException mysql
        && mysql.ErrorCode == MySqlErrorCode.DuplicateKeyEntry
        && excepcion.Entries.Count > 0
        && excepcion.Entries.All(entrada => entrada.Entity is Usuario);
}

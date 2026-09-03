using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Categorias;

/// <summary>
/// El catálogo que alimenta el selector del formulario (FR-006).
/// </summary>
public static class CategoriasEndpoints
{
    public static void MapCategorias(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/categorias", async (
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual) =>
        {
            // La lectura sale del canal (D-03). Acá sólo se proyecta a la forma del contrato: qué
            // filas se pueden ver lo decide `CategoriasConsulta`, y es lo que la barrera vigila.
            var categorias = await CategoriasConsulta
                .Ofrecibles(contexto, usuarioActual.Id)
                .Select(c => new CategoriaDto(
                    c.Id,
                    c.Nombre,
                    c.Tipo == TipoMovimiento.Gasto ? TipoMovimientoTexto.Gasto : TipoMovimientoTexto.Ingreso,
                    // Propia es tener dueño. Se calcula acá y no se compara del lado del cliente:
                    // el `usuario_id` no viaja, justamente para que no haya nada que comparar.
                    c.UsuarioId != null))
                .ToListAsync();

            return Results.Ok(categorias);
        });

        // POST /api/categorias — el alta (FR-004).
        rutas.MapPost("/api/categorias", async (
            NuevaCategoriaDto peticion,
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual) =>
        {
            var validacion = await ValidacionDeLaCategoria.ValidarAltaAsync(
                contexto, usuarioActual.Id, peticion.Nombre, peticion.Tipo);

            if (validacion.HayErrores)
            {
                return Results.ValidationProblem(validacion.Errores);
            }

            var categoria = new Categoria
            {
                // El propietario lo decide la sesión, nunca el cuerpo (INV-01). Es lo que hace que
                // la categoría nazca privada en vez de volverse privada después.
                UsuarioId = usuarioActual.Id,
                Nombre = validacion.Nombre,
                Tipo = validacion.Tipo,
                Activa = true,

                // 0 mientras esté activa. Se escribe explícito y no se deja al DEFAULT de la
                // columna: quien lee este alta tiene que ver de dónde sale el valor que después la
                // baja va a cambiar (D-01).
                Discriminador = 0,
            };

            contexto.Categorias.Add(categoria);
            await contexto.SaveChangesAsync();

            return Results.Created($"/api/categorias/{categoria.Id}", AlDto(categoria));
        });

        // PUT /api/categorias/{id} — el renombre (FR-007).
        //
        // Se BUSCA primero y se valida después, igual que en la edición de movimientos: al revés,
        // una categoría intocable con un nombre inválido respondería 400 en vez de 403 o 404, y ese
        // 400 confirma que se llegó a mirar el cuerpo.
        //
        // **El tipo no viaja en la petición** y no se toca: cambiarlo movería de tipo a todos los
        // movimientos que la usan, que es reescribir la historia por la puerta de atrás.
        rutas.MapPut("/api/categorias/{id:int}", async (
            int id,
            CategoriaEditadaDto peticion,
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual) =>
        {
            var (categoria, rechazo) = await BuscarPropiaAsync(
                contexto, usuarioActual.Id, id, debeEstarActiva: true);
            if (rechazo is not null)
            {
                return rechazo;
            }

            var validacion = await ValidacionDeLaCategoria.ValidarRenombreAsync(
                contexto, usuarioActual.Id, peticion.Nombre, categoria!.Tipo, categoria.Id);

            if (validacion.HayErrores)
            {
                return Results.ValidationProblem(validacion.Errores);
            }

            categoria.Nombre = validacion.Nombre;
            await contexto.SaveChangesAsync();

            return Results.Ok(AlDto(categoria));
        });

        // DELETE /api/categorias/{id} — la baja lógica (FR-010).
        //
        // **La fila NO se borra.** Se apaga `activa` y se le escribe el discriminador, y ésa es la
        // diferencia entera con el DELETE de movimientos: una categoría sigue nombrando lo que ya
        // nombró. Un borrado real dejaría los movimientos apuntando a la nada —la clave foránea es
        // RESTRICT, así que ni siquiera se podría— y el resumen de un mes cerrado cambiaría solo
        // (FR-011, AC-06).
        rutas.MapDelete("/api/categorias/{id:int}", async (
            int id,
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual) =>
        {
            // `debeEstarActiva: false`, al revés que el renombre: la baja es idempotente y tiene
            // que ENCONTRAR la fila ya apagada para responder 204 en vez de 404.
            var (categoria, rechazo) = await BuscarPropiaAsync(
                contexto, usuarioActual.Id, id, debeEstarActiva: false);
            if (rechazo is not null)
            {
                return rechazo;
            }

            // Idempotente (D-06): darle de baja a algo ya dado de baja devuelve 204 también. El
            // estado final es el mismo y obligar al cliente a distinguir dos situaciones idénticas
            // no le sirve a nadie — menos cuando la segunda petición suele ser un doble clic.
            //
            // Se comprueba `Activa` y no `Discriminador`: la que manda es la baja, el discriminador
            // es su consecuencia.
            if (categoria!.Activa)
            {
                categoria.Activa = false;

                // **En el MISMO UPDATE que el `Activa = false`**, y no en dos pasos. Entre los dos
                // habría un instante con la fila ya dada de baja y el discriminador todavía en 0, y
                // ahí el índice único la sigue considerando una activa: un alta homónima simultánea
                // chocaría contra una fila que ya no existe para nadie (D-01).
                categoria.Discriminador = categoria.Id;

                await contexto.SaveChangesAsync();
            }

            return Results.NoContent();
        });
    }

    /// <summary>
    /// Busca la categoría que la cuenta puede MODIFICAR, o el rechazo que corresponde.
    ///
    /// Los tres desenlaces son los del contrato, y la diferencia entre ellos es el aislamiento
    /// entero de esta feature (D-06):
    ///
    /// · **Existe y es propia** → se devuelve para tocarla.
    /// · **Existe y es predefinida** → `403`. La persona la está VIENDO en su selector, así que
    ///   decirle que no existe sería mentirle sobre algo que tiene a la vista. No hay nada que
    ///   ocultar: el catálogo predefinido es igual para todas las cuentas (FR-008).
    /// · **No existe, o es propia de otra cuenta** → `404`, el MISMO para los dos casos. Acá sí hay
    ///   algo que ocultar, y cualquier diferencia entre las dos respuestas confirmaría que esa fila
    ///   existe. Los ids son autoincrementales y contiguos, así que confirmarlo permite contar las
    ///   categorías de otra cuenta sin ver ninguna (FR-013).
    ///
    /// La búsqueda pasa por el canal, que acota por ámbito en la consulta. Traer la fila por `Id` y
    /// comprobar el dueño en memoria daría el mismo 404 visible y dejaría el `WHERE` sin
    /// `usuario_id` — o sea, `BarreraDeAislamientoTests` en rojo. Es a propósito.
    ///
    /// **El `activa` lo decide quien llama, y los dos verbos deciden distinto.** El canal no filtra
    /// por `activa` porque la baja es idempotente: darle de baja a algo ya dado de baja tiene que
    /// encontrarlo para responder `204` en vez de `404`. El renombre necesita lo contrario: una
    /// categoría apagada ya no es renombrable, porque su nombre es el que los movimientos viejos
    /// siguen mostrando y cambiarlo reescribe la historia que la baja lógica existe para preservar
    /// (FR-004, FR-011). Por eso el parámetro no tiene valor por omisión: elegir mal es exactamente
    /// el error que se acaba de arreglar, y un `= false` lo dejaría volver en silencio.
    /// </summary>
    /// <param name="debeEstarActiva">
    /// <c>true</c> para el renombre: una categoría dada de baja responde el mismo `404` que una
    /// inexistente, porque para el catálogo de esa cuenta ya no está (FR-013). <c>false</c> para la
    /// baja, que tiene que encontrarla para ser idempotente.
    /// </param>
    private static async Task<(Categoria? Categoria, IResult? Rechazo)> BuscarPropiaAsync(
        GestionGastosDbContext contexto,
        long usuarioId,
        int id,
        bool debeEstarActiva)
    {
        var categoria = await CategoriasConsulta
            .DelAmbitoPorId(contexto, usuarioId, id)
            .FirstOrDefaultAsync();

        if (categoria is null)
        {
            return (null, NoExiste());
        }

        if (categoria.UsuarioId is null)
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Categoría del sistema",
                detail: "Las categorías predefinidas no se pueden modificar ni dar de baja."));
        }

        if (debeEstarActiva && !categoria.Activa)
        {
            return (null, NoExiste());
        }

        return (categoria, null);
    }

    /// <summary>
    /// La respuesta única de "esa categoría no está a tu alcance".
    ///
    /// Está en un solo lugar a propósito, igual que en movimientos: la indistinguibilidad entre lo
    /// ajeno y lo inexistente (FR-013) se sostiene sola mientras haya UNA sola forma de decirlo.
    /// Dos `Results.NotFound()` escritos por separado divergen el día que alguien mejora un mensaje,
    /// y esa mejora es la fuga.
    /// </summary>
    private static IResult NoExiste() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado");

    /// <summary>
    /// La categoría en la forma del contrato, para las respuestas que devuelven una sola.
    ///
    /// El listado no la usa: proyecta dentro de la consulta, que es lo que evita traer la entidad
    /// entera de la base para descartarle dos columnas.
    /// </summary>
    private static CategoriaDto AlDto(Categoria categoria) => new(
        categoria.Id,
        categoria.Nombre,
        categoria.Tipo.ATexto(),
        categoria.UsuarioId != null);
}

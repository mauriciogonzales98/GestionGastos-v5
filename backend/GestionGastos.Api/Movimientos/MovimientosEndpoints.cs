using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Alta y listado de movimientos (FR-001, FR-002, FR-007).
/// </summary>
public static class MovimientosEndpoints
{
    public static void MapMovimientos(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/movimientos", async (
            NuevoMovimientoDto peticion,
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual,
            TimeProvider reloj,
            TimeZoneInfo zona) =>
        {
            // La categoría se busca con el MISMO criterio con el que el catálogo la ofrece:
            // predefinida del sistema o propia de esta cuenta, y activa. Buscar sólo por id
            // aceptaba cualquier fila de la tabla.
            //
            // Hoy todas las filas son predefinidas y activas, así que no cambia nada visible. Pero
            // el ticket 3 introduce categorías propias y bajas lógicas sin volver a pasar por acá:
            // con la búsqueda por id suelta, una cuenta podía registrar un movimiento contra la
            // categoría privada de otra —los ids son autoincrementales y contiguos, no hay nada que
            // adivinar— y el nombre ajeno aparecía en su listado. El aislamiento tiene que nacer
            // escrito, no agregarse cuando ya hay dos cuentas.
            //
            // No se distingue "no existe" de "no es tuya": la respuesta es la misma, para no
            // confirmar la existencia de una categoría ajena.
            var categoria = peticion.CategoriaId is { } id
                ? await contexto.Categorias.FirstOrDefaultAsync(c =>
                    c.Id == id
                    && (c.UsuarioId == null || c.UsuarioId == usuarioActual.Id)
                    && c.Activa)
                : null;

            // Se valida TODO antes de tocar la base: la respuesta junta los errores de los cuatro
            // campos en una sola pasada, en vez de hacer corregir de a uno.
            var errores = ValidacionDelMovimiento.Validar(peticion, categoria, out var tipo);
            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

            var monto = peticion.Monto!.Value;

            // FR-009: la moneda sale de la predeterminada del catálogo, no de una constante.
            //
            // SingleAsync y no FirstAsync: RF-25 dice que hay exactamente una, y la migración
            // UnicaMonedaPredeterminada lo hace cumplir en la base. Si igual hubiera dos, First
            // elegiría una sin criterio y en silencio; Single falla ruidosamente, que es lo que
            // corresponde ante una invariante rota.
            var moneda = await contexto.Monedas.SingleAsync(m => m.EsPredeterminada);

            // El "hoy" sale del reloj inyectado y no de DateTime.Now: es lo que vuelve verificable
            // AC-17 con una fecha fija (D-03).
            var fecha = peticion.Fecha ?? DiaActual.De(reloj, zona);

            var movimiento = new Movimiento
            {
                // FR-010: el propietario se asigna a mano en el INSERT. El filtro global de lectura
                // del ticket 1c no aplica a la escritura, así que esto no puede quedar implícito.
                UsuarioId = usuarioActual.Id,
                Tipo = tipo,
                Monto = monto,
                MonedaId = moneda.Id,
                CategoriaId = categoria!.Id,
                Fecha = fecha,
            };

            contexto.Movimientos.Add(movimiento);
            await contexto.SaveChangesAsync();

            var creado = new MovimientoDto(
                movimiento.Id,
                tipo.ATexto(),
                movimiento.Monto,
                categoria.Id,
                categoria.Nombre,
                moneda.Codigo,
                movimiento.Fecha);

            // El Location apunta a la lectura individual, que existe desde FEAT-001b. Hasta
            // entonces este Created iba sin encabezado, porque la URL habría dado un 404 y un
            // encabezado que promete un recurso inalcanzable es peor que no ponerlo.
            return Results.Created($"/api/movimientos/{movimiento.Id}", creado);
        });

        rutas.MapGet("/api/movimientos", async (
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual,
            TimeProvider reloj,
            TimeZoneInfo zona,
            DateOnly? desde,
            DateOnly? hasta,
            int? categoriaId) =>
        {
            // Las tres reglas del período —los dos extremos juntos o ninguno, el rango invertido
            // rechazado, y el mes en curso del SERVIDOR por omisión— viven en `PeriodoPedido` desde
            // FEAT-001c. No están acá porque el resumen necesita exactamente las mismas: dos copias
            // de una regla son dos copias que divergen el día que alguien arregla una sola (D-03).
            var hoy = DiaActual.De(reloj, zona);

            var errores = PeriodoPedido.Interpretar(desde, hasta, hoy, out var rango);
            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

            // La categoría NO se valida contra el catálogo: una que no existe simplemente no deja
            // pasar nada. Rechazarla con un 400 confirmaría cuáles existen, que es la misma fuga
            // que el 404 uniforme cierra en las rutas por identificador.
            var movimientos = await MovimientosConsulta
                .Filtrado(contexto, usuarioActual.Id, rango, categoriaId)
                .Select(m => new MovimientoDto(
                    m.Id,
                    m.Tipo == TipoMovimiento.Gasto ? TipoMovimientoTexto.Gasto : TipoMovimientoTexto.Ingreso,
                    m.Monto,
                    m.CategoriaId,
                    m.Categoria!.Nombre,
                    m.Moneda!.Codigo,
                    m.Fecha))
                .ToListAsync();

            // Arreglo vacío si no hay movimientos en el mes: NO es un 404 (FR-012).
            return Results.Ok(movimientos);
        });
        // GET /api/movimientos/{id} — la lectura individual (FR-001).
        //
        // Pasa por el canal, que acota por cuenta en la consulta. El 404 es el mismo para las tres
        // situaciones —no existe, es de otra cuenta, ya se eliminó— y eso no es comodidad: un 403
        // sobre lo ajeno confirmaría que ese identificador existe, y como son autoincrementales y
        // contiguos, confirmarlo permite contar los movimientos de otra cuenta sin ver ninguno
        // (FR-008, D-06).
        rutas.MapGet("/api/movimientos/{id:long}", async (
            long id,
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual) =>
        {
            var movimiento = await MovimientosConsulta
                .PropioPorId(contexto, usuarioActual.Id, id)
                .Select(m => new MovimientoDto(
                    m.Id,
                    m.Tipo == TipoMovimiento.Gasto ? TipoMovimientoTexto.Gasto : TipoMovimientoTexto.Ingreso,
                    m.Monto,
                    m.CategoriaId,
                    m.Categoria!.Nombre,
                    m.Moneda!.Codigo,
                    m.Fecha))
                .FirstOrDefaultAsync();

            return movimiento is null ? NoExiste() : Results.Ok(movimiento);
        });

        // PUT /api/movimientos/{id} — la edición (FR-002).
        //
        // El orden importa y es parte del contrato: se BUSCA primero, acotado por cuenta, y recién
        // después se valida el cuerpo. Al revés, un movimiento ajeno con un cuerpo inválido
        // respondería 400 en vez de 404, y ese 400 confirma que se llegó a mirar el cuerpo — o sea,
        // que el identificador existe.
        rutas.MapPut("/api/movimientos/{id:long}", async (
            long id,
            MovimientoEditadoDto peticion,
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual) =>
        {
            var movimiento = await MovimientosConsulta
                .PropioPorId(contexto, usuarioActual.Id, id)
                .FirstOrDefaultAsync();

            if (movimiento is null)
            {
                return NoExiste();
            }

            // Mismo criterio de búsqueda que el alta: predefinida del sistema o propia de esta
            // cuenta, y activa. Buscar sólo por id dejaría entrar la categoría privada de otra
            // cuenta cuando el ticket 3 las introduzca, y el nombre ajeno aparecería en el listado.
            var categoria = peticion.CategoriaId is { } categoriaId
                ? await contexto.Categorias.FirstOrDefaultAsync(c =>
                    c.Id == categoriaId
                    && (c.UsuarioId == null || c.UsuarioId == usuarioActual.Id)
                    && c.Activa)
                : null;

            var errores = ValidacionDelMovimiento.Validar(peticion, categoria, out var tipo);

            if (peticion.Fecha is null)
            {
                // Obligatoria sólo al editar: ausente significaría "hoy", y una edición sin fecha
                // movería el movimiento en silencio.
                errores["fecha"] = ["Indicá la fecha del movimiento."];
            }

            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

            // El propietario NO se toca: no es un campo del contrato, y si llegara igual en el JSON
            // se descarta al deserializar. Lo decide la sesión, siempre (INV-01).
            movimiento.Tipo = tipo;
            movimiento.Monto = peticion.Monto!.Value;
            movimiento.CategoriaId = categoria!.Id;
            movimiento.Fecha = peticion.Fecha!.Value;

            await contexto.SaveChangesAsync();

            var moneda = await contexto.Monedas.SingleAsync(m => m.Id == movimiento.MonedaId);

            return Results.Ok(new MovimientoDto(
                movimiento.Id,
                tipo.ATexto(),
                movimiento.Monto,
                categoria.Id,
                categoria.Nombre,
                moneda.Codigo,
                movimiento.Fecha));
        });

        // DELETE /api/movimientos/{id} — la eliminación (FR-006).
        //
        // Borra la fila: no hay baja lógica (D-09). El PRD usa ese término explícitamente para las
        // categorías y no para los movimientos, y el contraste parece deliberado.
        //
        // La carrera —dos operaciones sobre el mismo movimiento a la vez— se resuelve sola: la
        // segunda no lo encuentra por el canal y responde 404, que es justo lo que pide AC-10. No
        // hace falta concurrencia optimista porque no hay estado que corromper, sólo una operación
        // que llegó tarde.
        rutas.MapDelete("/api/movimientos/{id:long}", async (
            long id,
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual) =>
        {
            var movimiento = await MovimientosConsulta
                .PropioPorId(contexto, usuarioActual.Id, id)
                .FirstOrDefaultAsync();

            if (movimiento is null)
            {
                return NoExiste();
            }

            contexto.Movimientos.Remove(movimiento);
            await contexto.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    /// <summary>
    /// La respuesta única de "ese movimiento no está a tu alcance".
    ///
    /// Está en un solo lugar a propósito: la indistinguibilidad entre lo ajeno y lo inexistente
    /// (FR-008) se sostiene sola mientras haya una sola forma de decirlo. Dos `Results.NotFound()`
    /// escritos por separado divergen el día que alguien mejora un mensaje.
    /// </summary>
    private static IResult NoExiste() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado");
}

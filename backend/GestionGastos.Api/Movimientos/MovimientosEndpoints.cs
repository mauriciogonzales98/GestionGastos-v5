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
            TimeProvider reloj) =>
        {
            var categoria = peticion.CategoriaId is { } id
                ? await contexto.Categorias.FirstOrDefaultAsync(c => c.Id == id)
                : null;

            // Se valida TODO antes de tocar la base: la respuesta junta los errores de los cuatro
            // campos en una sola pasada, en vez de hacer corregir de a uno.
            var errores = ValidacionDelAlta.Validar(peticion, categoria, out var tipo);
            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

            var monto = peticion.Monto!.Value;

            // FR-009: la moneda sale de la predeterminada del catálogo, no de una constante.
            var moneda = await contexto.Monedas.FirstAsync(m => m.EsPredeterminada);

            // El "hoy" sale del reloj inyectado y no de DateTime.Now: es lo que vuelve verificable
            // AC-17 con una fecha fija (D-03).
            var fecha = peticion.Fecha ?? DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

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

            return Results.Created($"/api/movimientos/{movimiento.Id}", creado);
        });

        rutas.MapGet("/api/movimientos", async (
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual,
            TimeProvider reloj) =>
        {
            // El recorte al mes actual es del servidor y no se expone como control (FR-007):
            // ponerlo en el cliente lo convertiría en algo que el cliente puede cambiar. Los
            // parámetros de rango llegan en FEAT-001b.
            var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

            var movimientos = await MovimientosConsulta
                .DelMes(contexto, usuarioActual.Id, RangoDelMes.De(hoy))
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
    }
}

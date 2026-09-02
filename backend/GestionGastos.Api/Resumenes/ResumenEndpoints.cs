using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;


namespace GestionGastos.Api.Resumenes;

/// <summary>
/// El resumen del período (RF-19, RF-20, RF-21, RF-22).
///
/// **Es un endpoint y no dos.** AC-30 exige que el total del mes de la pantalla principal sea igual
/// al del dashboard filtrado por el mes actual; dos endpoints que tienen que dar lo mismo son dos
/// endpoints que algún día no van a darlo, y el día que diverjan nadie va a poder decir cuál de los
/// dos números está mal. La pantalla principal es este mismo resumen pedido sin período (D-02).
/// </summary>
public static class ResumenEndpoints
{
    public static void MapResumen(this IEndpointRouteBuilder rutas) =>
        rutas.MapGet("/api/resumen", async (
            GestionGastosDbContext contexto,
            IUsuarioActual usuarioActual,
            TimeProvider reloj,
            TimeZoneInfo zona,
            DateOnly? desde,
            DateOnly? hasta,
            CancellationToken cancelacion) =>
        {
            // Las reglas del período son LAS MISMAS del listado, y salen del mismo código. Que sean
            // el mismo código no es prolijidad: FR-005 exige que el resumen y el listado describan
            // el mismo conjunto ante el mismo período, y con dos intérpretes esa igualdad depende
            // de que nadie toque uno sin tocar el otro (D-03).
            var hoy = DiaActual.De(reloj, zona);

            var errores = PeriodoPedido.Interpretar(desde, hasta, hoy, out var rango);
            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

            return Results.Ok(await CalculoDelResumen.CalcularAsync(
                contexto, usuarioActual.Id, rango, cancelacion));
        });
}

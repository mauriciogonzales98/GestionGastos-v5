using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Monedas;

/// <summary>
/// El catálogo de monedas, que alimenta el selector del formulario y el acotado del listado
/// (FR-004, FR-005, FR-010).
///
/// **No hay `MonedasConsulta`, y no es un olvido** (D-03 de la feature 009). Los canales de
/// `MovimientosConsulta` y `CategoriasConsulta` existen por una razón concreta: el acotado por
/// cuenta o por ámbito se escribe a mano, así que una consulta nueva nace sin él salvo que alguien
/// se acuerde, y `BarreraDeAislamientoTests` vigila que nadie lea fuera del canal. **La moneda no
/// tiene dueño**: es una tabla del sistema, igual para todas las cuentas, sin `usuario_id` que
/// acotar. Un canal acá sería una clase que no protege nada y que sugeriría que hay algo que
/// aislar, que es justo la confusión que la barrera evita.
///
/// **Devuelve el catálogo entero, sin filtrar nada.** No hay monedas dadas de baja ni monedas
/// "elegibles": si está en la tabla, se ofrece. Es lo que hace que agregar una fila alcance para
/// que aparezca en la pantalla, sin tocar código (RF-32, `PRD:AC-04`).
///
/// Exige sesión como todo el resto: la `FallbackPolicy` de `Program.cs` la impone y este endpoint
/// no declara `AllowAnonymous`. Un catálogo no es un secreto, pero la regla del proyecto es que
/// todo endpoint pide sesión, y `verificar-autorizacion.sh` se pone en rojo ante uno que no.
/// </summary>
public static class MonedasEndpoints
{
    public static void MapMonedas(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/monedas", async (GestionGastosDbContext contexto) =>
        {
            // El orden se pide explícitamente aunque el motor hoy lo devuelva así: es parte del
            // contrato —el selector muestra las monedas en este orden— y heredarlo del plan de
            // ejecución lo deja a merced de que el plan cambie. Mismo criterio que
            // `CategoriasConsulta.Ofrecibles`.
            var monedas = await contexto.Monedas
                .OrderBy(m => m.Id)
                .Select(m => new MonedaDto(m.Id, m.Codigo, m.Nombre, m.Simbolo, m.EsPredeterminada))
                .ToListAsync();

            return Results.Ok(monedas);
        });
    }
}

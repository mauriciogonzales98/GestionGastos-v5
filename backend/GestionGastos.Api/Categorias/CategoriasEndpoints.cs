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
        rutas.MapGet("/api/categorias", async (GestionGastosDbContext contexto) =>
        {
            // Sólo las predefinidas del sistema y activas. El filtro por usuario_id llega en el
            // ticket 3, cuando existan categorías propias; hoy todas las filas son NULL, pero
            // escribirlo ahora evita que la consulta cambie de significado cuando dejen de serlo.
            var categorias = await contexto.Categorias
                .Where(c => c.UsuarioId == null && c.Activa)
                .OrderBy(c => c.Tipo).ThenBy(c => c.Id)
                .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Tipo == TipoMovimiento.Gasto
                    ? TipoMovimientoTexto.Gasto
                    : TipoMovimientoTexto.Ingreso))
                .ToListAsync();

            return Results.Ok(categorias);
        });
    }
}

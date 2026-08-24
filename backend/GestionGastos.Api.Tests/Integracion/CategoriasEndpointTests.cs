using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// AC-10: el selector del formulario ofrece únicamente las categorías del tipo que se está
/// cargando —ninguna de ingreso cuando se carga un gasto, y viceversa—. El cliente hace ese corte
/// agrupando por `tipo`, así que si este endpoint no distingue bien los tipos, AC-10 es
/// inverificable en la pantalla.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class CategoriasEndpointTests(BaseDeDatosFixture baseDeDatos)
{
    // El fixture se recibe para que la base esté creada y migrada antes de la primera petición.
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Devuelve_El_Catalogo_Completo_Separado_Por_Tipo_AC10()
    {
        Assert.NotNull(_baseDeDatos);
        using var factoria = new WebApplicationFactory<Program>();
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var categorias = json.RootElement.EnumerateArray().ToList();

        // Las diez de FR-006, exactamente: ni una de más.
        Assert.Equal(10, categorias.Count);
        Assert.Equal(7, categorias.Count(c => c.GetProperty("tipo").GetString() == "gasto"));
        Assert.Equal(3, categorias.Count(c => c.GetProperty("tipo").GetString() == "ingreso"));
    }

    [Fact]
    public async Task El_Tipo_Viaja_Como_Cadena_Y_No_Como_Numero_AC10()
    {
        Assert.NotNull(_baseDeDatos);
        using var factoria = new WebApplicationFactory<Program>();
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        // El tinyint de la base no sale a la red: obligaría al frontend a conocer el mapeo y lo
        // volvería frágil ante un cambio de esquema.
        foreach (var categoria in json.RootElement.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.String, categoria.GetProperty("tipo").ValueKind);
            Assert.Equal(JsonValueKind.Number, categoria.GetProperty("id").ValueKind);
            Assert.Equal(JsonValueKind.String, categoria.GetProperty("nombre").ValueKind);
        }
    }

    [Fact]
    public async Task Otros_Existe_En_Los_Dos_Tipos_Como_Dos_Filas_Distintas_AC10()
    {
        Assert.NotNull(_baseDeDatos);
        using var factoria = new WebApplicationFactory<Program>();
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        var otros = json.RootElement.EnumerateArray()
            .Where(c => c.GetProperty("nombre").GetString() == "Otros")
            .ToList();

        Assert.Equal(2, otros.Count);
        Assert.Single(otros, c => c.GetProperty("tipo").GetString() == "gasto");
        Assert.Single(otros, c => c.GetProperty("tipo").GetString() == "ingreso");
        Assert.NotEqual(otros[0].GetProperty("id").GetInt32(), otros[1].GetProperty("id").GetInt32());
    }
}

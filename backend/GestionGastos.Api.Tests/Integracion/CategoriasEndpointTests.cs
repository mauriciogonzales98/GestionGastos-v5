using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        using var respuesta = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var categorias = json.RootElement.EnumerateArray().ToList();

        // Las diez de FR-006, exactamente: ni una de más.
        Assert.Equal(10, categorias.Count);
        Assert.Equal(7, categorias.Count(c => c.GetProperty("tipo").GetString() == "gasto"));
        Assert.Equal(3, categorias.Count(c => c.GetProperty("tipo").GetString() == "ingreso"));
    }

    /// <summary>
    /// **FR-004**: el catálogo devuelve sólo las **activas del ámbito** — ni las dadas de baja, ni
    /// ninguna de otra cuenta—, ordenadas por tipo y después por identificador.
    ///
    /// Las tres mitades en un test y no en tres: son la misma promesa mirada desde tres lados, y
    /// separarlas deja pasar el día en que una pase por un motivo que rompe otra. El caso de la
    /// cuenta ajena es el que un `Where` mal escrito rompe en silencio, porque en una base con una
    /// sola cuenta no se nota.
    /// </summary>
    [Fact]
    public async Task Devuelve_Solo_Las_Activas_Del_Ambito_Ordenadas_FR004()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var mia = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var ajena = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        // Una propia que se da de baja, y una de la otra cuenta que nunca tiene que aparecer.
        var apagada = await CrearAsync(mia, "Gimnasio", "gasto");
        using (var baja = await mia.Cliente.DeleteAsync(
            new Uri($"/api/categorias/{apagada}", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        }

        var deLaOtra = await CrearAsync(ajena, "Privada", "gasto");

        var catalogo = await CatalogoAsync(mia);

        // Ni la dada de baja...
        Assert.DoesNotContain(catalogo, c => c.Id == apagada);

        // ...ni la de la otra cuenta.
        Assert.DoesNotContain(catalogo, c => c.Id == deLaOtra);
        Assert.DoesNotContain(catalogo, c => c.Nombre == "Privada");

        // Quedan las diez del alta, y en el orden del contrato.
        Assert.Equal(10, catalogo.Count);
        Assert.Equal(
            catalogo.OrderBy(c => c.Tipo == "gasto" ? 0 : 1).ThenBy(c => c.Id).Select(c => c.Id),
            catalogo.Select(c => c.Id));
    }

    /// <summary>Crea una categoría por la API y devuelve su identificador.</summary>
    private static async Task<int> CrearAsync(CuentaDePrueba cuenta, string nombre, string tipo)
    {
        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/categorias", UriKind.Relative), new { nombre, tipo });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    /// <summary>El catálogo que la API le ofrece a esa cuenta.</summary>
    private static async Task<List<(int Id, string Nombre, string Tipo)>> CatalogoAsync(
        CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/categorias", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return [.. json.RootElement.EnumerateArray().Select(c => (
            c.GetProperty("id").GetInt32(),
            c.GetProperty("nombre").GetString()!,
            c.GetProperty("tipo").GetString()!))];
    }

    [Fact]
    public async Task El_Tipo_Viaja_Como_Cadena_Y_No_Como_Numero_AC10()
    {
        Assert.NotNull(_baseDeDatos);
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

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
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

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

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Eliminar un movimiento propio (RF-15, historia 2 del ticket FEAT-001b).
///
/// La eliminación es la única transición irreversible del modelo: no hay baja lógica y no hay
/// deshacer (D-09). Por eso los escenarios de acá no comprueban sólo que la operación responda
/// bien, sino que **no haya tocado lo que no era suyo** — un borrado que se lleva puesto un
/// movimiento ajeno no se puede arreglar después.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class EliminacionDeMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 15);
    private static readonly DateOnly Fecha = new(2026, 8, 10);

    private const int Comida = 1;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-08 (PRD AC-21, mitad del listado): el dueño elimina su movimiento y deja de aparecer.
    ///
    /// Se siembran **dos** y se borra uno: con uno solo, un endpoint que vaciara la tabla entera
    /// pasaría en verde. Lo que queda tiene que ser exactamente el otro, comprobado por
    /// identificador.
    /// </summary>
    [Fact]
    public async Task El_Dueno_Elimina_Su_Movimiento_Y_Deja_De_Aparecer_AC08()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var aBorrar = await RegistrarAsync(cuenta, 100m);
        var aConservar = await RegistrarAsync(cuenta, 200m);

        var borrado = await CrudoAsync(cuenta, HttpMethod.Delete, $"/api/movimientos/{aBorrar}");
        Assert.Equal(HttpStatusCode.NoContent, borrado.Estado);
        Assert.Empty(borrado.Cuerpo);

        Assert.Equal([aConservar], await IdsDelListadoAsync(cuenta));

        // Y tampoco se alcanza por su ruta individual.
        var consulta = await CrudoAsync(cuenta, HttpMethod.Get, $"/api/movimientos/{aBorrar}");
        Assert.Equal(HttpStatusCode.NotFound, consulta.Estado);
    }

    /// <summary>
    /// AC-09 (deuda de 004, AC-05 del PRD): eliminar un movimiento ajeno responde igual que
    /// eliminar uno inexistente, **y el movimiento de la otra cuenta sigue en pie**.
    ///
    /// Las dos mitades hacen falta. La primera es la que impide averiguar qué identificadores
    /// existen; la segunda es la que distingue "respondió bien" de "no tocó nada", que en una
    /// operación irreversible no es un matiz.
    ///
    /// Acá el identificador inexistente sí es uno que **existió y se borró**, que se parece más al
    /// caso real que un número arbitrario. La suite de edición no puede usarlo porque no tiene el
    /// `DELETE` a mano; ésta sí.
    /// </summary>
    [Fact]
    public async Task Eliminar_Un_Movimiento_Ajeno_No_Lo_Toca_Y_Responde_Como_Inexistente_AC09()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var a = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var b = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        Assert.NotEqual(a.Id, b.Id);

        var deB = await RegistrarAsync(b, 500m);
        var antesDeB = await ListadoAsync(b);
        Assert.NotEmpty(antesDeB);

        // Un id que existió y ya no: A registra uno propio y lo borra.
        var borrado = await RegistrarAsync(a, 1m);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await CrudoAsync(a, HttpMethod.Delete, $"/api/movimientos/{borrado}")).Estado);

        var ajeno = await CrudoAsync(a, HttpMethod.Delete, $"/api/movimientos/{deB}");
        var fantasma = await CrudoAsync(a, HttpMethod.Delete, $"/api/movimientos/{borrado}");

        RespuestasIndistinguibles.Exigir(
            ajeno, fantasma, $"DELETE sobre ajeno ({deB}) y sobre uno ya borrado ({borrado})");

        // El movimiento de B sigue intacto, campo por campo.
        Assert.Equal(
            JsonSerializer.Serialize(antesDeB),
            JsonSerializer.Serialize(await ListadoAsync(b)));
    }

    /// <summary>
    /// AC-10: eliminar dos veces el mismo movimiento da `204` y después `404`, sin error inesperado.
    ///
    /// La segunda respuesta **no** es `204`. Sería más idempotente, pero convertiría al `DELETE` en
    /// el único endpoint que responde distinto que los otros dos ante lo inexistente, y esa
    /// asimetría es observable: el `404` de la consulta y el de la edición quedarían solos
    /// delatando. La uniformidad vale más que la idempotencia acá (contrato de movimientos).
    /// </summary>
    [Fact]
    public async Task Eliminar_Dos_Veces_Da_204_Y_Despues_404_AC10()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var id = await RegistrarAsync(cuenta, 300m);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await CrudoAsync(cuenta, HttpMethod.Delete, $"/api/movimientos/{id}")).Estado);

        var segunda = await CrudoAsync(cuenta, HttpMethod.Delete, $"/api/movimientos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, segunda.Estado);

        // Y la respuesta es la misma que la de un id que nunca existió: borrar dos veces no puede
        // ser una forma de averiguar que ahí hubo algo.
        var nuncaExistio = await CrudoAsync(cuenta, HttpMethod.Delete, $"/api/movimientos/{id + 1_000_000}");
        RespuestasIndistinguibles.Exigir(
            segunda, nuncaExistio, $"DELETE repetido ({id}) contra uno que nunca existió");
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private static async Task<RespuestaObservable> CrudoAsync(
        CuentaDePrueba cuenta, HttpMethod verbo, string ruta)
    {
        using var peticion = new HttpRequestMessage(verbo, new Uri(ruta, UriKind.Relative));
        using var respuesta = await cuenta.Cliente.SendAsync(peticion);

        return new RespuestaObservable(
            respuesta.StatusCode,
            await respuesta.Content.ReadAsStringAsync(),
            respuesta.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<long> RegistrarAsync(CuentaDePrueba cuenta, decimal monto)
    {
        var fecha = Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto, categoriaId = Comida, fecha });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt64();
    }

    private static async Task<List<JsonElement>> ListadoAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/movimientos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return [.. json.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    private static async Task<List<long>> IdsDelListadoAsync(CuentaDePrueba cuenta) =>
        [.. (await ListadoAsync(cuenta)).Select(m => m.GetProperty("id").GetInt64())];
}

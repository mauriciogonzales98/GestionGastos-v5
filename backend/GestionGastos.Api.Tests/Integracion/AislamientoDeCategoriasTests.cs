using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **Ninguna cuenta ve ni toca las categorías propias de otra** (FR-012, FR-013, FR-014, FR-021,
/// NFR-01, SC-009).
///
/// Los casos sueltos ya están repartidos por historia en `CategoriasPropiasTests`. Éste los reúne
/// para que la cobertura del 100 % que pide NFR-01 se pueda **afirmar mirando un archivo**, en vez
/// de reconstruirla juntando tests de tres suites. Es la misma forma que ya tiene
/// `AislamientoEntreCuentasTests` para movimientos.
///
/// La duplicación es a propósito y no es la clase de duplicación que hay que evitar: si algún día
/// se reorganizan los tests por historia, esta lista sigue siendo la que responde "¿los cuatro
/// endpoints están cubiertos?".
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class AislamientoDeCategoriasTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 24);

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// **Los cuatro endpoints, en un solo escenario.**
    ///
    /// Se arma una categoría de una cuenta y se la ataca desde otra por las cuatro puertas:
    ///
    /// · `GET` — no aparece en el catálogo ajeno (FR-012).
    /// · `POST` — el mismo nombre se acepta en la otra cuenta, y crea una fila distinta (AC-08).
    ///   Es el caso que un aislamiento demasiado celoso rompería: aislar no es prohibir.
    /// · `PUT` — `404`, indistinguible de un id inexistente (FR-013).
    /// · `DELETE` — `404`, igual, y la categoría sigue viva para su dueña.
    /// </summary>
    [Fact]
    public async Task Ninguna_Cuenta_Alcanza_Las_Categorias_De_Otra_Por_Ninguno_De_Los_Cuatro_Endpoints()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var duena = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var intrusa = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var ajena = await CrearCategoriaAsync(duena, "Gimnasio");

        // GET
        Assert.DoesNotContain(await CatalogoAsync(intrusa), c => c.Id == ajena);
        Assert.DoesNotContain(await CatalogoAsync(intrusa), c => c.Nombre == "Gimnasio");

        // POST — el mismo nombre en la otra cuenta se acepta y es OTRA fila.
        var propia = await CrearCategoriaAsync(intrusa, "Gimnasio");
        Assert.NotEqual(ajena, propia);

        // PUT y DELETE — indistinguibles de un identificador inexistente.
        using (var renombre = await RenombrarAsync(intrusa, ajena, "Mío ahora"))
        using (var fantasmaPut = await RenombrarAsync(intrusa, 999_999, "Mío ahora"))
        {
            RespuestasIndistinguibles.Exigir(
                await ObservarAsync(renombre), await ObservarAsync(fantasmaPut), "PUT sobre categoría ajena");
        }

        using (var baja = await BajaAsync(intrusa, ajena))
        using (var fantasmaDelete = await BajaAsync(intrusa, 999_999))
        {
            RespuestasIndistinguibles.Exigir(
                await ObservarAsync(baja), await ObservarAsync(fantasmaDelete), "DELETE sobre categoría ajena");
        }

        // Y la categoría de la dueña quedó intacta: ni renombrada ni dada de baja.
        var suya = (await CatalogoAsync(duena)).Single(c => c.Id == ajena);
        Assert.Equal("Gimnasio", suya.Nombre);
        Assert.True(suya.EsPropia);
    }

    /// <summary>
    /// FR-021 y SC-009: una cuenta **no** puede registrar ni editar un movimiento apuntando a una
    /// categoría propia de otra cuenta, y el rechazo no dice si esa categoría existe.
    ///
    /// **Esto ya funciona desde FEAT-001b**, y `ValidacionMovimientoTests` lo defiende. Pero ese
    /// test arma la categoría ajena **a mano**, insertándola en la base, porque cuando se escribió
    /// no existían las propias. Esta feature es la que las vuelve reales, así que es el ticket que
    /// introduce el riesgo de verdad y tiene que ser el que lo mire: acá la categoría ajena la crea
    /// la otra cuenta **por el endpoint**, con un id que el autoincremental eligió, igual que
    /// pasaría en producción.
    ///
    /// El rechazo es el mismo que el de una categoría inexistente, y por el mismo motivo que el 404
    /// uniforme: distinguirlos confirmaría que ese id existe, y los ids son contiguos.
    /// </summary>
    [Fact]
    public async Task Un_Movimiento_No_Puede_Apuntar_A_Una_Categoria_Ajena_FR021_SC009()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var duena = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var intrusa = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var ajena = await CrearCategoriaAsync(duena, "Gimnasio");

        // El alta apuntando a la categoría ajena se rechaza igual que apuntando a una inexistente.
        using (var conLaAjena = await RegistrarAsync(intrusa, ajena, 100m))
        using (var conUnaInexistente = await RegistrarAsync(intrusa, 999_999, 100m))
        {
            Assert.Equal(HttpStatusCode.BadRequest, conLaAjena.StatusCode);
            Assert.Equal(
                RespuestasIndistinguibles.SinTraza(await conUnaInexistente.Content.ReadAsStringAsync()),
                RespuestasIndistinguibles.SinTraza(await conLaAjena.Content.ReadAsStringAsync()));
        }

        // La edición, igual. Se necesita un movimiento propio y válido para poder intentarlo.
        long movimiento;
        using (var propio = await RegistrarAsync(intrusa, categoriaId: 1, monto: 100m))
        {
            Assert.Equal(HttpStatusCode.Created, propio.StatusCode);
            using var json = JsonDocument.Parse(await propio.Content.ReadAsStringAsync());
            movimiento = json.RootElement.GetProperty("id").GetInt64();
        }

        using (var aLaAjena = await EditarAsync(intrusa, movimiento, ajena))
        using (var aUnaInexistente = await EditarAsync(intrusa, movimiento, 999_999))
        {
            Assert.Equal(HttpStatusCode.BadRequest, aLaAjena.StatusCode);
            Assert.Equal(
                RespuestasIndistinguibles.SinTraza(await aUnaInexistente.Content.ReadAsStringAsync()),
                RespuestasIndistinguibles.SinTraza(await aLaAjena.Content.ReadAsStringAsync()));
        }

        // Y no se movió.
        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Equal(1, (await contexto.Movimientos.FindAsync(movimiento))!.CategoriaId);
    }

    private static async Task<int> CrearCategoriaAsync(CuentaDePrueba cuenta, string nombre)
    {
        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/categorias", UriKind.Relative), new { nombre, tipo = "gasto" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> RenombrarAsync(CuentaDePrueba cuenta, int id, string nombre) =>
        cuenta.Cliente.PutAsJsonAsync(new Uri($"/api/categorias/{id}", UriKind.Relative), new { nombre });

    private static Task<HttpResponseMessage> BajaAsync(CuentaDePrueba cuenta, int id) =>
        cuenta.Cliente.DeleteAsync(new Uri($"/api/categorias/{id}", UriKind.Relative));

    private static Task<HttpResponseMessage> RegistrarAsync(
        CuentaDePrueba cuenta, int categoriaId, decimal monto) =>
        cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new
            {
                tipo = "gasto",
                monto,
                categoriaId,
                fecha = Hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });

    private static Task<HttpResponseMessage> EditarAsync(
        CuentaDePrueba cuenta, long id, int categoriaId) =>
        cuenta.Cliente.PutAsJsonAsync(
            new Uri($"/api/movimientos/{id}", UriKind.Relative),
            new
            {
                tipo = "gasto",
                monto = 100m,
                categoriaId,
                fecha = Hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });

    private static async Task<IReadOnlyList<CategoriaVista>> CatalogoAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        return await respuesta.Content.ReadFromJsonAsync<List<CategoriaVista>>()
            ?? throw new InvalidOperationException("El catálogo vino nulo.");
    }

    private static async Task<RespuestaObservable> ObservarAsync(HttpResponseMessage respuesta) => new(
        respuesta.StatusCode,
        await respuesta.Content.ReadAsStringAsync(),
        respuesta.Content.Headers.ContentType?.ToString());
}

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Consultar y modificar un movimiento propio (RF-14, historia 1 del ticket FEAT-001b).
///
/// A diferencia de <see cref="AislamientoEntreCuentasTests"/>, acá **nada de esto funcionaba
/// antes**: los endpoints no existen, así que cada test de este archivo nace en rojo con un `404`
/// sin que haya que desarmar nada. Un test de acá que pase en verde la primera vez que se corre
/// está mal escrito.
///
/// Los escenarios cruzados usan dos cuentas de verdad por el mismo motivo que la suite de
/// aislamiento: con una sola, todo lo que este archivo tiene que impedir pasa en verde.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class EdicionDeMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    /// <summary>
    /// El reloj queda clavado acá. El listado recorta al mes en curso del servidor y ese recorte no
    /// es un parámetro: sin reloj fijo, un escenario sembrado "hoy" cruza el fin de mes y falla el
    /// día 1 sin que nada haya cambiado (Principio IV).
    /// </summary>
    private static readonly DateOnly Hoy = new(2026, 8, 15);

    private static readonly DateOnly FechaOriginal = new(2026, 8, 10);

    /// <summary>Otro día del MISMO mes: cambiar la fecha no tiene que sacarlo del listado acá.</summary>
    private static readonly DateOnly FechaNueva = new(2026, 8, 20);

    /// <summary>Un mes distinto, para el caso en que la edición sí lo saca del período.</summary>
    private static readonly DateOnly FechaDeOtroMes = new(2026, 5, 3);

    private const int Comida = 1;
    private const int Transporte = 2;
    private const int Sueldo = 8;

    private const decimal MontoOriginal = 1500m;
    private const decimal MontoCorregido = 15000m;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-03: el dueño consulta su movimiento por identificador y recibe la **misma forma** que
    /// devuelven el alta y el listado.
    ///
    /// Se comparan los dos JSON campo por campo en vez de mirar unos pocos: una forma nueva para el
    /// mismo concepto es exactamente lo que el contrato existe para evitar.
    /// </summary>
    [Fact]
    public async Task El_Dueno_Consulta_Su_Movimiento_Por_Id_Y_Recibe_La_Misma_Forma_AC03()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var id = await RegistrarAsync(cuenta, MontoOriginal);

        var (estado, cuerpo, _) = await CrudoAsync(cuenta, HttpMethod.Get, $"/api/movimientos/{id}");
        Assert.Equal(HttpStatusCode.OK, estado);

        var delListado = (await ListadoAsync(cuenta)).Single();
        using var individual = JsonDocument.Parse(cuerpo);

        Assert.Equal(
            JsonSerializer.Serialize(delListado),
            JsonSerializer.Serialize(individual.RootElement));
    }

    /// <summary>
    /// AC-01 (PRD AC-19, mitad del listado): corregir el monto y que **el listado** lo refleje.
    ///
    /// Se comprueba sobre el listado y no sólo sobre la respuesta del PUT: la respuesta puede estar
    /// bien armada y la fila haber quedado sin guardar, y ése es justo el fallo que hay que
    /// atrapar.
    /// </summary>
    [Fact]
    public async Task Corregir_El_Monto_Se_Ve_En_El_Listado_AC01()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var id = await RegistrarAsync(cuenta, MontoOriginal);

        var (estado, _, _) = await EditarAsync(cuenta, id, MontoCorregido, Comida, FechaOriginal);
        Assert.Equal(HttpStatusCode.OK, estado);

        var enElListado = (await ListadoAsync(cuenta)).Single();
        Assert.Equal(MontoCorregido, enElListado.GetProperty("monto").GetDecimal());

        // Y el valor viejo no quedó en ningún lado.
        Assert.DoesNotContain(
            await ListadoAsync(cuenta),
            m => m.GetProperty("monto").GetDecimal() == MontoOriginal);
    }

    /// <summary>
    /// AC-02 (PRD AC-20, mitad del listado): cambiar categoría y fecha a la vez.
    ///
    /// Dos casos, porque el AC tiene dos mitades: dentro del período consultado el movimiento
    /// aparece con sus valores nuevos, y fuera de él **desaparece del listado**. La segunda mitad es
    /// la que distingue "se guardó" de "se guardó donde corresponde".
    /// </summary>
    [Fact]
    public async Task Cambiar_Categoria_Y_Fecha_Se_Ve_En_El_Listado_AC02()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var id = await RegistrarAsync(cuenta, MontoOriginal);

        // Dentro del mes en curso: cambia y sigue viéndose.
        await EditarAsync(cuenta, id, MontoOriginal, Transporte, FechaNueva);

        var enElListado = (await ListadoAsync(cuenta)).Single();
        Assert.Equal(Transporte, enElListado.GetProperty("categoriaId").GetInt32());
        Assert.Equal("Transporte", enElListado.GetProperty("categoriaNombre").GetString());
        Assert.Equal(
            FechaNueva.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            enElListado.GetProperty("fecha").GetString());

        // Fuera del mes en curso: deja de verse, y no porque se haya borrado.
        await EditarAsync(cuenta, id, MontoOriginal, Transporte, FechaDeOtroMes);

        Assert.Empty(await ListadoAsync(cuenta));

        var (estado, _, _) = await CrudoAsync(cuenta, HttpMethod.Get, $"/api/movimientos/{id}");
        Assert.Equal(HttpStatusCode.OK, estado);
    }

    /// <summary>
    /// AC-04 (INV-01; deuda de 004, AC-07 del PRD): editar **no** cambia el propietario.
    ///
    /// Se manda `usuarioId` de la otra cuenta en el cuerpo. El contrato de la edición no tiene ese
    /// campo, así que hoy se descarta al deserializar y este test pasaría aunque el endpoint no
    /// hiciera nada al respecto. Se manda igual: el día que `MovimientoEditadoDto` gane un campo,
    /// éste es el test que se entera.
    /// </summary>
    [Fact]
    public async Task Editar_No_Cambia_El_Propietario_AC04()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var a = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var b = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        Assert.NotEqual(a.Id, b.Id);

        var deA = await RegistrarAsync(a, MontoOriginal);

        var (estado, _, _) = await EditarAsync(
            a, deA, MontoCorregido, Comida, FechaOriginal, propietarioEnElCuerpo: b.Id);
        Assert.Equal(HttpStatusCode.OK, estado);

        // Sigue siendo de A, con el valor nuevo...
        Assert.Equal(MontoCorregido, (await ListadoAsync(a)).Single().GetProperty("monto").GetDecimal());

        // ...y no se mudó a B.
        Assert.Empty(await ListadoAsync(b));
    }

    /// <summary>
    /// AC-05 y AC-06 (deuda de 004, AC-03 y AC-04 del PRD): un movimiento ajeno responde
    /// **exactamente igual** que uno inexistente, y no se toca.
    ///
    /// Se comparan las dos respuestas **entre sí**, no cada una contra un `404` esperado. Afirmar
    /// `404` en las dos pasa en verde aunque los cuerpos difieran —"no existe" contra "no es
    /// tuyo"— y el segundo confirma que ese identificador existe. Como los identificadores son
    /// autoincrementales y contiguos, esa diferencia permite contar los movimientos de otra cuenta
    /// sin ver ninguno (D-03).
    /// </summary>
    [Fact]
    public async Task Un_Movimiento_Ajeno_Responde_Igual_Que_Uno_Inexistente_AC05_AC06()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var a = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var b = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        Assert.NotEqual(a.Id, b.Id);

        var deB = await RegistrarAsync(b, MontoOriginal);
        var antesDeB = await ListadoAsync(b);

        var inexistente = await IdInexistenteAsync(a);

        // Consultar.
        await IndistinguiblesAsync(a, HttpMethod.Get, deB, inexistente);

        // Modificar.
        var ajeno = await EditarCrudoAsync(a, deB, MontoCorregido, Comida, FechaNueva);
        var fantasma = await EditarCrudoAsync(a, inexistente, MontoCorregido, Comida, FechaNueva);
        AssertIndistinguibles(ajeno, fantasma, $"PUT sobre ajeno ({deB}) e inexistente ({inexistente})");

        // Y el movimiento de B quedó intacto: campo por campo, no sólo "sigue estando".
        Assert.Equal(
            JsonSerializer.Serialize(antesDeB),
            JsonSerializer.Serialize(await ListadoAsync(b)));
    }

    /// <summary>
    /// AC-07 (INV-06): una edición inválida se rechaza con los errores por campo y deja el
    /// movimiento sin cambios.
    ///
    /// El segundo caso es de **orden**, y es el que importa: un movimiento **ajeno** con un cuerpo
    /// inválido responde `404` y no `400`. Un `400` confirmaría que se llegó a mirar el cuerpo, y
    /// eso sólo pasa si el movimiento existe.
    /// </summary>
    [Fact]
    public async Task Una_Edicion_Invalida_Se_Rechaza_Y_No_Cambia_Nada_AC07()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var a = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var b = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deA = await RegistrarAsync(a, MontoOriginal);
        var deB = await RegistrarAsync(b, MontoOriginal);

        // Monto negativo: 400 con la clave del campo, igual que el alta.
        var (estado, cuerpo, _) = await EditarCrudoAsync(a, deA, -5m, Comida, FechaOriginal);
        Assert.Equal(HttpStatusCode.BadRequest, estado);
        using (var json = JsonDocument.Parse(cuerpo))
        {
            Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("monto", out _));
        }

        // El movimiento no se movió.
        Assert.Equal(MontoOriginal, (await ListadoAsync(a)).Single().GetProperty("monto").GetDecimal());

        // Un par tipo/categoría incoherente tampoco pasa: "gasto" con una categoría de ingreso
        // (INV-03). El tipo se manda forzado, porque derivarlo de la categoría armaría un par
        // válido y el test no probaría nada.
        var (porTipo, _, _) = await EditarCrudoAsync(
            a, deA, MontoOriginal, Sueldo, FechaOriginal, tipoForzado: "gasto");
        Assert.Equal(HttpStatusCode.BadRequest, porTipo);

        // Y lo ajeno con cuerpo inválido responde 404, NO 400: el orden es buscar y después validar.
        var (ajeno, _, _) = await EditarCrudoAsync(a, deB, -5m, Comida, FechaOriginal);
        Assert.Equal(HttpStatusCode.NotFound, ajeno);
    }

    /// <summary>
    /// Dos peticiones **idénticas** al mismo identificador inexistente devuelven cuerpos que
    /// difieren, y difieren **sólo** en `traceId`.
    ///
    /// Es lo que justifica normalizarlo en <see cref="SinTraza"/>. Sin este test, ignorar un campo
    /// al comparar sería una decisión sin respaldo: mañana alguien agrega otro campo variable, la
    /// normalización lo tapa, y la comparación de indistinguibilidad deja de ver lo que importa.
    /// </summary>
    [Fact]
    public async Task El_TraceId_Es_Volatil_Y_Por_Eso_Se_Ignora_Al_Comparar()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var inexistente = await IdInexistenteAsync(cuenta);

        var una = await CrudoAsync(cuenta, HttpMethod.Get, $"/api/movimientos/{inexistente}");
        var otra = await CrudoAsync(cuenta, HttpMethod.Get, $"/api/movimientos/{inexistente}");

        Assert.NotEqual(una.Cuerpo, otra.Cuerpo);
        Assert.Equal(SinTraza(una.Cuerpo), SinTraza(otra.Cuerpo));
    }

    // ---- Helpers -------------------------------------------------------------------------------

    /// <summary>
    /// Exige que dos identificadores respondan **indistinguible** al mismo verbo: mismo código,
    /// mismo cuerpo y mismo <c>Content-Type</c>.
    /// </summary>
    private static async Task IndistinguiblesAsync(
        CuentaDePrueba cuenta, HttpMethod verbo, long ajeno, long inexistente)
    {
        var unaAjena = await CrudoAsync(cuenta, verbo, $"/api/movimientos/{ajeno}");
        var unaFantasma = await CrudoAsync(cuenta, verbo, $"/api/movimientos/{inexistente}");

        AssertIndistinguibles(
            unaAjena, unaFantasma, $"{verbo} sobre ajeno ({ajeno}) e inexistente ({inexistente})");
    }

    private static void AssertIndistinguibles(
        (HttpStatusCode Estado, string Cuerpo, string? Tipo) ajena,
        (HttpStatusCode Estado, string Cuerpo, string? Tipo) fantasma,
        string contexto)
    {
        Assert.Equal(HttpStatusCode.NotFound, ajena.Estado);

        Assert.True(
            ajena.Estado == fantasma.Estado
                && string.Equals(SinTraza(ajena.Cuerpo), SinTraza(fantasma.Cuerpo), StringComparison.Ordinal)
                && string.Equals(ajena.Tipo, fantasma.Tipo, StringComparison.Ordinal),
            $"Un movimiento ajeno se distingue de uno inexistente ({contexto}).\n" +
            $"  ajeno       -> {(int)ajena.Estado} {ajena.Tipo} {ajena.Cuerpo}\n" +
            $"  inexistente -> {(int)fantasma.Estado} {fantasma.Tipo} {fantasma.Cuerpo}\n\n" +
            "Cualquier diferencia observable confirma que ese identificador existe, y como son " +
            "contiguos eso permite contar los movimientos de otra cuenta sin ver ninguno.");
    }

    /// <summary>
    /// El cuerpo sin su <c>traceId</c>.
    ///
    /// `ProblemDetails` incluye un identificador de traza **por petición**, así que dos respuestas
    /// nunca son iguales byte a byte — ni siquiera dos peticiones idénticas al mismo identificador.
    /// Compararlo sería exigir algo imposible; ignorarlo sin más sería aflojar el test.
    ///
    /// La propiedad que hay que verificar no es "los cuerpos son idénticos" sino "nada que dependa
    /// de la existencia difiere", y `traceId` no depende de nada: es aleatorio. Que lo sea está
    /// comprobado en <see cref="El_TraceId_Es_Volatil_Y_Por_Eso_Se_Ignora_Al_Comparar"/>, para que
    /// esta normalización no se apoye en una suposición.
    /// </summary>
    private static string SinTraza(string cuerpo) =>
        Regex.Replace(cuerpo, "\"traceId\"\\s*:\\s*\"[^\"]*\"", "\"traceId\":\"<volatil>\"");

    /// <summary>
    /// Un identificador que no le corresponde a ningún movimiento.
    ///
    /// Se toma bien por encima del último que existe, en vez de registrar uno y borrarlo. Un id que
    /// existió se parece más al caso real, pero borrarlo exige el `DELETE`, que es de la historia 2
    /// — y esta historia tiene que poder entregarse sola. El caso del identificador **borrado** lo
    /// cubre <see cref="EliminacionDeMovimientoTests"/>, que sí lo tiene a mano.
    /// </summary>
    private static async Task<long> IdInexistenteAsync(CuentaDePrueba cuenta)
    {
        var propios = await ListadoAsync(cuenta);
        var ultimo = propios.Count == 0 ? 0 : propios.Max(m => m.GetProperty("id").GetInt64());
        return ultimo + 1_000_000;
    }

    private static async Task<(HttpStatusCode Estado, string Cuerpo, string? Tipo)> CrudoAsync(
        CuentaDePrueba cuenta, HttpMethod verbo, string ruta)
    {
        using var peticion = new HttpRequestMessage(verbo, new Uri(ruta, UriKind.Relative));
        using var respuesta = await cuenta.Cliente.SendAsync(peticion);

        return (
            respuesta.StatusCode,
            await respuesta.Content.ReadAsStringAsync(),
            respuesta.Content.Headers.ContentType?.MediaType);
    }

    private static Task<(HttpStatusCode Estado, string Cuerpo, string? Tipo)> EditarAsync(
        CuentaDePrueba cuenta, long id, decimal monto, int categoriaId, DateOnly fecha,
        long? propietarioEnElCuerpo = null) =>
        EditarCrudoAsync(cuenta, id, monto, categoriaId, fecha, propietarioEnElCuerpo);

    /// <param name="tipoForzado">
    /// Para mandar un par tipo/categoría **incoherente** a propósito. Sin esto, derivar el tipo de
    /// la categoría hace que el test no pueda expresar el caso que quiere probar: mandaría siempre
    /// un par válido y pasaría en verde sin verificar la regla.
    /// </param>
    private static async Task<(HttpStatusCode Estado, string Cuerpo, string? Tipo)> EditarCrudoAsync(
        CuentaDePrueba cuenta, long id, decimal monto, int categoriaId, DateOnly fecha,
        long? propietarioEnElCuerpo = null, string? tipoForzado = null)
    {
        var tipo = tipoForzado ?? (categoriaId == Sueldo ? "ingreso" : "gasto");
        var texto = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        object cuerpo = propietarioEnElCuerpo is { } ajeno
            ? new { tipo, monto, categoriaId, fecha = texto, usuarioId = ajeno }
            : new { tipo, monto, categoriaId, fecha = texto };

        using var respuesta = await cuenta.Cliente.PutAsJsonAsync(
            new Uri($"/api/movimientos/{id}", UriKind.Relative), cuerpo);

        return (
            respuesta.StatusCode,
            await respuesta.Content.ReadAsStringAsync(),
            respuesta.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<long> RegistrarAsync(CuentaDePrueba cuenta, decimal monto)
    {
        var fecha = FechaOriginal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

        // Clone(): los JsonElement mueren con su JsonDocument, que se libera al salir de acá.
        return [.. json.RootElement.EnumerateArray().Select(e => e.Clone())];
    }
}

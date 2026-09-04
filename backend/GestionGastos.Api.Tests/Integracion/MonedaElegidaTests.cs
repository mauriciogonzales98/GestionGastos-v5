using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **La moneda deja de ser una decisión del servidor y pasa a ser una elección del usuario**
/// (FR-001 a FR-006 de la feature 009).
///
/// Hasta acá todo movimiento se registraba en la predeterminada del catálogo y el cliente no tenía
/// forma de decir otra cosa: `NuevoMovimientoDto` no llevaba moneda. Eso hacía que
/// **`PRD:FR-04`—rechazar una moneda fuera del catálogo— no se pudiera probar**, porque no existía
/// ninguna entrada que validar; quedó anotado como la deuda **D8-01** de la feature 008, con este
/// ticket como el que la cubre. `Un_MonedaId_Fuera_Del_Catalogo_Se_Rechaza_AC11` es esa deuda
/// saldándose.
///
/// **Ningún caso de este archivo escribe un número fijo sobre el tamaño del catálogo** (D-10). La
/// moneda con la que se ejercita se agrega con <see cref="CatalogoDeMonedas.ConLaMonedaAsync"/> y
/// lo que se afirma sobre el catálogo se compara contra el catálogo. `verificar-monedas.sh` corre
/// esta suite con una moneda de más puesta en la base, así que un literal acá pasa en la suite y
/// se rompe en la barrera.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class MonedaElegidaTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 9, 4);

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-01 y FR-001: un alta que elige una moneda queda registrada **en esa moneda**, y su monto
    /// suma en los totales de esa moneda y en los de ninguna otra.
    ///
    /// Las dos mitades importan. Que el movimiento vuelva con el código correcto prueba que la API
    /// usó el campo; que el resumen lo sume en la entrada correcta prueba que lo **guardó**, que no
    /// es lo mismo — una respuesta armada con la moneda pedida y una fila escrita con la
    /// predeterminada darían el mismo `201`.
    /// </summary>
    [Fact]
    public async Task El_Alta_Con_MonedaId_Queda_En_Esa_Moneda_AC01()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await CatalogoDeMonedas.ConLaMonedaAsync(_baseDeDatos, "XEL", async moneda =>
        {
            using var factoria = new FactoriaConReloj(Hoy);
            using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
            var cliente = cuenta.Cliente;

            using var creacion = await cliente.PostAsJsonAsync(
                new Uri("/api/movimientos", UriKind.Relative),
                new { tipo = "gasto", monto = 123.45m, categoriaId = 1, monedaId = moneda.Id, fecha = "2026-09-04" });

            Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);

            using var creado = JsonDocument.Parse(await creacion.Content.ReadAsStringAsync());
            Assert.Equal(moneda.Codigo, creado.RootElement.GetProperty("monedaCodigo").GetString());

            using var respuesta = await cliente.GetAsync(new Uri("/api/resumen", UriKind.Relative));
            using var resumen = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

            foreach (var entrada in resumen.RootElement.GetProperty("monedas").EnumerateArray())
            {
                var esperado = entrada.GetProperty("monedaCodigo").GetString() == moneda.Codigo
                    ? 123.45m
                    : 0m;

                Assert.Equal(esperado, entrada.GetProperty("totalGastado").GetDecimal());
            }
        });
    }

    /// <summary>
    /// AC-02, FR-002 y `PRD:NFR-01`: un alta **sin** `monedaId` cae en la predeterminada del
    /// catálogo.
    ///
    /// **Este caso pasa desde el primer intento, y es a propósito.** Es el único de la feature cuyo
    /// verde inicial es la respuesta correcta: lo que verifica no es una capacidad nueva sino que
    /// **no se rompió la que ya había**. Es `PRD:NFR-01` —quien opera en una sola moneda no agrega
    /// ni un paso— y es la compatibilidad hacia atrás del contrato: hasta esta feature el campo no
    /// existía, así que todo cliente que ya andaba tiene que seguir andando sin mandarlo.
    ///
    /// El error que atrapa es el más fácil de cometer al implementar: hacer `monedaId` obligatorio,
    /// que es lo que sale natural cuando se lo agrega al DTO y se lo valida.
    /// </summary>
    [Fact]
    public async Task El_Alta_Sin_MonedaId_Cae_En_La_Predeterminada_AC02()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        string predeterminada;
        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            // Sale del catálogo, no de un "ARS" escrito acá: que la predeterminada sea pesos es
            // una decisión de la semilla y puede cambiar; que el alta use LA predeterminada es la
            // regla, y es lo único que este test tiene que afirmar (D-10).
            predeterminada = await contexto.Monedas
                .Where(m => m.EsPredeterminada)
                .Select(m => m.Codigo)
                .SingleAsync();
        }

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var creacion = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 50m, categoriaId = 1, fecha = "2026-09-04" });

        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);

        using var creado = JsonDocument.Parse(await creacion.Content.ReadAsStringAsync());
        Assert.Equal(predeterminada, creado.RootElement.GetProperty("monedaCodigo").GetString());
    }

    /// <summary>
    /// AC-11 y FR-003 — **la deuda D8-01 de la feature 008 saldándose**.
    ///
    /// Aquella feature difirió `PRD:FR-04` con una razón exacta: la moneda no viajaba en ninguna
    /// petición, así que un test de "rechazar una moneda fuera del catálogo" tenía que inventarse
    /// primero la vía de entrada que decía comprobar. Esta feature abre esa vía, y acá está la
    /// comprobación.
    ///
    /// **No alcanza con el `400`.** Se verifica además que no quedó ningún movimiento: un servidor
    /// que valida después de escribir devuelve el mismo código de estado y deja la fila puesta.
    /// </summary>
    [Fact]
    public async Task Un_MonedaId_Fuera_Del_Catalogo_Se_Rechaza_AC11()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        short inexistente;
        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            // Uno más que el mayor del catálogo: no existe hoy y no puede existir por accidente
            // mañana. Un `9999` a mano funcionaría igual, pero deja de ser evidente por qué.
            inexistente = (short)(await contexto.Monedas.MaxAsync(m => m.Id) + 1);
        }

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 10m, categoriaId = 1, monedaId = inexistente, fecha = "2026-09-04" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var error = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var errores = error.RootElement.GetProperty("errors");

        // La clave es el nombre del campo de la petición: es lo que le permite al frontend poner el
        // mensaje al lado de su control en vez de volcar un texto suelto.
        Assert.True(
            errores.TryGetProperty("monedaId", out _),
            $"El rechazo tiene que venir bajo la clave `monedaId`. Vino: {errores}");

        using var listado = await cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));
        using var movimientos = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());

        Assert.Empty(movimientos.RootElement.EnumerateArray());
    }

    /// <summary>
    /// AC-03, AC-04, FR-004, FR-005 y FR-006: `GET /api/monedas` devuelve **una entrada por fila
    /// del catálogo**, con exactamente una predeterminada, y **una moneda agregada sólo como dato
    /// aparece**.
    ///
    /// La comparación es contra la tabla y no contra un número (D-10). Y el caso de la moneda
    /// agregada es el que **no puede pasar con una lista escrita en el código**: es `PRD:AC-04` del
    /// lado del servidor, la misma promesa que `verificar-monedas.sh` sostiene para el resumen.
    /// </summary>
    [Fact]
    public async Task El_Catalogo_Se_Expone_Entero_Y_Una_Moneda_Agregada_Aparece_AC03_AC04()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await CatalogoDeMonedas.ConLaMonedaAsync(_baseDeDatos, "XCT", async moneda =>
        {
            List<string> enLaTabla;
            await using (var contexto = _baseDeDatos.CrearContexto())
            {
                enLaTabla = await contexto.Monedas.OrderBy(m => m.Id).Select(m => m.Codigo).ToListAsync();
            }

            using var factoria = new FactoriaConReloj(Hoy);
            using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

            using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/monedas", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            var entradas = json.RootElement.EnumerateArray().ToList();

            var enLaRespuesta = entradas.Select(e => e.GetProperty("codigo").GetString()!).ToList();

            Assert.Equal(enLaTabla, enLaRespuesta);
            Assert.Contains(moneda.Codigo, enLaRespuesta);
            Assert.Single(entradas, e => e.GetProperty("esPredeterminada").GetBoolean());
        });
    }
}

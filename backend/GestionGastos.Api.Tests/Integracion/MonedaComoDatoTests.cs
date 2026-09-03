using System.Net;
using System.Net.Http.Json;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **Que sumar una moneda sea de verdad sólo un dato** (FR-001, `PRD:RF-32`).
///
/// El catálogo de monedas existe desde la migración `Inicial` y el resumen ya separa por moneda:
/// esta feature no construye nada de eso, lo **verifica**. La diferencia importa porque hasta acá
/// "se puede agregar una moneda sin tocar código" era una afirmación que nadie había ejecutado —
/// plausible leyendo el código, que es exactamente la clase de creencia que el Principio V de la
/// constitución existe para no aceptar.
///
/// `backend/verificar-monedas.sh` es la otra mitad: estos tests comprueban el **comportamiento**
/// —la moneda nueva aparece y se puede usar— y el script comprueba el **proceso** —que para eso no
/// hizo falta modificar ni recompilar nada—. Un test no puede sostener lo segundo: corre dentro de
/// un proceso que ya se compiló.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class MonedaComoDatoTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 24);

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Agrega una moneda al catálogo, corre lo que se le pida con ella puesta, y **la borra pase lo
    /// que pase**.
    ///
    /// El `finally` no es celo: si el cuerpo falla, la moneda queda igual, y entonces el rojo que
    /// alguien va a leer mañana es el del canario y no el del test que de verdad falló. Un test que
    /// ensucia al fallar convierte un rojo legible en dos ilegibles.
    ///
    /// **La limpieza va acá y no en `LimpiarCuentasAsync`.** Ahí borraría las dos monedas sembradas
    /// para toda la suite —que la migración siembra una sola vez y media suite da por dadas—, que es
    /// el mismo error que ese método ya evita en categorías filtrando por `usuario_id != null`.
    /// </summary>
    private async Task ConLaMonedaAsync(string codigo, Func<Moneda, Task> cuerpo)
    {
        var moneda = new Moneda
        {
            Codigo = codigo,
            Nombre = $"Moneda de prueba {codigo}",
            Simbolo = codigo,
            Decimales = 2,
            EsPredeterminada = false,
        };

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            contexto.Monedas.Add(moneda);
            await contexto.SaveChangesAsync();
        }

        try
        {
            await cuerpo(moneda);
        }
        finally
        {
            await using var contexto = _baseDeDatos.CrearContexto();

            // **Los movimientos primero.** `movimiento.moneda_id` es una clave foránea RESTRICT, así
            // que borrar la moneda con un movimiento apuntándola falla — y falla DENTRO del
            // `finally`, que es el peor lugar: la moneda queda, el rojo que se lee es el del canario
            // de la corrida siguiente, y la causa no aparece en ninguno de los dos. Es el mismo
            // orden que `LimpiarCuentasAsync` documenta para cuentas y categorías, por el mismo
            // motivo.
            await contexto.Movimientos.Where(m => m.Moneda!.Codigo == codigo).ExecuteDeleteAsync();
            await contexto.Monedas.Where(m => m.Codigo == codigo).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// AC-03 y FR-003: el catálogo migrado trae pesos y dólares, y **exactamente una** marcada como
    /// predeterminada.
    ///
    /// Cuenta las predeterminadas en vez de comprobar cuál es, y la diferencia no es menor: que sea
    /// `ARS` es una decisión de la semilla y puede cambiar; que haya **una sola** es la invariante
    /// que el alta da por cierta cuando hace `SingleAsync(m => m.EsPredeterminada)` (FR-004). Con
    /// dos, elegiría una sin criterio y podría cambiar entre reinicios; con cero, reventaría.
    /// </summary>
    [Fact]
    public async Task El_Catalogo_Tiene_Exactamente_Una_Predeterminada_AC03()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        Assert.Equal(1, await contexto.Monedas.CountAsync(m => m.EsPredeterminada));
    }

    /// <summary>
    /// AC-01 y FR-002: una moneda agregada al catálogo **sólo como dato** aparece en el resumen,
    /// con sus tres totales y su desglose en cero, y al final de la lista.
    ///
    /// Es la mitad observable de `PRD:RF-32`. La otra —que para esto no hizo falta tocar ni
    /// recompilar nada— la sostiene `verificar-monedas.sh`, porque un test no puede: corre dentro de
    /// un proceso ya compilado.
    ///
    /// **Que aparezca en cero y no que falte es lo que esta feature decidió conservar** contra
    /// `PRD:AC-07`: el resumen informa sobre todas las monedas del catálogo, no sobre las que
    /// tuvieron actividad (FR-009, `006:AC-31`). Y es justamente esa decisión la que hace que
    /// agregar una moneda se note sin que nadie registre nada con ella.
    /// </summary>
    [Fact]
    public async Task Una_Moneda_Agregada_Como_Dato_Aparece_En_El_Resumen_AC01()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var antes = await MonedasDelResumenAsync(cuenta);

        await ConLaMonedaAsync("EUR", async nueva =>
        {
            var despues = await MonedasDelResumenAsync(cuenta);

            Assert.Equal(antes.Count + 1, despues.Count);

            var euro = Assert.Single(despues, m => m.MonedaCodigo == "EUR");
            Assert.Equal(nueva.Id, euro.MonedaId);
            Assert.Equal(0m, euro.TotalIngresado);
            Assert.Equal(0m, euro.TotalGastado);
            Assert.Equal(0m, euro.Balance);
            Assert.Empty(euro.GastosPorCategoria);

            // Al final, porque el orden es por id y es parte del contrato: sin él, la pantalla las
            // reordenaría sola entre dos pedidos idénticos.
            Assert.Equal("EUR", despues[^1].MonedaCodigo);
        });
    }

    /// <summary>
    /// AC-02, FR-001, FR-010 y SC-001: se registra un movimiento **en la moneda recién agregada**, y
    /// suma en sus totales y en los de ninguna otra.
    ///
    /// **Sin selector todavía —eso es el ticket 4b— la única vía que el sistema permite es la
    /// predeterminada** (FR-010): el alta la lee del catálogo. Así que mover la marca es lo que
    /// convierte a la moneda nueva en usable, y mover la marca también es administrar el catálogo
    /// como dato. No es un rodeo para esquivar la falta del selector: es `PRD:RF-32` ejercido.
    ///
    /// **Van dos sentencias, apagar y después prender, y el orden no es indiferente.**
    /// `ux_moneda_unica_predeterminada` es un índice único sobre una columna generada que vale 1
    /// para la predeterminada y NULL para el resto. Un `UPDATE` que apague una y prenda otra en la
    /// misma sentencia puede violarlo transitoriamente, según el orden en que el motor toque las
    /// filas. Entre las dos hay un instante sin ninguna predeterminada, en el que un alta fallaría
    /// con `SingleAsync` — acá no importa, pero que nadie copie el patrón a producción sin pensarlo.
    /// </summary>
    [Fact]
    public async Task Un_Movimiento_Se_Registra_En_La_Moneda_Agregada_Como_Dato_AC02()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await ConLaMonedaAsync("EUR", async nueva =>
        {
            await ConLaPredeterminadaEnAsync(nueva.Id, async () =>
            {
                using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
                    new Uri("/api/movimientos", UriKind.Relative),
                    new { tipo = "gasto", monto = 1500m, categoriaId = 1, fecha = Hoy.ToString("yyyy-MM-dd") });

                Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

                var monedas = await MonedasDelResumenAsync(cuenta);

                var euro = Assert.Single(monedas, m => m.MonedaCodigo == "EUR");
                Assert.Equal(1500m, euro.TotalGastado);
                Assert.Equal(-1500m, euro.Balance);
                Assert.Equal(1500m, Assert.Single(euro.GastosPorCategoria).Total);

                // Y en ninguna otra. Es la mitad que distingue "la moneda nueva se usó" de "se
                // registró algo en alguna moneda".
                Assert.All(
                    monedas.Where(m => m.MonedaCodigo != "EUR"),
                    m => Assert.Equal(0m, m.TotalGastado));
            });
        });
    }

    /// <summary>
    /// AC-05, FR-006 y SC-002: una **misma** categoría con gastos en dos monedas aparece una vez
    /// dentro de cada una, con el total de esa moneda, y ninguno incluye montos de la otra.
    ///
    /// Es el caso que el PRD llama plausible y por eso peligroso: sumar 10.000 pesos con 50 dólares
    /// da 10.050, un número que nadie mira dos veces.
    /// </summary>
    [Fact]
    public async Task Una_Categoria_Con_Gastos_En_Dos_Monedas_No_Los_Mezcla_AC05()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await SembrarAsync(cuenta.Id, monedaId: 1, categoriaId: 1, TipoMovimiento.Gasto, 10_000m);
        await SembrarAsync(cuenta.Id, monedaId: 2, categoriaId: 1, TipoMovimiento.Gasto, 50m);

        var monedas = await MonedasDelResumenAsync(cuenta);

        var ars = Assert.Single(monedas, m => m.MonedaCodigo == "ARS");
        var usd = Assert.Single(monedas, m => m.MonedaCodigo == "USD");

        Assert.Equal(10_000m, Assert.Single(ars.GastosPorCategoria, c => c.CategoriaId == 1).Total);
        Assert.Equal(50m, Assert.Single(usd.GastosPorCategoria, c => c.CategoriaId == 1).Total);
        Assert.Equal(10_000m, ars.TotalGastado);
        Assert.Equal(50m, usd.TotalGastado);

        // La comprobación que atrapa la mezcla: 10.050 sería el número plausible y equivocado.
        Assert.NotEqual(10_050m, ars.TotalGastado);
        Assert.NotEqual(10_050m, usd.TotalGastado);
    }

    /// <summary>
    /// AC-06, FR-005 y SC-002: el balance de cada moneda es sus ingresos menos sus gastos, sin
    /// cruzar nada.
    ///
    /// Se eligen números que hacen que la mezcla se note: si los universos se cruzaran, el balance
    /// de alguna de las dos daría 0 —que es un número que parece razonable— en vez de sus valores.
    /// </summary>
    [Fact]
    public async Task El_Balance_De_Cada_Moneda_Sale_De_Sus_Propios_Movimientos_AC06()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await SembrarAsync(cuenta.Id, monedaId: 1, categoriaId: 8, TipoMovimiento.Ingreso, 500m);
        await SembrarAsync(cuenta.Id, monedaId: 1, categoriaId: 1, TipoMovimiento.Gasto, 200m);
        await SembrarAsync(cuenta.Id, monedaId: 2, categoriaId: 8, TipoMovimiento.Ingreso, 200m);
        await SembrarAsync(cuenta.Id, monedaId: 2, categoriaId: 1, TipoMovimiento.Gasto, 500m);

        var monedas = await MonedasDelResumenAsync(cuenta);

        var ars = Assert.Single(monedas, m => m.MonedaCodigo == "ARS");
        var usd = Assert.Single(monedas, m => m.MonedaCodigo == "USD");

        Assert.Equal(300m, ars.Balance);    // 500 - 200
        Assert.Equal(-300m, usd.Balance);   // 200 - 500, y en rojo es un resultado, no un error
    }

    /// <summary>
    /// AC-10 y FR-007 (`PRD:AC-11`): la respuesta trae los totales **ya agregados** y a lo sumo una
    /// fila por categoría dentro de cada moneda, y **no** trae los movimientos individuales.
    ///
    /// Mira la FORMA de la respuesta, no sus números: es lo que impide que alguien "arregle" un
    /// rendimiento devolviendo el listado y sumando del lado del cliente. Con seis movimientos
    /// repartidos en dos categorías y dos monedas, un desglose que no agregara traería seis filas.
    /// </summary>
    [Fact]
    public async Task El_Resumen_Devuelve_Totales_Agregados_Y_No_Movimientos_AC10()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        foreach (var monedaId in new short[] { 1, 2 })
        {
            foreach (var categoriaId in new[] { 1, 2 })
            {
                await SembrarAsync(cuenta.Id, monedaId, categoriaId, TipoMovimiento.Gasto, 100m);
                await SembrarAsync(cuenta.Id, monedaId, categoriaId, TipoMovimiento.Gasto, 50m);
            }
        }

        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/resumen", UriKind.Relative));
        var crudo = await respuesta.Content.ReadAsStringAsync();

        var monedas = await MonedasDelResumenAsync(cuenta);

        foreach (var moneda in monedas)
        {
            // A lo sumo una fila por categoría: sin agregar serían dos por cada una.
            Assert.Equal(
                moneda.GastosPorCategoria.Select(c => c.CategoriaId).Distinct().Count(),
                moneda.GastosPorCategoria.Count);
        }

        var ars = Assert.Single(monedas, m => m.MonedaCodigo == "ARS");
        Assert.Equal(2, ars.GastosPorCategoria.Count);
        Assert.All(ars.GastosPorCategoria, c => Assert.Equal(150m, c.Total));

        // Y nada que huela a movimiento suelto: ni el id, ni la fecha, ni el monto individual.
        Assert.DoesNotContain("\"fecha\"", crudo, StringComparison.Ordinal);
        Assert.DoesNotContain("\"movimientos\"", crudo, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-08, FR-009 y SC-005: una moneda del catálogo **sin** movimientos en el período aparece
    /// igual, con todo en cero y sin ningún error.
    ///
    /// **Esto es `006:AC-31` conservado a propósito, y contradice `PRD:AC-07` y `PRD:AC-08` de la
    /// 4a.** No es un descuido: son dos criterios razonables que no pueden convivir, y ganó el que
    /// tiene la razón escrita — devolver los ceros en vez de una respuesta vacía que obligue a quien
    /// la muestre a inventarlos. La decisión está registrada como D8-04 en la spec.
    ///
    /// Y no es sólo una decisión heredada: **es lo que hace que agregar una moneda al catálogo se
    /// note sin que nadie registre nada con ella**, que es AC-01. Si el resumen informara sólo sobre
    /// las monedas con actividad, `PRD:RF-32` se cumpliría a medias.
    /// </summary>
    [Fact]
    public async Task Una_Moneda_Sin_Movimientos_Aparece_En_Cero_AC08()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        // Sólo ARS tiene movimientos. USD está en el catálogo y no se usó.
        await SembrarAsync(cuenta.Id, monedaId: 1, categoriaId: 1, TipoMovimiento.Gasto, 700m);

        var monedas = await MonedasDelResumenAsync(cuenta);

        var usd = Assert.Single(monedas, m => m.MonedaCodigo == "USD");
        Assert.Equal(0m, usd.TotalIngresado);
        Assert.Equal(0m, usd.TotalGastado);
        Assert.Equal(0m, usd.Balance);
        Assert.Empty(usd.GastosPorCategoria);
    }

    /// <summary>
    /// AC-09 y SC-005: una cuenta sin ningún movimiento en el período devuelve una entrada en cero
    /// por **cada** moneda del catálogo, y ningún error.
    ///
    /// Un período vacío es una respuesta válida, no un caso de error: la lista de monedas sale del
    /// catálogo y no del agregado, así que siempre hay sobre qué informar.
    /// </summary>
    [Fact]
    public async Task Un_Periodo_Sin_Movimientos_Devuelve_Ceros_Y_No_Un_Vacio_AC09()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var monedas = await MonedasDelResumenAsync(cuenta);

        // **Se compara contra el catálogo y no contra un 2 escrito a mano.** El AC dice "una entrada
        // por CADA moneda del catálogo", así que el catálogo es la fuente. Y además: con el número
        // fijo, este caso se ponía en rojo apenas hubiera una moneda de más — que es exactamente lo
        // que `verificar-monedas.sh` agrega a propósito para correr estos mismos tests. La barrera
        // se rompía por su propio montaje, y lo encontró el quickstart.
        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            Assert.Equal(await contexto.Monedas.CountAsync(), monedas.Count);
        }

        Assert.All(monedas, m =>
        {
            Assert.Equal(0m, m.TotalIngresado);
            Assert.Equal(0m, m.TotalGastado);
            Assert.Equal(0m, m.Balance);
            Assert.Empty(m.GastosPorCategoria);
        });
    }

    /// <summary>
    /// AC-07, FR-008 y SC-004: **con una sola moneda con movimientos, nada cambió.**
    ///
    /// Este caso NO duplica los tests del resumen de la feature 006: los nombra. La regresión de que
    /// los totales, el balance y el desglose sigan dando lo mismo ya está cubierta, con más casos y
    /// mejor, por `ResumenEndpointTests` y `ResumenDelMesTests` — y `verificar-contrato.sh` en verde
    /// al cierre prueba además que el contrato no se movió, que es lo que FR-009 pide.
    ///
    /// Lo que sí aporta acá es la comprobación de que esta feature no rompió el caso base: una
    /// cuenta con movimientos en una sola moneda ve exactamente los mismos números que vería sin
    /// nada de esto. Es barato y cierra el AC sin inventar una segunda fuente de verdad.
    /// </summary>
    [Fact]
    public async Task Con_Una_Sola_Moneda_El_Resumen_Da_Lo_Mismo_De_Siempre_AC07()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await SembrarAsync(cuenta.Id, monedaId: 1, categoriaId: 8, TipoMovimiento.Ingreso, 1000m);
        await SembrarAsync(cuenta.Id, monedaId: 1, categoriaId: 1, TipoMovimiento.Gasto, 300m);
        await SembrarAsync(cuenta.Id, monedaId: 1, categoriaId: 2, TipoMovimiento.Gasto, 100m);

        var ars = Assert.Single(await MonedasDelResumenAsync(cuenta), m => m.MonedaCodigo == "ARS");

        Assert.Equal(1000m, ars.TotalIngresado);
        Assert.Equal(400m, ars.TotalGastado);
        Assert.Equal(600m, ars.Balance);

        // INV-02: la suma del desglose es el total gastado. No se verifica al final, se cumple
        // porque los dos números salen de las mismas filas.
        Assert.Equal(ars.TotalGastado, ars.GastosPorCategoria.Sum(c => c.Total));

        // De mayor a menor, que es el orden del contrato.
        Assert.Equal([300m, 100m], ars.GastosPorCategoria.Select(c => c.Total));
    }

    /// <summary>Siembra un movimiento directo en la base, para armar escenarios de varias monedas.</summary>
    private async Task SembrarAsync(
        long usuarioId, short monedaId, int categoriaId, TipoMovimiento tipo, decimal monto)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        contexto.Movimientos.Add(new Movimiento
        {
            UsuarioId = usuarioId,
            Tipo = tipo,
            Monto = monto,
            MonedaId = monedaId,
            CategoriaId = categoriaId,
            Fecha = Hoy,
        });
        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// Mueve la marca de predeterminada, corre el cuerpo, y la devuelve a donde estaba.
    ///
    /// Dos sentencias en cada sentido, por el índice único sobre la columna generada. El `finally`
    /// es tan necesario como el de `ConLaMonedaAsync`: una predeterminada movida sobrevive al test
    /// y se lleva puesta la suite entera, porque **todo** movimiento nuevo pasaría a registrarse en
    /// la moneda equivocada.
    /// </summary>
    private async Task ConLaPredeterminadaEnAsync(short monedaId, Func<Task> cuerpo)
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        var anterior = await contexto.Monedas.SingleAsync(m => m.EsPredeterminada);

        await MoverPredeterminadaAsync(contexto, desde: anterior.Id, hasta: monedaId);

        try
        {
            await cuerpo();
        }
        finally
        {
            await MoverPredeterminadaAsync(contexto, desde: monedaId, hasta: anterior.Id);
        }
    }

    private static async Task MoverPredeterminadaAsync(
        GestionGastosDbContext contexto, short desde, short hasta)
    {
        await contexto.Monedas.Where(m => m.Id == desde)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.EsPredeterminada, false));
        await contexto.Monedas.Where(m => m.Id == hasta)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.EsPredeterminada, true));
    }

    /// <summary>Las monedas del resumen del mes, ya deserializadas.</summary>
    private static async Task<IReadOnlyList<MonedaDelResumen>> MonedasDelResumenAsync(
        CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/resumen", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var resumen = await respuesta.Content.ReadFromJsonAsync<ResumenVisto>()
            ?? throw new InvalidOperationException("El resumen vino nulo.");

        return resumen.Monedas;
    }

}

/// <summary>
/// **El canario del catálogo de monedas.** No verifica ningún requisito: verifica que los demás
/// tests sean confiables.
///
/// `moneda` es una tabla que `LimpiarCuentasAsync` NO toca, y hasta esta feature eso estaba bien:
/// ningún test creaba monedas. `MonedaComoDatoTests` sí las crea, y una que sobreviva se le queda
/// al siguiente — que entonces falla por lo que hizo la corrida anterior y no por el código, que es
/// la peor forma de rojo que hay. Comprobado haciéndolo ocurrir antes de escribir la limpieza.
///
/// **Vive en su propia clase, y no junto a los tests que vigila, por una razón concreta**:
/// `verificar-monedas.sh` corre `--filter FullyQualifiedName~MonedaComoDato` con una moneda extra
/// puesta a propósito en el catálogo. Si este caso entrara en ese filtro, la barrera se pondría en
/// rojo por su propio montaje. Lo que este canario persigue son las monedas que un TEST deja
/// olvidadas, no las que el script agrega y borra.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class CatalogoDeMonedasLimpioTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task El_Catalogo_Queda_Con_Las_Dos_Monedas_Sembradas()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        var codigos = await contexto.Monedas.OrderBy(m => m.Id).Select(m => m.Codigo).ToListAsync();

        Assert.Equal(["ARS", "USD"], codigos);
    }
}

/// <summary>
/// El resumen como lo ve el cliente, para deserializarlo en los tests.
///
/// **Públicos y fuera de la clase**, igual que `CategoriaVista`: los instancia el deserializador de
/// JSON y no el código, así que CA1812 los da por muertos si son privados y `-warnaserror` rompe el
/// build. La forma la fija <c>Resumenes/ResumenDtos.cs</c>.
/// </summary>
public sealed record ResumenVisto(IReadOnlyList<MonedaDelResumen> Monedas);

public sealed record MonedaDelResumen(
    short MonedaId,
    string MonedaCodigo,
    decimal TotalIngresado,
    decimal TotalGastado,
    decimal Balance,
    IReadOnlyList<CategoriaDelDesglose> GastosPorCategoria);

public sealed record CategoriaDelDesglose(int CategoriaId, string CategoriaNombre, decimal Total);

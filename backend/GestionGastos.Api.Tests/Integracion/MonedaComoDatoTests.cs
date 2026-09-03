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

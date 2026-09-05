using System.Diagnostics;
using System.Net;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace GestionGastos.Api.Tests.Rendimiento;

/// <summary>
/// RNF-01: el dashboard carga en menos de 2 s p95 con hasta 1000 movimientos, y en menos de 4 s con
/// hasta 10000.
///
/// **También son `PRD:AC-11` y `PRD:AC-12` del ticket 5 (DISC-001-05), y la cita no es un detalle.**
/// Ese PRD da por sentado que el volumen de 10000 nunca se probó y le pide al plan que lo ataque
/// temprano; la reconciliación de la feature 010 mostró que este archivo lo mide desde la 006 y que
/// la 008 le agregó el caso de dos monedas. Lo que faltaba era que un test **los nombrara**, que es
/// lo que el Principio II de la constitución exige para considerar cubierto un AC. Se agrega la
/// cita, no la medición: los dos escalones que esos AC piden son exactamente los dos `InlineData`
/// que ya estaban escritos, y medirlos dos veces sería dos minutos de suite para saber lo mismo.
///
/// **El resumen es el primer endpoint al que este RNF le aplica de lleno**, porque es el dashboard.
/// Y a diferencia del listado, agrega: el índice `(usuario_id, fecha DESC, id DESC)` acota y ordena,
/// pero el `GROUP BY` por categoría no lo cubre. La expectativa era que igual alcanzara —el conjunto
/// ya viene acotado a un período de una cuenta— pero eso era una expectativa y RNF-01 es un número,
/// así que se mide (D-09).
///
/// Si esto se pone en rojo, la salida es el índice por `categoria_id` que D-10 dejó anotado, **con
/// el número en la mano** y no antes: un índice de más se paga en cada INSERT.
///
/// Mide tiempo de pared, así que el CI la excluye con `FullyQualifiedName!~Rendimiento`: en un
/// runner compartido da rojos que no dicen nada del código.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class RendimientoResumenTests(BaseDeDatosFixture baseDeDatos, ITestOutputHelper salida)
{
    /// <summary>
    /// Las mediciones por caso. **100 y no 30, que es lo que RNF-01 y FR-011 piden.**
    ///
    /// No es un número más grande porque sí: con 30 muestras el p95 cae en el elemento 29 —o sea,
    /// prácticamente el máximo—, y eso es otro estadístico y mucho más ruidoso que el percentil
    /// real. En un test que ya está excluido del CI por medir tiempo de pared, el ruido de más es lo
    /// último que hace falta.
    /// </summary>
    private const int Ejecuciones = 100;

    /// <summary>Las diez categorías del catálogo, para que el agrupado tenga sobre qué agrupar.</summary>
    private static readonly int[] CategoriasDeGasto = [1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] CategoriasDeIngreso = [8, 9, 10];

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Por dónde salen los números medidos.
    ///
    /// **Existe porque hasta la feature 010 el p95 sólo se podía ver haciendo fallar el test**: el
    /// número vivía en el mensaje del `Assert`, o sea únicamente en el caso en que no importa. El
    /// quickstart pide anotar las tres mediciones, y esta suite está excluida del CI justamente
    /// para que los números salgan de una corrida local — así que tienen que ser legibles cuando
    /// todo sale bien, que es siempre.
    /// </summary>
    private readonly ITestOutputHelper _salida = salida;

    /// <summary>
    /// Los dos escalones de RNF-01, `PRD:AC-11` y `PRD:AC-12`, medidos sobre `GET /api/resumen` sin
    /// parámetros — que es como lo pide la pantalla principal desde la feature 010.
    ///
    /// `AC-11` es el caso de 1000 y `AC-12` el de 10000. Las 100 ejecuciones y el p95 que esos AC
    /// piden son los que <see cref="Ejecuciones"/> ya fijaba.
    /// </summary>
    [Theory]
    [InlineData(1000, 2000)]
    [InlineData(10_000, 4000)]
    public async Task El_P95_Del_Resumen_Cumple_RNF01_AC11_AC12(int filas, int techoMs) =>
        await MedirAsync(filas, techoMs, monedas: 1);

    /// <summary>
    /// AC-04, FR-011 y SC-003: el mismo volumen, **repartido en dos monedas**.
    ///
    /// El `GROUP BY` del resumen agrupa por moneda, tipo y categoría. Con una sola moneda ese primer
    /// nivel no discrimina nada, así que el caso de arriba —1000 filas, todas en ARS— **no ejercita
    /// la agrupación que esta feature dice sostener**. Repartir en dos duplica los grupos sin
    /// duplicar las filas, que es exactamente la condición que NFR-03 acota.
    ///
    /// **El caso de una sola moneda se deja**, y no por completitud: es la referencia. Si éste se
    /// pone en rojo y aquél no, el costo lo agregó la segunda moneda y no el volumen — y la salida
    /// es el índice por `categoria_id` que la feature 006 dejó anotado en su deuda D6-05, con el
    /// número en la mano. Dos números que se comparan valen más que uno que hay que interpretar.
    ///
    /// Medido al escribirlo: 1000 filas en una moneda dan p95 de 6 ms; en dos, 9 ms. La segunda
    /// moneda cuesta alrededor de un 50 % más y las dos quedan dos órdenes de magnitud debajo del
    /// techo, así que el índice de D6-05 sigue sin justificarse.
    /// </summary>
    [Fact]
    public async Task El_P95_Del_Resumen_Con_Dos_Monedas_Cumple_RNF01_AC04() =>
        await MedirAsync(filas: 1000, techoMs: 2000, monedas: 2);

    private async Task MedirAsync(int filas, int techoMs, int monedas)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        using var factoria = new FactoriaConReloj(hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await SembrarAsync(cuenta.Id, hoy, filas, monedas);

        // El guardarraíl, por el mismo motivo que en el alta: un sembrado que dejó de caer en el
        // mes medido convierte la medición en un agregado sobre cero filas, y eso pasa en verde sin
        // medir nada. Acá es peor que en el listado — un resumen vacío es una respuesta válida.
        await ConfirmarQueElMesTieneFilasAsync(hoy, filas, monedas);

        // Calentamiento fuera de la medición: la primera paga la compilación del pipeline y el
        // primer plan de consulta del GROUP BY.
        await PedirResumenAsync(cuenta);

        var muestras = new List<double>(Ejecuciones);
        for (var i = 0; i < Ejecuciones; i++)
        {
            var cronometro = Stopwatch.StartNew();
            await PedirResumenAsync(cuenta);
            cronometro.Stop();
            muestras.Add(cronometro.Elapsed.TotalMilliseconds);
        }

        muestras.Sort();
        var p95 = muestras[(int)Math.Ceiling(0.95 * muestras.Count) - 1];

        _salida.WriteLine(
            $"RNF-01 · {filas} movimientos en {monedas} moneda(s): p95 {p95:F0} ms, " +
            $"mediana {muestras[muestras.Count / 2]:F0} ms, máximo {muestras[^1]:F0} ms " +
            $"(techo {techoMs} ms).");

        Assert.True(
            p95 < techoMs,
            $"RNF-01: el p95 del resumen con {filas} movimientos en {monedas} moneda(s) fue " +
            $"{p95:F0} ms y el criterio " +
            $"exige < {techoMs} ms. Mediana {muestras[muestras.Count / 2]:F0} ms, " +
            $"máximo {muestras[^1]:F0} ms. Si es el GROUP BY, el índice por categoria_id que " +
            "research.md D-10 dejó anotado es la salida.");
    }

    private static async Task PedirResumenAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/resumen", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// Siembra repartiendo por categoría y por tipo: agrupar 1000 filas que caen todas en la misma
    /// categoría no ejercita el `GROUP BY`, lo esquiva.
    /// </summary>
    private async Task SembrarAsync(long usuarioId, DateOnly hoy, int filas, int monedas)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        var fechas = SembradoDeRendimiento.GenerarFechasSembradas(hoy, filas);

        contexto.Movimientos.AddRange(fechas.Select((fecha, i) =>
        {
            var esGasto = i % 4 != 0;

            return new Movimiento
            {
                UsuarioId = usuarioId,
                Tipo = esGasto ? TipoMovimiento.Gasto : TipoMovimiento.Ingreso,
                Monto = 100m + (i % 97),
                // Reparte por moneda además de por categoría y por tipo: agrupar 1000 filas que
                // caen todas en la misma moneda no ejercita ese nivel del GROUP BY, lo esquiva.
                MonedaId = (short)((i % monedas) + 1),
                CategoriaId = esGasto
                    ? CategoriasDeGasto[i % CategoriasDeGasto.Length]
                    : CategoriasDeIngreso[i % CategoriasDeIngreso.Length],
                Fecha = fecha,
            };
        }));

        await contexto.SaveChangesAsync();
    }

    private async Task ConfirmarQueElMesTieneFilasAsync(DateOnly hoy, int esperadas, int monedas)
    {
        var rango = RangoDelMes.De(hoy);

        await using var contexto = _baseDeDatos.CrearContexto();
        var delMes = contexto.Movimientos.Where(m => m.Fecha >= rango.Desde && m.Fecha <= rango.Hasta);

        var filas = await delMes.CountAsync();

        Assert.True(
            filas >= esperadas,
            $"El sembrado dejó {filas} filas en el mes de {hoy:yyyy-MM} y se esperaban al menos " +
            $"{esperadas}. El resumen estaría agregando sobre casi nada y pasaría en verde sin " +
            "medir nada real.");

        // **La otra mitad del guardarraíl, y es nueva.** Un sembrado que dejó de repartir por moneda
        // mide exactamente lo mismo que el caso de una sola, y pasa en verde sin haber ejercitado
        // nada de lo que este caso existe para medir. La cantidad de filas no lo delata: son las
        // mismas 1000.
        var sembradas = await delMes.Select(m => m.MonedaId).Distinct().CountAsync();

        Assert.True(
            sembradas == monedas,
            $"El sembrado dejó {sembradas} moneda(s) distinta(s) en el mes y este caso mide con " +
            $"{monedas}. Con una sola, el GROUP BY no discrimina por moneda y la medición no dice " +
            "nada que el caso de una moneda no dijera ya.");
    }
}

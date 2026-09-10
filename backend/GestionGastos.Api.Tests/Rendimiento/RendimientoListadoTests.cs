using System.Diagnostics;
using System.Net;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace GestionGastos.Api.Tests.Rendimiento;

/// <summary>
/// `NFR-003` y `PRD:AC-10` de la feature 012: el listado carga en menos de 2 s en el p95 sobre una
/// cuenta con 1000 movimientos **que tienen nota**.
///
/// **Es la primera medición del listado del proyecto, y hasta acá no existía.** Había tres tests de
/// rendimiento —el alta, el resumen y el límite de intentos— y ninguno medía esta consulta, así que la
/// afirmación de `RNF-01` sobre el listado no estaba respaldada por nada, ni antes ni después de
/// agregar la columna. El PRD del ticket 2 la pide y es la primera vez que alguien la pide.
///
/// **Qué mide y qué no, dicho para no afirmar de más**: mide la respuesta de `GET /api/movimientos`,
/// no lo que tarda el navegador en pintar la tabla. Medir la pantalla exigiría un runner de navegador,
/// que `NFR-005` prohíbe. Es la misma honestidad que la feature 011 aplicó a los 360 px: se afirma lo
/// que se midió, y lo que no se puede medir queda anotado como deuda (**D12-01**).
///
/// Mide tiempo de pared, así que el CI la excluye con `FullyQualifiedName!~Rendimiento`: en un runner
/// compartido da rojos que no dicen nada del código.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class RendimientoListadoTests(BaseDeDatosFixture baseDeDatos, ITestOutputHelper salida)
{
    /// <summary>Las mediciones. 100 y no 30, por el mismo motivo que en el resumen.</summary>
    private const int Ejecuciones = 100;

    /// <summary>El largo de las notas sembradas. Realista, y cerca del techo sin tocarlo.</summary>
    private const int LargoDeLaNota = 100;

    private static readonly int[] CategoriasDeGasto = [1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] CategoriasDeIngreso = [8, 9, 10];

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;
    private readonly ITestOutputHelper _salida = salida;

    /// <summary>
    /// `PRD:AC-10`: 1000 movimientos con nota, 100 ejecuciones, p95 por debajo de 2 s.
    ///
    /// **Las notas se siembran no vacías y de largo realista, y eso es la mitad del test.** Sembrarlas
    /// sin nota mediría la consulta de antes de esta feature: la columna estaría en el `SELECT` pero
    /// no habría bytes que traer, y el test daría verde sin haber ejercitado nada de lo que dice
    /// medir. Es el mismo error que la feature 009 evitó al exigir dos monedas en el sembrado del
    /// resumen — agrupar 1000 filas que caen todas en el mismo grupo esquiva el `GROUP BY` en vez de
    /// ejercitarlo.
    /// </summary>
    [Fact]
    public async Task El_P95_Del_Listado_Con_Mil_Movimientos_Con_Nota_Cumple_NFR003_AC10()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        using var factoria = new FactoriaConReloj(hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        const int Filas = 1000;
        const int TechoMs = 2000;

        await SembrarConNotaAsync(cuenta.Id, hoy, Filas);
        await ConfirmarQueElMesTieneFilasConNotaAsync(hoy, Filas);

        // Calentamiento fuera de la medición: la primera paga la compilación del pipeline y el primer
        // plan de la consulta.
        await PedirListadoAsync(cuenta, Filas);

        var muestras = new List<double>(Ejecuciones);
        for (var i = 0; i < Ejecuciones; i++)
        {
            var cronometro = Stopwatch.StartNew();
            await PedirListadoAsync(cuenta, Filas);
            cronometro.Stop();
            muestras.Add(cronometro.Elapsed.TotalMilliseconds);
        }

        muestras.Sort();
        var p95 = muestras[(int)Math.Ceiling(0.95 * muestras.Count) - 1];

        _salida.WriteLine(
            $"NFR-003 · {Filas} movimientos con nota de {LargoDeLaNota} caracteres: p95 {p95:F0} ms, " +
            $"mediana {muestras[muestras.Count / 2]:F0} ms, máximo {muestras[^1]:F0} ms " +
            $"(techo {TechoMs} ms).");

        Assert.True(
            p95 < TechoMs,
            $"NFR-003: el p95 del listado con {Filas} movimientos con nota fue {p95:F0} ms y el " +
            $"criterio exige < {TechoMs} ms. Mediana {muestras[muestras.Count / 2]:F0} ms, máximo " +
            $"{muestras[^1]:F0} ms. Si el costo lo agregó la columna de la nota, la comparación útil " +
            "es contra el p95 del alta, que mide la misma tabla sin traerla.");
    }

    /// <summary>
    /// Siembra 1000 movimientos **todos con nota**, repartidos por categoría y tipo.
    ///
    /// Las fechas salen de <see cref="SembradoDeRendimiento"/>, que las ancla al año de la fecha que
    /// recibe: es la lección de FIX-004, donde un sembrado anclado a un año fijo vencía y el test no
    /// fallaba ruidosamente sino que medía una tabla vacía y pasaba en verde.
    ///
    /// Las notas se generan distintas entre sí. Mil filas con la misma nota le darían a MySQL una
    /// oportunidad de compresión o de caché de página que la carga real no tiene.
    /// </summary>
    private async Task SembrarConNotaAsync(long usuarioId, DateOnly hoy, int filas)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        var fechas = SembradoDeRendimiento.GenerarFechasSembradas(hoy, filas);

        contexto.Movimientos.AddRange(fechas.Select((fecha, i) =>
        {
            var esGasto = i % 4 != 0;
            var prefijo = $"movimiento {i} · ";

            return new Movimiento
            {
                UsuarioId = usuarioId,
                Tipo = esGasto ? TipoMovimiento.Gasto : TipoMovimiento.Ingreso,
                Monto = 100m + (i % 97),
                MonedaId = 1,
                CategoriaId = esGasto
                    ? CategoriasDeGasto[i % CategoriasDeGasto.Length]
                    : CategoriasDeIngreso[i % CategoriasDeIngreso.Length],
                Fecha = fecha,
                Nota = prefijo + new string('x', LargoDeLaNota - prefijo.Length),
            };
        }));

        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// El guardarraíl del sembrado, con **las dos mitades**.
    ///
    /// La primera es la de siempre: un sembrado que dejó de caer en el mes medido convierte la
    /// medición en una consulta sobre cero filas, y eso pasa en verde sin medir nada.
    ///
    /// La segunda es propia de este test y es la que lo vuelve honesto: **si las notas dejaran de
    /// sembrarse, este test mediría el listado de antes de la feature 012 y pasaría igual**. La
    /// cantidad de filas no lo delata — son las mismas 1000, con la columna vacía.
    /// </summary>
    private async Task ConfirmarQueElMesTieneFilasConNotaAsync(DateOnly hoy, int esperadas)
    {
        var rango = RangoDelMes.De(hoy);

        await using var contexto = _baseDeDatos.CrearContexto();
        var delMes = contexto.Movimientos.Where(m => m.Fecha >= rango.Desde && m.Fecha <= rango.Hasta);

        var filas = await delMes.CountAsync();

        Assert.True(
            filas >= esperadas,
            $"El sembrado dejó {filas} filas en el mes de {hoy:yyyy-MM} y se esperaban al menos " +
            $"{esperadas}. El listado estaría midiendo sobre casi nada y pasaría en verde sin medir " +
            "nada real.");

        var conNota = await delMes.CountAsync(m => m.Nota != null && m.Nota != string.Empty);

        Assert.True(
            conNota >= esperadas,
            $"El sembrado dejó {conNota} filas CON NOTA de las {filas} del mes, y se esperaban al " +
            $"menos {esperadas}. Sin notas, este test mide el listado de antes de la feature 012 y " +
            "pasa en verde sin haber ejercitado la columna que dice medir.");
    }

    private static async Task PedirListadoAsync(CuentaDePrueba cuenta, int esperadas)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/movimientos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        // Se lee el cuerpo entero, y no es ceremonia: sin leerlo, la medición cortaría en los
        // encabezados y no incluiría el costo de traer las 1000 notas por la red — que es exactamente
        // lo que esta feature agrega y lo que `NFR-003` acota.
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.True(
            cuerpo.Length > esperadas * LargoDeLaNota,
            $"La respuesta del listado midió {cuerpo.Length} caracteres, menos de lo que ocuparían " +
            $"{esperadas} notas de {LargoDeLaNota}. O el listado dejó de devolver las notas, o dejó " +
            "de devolver las filas: en los dos casos la medición no dice lo que dice.");
    }
}

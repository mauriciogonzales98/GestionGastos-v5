using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Rendimiento;

/// <summary>
/// AC-34 (RNF-02): el percentil 95 del guardado, sobre 100 ejecuciones, es menor a 1 segundo.
///
/// Esta suite mide tiempo de pared, así que el CI la excluye: en un runner compartido da rojos que
/// no dicen nada del código. Corre en local, que es donde la medición significa algo.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class RendimientoAltaTests(BaseDeDatosFixture baseDeDatos)
{
    private const int Ejecuciones = 100;
    private const int FilasSembradas = 500;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task El_P95_Del_Guardado_Es_Menor_A_Un_Segundo_AC34()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        using var factoria = new FactoriaConReloj(hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        await SembrarAsync(cuenta.Id, hoy);

        // El guardarraíl. Sin esto, un sembrado que dejó de coincidir con el mes medido convierte
        // la medición en una tabla vacía y el test pasa en verde sin medir nada.
        await ConfirmarQueElMesTieneFilasAsync(hoy);

        // Una ejecución de calentamiento fuera de la medición: la primera paga la compilación del
        // pipeline y el primer plan de consulta, y no representa el guardado real.
        await GuardarAsync(cliente, hoy);

        var muestras = new List<double>(Ejecuciones);
        for (var i = 0; i < Ejecuciones; i++)
        {
            var cronometro = Stopwatch.StartNew();
            await GuardarAsync(cliente, hoy);
            cronometro.Stop();
            muestras.Add(cronometro.Elapsed.TotalMilliseconds);
        }

        muestras.Sort();
        var p95 = muestras[(int)Math.Ceiling(0.95 * muestras.Count) - 1];

        Assert.True(
            p95 < 1000,
            $"AC-34: el p95 del guardado fue {p95:F0} ms sobre {Ejecuciones} ejecuciones, y el " +
            $"criterio exige < 1000 ms. Mediana {muestras[muestras.Count / 2]:F0} ms, " +
            $"máximo {muestras[^1]:F0} ms.");
    }

    /// <summary>
    /// SC-008 y `PRD:NFR-03` de la feature 009: el mismo criterio, **con la moneda elegida**.
    ///
    /// Elegir la moneda le agrega al alta un `SELECT` por clave primaria que antes no existía: sin
    /// `monedaId`, el servidor busca la predeterminada con un `WHERE es_predeterminada`; con él,
    /// busca por `id`. La pregunta que este caso responde es si ese cambio cuesta algo.
    ///
    /// **El caso de arriba se deja intacto a propósito** (D-09, heredado de la feature 008). Es la
    /// referencia: si este p95 sale feo, tener el otro medido en la misma corrida y en la misma
    /// máquina es lo único que permite atribuirlo al `SELECT` y no a que el runner estaba ocupado.
    /// Sin él, un rojo acá no dice nada.
    /// </summary>
    [Fact]
    public async Task El_P95_Del_Guardado_Con_Moneda_Elegida_Es_Menor_A_Un_Segundo_SC008()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        await Integracion.CatalogoDeMonedas.ConLaMonedaAsync(_baseDeDatos, "XPF", async moneda =>
        {
            using var factoria = new FactoriaConReloj(hoy);
            using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
            var cliente = cuenta.Cliente;

            await SembrarAsync(cuenta.Id, hoy);
            await ConfirmarQueElMesTieneFilasAsync(hoy);

            await GuardarAsync(cliente, hoy, moneda.Id);

            var muestras = new List<double>(Ejecuciones);
            for (var i = 0; i < Ejecuciones; i++)
            {
                var cronometro = Stopwatch.StartNew();
                await GuardarAsync(cliente, hoy, moneda.Id);
                cronometro.Stop();
                muestras.Add(cronometro.Elapsed.TotalMilliseconds);
            }

            muestras.Sort();
            var p95 = muestras[(int)Math.Ceiling(0.95 * muestras.Count) - 1];

            Assert.True(
                p95 < 1000,
                $"SC-008: el p95 del guardado CON moneda elegida fue {p95:F0} ms sobre " +
                $"{Ejecuciones} ejecuciones, y el criterio exige < 1000 ms. Mediana " +
                $"{muestras[muestras.Count / 2]:F0} ms, máximo {muestras[^1]:F0} ms.");
        });
    }

    private static async Task GuardarAsync(HttpClient cliente, DateOnly hoy, short? monedaId = null)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new
            {
                tipo = "gasto",
                monto = 123.45m,
                categoriaId = 1,
                monedaId,
                fecha = hoy.ToString("yyyy-MM-dd"),
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    /// <summary>
    /// Se mide contra una tabla con filas, no contra una vacía: insertar en una tabla vacía y en
    /// una poblada no cuestan lo mismo, y el índice del listado se mantiene en cada INSERT.
    /// </summary>
    private async Task SembrarAsync(long usuarioId, DateOnly hoy)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        var fechas = SembradoDeRendimiento.GenerarFechasSembradas(hoy, FilasSembradas);

        contexto.Movimientos.AddRange(fechas.Select(fecha => new Movimiento
        {
            UsuarioId = usuarioId,
            Tipo = TipoMovimiento.Gasto,
            Monto = 100m,
            MonedaId = 1,
            CategoriaId = 1,
            Fecha = fecha,
        }));

        await contexto.SaveChangesAsync();
    }

    private async Task ConfirmarQueElMesTieneFilasAsync(DateOnly hoy)
    {
        var rango = RangoDelMes.De(hoy);

        await using var contexto = _baseDeDatos.CrearContexto();
        var filas = await contexto.Movimientos
            .CountAsync(m => m.Fecha >= rango.Desde && m.Fecha <= rango.Hasta);

        Assert.True(
            filas >= FilasSembradas,
            $"El sembrado dejó {filas} filas en el mes de {hoy:yyyy-MM} y se esperaban al menos " +
            $"{FilasSembradas}. La medición estaría corriendo sobre una tabla casi vacía y pasaría " +
            "en verde sin medir nada real.");
    }
}

using System.Diagnostics;
using System.Net;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Rendimiento;

/// <summary>
/// RNF-01: el dashboard carga en menos de 2 s p95 con hasta 1000 movimientos, y en menos de 4 s con
/// hasta 10000.
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
public class RendimientoResumenTests(BaseDeDatosFixture baseDeDatos)
{
    private const int Ejecuciones = 30;

    /// <summary>Las diez categorías del catálogo, para que el agrupado tenga sobre qué agrupar.</summary>
    private static readonly int[] CategoriasDeGasto = [1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] CategoriasDeIngreso = [8, 9, 10];

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Los dos escalones de RNF-01, medidos sobre `GET /api/resumen` sin parámetros — que es como
    /// lo pide la pantalla principal.
    /// </summary>
    [Theory]
    [InlineData(1000, 2000)]
    [InlineData(10_000, 4000)]
    public async Task El_P95_Del_Resumen_Cumple_RNF01(int filas, int techoMs)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        using var factoria = new FactoriaConReloj(hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await SembrarAsync(cuenta.Id, hoy, filas);

        // El guardarraíl, por el mismo motivo que en el alta: un sembrado que dejó de caer en el
        // mes medido convierte la medición en un agregado sobre cero filas, y eso pasa en verde sin
        // medir nada. Acá es peor que en el listado — un resumen vacío es una respuesta válida.
        await ConfirmarQueElMesTieneFilasAsync(hoy, filas);

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

        Assert.True(
            p95 < techoMs,
            $"RNF-01: el p95 del resumen con {filas} movimientos fue {p95:F0} ms y el criterio " +
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
    private async Task SembrarAsync(long usuarioId, DateOnly hoy, int filas)
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
                MonedaId = 1,
                CategoriaId = esGasto
                    ? CategoriasDeGasto[i % CategoriasDeGasto.Length]
                    : CategoriasDeIngreso[i % CategoriasDeIngreso.Length],
                Fecha = fecha,
            };
        }));

        await contexto.SaveChangesAsync();
    }

    private async Task ConfirmarQueElMesTieneFilasAsync(DateOnly hoy, int esperadas)
    {
        var rango = RangoDelMes.De(hoy);

        await using var contexto = _baseDeDatos.CrearContexto();
        var filas = await contexto.Movimientos
            .CountAsync(m => m.Fecha >= rango.Desde && m.Fecha <= rango.Hasta);

        Assert.True(
            filas >= esperadas,
            $"El sembrado dejó {filas} filas en el mes de {hoy:yyyy-MM} y se esperaban al menos " +
            $"{esperadas}. El resumen estaría agregando sobre casi nada y pasaría en verde sin " +
            "medir nada real.");
    }
}

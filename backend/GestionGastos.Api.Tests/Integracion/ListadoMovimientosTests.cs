using System.Net;
using System.Text.Json;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El listado del mes actual (FR-007, FR-008).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ListadoMovimientosTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-25 (RF-18): sin filtro de fecha, se ven únicamente los movimientos del mes actual.
    ///
    /// Los cuatro bordes se verifican con fechas fijas, no con "hoy": un test anclado al día en
    /// que corre no puede verificar el borde de fin de mes salvo el día 31.
    /// </summary>
    [Fact]
    public async Task Devuelve_Solo_Los_Del_Mes_Actual_Y_Excluye_Los_Cuatro_Bordes_AC25()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 15));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await SembrarAsync(
            cuenta.Id,
            new DateOnly(2026, 7, 31),   // último del mes anterior — afuera
            new DateOnly(2026, 8, 1),    // primero del actual — adentro
            new DateOnly(2026, 8, 31),   // último del actual — adentro
            new DateOnly(2026, 9, 1));   // primero del siguiente — afuera

        var fechas = await FechasDelListadoAsync(cuenta);

        Assert.Equal(["2026-08-31", "2026-08-01"], fechas);
    }

    /// <summary>
    /// Primera capa del orden: el resultado sale como se espera, con `id DESC` desempatando a
    /// igual fecha — el último cargado va primero, que es lo que la persona espera al guardar.
    /// </summary>
    [Fact]
    public async Task Ordena_Por_Fecha_Descendente_Y_Desempata_Por_Id_Descendente()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 15));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var ids = await SembrarAsync(
            cuenta.Id,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 10));

        var listado = await ListadoAsync(cuenta);

        var idsDevueltos = listado.Select(m => m.GetProperty("id").GetInt64()).ToList();

        // 20/8 primero; después los dos del 10/8, el de id mayor antes.
        Assert.Equal([ids[1], ids[2], ids[0]], idsDevueltos);
    }

    /// <summary>
    /// Segunda capa del orden, la que D-04 exige. El índice (usuario_id, fecha DESC, id DESC) hace
    /// que MySQL devuelva las filas ya ordenadas aunque la consulta no lo pida: el test de arriba
    /// pasa en verde con el OrderBy borrado. Éste mira la consulta, no el resultado.
    /// </summary>
    [Fact]
    public void La_Consulta_Pide_El_Orden_Explicitamente_Y_No_Lo_Hereda_Del_Indice()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var sql = MovimientosConsulta
            // El id no importa: este test sólo mira el SQL que se genera, no filas.
            .Filtrado(contexto, usuarioId: 1, RangoDelMes.De(new DateOnly(2026, 8, 15)))
            .ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);

        var ordenBy = sql[sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase)..];
        Assert.Contains("fecha", ordenBy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DESC", ordenBy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id", ordenBy, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// FR-012: un mes sin movimientos es un listado vacío, no un recurso que no existe. La pantalla
    /// lo muestra como listado vacío con su mensaje.
    /// </summary>
    [Fact]
    public async Task Sin_Movimientos_En_El_Mes_Devuelve_200_Con_Arreglo_Vacio_FR012()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 15));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        await SembrarAsync(cuenta.Id, new DateOnly(2026, 1, 15));

        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Empty(json.RootElement.EnumerateArray());
    }

    /// <summary>
    /// AC-22 (RF-17): el listado muestra gastos e ingresos juntos, y cada uno con su tipo. Es lo
    /// que permite distinguirlos sin abrir cada fila.
    /// </summary>
    [Fact]
    public async Task Devuelve_Gastos_E_Ingresos_Del_Mes_Cada_Uno_Con_Su_Tipo_AC22()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 15));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            contexto.Movimientos.AddRange(
                new Movimiento
                {
                    UsuarioId = cuenta.Id,
                    Tipo = TipoMovimiento.Gasto,
                    Monto = 100m,
                    MonedaId = 1,
                    CategoriaId = 1,
                    Fecha = new DateOnly(2026, 8, 10),
                },
                new Movimiento
                {
                    UsuarioId = cuenta.Id,
                    Tipo = TipoMovimiento.Ingreso,
                    Monto = 50000m,
                    MonedaId = 1,
                    CategoriaId = 8,
                    Fecha = new DateOnly(2026, 8, 20),
                });
            await contexto.SaveChangesAsync();
        }

        var listado = await ListadoAsync(cuenta);

        Assert.Equal(2, listado.Count);
        Assert.Equal("ingreso", listado[0].GetProperty("tipo").GetString());
        Assert.Equal("Sueldo", listado[0].GetProperty("categoriaNombre").GetString());
        Assert.Equal("gasto", listado[1].GetProperty("tipo").GetString());
        Assert.Equal("Comida", listado[1].GetProperty("categoriaNombre").GetString());
    }

    /// <summary>
    /// AC-08 (FR-010): cada cuenta ve únicamente sus movimientos.
    ///
    /// Las dos siembras son del MISMO mes y con el mismo monto: si el recorte por propietario
    /// fallara, los movimientos ajenos aparecerían mezclados con los propios y nada en la
    /// respuesta los delataría. Lo que separa un listado correcto de uno que muestra todo es
    /// exactamente esta comparación.
    /// </summary>
    [Fact]
    public async Task Cada_Cuenta_Ve_Solo_Sus_Movimientos_AC08()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 15));

        using var ana = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var bruno = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deAna = await SembrarAsync(ana.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 11));
        var deBruno = await SembrarAsync(bruno.Id, new DateOnly(2026, 8, 12));

        var vistosPorAna = (await ListadoAsync(ana))
            .Select(m => m.GetProperty("id").GetInt64())
            .ToList();
        var vistosPorBruno = (await ListadoAsync(bruno))
            .Select(m => m.GetProperty("id").GetInt64())
            .ToList();

        // En las dos direcciones: que Ana vea los suyos no prueba nada si además ve los de Bruno.
        Assert.Equal(deAna.OrderBy(id => id), vistosPorAna.OrderBy(id => id));
        Assert.Equal(deBruno.OrderBy(id => id), vistosPorBruno.OrderBy(id => id));
        Assert.DoesNotContain(deBruno[0], vistosPorAna);
    }

    private async Task<List<long>> SembrarAsync(long usuarioId, params DateOnly[] fechas)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        var creados = new List<Movimiento>();

        foreach (var fecha in fechas)
        {
            var movimiento = new Movimiento
            {
                UsuarioId = usuarioId,
                Tipo = TipoMovimiento.Gasto,
                Monto = 100m,
                MonedaId = 1,
                CategoriaId = 1,
                Fecha = fecha,
            };

            contexto.Movimientos.Add(movimiento);

            // De a uno, para que los id queden en el orden en que se sembraron y el desempate sea
            // verificable.
            await contexto.SaveChangesAsync();
            creados.Add(movimiento);
        }

        return creados.Select(m => m.Id).ToList();
    }

    private static async Task<List<JsonElement>> ListadoAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        // Clone(): los JsonElement mueren con su JsonDocument, y éste se libera al salir del
        // método. Sin la copia, el que lea el resultado se come un ObjectDisposedException.
        return [.. json.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    private static async Task<List<string>> FechasDelListadoAsync(CuentaDePrueba cuenta)
    {
        var listado = await ListadoAsync(cuenta);
        return listado.Select(m => m.GetProperty("fecha").GetString() ?? string.Empty).ToList();
    }
}

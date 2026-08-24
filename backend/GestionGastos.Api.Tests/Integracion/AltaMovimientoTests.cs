using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El alta de un movimiento (FR-001, FR-002).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class AltaMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-15 (RF-10): completado monto, categoría y fecha de un gasto, al guardar el gasto queda
    /// registrado y disponible para el listado. Acá se verifica el lado del servidor: la respuesta
    /// y la fila.
    /// </summary>
    [Fact]
    public async Task Registra_Un_Gasto_Valido_Y_Devuelve_El_Movimiento_Entero_AC15()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = CrearFactoria(new DateOnly(2026, 8, 23));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 1250.50m, categoriaId = 1, fecha = "2026-08-23" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var creado = json.RootElement;

        // Devolver el movimiento entero —y no sólo el id— es lo que permite a la pantalla
        // insertarlo en el listado sin volver a pedirlo (FR-014).
        Assert.True(creado.GetProperty("id").GetInt64() > 0);
        Assert.Equal("gasto", creado.GetProperty("tipo").GetString());
        Assert.Equal(1250.50m, creado.GetProperty("monto").GetDecimal());
        Assert.Equal(1, creado.GetProperty("categoriaId").GetInt32());
        Assert.Equal("Comida", creado.GetProperty("categoriaNombre").GetString());
        Assert.Equal("ARS", creado.GetProperty("monedaCodigo").GetString());
        Assert.Equal("2026-08-23", creado.GetProperty("fecha").GetString());

        // La fila existe y es de la cuenta que estaba en sesión (AC-07). FR-010: el propietario se
        // asigna en el INSERT, a mano, y no por un default del esquema. Antes del ticket 01a esto
        // comparaba contra una fila fija, que no podía distinguir "asignó bien" de "asignó siempre
        // lo mismo".
        await using var contexto = _baseDeDatos.CrearContexto();
        var fila = await contexto.Movimientos.SingleAsync();
        Assert.Equal(cuenta.Id, fila.UsuarioId);
        Assert.Equal(1250.50m, fila.Monto);
        Assert.Equal(new DateOnly(2026, 8, 23), fila.Fecha);
    }

    /// <summary>
    /// AC-17 (RF-12): completados monto y categoría sin tocar el campo fecha, el movimiento queda
    /// registrado con la fecha del día actual.
    ///
    /// El "hoy" se inyecta. Si el servidor leyera DateTime.Now, este test pasaría igual el día que
    /// corre y sería incapaz de detectar que lee mal — por eso la fecha inyectada es una que no es
    /// hoy.
    /// </summary>
    [Fact]
    public async Task Sin_Fecha_Queda_Registrado_Con_El_Hoy_Inyectado_AC17()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var hoyInyectado = new DateOnly(2026, 3, 7);
        using var factoria = CrearFactoria(hoyInyectado);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 100m, categoriaId = 1 });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("2026-03-07", json.RootElement.GetProperty("fecha").GetString());

        await using var contexto = _baseDeDatos.CrearContexto();
        var fila = await contexto.Movimientos.SingleAsync();
        Assert.Equal(hoyInyectado, fila.Fecha);
        Assert.NotEqual(DateOnly.FromDateTime(DateTime.Now), fila.Fecha);
    }

    [Fact]
    public async Task Fecha_Nula_Explicita_Tambien_Cae_En_El_Hoy_Inyectado_AC17()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = CrearFactoria(new DateOnly(2026, 3, 7));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        // El contrato dice "ausente o null": mandar null explícito no puede comportarse distinto.
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 100m, categoriaId = 1, fecha = (string?)null });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("2026-03-07", json.RootElement.GetProperty("fecha").GetString());
    }

    /// <summary>
    /// AC-16 (RF-11): un ingreso se registra igual que un gasto y queda marcado como ingreso. Si
    /// el tipo no se persistiera bien, el listado y el dashboard sumarían el ingreso como gasto.
    /// </summary>
    [Fact]
    public async Task Registra_Un_Ingreso_Y_La_Fila_Queda_Con_Tipo_Ingreso_AC16()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = CrearFactoria(new DateOnly(2026, 8, 23));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        // Categoría 8 = Sueldo, del catálogo de ingreso.
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "ingreso", monto = 50000m, categoriaId = 8, fecha = "2026-08-23" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("ingreso", json.RootElement.GetProperty("tipo").GetString());
        Assert.Equal("Sueldo", json.RootElement.GetProperty("categoriaNombre").GetString());

        await using var contexto = _baseDeDatos.CrearContexto();
        var fila = await contexto.Movimientos.SingleAsync();
        Assert.Equal(TipoMovimiento.Ingreso, fila.Tipo);
    }

    /// <summary>
    /// AC-07 (FR-010): el propietario del movimiento es la cuenta de la sesión, y no un valor fijo.
    ///
    /// Van DOS cuentas y no una a propósito. Con una sola, un endpoint que asignara siempre el
    /// mismo identificador —una constante, la primera fila de la tabla, la semilla que este ticket
    /// borró— pasaría el test igual: no hay con qué distinguir "asignó el de la sesión" de "asignó
    /// siempre lo mismo". Recién con dos cuentas registrando una cada una, las dos filas tienen
    /// que salir distintas.
    /// </summary>
    [Fact]
    public async Task El_Propietario_Es_La_Cuenta_De_La_Sesion_Y_No_Un_Valor_Fijo_AC07()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = CrearFactoria(new DateOnly(2026, 8, 23));

        using var ana = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var bruno = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        Assert.NotEqual(ana.Id, bruno.Id);

        var deAna = await RegistrarAsync(ana, 111.11m);
        var deBruno = await RegistrarAsync(bruno, 222.22m);

        await using var contexto = _baseDeDatos.CrearContexto();

        Assert.Equal(ana.Id, (await contexto.Movimientos.SingleAsync(m => m.Id == deAna)).UsuarioId);
        Assert.Equal(
            bruno.Id,
            (await contexto.Movimientos.SingleAsync(m => m.Id == deBruno)).UsuarioId);
    }

    /// <summary>Registra un gasto por la API y devuelve el id del movimiento creado.</summary>
    private static async Task<long> RegistrarAsync(CuentaDePrueba cuenta, decimal monto)
    {
        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto, categoriaId = 1, fecha = "2026-08-23" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt64();
    }

    private static FactoriaConReloj CrearFactoria(DateOnly hoy) => new(hoy);
}

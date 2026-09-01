using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Los filtros del listado (RF-17 y RF-18, historia 3 del ticket FEAT-001b).
///
/// Todo lo de acá depende de fechas, así que el reloj va clavado sin excepción. No es celo: el
/// listado sin filtros recorta al mes en curso **del servidor**, y los casos de rango se escriben
/// naturalmente con "hoy" — un test así pasa todos los días y falla el 1° de mes sin que nada haya
/// cambiado (Principio IV).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class FiltrosDelListadoTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 15);

    /// <summary>Tres fechas del mes en curso, y una de otro mes.</summary>
    private static readonly DateOnly Temprano = new(2026, 8, 5);
    private static readonly DateOnly Medio = new(2026, 8, 15);
    private static readonly DateOnly Tarde = new(2026, 8, 25);
    private static readonly DateOnly OtroMes = new(2026, 5, 20);

    private const int Comida = 1;
    private const int Transporte = 2;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-13 (PRD AC-25) y SC-006: sin filtros, el listado devuelve **el mes en curso del
    /// servidor**, exactamente como antes de esta feature.
    ///
    /// Es la prueba de regresión de la historia: agregar filtros no puede cambiar lo que ve quien
    /// no pide ninguno. Si esto se rompe, el comportamiento por omisión pasó a decidirlo el
    /// cliente, que es lo que FR-013 prohíbe.
    /// </summary>
    [Fact]
    public async Task Sin_Filtros_El_Listado_Sigue_Siendo_El_Mes_En_Curso_AC13()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var enElMes = await RegistrarAsync(cuenta, 100m, Comida, Medio);
        var tambienEnElMes = await RegistrarAsync(cuenta, 200m, Comida, Temprano);
        await RegistrarAsync(cuenta, 300m, Comida, OtroMes);

        var ids = await IdsAsync(cuenta);

        Assert.Equal(2, ids.Count);
        Assert.Contains(enElMes, ids);
        Assert.Contains(tambienEnElMes, ids);
    }

    /// <summary>
    /// AC-14 (PRD AC-26): el rango incluye **sus dos extremos**.
    ///
    /// El caso es un rango de **un solo día** que contiene un movimiento. Que el resultado no sea
    /// vacío es lo que prueba la inclusión: con un rango amplio, una implementación que excluyera
    /// los extremos pasaría en verde igual.
    /// </summary>
    [Fact]
    public async Task El_Rango_Incluye_Sus_Dos_Extremos_AC14()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var soloEseDia = await RegistrarAsync(cuenta, 100m, Comida, Temprano);
        await RegistrarAsync(cuenta, 200m, Comida, Tarde);

        // Un solo día: desde y hasta son la misma fecha, y el movimiento cae justo ahí.
        Assert.Equal([soloEseDia], await IdsAsync(cuenta, desde: Temprano, hasta: Temprano));

        // Y un rango cuyos extremos son exactamente las dos fechas trae las dos.
        Assert.Equal(2, (await IdsAsync(cuenta, desde: Temprano, hasta: Tarde)).Count);

        // Un día antes del primero y un día después del último: los extremos quedan afuera.
        Assert.Empty(await IdsAsync(
            cuenta, desde: Temprano.AddDays(-2), hasta: Temprano.AddDays(-1)));
    }

    /// <summary>AC-11 y AC-12 (PRD AC-23 y AC-24): el filtro por categoría.</summary>
    [Fact]
    public async Task El_Filtro_Por_Categoria_Devuelve_Solo_Esa_Y_Sin_Filtro_Todas_AC11_AC12()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deComida = await RegistrarAsync(cuenta, 100m, Comida, Medio);
        var deTransporte = await RegistrarAsync(cuenta, 200m, Transporte, Medio);

        Assert.Equal([deComida], await IdsAsync(cuenta, categoriaId: Comida));
        Assert.Equal([deTransporte], await IdsAsync(cuenta, categoriaId: Transporte));

        var sinFiltro = await IdsAsync(cuenta);
        Assert.Contains(deComida, sinFiltro);
        Assert.Contains(deTransporte, sinFiltro);
    }

    /// <summary>
    /// AC-15: los filtros se combinan con **y**, no con **o**.
    ///
    /// Se siembra a propósito un movimiento que cumple **sólo una** de las dos condiciones — la
    /// categoría correcta pero fuera del rango. Sin él, una implementación que combinara con `or`
    /// devolvería lo mismo que una correcta y el test pasaría en verde sin verificar nada.
    /// </summary>
    [Fact]
    public async Task Los_Filtros_Se_Combinan_Con_Y_No_Con_O_AC15()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var cumpleLasDos = await RegistrarAsync(cuenta, 100m, Comida, Medio);

        // Cumple sólo la categoría: misma categoría, fuera del rango.
        await RegistrarAsync(cuenta, 200m, Comida, Tarde);

        // Cumple sólo el rango: dentro del rango, otra categoría.
        await RegistrarAsync(cuenta, 300m, Transporte, Medio);

        var resultado = await IdsAsync(
            cuenta, desde: Temprano, hasta: Medio, categoriaId: Comida);

        Assert.Equal([cumpleLasDos], resultado);
    }

    /// <summary>
    /// AC-16: un filtro que no deja pasar nada devuelve una lista vacía, **no** un `404`.
    ///
    /// Es coherente con lo que el listado ya hacía ante un mes sin movimientos (FR-012 de la
    /// feature 001): "no hay nada que mostrar" no es "el recurso no existe".
    /// </summary>
    [Fact]
    public async Task Un_Filtro_Sin_Resultados_Devuelve_Lista_Vacia_Y_No_404_AC16()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, 100m, Comida, Medio);

        var (estado, cuerpo) = await CrudoAsync(
            cuenta, "?desde=2020-01-01&hasta=2020-01-31");

        Assert.Equal(HttpStatusCode.OK, estado);

        using var json = JsonDocument.Parse(cuerpo);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(0, json.RootElement.GetArrayLength());
    }

    /// <summary>
    /// AC-17: filtrar por una categoría que no existe devuelve una lista vacía, no un `400`.
    ///
    /// Rechazarla confirmaría cuáles existen, que es la misma fuga que el `404` uniforme cierra en
    /// las rutas por identificador. Un filtro no tiene por qué ser un oráculo del catálogo.
    /// </summary>
    [Fact]
    public async Task Filtrar_Por_Una_Categoria_Inexistente_No_Revela_Nada_AC17()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, 100m, Comida, Medio);

        var (inexistente, cuerpoInexistente) = await CrudoAsync(cuenta, "?categoriaId=999999");
        Assert.Equal(HttpStatusCode.OK, inexistente);

        using (var json = JsonDocument.Parse(cuerpoInexistente))
        {
            Assert.Equal(0, json.RootElement.GetArrayLength());
        }

        // Y una que existe pero sin movimientos propios responde exactamente igual: el resultado no
        // permite distinguir "esa categoría no existe" de "no tenés nada ahí".
        var (existente, cuerpoExistente) = await CrudoAsync(cuenta, $"?categoriaId={Transporte}");
        Assert.Equal(HttpStatusCode.OK, existente);
        Assert.Equal(cuerpoInexistente, cuerpoExistente);
    }

    /// <summary>
    /// FR-015: un rango imposible se rechaza, en vez de devolver una lista vacía.
    ///
    /// La lista vacía está prohibida acá justamente porque **se lee como "no hay nada"**: quien
    /// invirtió los extremos por error concluiría que no tiene movimientos, en vez de enterarse de
    /// que preguntó mal.
    ///
    /// Medio rango también se rechaza. Suponer un extremo abierto que nadie declaró es un supuesto
    /// distinto para cada quien.
    /// </summary>
    [Theory]
    [InlineData("?desde=2026-12-31&hasta=2026-01-01", "rango invertido")]
    [InlineData("?desde=2026-08-01", "sólo desde")]
    [InlineData("?hasta=2026-08-31", "sólo hasta")]
    public async Task Un_Rango_Imposible_O_A_Medias_Se_Rechaza_FR015(string consulta, string caso)
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await RegistrarAsync(cuenta, 100m, Comida, Medio);

        var (estado, _) = await CrudoAsync(cuenta, consulta);

        Assert.True(
            estado == HttpStatusCode.BadRequest,
            $"El caso «{caso}» respondió {(int)estado} en vez de 400. Devolver una lista vacía acá " +
            "se lee como «no hay nada» y esconde que la pregunta estaba mal formada.");
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private static async Task<(HttpStatusCode Estado, string Cuerpo)> CrudoAsync(
        CuentaDePrueba cuenta, string consulta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/movimientos" + consulta, UriKind.Relative));

        return (respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<List<long>> IdsAsync(
        CuentaDePrueba cuenta, DateOnly? desde = null, DateOnly? hasta = null, int? categoriaId = null)
    {
        var partes = new List<string>();
        if (desde is { } d)
        {
            partes.Add("desde=" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (hasta is { } h)
        {
            partes.Add("hasta=" + h.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (categoriaId is { } c)
        {
            partes.Add("categoriaId=" + c.ToString(CultureInfo.InvariantCulture));
        }

        var consulta = partes.Count == 0 ? string.Empty : "?" + string.Join("&", partes);
        var (estado, cuerpo) = await CrudoAsync(cuenta, consulta);

        Assert.Equal(HttpStatusCode.OK, estado);

        using var json = JsonDocument.Parse(cuerpo);
        return [.. json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt64())];
    }

    private static async Task<long> RegistrarAsync(
        CuentaDePrueba cuenta, decimal monto, int categoriaId, DateOnly fecha)
    {
        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new
            {
                tipo = "gasto",
                monto,
                categoriaId,
                fecha = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt64();
    }
}

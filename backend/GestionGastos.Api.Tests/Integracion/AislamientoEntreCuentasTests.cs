using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El aislamiento entre cuentas, verificado con dos cuentas reales (RF-04, ticket 01c).
///
/// Todo lo que se verifica acá **ya funcionaba** antes de esta suite: el listado siempre acotó por
/// cuenta y el alta siempre asignó el propietario desde la sesión. Nadie lo había comprobado nunca
/// con dos cuentas, que es otra cosa.
///
/// Eso hace que un verde acá no signifique gran cosa por sí solo: un test de aislamiento roto se ve
/// exactamente igual que uno que funciona. Lo que le da valor es haberle visto el rojo —quitando el
/// acotado de la consulta, o cambiando el propietario que asigna el alta—, y que
/// `backend/verificar-aislamiento.sh` lo vuelva a comprobar en cada corrida.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class AislamientoEntreCuentasTests(BaseDeDatosFixture baseDeDatos)
{
    /// <summary>
    /// El día en que queda clavado el reloj. El listado recorta al mes en curso del servidor y ese
    /// recorte no es un parámetro, así que sin reloj fijo un escenario sembrado "hoy" cruza el fin
    /// de mes y falla el día 1 sin que nada haya cambiado (Principio IV).
    /// </summary>
    private static readonly DateOnly Hoy = new(2026, 8, 15);

    /// <summary>
    /// La fecha es LA MISMA para las dos cuentas, a propósito, y la categoría también.
    ///
    /// Si difirieran, el aislamiento lo podría estar haciendo la casualidad —el recorte del mes, o
    /// cualquier otra condición de la consulta— y el test no distinguiría un caso del otro. Lo
    /// único que separa un movimiento de otro acá es el dueño, que es exactamente lo que se
    /// verifica.
    /// </summary>
    private static readonly DateOnly FechaCompartida = new(2026, 8, 10);

    private const int CategoriaCompartida = 1;

    /// <summary>Los montos sí difieren: es lo que permite señalar cuál es cuál al leer un fallo.</summary>
    private const decimal MontoDeA = 111m;
    private const decimal MontoDeB = 222m;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-01 (FR-001): con dos cuentas que tienen movimientos propios en el mes en curso, el
    /// listado de cada una devuelve únicamente los suyos.
    ///
    /// Se compara por **identificador** y no por cantidad: dos listados de largo 1 son igual de
    /// largos aunque el movimiento sea el equivocado, y ése es justo el fallo que hay que atrapar.
    /// </summary>
    [Fact]
    public async Task El_Listado_De_Cada_Cuenta_Trae_Solo_Lo_Suyo_AC01()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();

        using var a = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var b = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var (deA, deB) = await SembrarEnLasDosAsync(a, b);

        Assert.Equal([deA], await IdsDelListadoAsync(a));
        Assert.Equal([deB], await IdsDelListadoAsync(b));
    }

    /// <summary>
    /// FR-001: una cuenta sin movimientos propios recibe un arreglo vacío, aunque la otra tenga
    /// varios en el mismo mes.
    ///
    /// Es el caso que distingue "acota por cuenta" de "devuelve lo que haya": sin él, una consulta
    /// que ignorara el dueño pasaría igual mientras las dos cuentas tuvieran la misma cantidad de
    /// movimientos.
    /// </summary>
    [Fact]
    public async Task Una_Cuenta_Sin_Movimientos_Propios_Recibe_Un_Listado_Vacio()
    {
        using var factoria = new FactoriaConReloj(Hoy);
        await _baseDeDatos.LimpiarCuentasAsync();

        using var conMovimientos = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var sinMovimientos = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        Assert.NotEqual(conMovimientos.Id, sinMovimientos.Id);

        await RegistrarAsync(conMovimientos, MontoDeA);
        await RegistrarAsync(conMovimientos, MontoDeB);

        Assert.Equal(2, (await ListadoAsync(conMovimientos)).Count);
        Assert.Empty(await ListadoAsync(sinMovimientos));
    }

    /// <summary>
    /// Siembra un movimiento en cada cuenta y comprueba las dos condiciones sin las cuales esta
    /// suite entera pasaría en verde sin verificar nada.
    ///
    /// No es celo de más: si el fixture reusara una cuenta, o si una de las dos quedara sin
    /// movimientos, comparar los dos listados daría el resultado esperado por el motivo equivocado.
    /// Es el riesgo que el PRD nombra —"un test de aislamiento puede dar verde sin probar nada"— y
    /// se cierra acá o no se cierra en ningún lado.
    /// </summary>
    private static async Task<(long DeA, long DeB)> SembrarEnLasDosAsync(
        CuentaDePrueba a, CuentaDePrueba b)
    {
        // 1 · Son realmente dos cuentas.
        Assert.NotEqual(a.Id, b.Id);

        var deA = await RegistrarAsync(a, MontoDeA);
        var deB = await RegistrarAsync(b, MontoDeB);

        // 2 · Las dos tienen movimientos propios. Un listado ajeno vacío hace pasar cualquier
        //     comparación de aislamiento.
        Assert.NotEmpty(await ListadoAsync(a));
        Assert.NotEmpty(await ListadoAsync(b));

        return (deA, deB);
    }

    /// <summary>
    /// Registra un movimiento por la API, con el cliente de esa cuenta.
    ///
    /// Por la API y no sembrando en la base: es el camino que recorre una persona, y es el único en
    /// el que el propietario lo decide la sesión — que es la mitad de lo que esta suite verifica.
    /// </summary>
    /// <param name="propietarioEnElCuerpo">
    /// Si viene, se agrega un campo `usuarioId` al cuerpo. El contrato del alta no lo tiene, así
    /// que hoy se descarta al deserializar; el escenario lo manda igual para que el test siga
    /// valiendo el día que <c>NuevoMovimientoDto</c> gane un campo.
    /// </param>
    private static async Task<long> RegistrarAsync(
        CuentaDePrueba cuenta, decimal monto, long? propietarioEnElCuerpo = null)
    {
        var fecha = FechaCompartida.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        object cuerpo = propietarioEnElCuerpo is { } ajeno
            ? new { tipo = "gasto", monto, categoriaId = CategoriaCompartida, fecha, usuarioId = ajeno }
            : new { tipo = "gasto", monto, categoriaId = CategoriaCompartida, fecha };

        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative), cuerpo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt64();
    }

    private static async Task<List<JsonElement>> ListadoAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(
            new Uri("/api/movimientos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        // Clone(): los JsonElement mueren con su JsonDocument, y éste se libera al salir del
        // método. Sin la copia, el que lea el resultado se come un ObjectDisposedException.
        return [.. json.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    private static async Task<List<long>> IdsDelListadoAsync(CuentaDePrueba cuenta) =>
        [.. (await ListadoAsync(cuenta)).Select(m => m.GetProperty("id").GetInt64())];
}

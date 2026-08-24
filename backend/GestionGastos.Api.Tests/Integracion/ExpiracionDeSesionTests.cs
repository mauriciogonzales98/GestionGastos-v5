using System.Net;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// AC-12 (NFR-02): una sesión que pasa más de 24 h **sin actividad** deja de valer.
///
/// El reloj se adelanta; no se espera. Un test que esperara 24 h no correría nunca, y uno que
/// midiera tiempo real sería intermitente — las dos cosas que el Principio IV prohíbe.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ExpiracionDeSesionTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Tras_24h_Sin_Actividad_La_Sesion_Deja_De_Valer_AC12()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using (var antes = await cuenta.Cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        factoria.Reloj.Avanzar(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        using var despues = await cuenta.Cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    /// <summary>
    /// El caso complementario, y no es un adorno: sin él, una expiración rota "hacia el otro lado"
    /// —una que caduca todo apenas empieza— pasaría en verde con el test de arriba solo.
    ///
    /// Además verifica lo que NFR-02 pide de verdad: la ventana se cuenta desde la última
    /// actividad, no desde el inicio de sesión. Con actividad en el medio, la sesión sobrevive más
    /// de 24 h desde que se abrió.
    /// </summary>
    [Fact]
    public async Task Con_Actividad_Dentro_De_La_Ventana_La_Sesion_Sigue_Valiendo_AC12()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        // Tres tramos de 20 h con una petición en el medio: 60 h desde el login, ninguna ventana
        // de 24 h sin actividad.
        for (var tramo = 0; tramo < 3; tramo++)
        {
            factoria.Reloj.Avanzar(TimeSpan.FromHours(20));

            using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }
}

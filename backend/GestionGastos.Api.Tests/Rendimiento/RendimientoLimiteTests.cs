using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using GestionGastos.Api.Sesion;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Rendimiento;

/// <summary>
/// AC-12 (NFR-02): comprobar el límite de intentos agrega a lo sumo 50 ms al inicio de sesión, en
/// el percentil 95 sobre 100 ejecuciones.
///
/// Esta suite mide tiempo de pared, así que el CI la excluye: en un runner compartido da rojos que
/// no dicen nada del código. Corre en local, que es donde la medición significa algo.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class RendimientoLimiteTests(BaseDeDatosFixture baseDeDatos)
{
    private const int Ejecuciones = 100;
    private const double ToleranciaMs = 50;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// Se mide **lo que la comprobación agrega**, que es su consulta más su escritura, y no el
    /// login entero contra un login sin límite.
    ///
    /// El AC está redactado como una comparación con y sin la comprobación activa, y mantener una
    /// segunda versión del endpoint sin límite sólo para poder medirla sería código de producción
    /// escrito para un test. Medir el costo agregado responde exactamente la misma pregunta: es la
    /// diferencia entre los dos endpoints, aislada. Se mide el caso **peor** —consulta y escritura,
    /// que es el del intento fallido—; el del login exitoso sólo consulta.
    /// </summary>
    [Fact]
    public async Task El_P95_De_La_Comprobacion_Agrega_Menos_De_Cincuenta_Milisegundos_AC12()
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        var limite = new LimiteDeIntentos(contexto, TimeProvider.System);
        var email = $"rendimiento-{Guid.NewGuid():N}@ejemplo.com";

        // Calentamiento fuera de la medición. Diez y no una: además del plan de consulta, hay que
        // dejar asentado el pool de conexiones, que llega caliente de lo que haya corrido antes en
        // esta misma colección —el test de AC-13 son 200 logins con bcrypt— y contamina las
        // primeras muestras con contención que no es del código medido.
        for (var i = 0; i < 10; i++)
        {
            await MedirAsync(limite, email);
        }

        var muestras = new List<double>(Ejecuciones);
        for (var i = 0; i < Ejecuciones; i++)
        {
            muestras.Add(await MedirAsync(limite, email));
        }

        await _baseDeDatos.LimpiarIntentosDeAccesoAsync();

        muestras.Sort();
        var p95 = muestras[(int)Math.Ceiling(0.95 * muestras.Count) - 1];

        Assert.True(
            p95 < ToleranciaMs,
            $"AC-12: comprobar el límite agregó {p95:F1} ms en el p95 sobre {Ejecuciones} " +
            $"ejecuciones, y el criterio admite hasta {ToleranciaMs:F0} ms. " +
            $"Mediana {muestras[muestras.Count / 2]:F1} ms, máximo {muestras[^1]:F1} ms.");
    }

    /// <summary>
    /// AC-13 (NFR-03): rechazar un intento sobre un email bloqueado tarda lo mismo que rechazarlo
    /// por credenciales incorrectas, dentro de 50 ms en el p95.
    ///
    /// Si el bloqueo respondiera antes de verificar ningún hash, volvería en ~2 ms contra los
    /// ~100 ms del otro camino, y esa diferencia dice con un cronómetro qué emails acumularon cinco
    /// fallos. Su mitad determinista —que el camino bloqueado ejecuta la verificación— vive en
    /// `Integracion/LimiteDeIntentosTests` y sí corre en el CI.
    /// </summary>
    [Fact]
    public async Task El_Rechazo_Por_Bloqueo_Tarda_Lo_Mismo_Que_El_De_Credenciales_AC13()
    {
        const string Contrasena = "una frase larga y buena";
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 26));
        using var cliente = factoria.CreateClient();

        var conCredencialesMal = await CrearCuentaAsync(cliente, Contrasena);
        var bloqueado = await CrearCuentaAsync(cliente, Contrasena);

        // El segundo email se deja bloqueado antes de medir. Los intentos rechazados por el bloqueo
        // no tocan la fila, así que sigue bloqueado durante las 100 mediciones.
        for (var i = 0; i < LimiteDeIntentos.MaximoDeFallos; i++)
        {
            await IntentarAsync(cliente, bloqueado, "la que no es");
        }

        // Calentamiento fuera de la medición.
        await IntentarAsync(cliente, conCredencialesMal, "la que no es");
        await BorrarContadorAsync(conCredencialesMal);

        var porCredenciales = new List<double>(Ejecuciones);
        var porBloqueo = new List<double>(Ejecuciones);

        // Los dos caminos se miden INTERCALADOS, no uno después del otro. Medirlos en dos tandas
        // los expone a condiciones distintas de la máquina —otra suite terminando, el disco
        // ocupado—, y esa deriva aparece como una diferencia entre caminos que no existe. Ya pasó:
        // en tandas separadas, el mismo código dio 121 ms contra 615 ms, y aislado, 120 contra 130.
        for (var i = 0; i < Ejecuciones; i++)
        {
            // Contraseña incorrecta sobre un email que NUNCA llega a bloquearse: su contador se
            // borra después de cada intento, o al sexto estaría midiendo el otro camino.
            porCredenciales.Add(await CronometrarAsync(cliente, conCredencialesMal, "la que no es"));
            await BorrarContadorAsync(conCredencialesMal);

            // Email bloqueado, y con la contraseña CORRECTA.
            porBloqueo.Add(await CronometrarAsync(cliente, bloqueado, Contrasena));
        }

        await _baseDeDatos.LimpiarIntentosDeAccesoAsync();

        var p95Credenciales = P95(porCredenciales);
        var p95Bloqueo = P95(porBloqueo);
        var diferencia = Math.Abs(p95Bloqueo - p95Credenciales);

        Assert.True(
            diferencia <= ToleranciaMs,
            $"AC-13: el rechazo por bloqueo tuvo un p95 de {p95Bloqueo:F0} ms y el rechazo por " +
            $"credenciales incorrectas {p95Credenciales:F0} ms sobre {Ejecuciones} ejecuciones. " +
            $"La diferencia es {diferencia:F0} ms y el criterio admite hasta {ToleranciaMs:F0} ms: " +
            "con esa diferencia, un cronómetro distingue un email bloqueado de uno que no lo está.");
    }

    private static double P95(List<double> muestras)
    {
        muestras.Sort();
        return muestras[(int)Math.Ceiling(0.95 * muestras.Count) - 1];
    }

    private static async Task<string> CrearCuentaAsync(HttpClient cliente, string contrasena)
    {
        var email = $"rendimiento-{Guid.NewGuid():N}@ejemplo.com";

        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return email;
    }

    private static async Task IntentarAsync(HttpClient cliente, string email, string contrasena)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private static async Task<double> CronometrarAsync(
        HttpClient cliente, string email, string contrasena)
    {
        var cronometro = Stopwatch.StartNew();
        await IntentarAsync(cliente, email, contrasena);
        cronometro.Stop();

        return cronometro.Elapsed.TotalMilliseconds;
    }

    private async Task BorrarContadorAsync(string email)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        await contexto.IntentosDeAcceso.Where(i => i.Email == email).ExecuteDeleteAsync();
    }

    private static async Task<double> MedirAsync(LimiteDeIntentos limite, string email)
    {
        var cronometro = Stopwatch.StartNew();
        await limite.EstaBloqueadoAsync(email);
        await limite.RegistrarFalloAsync(email);
        cronometro.Stop();

        return cronometro.Elapsed.TotalMilliseconds;
    }
}

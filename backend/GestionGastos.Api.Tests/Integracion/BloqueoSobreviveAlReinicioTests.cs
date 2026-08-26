using System.Net;
using System.Net.Http.Json;
using GestionGastos.Api.Sesion;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// AC-11 (NFR-01): el bloqueo sigue en pie después de reiniciar la aplicación.
///
/// Lo que se verifica es que el estado **no vive en el proceso**. Se descarta la aplicación y se
/// levanta otra sobre la misma base: todo lo que estuviera en memoria se perdió, y el email tiene
/// que seguir rechazado. Reiniciar un proceso de verdad sería lento, dependiente del entorno e
/// intermitente, y el Principio IV lo prohíbe.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class BloqueoSobreviveAlReinicioTests(BaseDeDatosFixture baseDeDatos)
{
    private const string Contrasena = "una frase larga y buena";
    private static readonly DateOnly Hoy = new(2026, 8, 26);
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task El_Email_Sigue_Bloqueado_Despues_De_Reiniciar_AC11()
    {
        var email = $"reinicio-{Guid.NewGuid():N}@ejemplo.com";

        using (var antes = new FactoriaConReloj(Hoy))
        {
            using var cliente = antes.CreateClient();

            using (var alta = await cliente.PostAsJsonAsync(
                new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena }))
            {
                Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
            }

            await _baseDeDatos.LimpiarIntentosDeAccesoAsync();

            for (var i = 0; i < LimiteDeIntentos.MaximoDeFallos; i++)
            {
                using var fallo = await cliente.PostAsJsonAsync(
                    new Uri("/api/sesion", UriKind.Relative),
                    new { email, contrasena = "esta no es la contraseña" });

                Assert.Equal(HttpStatusCode.Unauthorized, fallo.StatusCode);
            }
        }

        // La aplicación nueva arranca con el reloj en el MISMO instante que la anterior. Si
        // arrancara en el instante real, la ventana podría aparecer vencida —o eterna— por el salto
        // del reloj y no por lo que se está probando (research.md D-07).
        using var despues = new FactoriaConReloj(Hoy);
        using var otroCliente = despues.CreateClient();

        using var intento = await otroCliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
    }
}

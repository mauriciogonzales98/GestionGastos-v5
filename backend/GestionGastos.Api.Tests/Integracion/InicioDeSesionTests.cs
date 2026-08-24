using System.Net;
using System.Net.Http.Json;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// AC-03 (FR-03): con email y contraseña correctos, la sesión queda iniciada.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class InicioDeSesionTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Credenciales_Correctas_Inician_La_Sesion_AC03()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = $"acceso-{Guid.NewGuid():N}@ejemplo.com";
        const string Contrasena = "una frase larga y buena";

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        using var sesion = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);

        // La cookie tiene que venir en la respuesta: es lo que sostiene la sesión.
        Assert.True(
            sesion.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(),
            "El inicio de sesión no devolvió ninguna cookie.");

        // Y a partir de acá el servidor reconoce a esa cuenta.
        using var actual = await cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
    }
}

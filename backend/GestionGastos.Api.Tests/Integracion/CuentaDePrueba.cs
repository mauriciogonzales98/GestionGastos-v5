using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Una cuenta real con su sesión abierta, para los tests que antes se apoyaban en la fila semilla.
///
/// Desde el ticket 01a no hay ninguna fila fija: el propietario de un movimiento es la cuenta de la
/// sesión. Un test que quiera escribir movimientos necesita una cuenta de verdad, y ésta es la
/// forma de conseguirla sin duplicar el alta y el login en cada archivo.
/// </summary>
public sealed record CuentaDePrueba(long Id, string Email, HttpClient Cliente) : IDisposable
{
    private const string Contrasena = "una frase larga de prueba";

    /// <summary>
    /// Crea una cuenta por la API, inicia sesión y devuelve el cliente que ya lleva la cookie.
    ///
    /// Va por la API y no por la base a propósito: así el camino que ejercitan los tests es el
    /// mismo que recorre una persona, y una regresión en el alta o en el login se ve en toda la
    /// suite y no sólo en sus dos archivos.
    /// </summary>
    public static async Task<CuentaDePrueba> CrearYEntrarAsync(
        FactoriaConReloj factoria,
        BaseDeDatosFixture baseDeDatos)
    {
        var cliente = factoria.CreateClient();
        var email = $"prueba-{Guid.NewGuid():N}@ejemplo.com";

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        }

        using (var sesion = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
        }

        await using var contexto = baseDeDatos.CrearContexto();
        var id = (await contexto.Usuarios.SingleAsync(u => u.Email == email)).Id;

        return new CuentaDePrueba(id, email, cliente);
    }

    public void Dispose() => Cliente.Dispose();
}

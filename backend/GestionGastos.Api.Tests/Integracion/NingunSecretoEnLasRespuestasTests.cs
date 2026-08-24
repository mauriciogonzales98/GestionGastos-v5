using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// NFR-01: ni la contraseña ni su hash salen a la red, por ninguna respuesta.
///
/// Es una revisión que podría hacerse leyendo los DTOs, y por eso mismo se automatiza: leerlos hoy
/// no dice nada del día que alguien devuelva la entidad `Usuario` directamente para "ahorrarse un
/// DTO". Ese cambio compila, pasa el resto de la suite y publica el hash de todas las cuentas.
///
/// Se mira el cuerpo crudo y no un campo concreto: un campo nuevo con otro nombre —`clave`,
/// `verificador`, el `ContrasenaHash` de la entidad— se escapa de cualquier lista que se escriba
/// al lado.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class NingunSecretoEnLasRespuestasTests(BaseDeDatosFixture baseDeDatos)
{
    private const string Contrasena = "una frase larga que no debe salir";

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Ninguna_Respuesta_Trae_La_Contrasena_Ni_Su_Hash_NFR01()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        var email = $"secreto-{Guid.NewGuid():N}@ejemplo.com";

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        var cuerpos = new List<(string Donde, string Contenido)>();

        using (var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
            cuerpos.Add(("POST /api/cuentas", await alta.Content.ReadAsStringAsync()));
        }

        using (var entrada = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena = Contrasena }))
        {
            Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
            cuerpos.Add(("POST /api/sesion", await entrada.Content.ReadAsStringAsync()));
        }

        using (var consulta = await cliente.GetAsync(new Uri("/api/sesion", UriKind.Relative)))
        {
            cuerpos.Add(("GET /api/sesion", await consulta.Content.ReadAsStringAsync()));
        }

        // El rechazo también: es la respuesta que más tienta a explicar de más.
        using (var rechazo = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative), new { email, contrasena = "la que no era" }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, rechazo.StatusCode);
            cuerpos.Add(("POST /api/sesion rechazado", await rechazo.Content.ReadAsStringAsync()));
        }

        // El hash real de esta cuenta, tomado de la base: se busca ESE valor y no un patrón, así
        // que el test no depende de cómo se vea un hash de bcrypt.
        string hash;
        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            hash = (await contexto.Usuarios.SingleAsync(u => u.Email == email)).ContrasenaHash;
        }

        Assert.False(string.IsNullOrWhiteSpace(hash));

        foreach (var (donde, contenido) in cuerpos)
        {
            Assert.False(
                contenido.Contains(Contrasena, StringComparison.Ordinal),
                $"{donde} devolvió la contraseña en claro.");

            Assert.False(
                contenido.Contains(hash, StringComparison.Ordinal),
                $"{donde} devolvió el hash de la contraseña.");

            // El prefijo de bcrypt, por si algún día se devolviera el hash de OTRA cuenta: el
            // valor no coincidiría con el de arriba y la comparación anterior pasaría en verde.
            Assert.DoesNotContain("$2a$", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("$2b$", contenido, StringComparison.Ordinal);
        }
    }
}

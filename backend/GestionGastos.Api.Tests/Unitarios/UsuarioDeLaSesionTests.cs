using System.Globalization;
using System.Security.Claims;
using GestionGastos.Api.Persistencia;
using Microsoft.AspNetCore.Http;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// La red que hay debajo de la autorización (D-05).
///
/// La protección real es la autorización global de `Program.cs`, y por eso estos casos no se
/// alcanzan por HTTP: la petición se rechaza antes. Eso mismo los deja sin cubrir y sin verificar,
/// que es justo lo que no se quiere de una red de seguridad — se descubre que no estaba tendida el
/// día que hace falta. Acá se ejercitan directamente.
/// </summary>
public class UsuarioDeLaSesionTests
{
    [Fact]
    public void Devuelve_El_Id_Del_Claim_De_La_Sesion()
    {
        var usuario = new UsuarioDeLaSesion(AccesoCon(new Claim(ClaimTypes.NameIdentifier, "42")));

        Assert.Equal(42, usuario.Id);
    }

    /// <summary>
    /// Sin sesión lanza, en vez de devolver un valor por omisión.
    ///
    /// Un 0 o un null escribiría filas huérfanas —o peor, filas a nombre del usuario 0— en
    /// silencio, y el descuido recién se vería en la base semanas después. Así se convierte en un
    /// error visible en la primera petición.
    /// </summary>
    [Theory]
    [InlineData(null, "sin ningún claim")]
    [InlineData("", "con el claim vacío")]
    [InlineData("no-es-un-numero", "con un claim que no es un identificador")]
    public void Sin_Una_Sesion_Utilizable_Lanza(string? valorDelClaim, string caso)
    {
        var acceso = valorDelClaim is null
            ? AccesoCon()
            : AccesoCon(new Claim(ClaimTypes.NameIdentifier, valorDelClaim));

        var error = Assert.Throws<InvalidOperationException>(
            () => new UsuarioDeLaSesion(acceso).Id);

        // El mensaje dice qué revisar. Un "Object reference not set" no le sirve a nadie.
        Assert.True(
            error.Message.Contains("sin una sesión iniciada", StringComparison.Ordinal)
            && error.Message.Contains("autorización", StringComparison.Ordinal),
            $"El caso «{caso}» lanzó, pero con un mensaje que no dice qué revisar: {error.Message}");
    }

    /// <summary>
    /// Sin `HttpContext` tampoco devuelve un valor por omisión. Pasa fuera de una petición: un
    /// servicio en segundo plano que pidiera `IUsuarioActual` no tiene sesión de la que hablar.
    /// </summary>
    [Fact]
    public void Fuera_De_Una_Peticion_Tambien_Lanza()
    {
        var usuario = new UsuarioDeLaSesion(new HttpContextAccessor { HttpContext = null });

        Assert.Throws<InvalidOperationException>(() => usuario.Id);
    }

    private static HttpContextAccessor AccesoCon(params Claim[] claims)
    {
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba")),
        };

        return new HttpContextAccessor { HttpContext = contexto };
    }
}

using System.Net;
using System.Net.Http.Json;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Autorizacion;

/// <summary>
/// AC-05 (FR-04): sin sesión iniciada, una operación de la aplicación se deniega **y su efecto no
/// se ejecuta**.
///
/// Las dos mitades importan. Un `POST` que responde 401 y aun así inserta cumple el código de
/// estado y falla el requisito, y ningún test que mire sólo el código lo detectaría.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class SinSesionTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Sin_Sesion_El_Listado_Y_El_Catalogo_Responden_401_AC05()
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var movimientos = await cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, movimientos.StatusCode);

        using var categorias = await cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, categorias.StatusCode);
    }

    [Fact]
    public async Task Sin_Sesion_El_Alta_De_Movimiento_No_Ejecuta_Su_Efecto_AC05()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 100m, categoriaId = 1, fecha = "2026-08-24" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        // La mitad que un test perezoso se saltea.
        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Equal(0, await contexto.Movimientos.CountAsync());
    }

    [Fact]
    public async Task Los_Dos_Endpoints_De_Acceso_Siguen_Siendo_Alcanzables_Sin_Sesion()
    {
        // Si estos dos también exigieran sesión, no habría forma de obtener una. Es la razón por la
        // que las excepciones son explícitas y son exactamente dos.
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/cuentas", UriKind.Relative),
            new { email = $"acceso-{Guid.NewGuid():N}@ejemplo.com", contrasena = "una frase larga" });
        Assert.NotEqual(HttpStatusCode.Unauthorized, alta.StatusCode);

        using var sesion = await cliente.PostAsJsonAsync(
            new Uri("/api/sesion", UriKind.Relative),
            new { email = "nadie@ejemplo.com", contrasena = "cualquier cosa" });
        // 401 por credenciales, no por falta de sesión: lo que importa es que el endpoint respondió.
        Assert.Equal(HttpStatusCode.Unauthorized, sesion.StatusCode);
        Assert.Contains("incorrect", await sesion.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}

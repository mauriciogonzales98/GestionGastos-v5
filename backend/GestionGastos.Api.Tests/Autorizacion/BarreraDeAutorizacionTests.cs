using System.Net;
using System.Text;
using GestionGastos.Api.Tests.Integracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GestionGastos.Api.Tests.Autorizacion;

/// <summary>
/// La barrera del Principio V para la autorización: **ningún endpoint responde sin sesión**, salvo
/// los dos declarados.
///
/// Los endpoints se descubren del <see cref="EndpointDataSource"/> en tiempo de ejecución, no de
/// una lista escrita al lado. La diferencia no es de estilo: una lista a mano pasa en verde justo
/// el día que alguien agrega un endpoint desprotegido, que es el único día en que esta barrera
/// tendría que servir para algo.
/// </summary>
public class BarreraDeAutorizacionTests
{
    /// <summary>
    /// Los únicos endpoints que pueden responder sin sesión, y por qué: si también la exigieran, no
    /// habría forma de obtener una.
    ///
    /// Agregar algo acá es una decisión de seguridad. Que sea una constante visible, y no un
    /// atributo perdido en un archivo, es lo que la vuelve revisable.
    /// </summary>
    private static readonly HashSet<string> ExcepcionesDeclaradas = new(StringComparer.Ordinal)
    {
        "POST /api/cuentas",
        "POST /api/sesion",
        "DELETE /api/sesion",
    };

    [Fact]
    public void Todo_Endpoint_No_Declarado_Exige_Sesion()
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var _ = factoria.CreateClient();

        var sinProteger = new List<string>();

        foreach (var endpoint in EndpointsDeLaAplicacion(factoria))
        {
            var nombre = Describir(endpoint);
            var anonimo = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

            if (anonimo && !ExcepcionesDeclaradas.Contains(nombre))
            {
                sinProteger.Add(nombre);
            }
        }

        Assert.True(
            sinProteger.Count == 0,
            "Estos endpoints responden sin sesión y no están declarados como excepción:\n  " +
            string.Join("\n  ", sinProteger) +
            "\n\nSi es a propósito, agregalos a ExcepcionesDeclaradas y explicá por qué. Si no, " +
            "sacales el AllowAnonymous: la autorización es global y sólo se exceptúa a mano.");
    }

    /// <summary>
    /// La otra dirección: que cada excepción declarada corresponda a un endpoint real **y que ese
    /// endpoint sea efectivamente anónimo**.
    ///
    /// Las dos mitades importan, y la segunda se aprendió a los golpes. Comprobar sólo que la ruta
    /// existe deja pasar una excepción declarada sobre un endpoint que sí exige sesión: la lista
    /// dice que esa puerta está abierta, no lo está, y nadie se entera. Peor todavía, la
    /// autorización queda dada por adelantado — el día que alguien le agregue `AllowAnonymous` a
    /// ese endpoint, la barrera lo aprueba en silencio porque su nombre ya figuraba.
    /// </summary>
    [Fact]
    public void Toda_Excepcion_Declarada_Corresponde_A_Un_Endpoint_Anonimo_Real()
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var _ = factoria.CreateClient();

        var anonimos = EndpointsDeLaAplicacion(factoria)
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Describir)
            .ToHashSet(StringComparer.Ordinal);

        var sobran = ExcepcionesDeclaradas.Except(anonimos, StringComparer.Ordinal).ToList();

        Assert.True(
            sobran.Count == 0,
            $"Estas excepciones no corresponden a ningún endpoint anónimo: {string.Join(", ", sobran)}. " +
            "O el endpoint ya no existe, o existe y exige sesión: en los dos casos la lista dejó de " +
            "describir la aplicación y empezó a mentir sobre ella.");
    }

    [Fact]
    public async Task Un_Endpoint_Protegido_Responde_401_Y_No_Redirige()
    {
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 24));
        using var cliente = factoria.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));

        // 401 y no 302: esto es una API. Una redirección al login rompería al cliente, que usa el
        // 401 como señal de que la sesión venció.
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Null(respuesta.Headers.Location);
    }

    private static IEnumerable<RouteEndpoint> EndpointsDeLaAplicacion(FactoriaConReloj factoria) =>
        factoria.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>();

    private static string Describir(RouteEndpoint endpoint)
    {
        var metodos = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
        var texto = new StringBuilder();
        texto.Append(metodos.Count > 0 ? string.Join(",", metodos) : "ANY");
        texto.Append(' ');
        texto.Append('/');
        texto.Append(endpoint.RoutePattern.RawText?.TrimStart('/') ?? string.Empty);

        return texto.ToString();
    }
}

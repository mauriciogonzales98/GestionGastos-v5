using System.Net;
using System.Text.RegularExpressions;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Una respuesta reducida a lo que un tercero puede observar de ella: el código, el cuerpo y el
/// tipo de contenido. Es la unidad de comparación de <see cref="RespuestasIndistinguibles"/>.
/// </summary>
public readonly record struct RespuestaObservable(
    HttpStatusCode Estado,
    string Cuerpo,
    string? TipoDeContenido);

/// <summary>
/// La comprobación de que **un recurso ajeno responde igual que uno inexistente** (FR-008 de
/// FEAT-001b; AC-03, AC-04 y AC-05 del PRD del ticket 01c).
///
/// Vive acá y no dentro de una suite porque la usan la edición y la eliminación, y duplicarla es
/// exactamente cómo dos verificaciones de la misma propiedad terminan diciendo cosas distintas.
///
/// **Lo que se compara son dos respuestas entre sí, no cada una contra un `404` esperado.** Afirmar
/// `404` en las dos pasa en verde aunque los cuerpos difieran —"no existe" contra "no es tuyo"— y
/// el segundo confirma que ese identificador existe. Como los identificadores son autoincrementales
/// y contiguos, esa diferencia permite recorrerlos y contar los movimientos de otra cuenta sin ver
/// ninguno.
/// </summary>
public static class RespuestasIndistinguibles
{
    /// <summary>
    /// El cuerpo sin su <c>traceId</c>.
    ///
    /// `ProblemDetails` incluye un identificador de traza **por petición**, así que dos respuestas
    /// nunca son iguales byte a byte — ni siquiera dos peticiones idénticas al mismo identificador.
    /// Compararlo sería exigir algo imposible; ignorarlo sin más sería aflojar la comprobación.
    ///
    /// La propiedad a verificar no es "los cuerpos son idénticos" sino **"nada que dependa de la
    /// existencia difiere"**, y `traceId` no depende de nada: es aleatorio. Que lo sea está
    /// comprobado por un test, para que esta normalización no se apoye en una suposición.
    /// </summary>
    public static string SinTraza(string cuerpo) =>
        Regex.Replace(
            cuerpo,
            "\"traceId\"\\s*:\\s*\"[^\"]*\"",
            "\"traceId\":\"<volatil>\"",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

    /// <summary>
    /// Exige que la respuesta sobre un recurso ajeno sea indistinguible de la respuesta sobre uno
    /// inexistente.
    /// </summary>
    public static void Exigir(RespuestaObservable ajena, RespuestaObservable fantasma, string contexto)
    {
        Assert.Equal(HttpStatusCode.NotFound, ajena.Estado);

        Assert.True(
            ajena.Estado == fantasma.Estado
                && string.Equals(SinTraza(ajena.Cuerpo), SinTraza(fantasma.Cuerpo), StringComparison.Ordinal)
                && string.Equals(ajena.TipoDeContenido, fantasma.TipoDeContenido, StringComparison.Ordinal),
            $"Un recurso ajeno se distingue de uno inexistente ({contexto}).\n" +
            $"  ajeno       -> {(int)ajena.Estado} {ajena.TipoDeContenido} {ajena.Cuerpo}\n" +
            $"  inexistente -> {(int)fantasma.Estado} {fantasma.TipoDeContenido} {fantasma.Cuerpo}\n\n" +
            "Cualquier diferencia observable confirma que ese identificador existe, y como son " +
            "contiguos eso permite contar los movimientos de otra cuenta sin ver ninguno.");
    }
}

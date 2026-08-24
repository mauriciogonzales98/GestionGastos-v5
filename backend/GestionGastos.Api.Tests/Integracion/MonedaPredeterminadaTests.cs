using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// `data-model.md` declara la invariante: "Exactamente una fila en `1`" (RF-25). El alta la da por
/// cierta —toma la moneda predeterminada del catálogo— pero nada la sostenía: `es_predeterminada`
/// era un `bit(1)` con default `0` y ninguna restricción encima.
///
/// Una invariante escrita sólo en un documento no es una invariante.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class MonedaPredeterminadaTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task El_Catalogo_Sembrado_Tiene_Exactamente_Una_Predeterminada()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        var predeterminadas = await contexto.Monedas.CountAsync(m => m.EsPredeterminada);

        Assert.Equal(1, predeterminadas);
    }

    /// <summary>
    /// La base tiene que rechazar una segunda predeterminada. Sin esto, el alta elegiría una de las
    /// dos sin criterio y podría cambiar entre reinicios: los movimientos quedarían registrados en
    /// una moneda arbitraria y nadie se enteraría.
    /// </summary>
    [Fact]
    public async Task La_Base_Rechaza_Una_Segunda_Moneda_Predeterminada()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        contexto.Monedas.Add(new Moneda
        {
            Id = 900,
            Codigo = "EUR",
            Nombre = "Euro",
            Simbolo = "€",
            Decimales = 2,
            EsPredeterminada = true,
        });

        try
        {
            var falla = await Assert.ThrowsAnyAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
            Assert.NotNull(falla);

            // Y la fila no quedó: el rechazo es de la base, no un descarte silencioso de la
            // aplicación.
            await using var verificacion = _baseDeDatos.CrearContexto();
            Assert.Equal(1, await verificacion.Monedas.CountAsync(m => m.EsPredeterminada));
        }
        finally
        {
            // Si la restricción no estuviera, la fila entra y contamina al resto de la suite: el
            // test de al lado contaría dos predeterminadas y fallaría por culpa de éste, no por el
            // código. Un test que deja basura hace fallar a sus vecinos.
            await using var limpieza = _baseDeDatos.CrearContexto();
            await limpieza.Monedas.Where(m => m.Id == 900).ExecuteDeleteAsync();
        }
    }
}

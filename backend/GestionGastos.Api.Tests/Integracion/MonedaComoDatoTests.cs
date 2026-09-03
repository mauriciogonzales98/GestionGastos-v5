using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **Que sumar una moneda sea de verdad sólo un dato** (FR-001, `PRD:RF-32`).
///
/// El catálogo de monedas existe desde la migración `Inicial` y el resumen ya separa por moneda:
/// esta feature no construye nada de eso, lo **verifica**. La diferencia importa porque hasta acá
/// "se puede agregar una moneda sin tocar código" era una afirmación que nadie había ejecutado —
/// plausible leyendo el código, que es exactamente la clase de creencia que el Principio V de la
/// constitución existe para no aceptar.
///
/// `backend/verificar-monedas.sh` es la otra mitad: estos tests comprueban el **comportamiento**
/// —la moneda nueva aparece y se puede usar— y el script comprueba el **proceso** —que para eso no
/// hizo falta modificar ni recompilar nada—. Un test no puede sostener lo segundo: corre dentro de
/// un proceso que ya se compiló.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class MonedaComoDatoTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// **El canario de la suite.**
    ///
    /// `moneda` es una tabla que `LimpiarCuentasAsync` NO toca, y hasta esta feature eso estaba
    /// bien: ningún test creaba monedas, así que no había nada que limpiar. Estos tests sí las
    /// crean, y una que sobreviva se le queda al siguiente — que entonces falla por lo que hizo la
    /// corrida anterior y no por el código, que es la peor forma de rojo que hay.
    ///
    /// Este caso no verifica ningún requisito: verifica que los otros sean confiables.
    /// </summary>
    [Fact]
    public async Task El_Catalogo_Queda_Con_Las_Dos_Monedas_Sembradas()
    {
        await using var contexto = _baseDeDatos.CrearContexto();

        var codigos = await contexto.Monedas.OrderBy(m => m.Id).Select(m => m.Codigo).ToListAsync();

        Assert.Equal(["ARS", "USD"], codigos);
    }

    /// <summary>
    /// Agrega una moneda al catálogo, corre lo que se le pida con ella puesta, y **la borra pase lo
    /// que pase**.
    ///
    /// El `finally` no es celo: si el cuerpo falla, la moneda queda igual, y entonces el rojo que
    /// alguien va a leer mañana es el del canario y no el del test que de verdad falló. Un test que
    /// ensucia al fallar convierte un rojo legible en dos ilegibles.
    ///
    /// **La limpieza va acá y no en `LimpiarCuentasAsync`.** Ahí borraría las dos monedas sembradas
    /// para toda la suite —que la migración siembra una sola vez y media suite da por dadas—, que es
    /// el mismo error que ese método ya evita en categorías filtrando por `usuario_id != null`.
    /// </summary>
    private async Task ConLaMonedaAsync(string codigo, Func<Moneda, Task> cuerpo)
    {
        var moneda = new Moneda
        {
            Codigo = codigo,
            Nombre = $"Moneda de prueba {codigo}",
            Simbolo = codigo,
            Decimales = 2,
            EsPredeterminada = false,
        };

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            contexto.Monedas.Add(moneda);
            await contexto.SaveChangesAsync();
        }

        try
        {
            await cuerpo(moneda);
        }
        finally
        {
            await using var contexto = _baseDeDatos.CrearContexto();
            await contexto.Monedas.Where(m => m.Codigo == codigo).ExecuteDeleteAsync();
        }
    }

    // TEMPORAL — T004. El mismo caso de T003, ahora pasando por el helper: agrega y limpia.
    [Fact]
    public async Task Temporal_Ensucia_El_Catalogo() =>
        await ConLaMonedaAsync("EUR", _ => Task.CompletedTask);
}

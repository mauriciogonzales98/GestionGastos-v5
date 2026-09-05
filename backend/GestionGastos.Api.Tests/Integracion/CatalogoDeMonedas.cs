using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **Agregar una moneda al catálogo desde un test, y borrarla pase lo que pase.**
///
/// Nació privado dentro de `MonedaComoDatoTests` en la feature 008 y se extrajo acá en la 009,
/// cuando la misma necesidad apareció en tres archivos más. La extracción no cambió nada de lo que
/// hace: es el mismo cuerpo, con las mismas dos razones escritas.
///
/// **La regla que hay que respetar al usarlo** (D8-08 de la feature 008, D-10 de la 009):
/// **ningún test puede escribir un número fijo sobre el tamaño del catálogo.** Ni "hay dos
/// monedas", ni "la segunda es USD", ni un `monedaId = 2` a mano. `verificar-monedas.sh` corre la
/// suite **con una moneda de más puesta en la base**, así que cualquier literal sobre el tamaño
/// pasa en la suite y se rompe en la barrera. Ya pasó una vez, con el `2` escrito a mano de la 008,
/// y lo encontró el quickstart y no los tests. Todo lo que se afirme sobre el catálogo se compara
/// **contra el catálogo**.
/// </summary>
public static class CatalogoDeMonedas
{
    /// <summary>
    /// Agrega una moneda al catálogo, corre lo que se le pida con ella puesta, y **la borra pase lo
    /// que pase**.
    ///
    /// El `finally` no es celo: si el cuerpo falla, la moneda queda igual, y entonces el rojo que
    /// alguien va a leer mañana es el del canario y no el del test que de verdad falló. Un test que
    /// ensucia al fallar convierte un rojo legible en dos ilegibles.
    ///
    /// **La limpieza va acá y no en `LimpiarCuentasAsync`.** Ahí borraría las monedas sembradas
    /// para toda la suite —que la migración siembra una sola vez y media suite da por dadas—, que es
    /// el mismo error que ese método ya evita en categorías filtrando por `usuario_id != null`.
    /// </summary>
    public static async Task ConLaMonedaAsync(
        BaseDeDatosFixture baseDeDatos,
        string codigo,
        Func<Moneda, Task> cuerpo)
    {
        var moneda = new Moneda
        {
            Codigo = codigo,
            Nombre = $"Moneda de prueba {codigo}",
            Simbolo = codigo,
            Decimales = 2,
            EsPredeterminada = false,
        };

        await using (var contexto = baseDeDatos.CrearContexto())
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
            await using var contexto = baseDeDatos.CrearContexto();

            // **Los movimientos primero.** `movimiento.moneda_id` es una clave foránea RESTRICT, así
            // que borrar la moneda con un movimiento apuntándola falla — y falla DENTRO del
            // `finally`, que es el peor lugar: la moneda queda, el rojo que se lee es el del canario
            // de la corrida siguiente, y la causa no aparece en ninguno de los dos. Es el mismo
            // orden que `LimpiarCuentasAsync` documenta para cuentas y categorías, por el mismo
            // motivo.
            await contexto.Movimientos.Where(m => m.Moneda!.Codigo == codigo).ExecuteDeleteAsync();
            await contexto.Monedas.Where(m => m.Codigo == codigo).ExecuteDeleteAsync();
        }
    }
}

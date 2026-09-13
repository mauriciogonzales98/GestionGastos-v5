using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Rompe el catálogo inicial justo antes de guardarlo: le pone a dos de las diez categorías el
/// mismo nombre y el mismo tipo, para que el índice único las rechace con un <c>1062</c>.
///
/// **Existe para un solo test, y ese test cubre un riesgo concreto** (research D-07 de la feature
/// 013): el alta atrapa el <c>1062</c> del email duplicado y responde igual que un alta exitosa, y
/// desde esta feature ese mismo <c>SaveChanges</c> escribe además diez categorías. Un
/// <c>catch</c> que mire sólo el número de error atraparía también el choque de categorías y lo
/// haría pasar por "email ya registrado": la cuenta no se crearía, la respuesta diría que quizá sí,
/// y nadie se enteraría nunca.
///
/// El choque se fabrica acá y no con datos porque no hay forma de provocarlo desde afuera: las diez
/// nacen con nombres distintos y con una cuenta que todavía no tiene identificador, así que ninguna
/// fila preexistente puede colisionar con ellas.
/// </summary>
public sealed class ChocaElCatalogoAntesDeGuardar : SaveChangesInterceptor
{
    /// <summary>
    /// <c>true</c> si llegó a provocar el choque. El test lo comprueba: un interceptor inerte deja
    /// el test en verde sin haber ejercitado nada.
    /// </summary>
    public bool Intervino { get; private set; }

    // Los nombres de los parámetros están en inglés y no en castellano como el resto del proyecto:
    // CA1725 exige que un override conserve los del método base, y el build corre con -warnaserror.
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var nuevas = eventData.Context?.ChangeTracker.Entries<Categoria>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        if (!Intervino && nuevas is { Count: >= 2 })
        {
            Intervino = true;

            // La segunda pasa a ser un calco de la primera: mismo nombre, mismo tipo, mismo ámbito
            // y mismo discriminador. Es exactamente la clave que `ux_categoria_ambito_nombre_tipo`
            // prohíbe repetir.
            nuevas[1].Nombre = nuevas[0].Nombre;
            nuevas[1].Tipo = nuevas[0].Tipo;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

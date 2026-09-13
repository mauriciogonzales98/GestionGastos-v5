using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Hace fallar el guardado con un <c>DbUpdateException</c> que lleva un <c>1062</c> **de verdad** y
/// **ninguna entrada**.
///
/// Existe para un solo test, y el caso que fabrica es el que ninguna escritura normal produce: EF
/// completa <c>DbUpdateException.Entries</c> con las entidades del comando que falló, así que la
/// lista suele traer algo. "Suele" no es "siempre", y el <c>catch</c> del alta de cuenta decide
/// **sobre esa lista** si el fallo fue el email duplicado. Con la lista vacía, un
/// <c>Entries.All(...)</c> devuelve <c>true</c> por vacuidad y el alta respondería como si la cuenta
/// se hubiera creado sin haber creado nada.
///
/// **El <c>1062</c> es real y no un doble.** Se consigue ejecutando una violación de clave única y
/// atrapando la excepción que MySQL devuelve: `MySqlException` no tiene constructor público, y
/// fabricar una a mano probaría contra una imitación en vez de contra lo que el motor manda.
/// </summary>
public sealed class FallaConUnMilSesentaYDosSinEntradas : SaveChangesInterceptor
{
    /// <summary>
    /// <c>true</c> si llegó a intervenir. El test lo comprueba: un interceptor inerte deja el test
    /// en verde sin haber ejercitado nada.
    /// </summary>
    public bool Intervino { get; private set; }

    // Los nombres de los parámetros están en inglés y no en castellano como el resto del proyecto:
    // CA1725 exige que un override conserve los del método base, y el build corre con -warnaserror.
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (Intervino || eventData.Context is null)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        Intervino = true;

        throw new DbUpdateException(
            "Un 1062 que EF no pudo atribuir a ninguna entidad.",
            await UnMilSesentaYDosRealAsync(eventData.Context, cancellationToken));
    }

    /// <summary>
    /// Un <c>1062</c> tal como lo manda MySQL: se provoca insertando dos veces el mismo email.
    /// </summary>
    private static async Task<MySqlException> UnMilSesentaYDosRealAsync(
        DbContext contexto, CancellationToken cancelacion)
    {
        // Conexión propia: la del contexto está en medio del `SaveChanges` que se está
        // interceptando, y meterle otro comando adentro mezcla dos cosas que no tienen que ver.
        var cadena = contexto.Database.GetConnectionString();

        await using var conexion = new MySqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);

        var email = $"choque-{Guid.NewGuid():N}@ejemplo.local";

        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO usuario (email, contrasena_hash) VALUES (@email, 'x'), (@email, 'x');";
        comando.Parameters.AddWithValue("@email", email);

        try
        {
            await comando.ExecuteNonQueryAsync(cancelacion);
        }
        catch (MySqlException mysql) when (mysql.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return mysql;
        }

        throw new InvalidOperationException(
            "El INSERT duplicado no produjo el 1062 que este interceptor necesita. O el índice " +
            "único de `usuario.email` dejó de existir, o la colación cambió — las dos cosas son " +
            "problemas de verdad y no de este test.");
    }
}

using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Fabrica la carrera del alta: inserta la cuenta —por su propia conexión— justo antes de que el
/// endpoint ejecute su INSERT.
///
/// Es la única forma honesta de verificar ese camino. La carrera real ocurre entre la consulta que
/// pregunta si el email existe y el INSERT que lo escribe, y reproducirla mandando dos peticiones a
/// la vez sale distinto en cada corrida. Acá el momento es exacto y el resultado, siempre el mismo.
/// </summary>
public sealed class CreaLaCuentaAntesDeGuardar(string cadena) : SaveChangesInterceptor
{
    /// <summary>Interviene una sola vez: es una carrera, no una regla de la aplicación.</summary>
    private bool _yaIntervino;

    /// <summary>
    /// `true` si llegó a insertar la cuenta rival. El test lo comprueba: un interceptor que no
    /// corre deja pasar el test en verde sin que haya existido ninguna carrera, y entonces el test
    /// no verifica nada.
    /// </summary>
    public bool Intervino => _yaIntervino;

    // Los nombres de los parámetros están en inglés y no en castellano como el resto del
    // proyecto: CA1725 exige que un override conserve los del método base, y el build corre con
    // -warnaserror.
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var email = eventData.Context?.ChangeTracker.Entries<Usuario>()
            .FirstOrDefault(e => e.State == EntityState.Added)?.Entity.Email;

        if (!_yaIntervino && email is not null)
        {
            _yaIntervino = true;

            await using var conexion = new MySqlConnection(cadena);
            await conexion.OpenAsync(cancellationToken);

            await using var comando = conexion.CreateCommand();
            comando.CommandText =
                "INSERT INTO usuario (email, contrasena_hash) VALUES (@email, @hash);";
            comando.Parameters.AddWithValue("@email", email);
            // Un hash cualquiera con formato válido: esta cuenta representa a la que ganó la
            // carrera, y lo que importa es que ocupe el email.
            comando.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword("la que ganó"));

            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

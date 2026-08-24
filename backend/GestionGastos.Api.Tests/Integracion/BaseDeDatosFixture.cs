using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// La base contra la que corren los tests de integración: MySQL de verdad, no un doble en memoria.
/// El tipo de columna y las restricciones del esquema son parte de lo que se está verificando, y
/// un proveedor en memoria no las tiene.
///
/// El fixture crea y migra la base por su cuenta, así que la suite arranca igual en una máquina
/// nueva y en el runner del CI, sin un paso manual previo.
/// </summary>
public class BaseDeDatosFixture : IAsyncLifetime
{
    /// <summary>
    /// La única base que este fixture acepta. Está fijado a propósito: el fixture borra y recrea
    /// tablas, y apuntarlo sin querer al esquema de desarrollo se lleva puestos los datos con los
    /// que estabas probando a mano.
    /// </summary>
    private const string BaseEsperada = "gestiongastos_test";

    public string Cadena { get; }

    public BaseDeDatosFixture()
    {
        Cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException(
                "Falta la variable de entorno ConnectionStrings__Default. La suite de integración " +
                $"no adivina contra qué base escribe: definila apuntando a `{BaseEsperada}`.");

        var baseApuntada = LeerBaseDeDatos(Cadena);
        if (!string.Equals(baseApuntada, BaseEsperada, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings__Default apunta a `{baseApuntada}` y esta suite sólo corre " +
                $"contra `{BaseEsperada}`. El fixture migra y limpia tablas: correrlo contra otra " +
                "base destruye datos que no son de tests.");
        }
    }

    public async Task InitializeAsync()
    {
        await using var contexto = CrearContexto();
        await contexto.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public GestionGastosDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<GestionGastosDbContext>()
            .UseMySql(Cadena, new MySqlServerVersion(new Version(8, 4, 10)))
            .Options;

        return new GestionGastosDbContext(opciones);
    }

    /// <summary>
    /// Deja la tabla de movimientos vacía. Las tablas de catálogo no se tocan: las siembra la
    /// migración y los tests las dan por dadas.
    /// </summary>
    public async Task LimpiarMovimientosAsync()
    {
        await using var contexto = CrearContexto();
        await contexto.Movimientos.ExecuteDeleteAsync();
    }

    private static string LeerBaseDeDatos(string cadena)
    {
        foreach (var parte in cadena.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = parte.Split('=', 2);
            if (partes.Length == 2 && partes[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                return partes[1].Trim();
            }
        }

        return string.Empty;
    }
}

/// <summary>
/// Una sola instancia del fixture para toda la suite de integración: migrar la base una vez por
/// corrida y no una vez por clase.
/// </summary>
[CollectionDefinition(Nombre)]
public class BaseDeDatosSuite : ICollectionFixture<BaseDeDatosFixture>
{
    public const string Nombre = "Base de datos";
}

using GestionGastos.Api.Tests.Integracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GestionGastos.Api.Tests.Migraciones;

/// <summary>
/// AC-09 (FR-07) — **criterio de migración, no de comportamiento**: aplicada sobre una base que
/// todavía contiene la fila semilla y sus movimientos, la migración no deja ninguno de los dos.
///
/// Es el único criterio de este ticket que no se puede verificar contra la API: una vez borrada la
/// semilla, no hay estado inicial que reproduzca el escenario. Así que el test lo fabrica —baja la
/// migración, siembra, y la vuelve a subir— y restaura pase lo que pase.
///
/// Va en la colección compartida a propósito: mover el esquema mientras otro test lo usa sería
/// exactamente el tipo de interferencia que el Principio IV prohíbe, y la colección serializa.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class MigracionDeCuentasTests(BaseDeDatosFixture baseDeDatos)
{
    private const string MigracionAnterior = "Inicial";
    private const long IdDeLaSemilla = 1;

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    [Fact]
    public async Task Borra_La_Fila_Semilla_Y_Todos_Sus_Movimientos_AC09()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        await using var contexto = _baseDeDatos.CrearContexto();
        var migrador = contexto.Database.GetService<IMigrator>();

        try
        {
            // El estado de partida que AC-09 describe: la semilla existe.
            await migrador.MigrateAsync(MigracionAnterior);

            // SQL crudo y no el modelo de EF: el modelo actual tiene `contrasena_hash` y el esquema
            // al que acabamos de bajar no. Usarlo acá fallaría por una razón que no es la que se
            // está verificando.
            await contexto.Database.ExecuteSqlRawAsync($"""
                INSERT INTO movimiento (usuario_id, tipo, monto, moneda_id, categoria_id, fecha)
                VALUES ({IdDeLaSemilla}, 0, 100.00, 1, 1, '2026-08-01'),
                       ({IdDeLaSemilla}, 1, 200.00, 1, 8, '2026-08-02');
                """);

            Assert.Equal(1, await ContarUsuariosSemillaAsync(contexto));
            Assert.Equal(2, await ContarMovimientosDeLaSemillaAsync(contexto));

            // El hecho que AC-09 mide.
            await migrador.MigrateAsync();

            Assert.Equal(0, await ContarMovimientosDeLaSemillaAsync(contexto));
            Assert.Equal(0, await ContarUsuariosSemillaAsync(contexto));
        }
        finally
        {
            // El esquema vuelve al día pase lo que pase. Si este test muriera dejando la base una
            // migración atrás, todos los que corren después fallarían por su culpa y no por la
            // suya — la peor forma de romper una suite.
            await migrador.MigrateAsync();
            await _baseDeDatos.LimpiarCuentasAsync();
        }
    }

    // Consultas constantes y no interpoladas: CA2100 marca cualquier SQL armado en tiempo de
    // ejecución, y tiene razón aunque acá el valor sea una constante nuestra. Se respeta la regla
    // en vez de apagarla.
    private static Task<long> ContarUsuariosSemillaAsync(DbContext contexto) =>
        ContarAsync(contexto, "SELECT COUNT(*) FROM usuario WHERE id = 1");

    private static Task<long> ContarMovimientosDeLaSemillaAsync(DbContext contexto) =>
        ContarAsync(contexto, "SELECT COUNT(*) FROM movimiento WHERE usuario_id = 1");

    private static async Task<long> ContarAsync(DbContext contexto, string sqlConstante)
    {
        await contexto.Database.OpenConnectionAsync();
        await using var comando = contexto.Database.GetDbConnection().CreateCommand();
#pragma warning disable CA2100 // Las dos únicas consultas son constantes literales de esta clase.
        comando.CommandText = sqlConstante;
#pragma warning restore CA2100

        return Convert.ToInt64(await comando.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}

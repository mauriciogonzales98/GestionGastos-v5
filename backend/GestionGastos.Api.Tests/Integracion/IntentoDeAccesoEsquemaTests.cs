using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El esquema de `intento_de_acceso` (data-model.md de 003-limite-intentos).
///
/// Va contra SQL directo y no contra la entidad de EF a propósito: lo que se verifica es lo que la
/// migración dejó en MySQL —la clave primaria y su colación—, no lo que el modelo de EF cree. Un
/// test que pasara por el `DbSet` no distinguiría una colación de otra.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class IntentoDeAccesoEsquemaTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// El mismo email con otra combinación de mayúsculas es LA MISMA fila.
    ///
    /// Si esta tabla usara la colación binaria por defecto, `ana@` y `Ana@` llevarían contadores
    /// separados y el límite de intentos se esquivaría cambiando una letra de mayúscula: cinco
    /// intentos con cada forma de escribir el mismo email. El contador tiene que usar la misma
    /// clave que la búsqueda de la cuenta, o no limita nada.
    /// </summary>
    [Fact]
    public async Task El_Email_Es_La_Clave_Y_No_Distingue_Mayusculas()
    {
        var email = $"Esquema-{Guid.NewGuid():N}@Ejemplo.com";
        var enMinusculas = email.ToLowerInvariant();

        await using var contexto = _baseDeDatos.CrearContexto();
        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM intento_de_acceso WHERE email = {email}");

        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO intento_de_acceso (email, fallos_consecutivos, ultimo_fallo) VALUES ({email}, 1, UTC_TIMESTAMP(6))");

        // La segunda inserción choca contra la clave primaria: para MySQL es el mismo email.
        // El SQL va directo, así que la excepción es la del proveedor y no una de EF.
        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO intento_de_acceso (email, fallos_consecutivos, ultimo_fallo) VALUES ({enMinusculas}, 1, UTC_TIMESTAMP(6))"));

        var filas = await contexto.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM intento_de_acceso WHERE email = {enMinusculas}")
            .SingleAsync();

        Assert.Equal(1, filas);

        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM intento_de_acceso WHERE email = {email}");
    }
}

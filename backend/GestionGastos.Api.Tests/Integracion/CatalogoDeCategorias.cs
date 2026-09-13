using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **Resolver una categoría por nombre y tipo dentro del catálogo de una cuenta.**
///
/// Es el gemelo de <see cref="CatalogoDeMonedas"/> y existe por la misma razón llevada a la otra
/// tabla: desde la feature 013 las categorías **no tienen identificadores estables**. Cada cuenta
/// recibe las suyas al registrarse, así que el <c>1</c> que era Comida para todo el mundo pasó a ser
/// el Comida de alguna cuenta y nada para las demás.
///
/// **La regla que hay que respetar al usarlo**: ningún test escribe un identificador de categoría a
/// mano. Ni <c>categoriaId = 1</c>, ni <c>const int Comida = 1</c>. Un literal así falla
/// ruidosamente mientras la categoría sea de otra cuenta, pero el día que el número acierte por
/// casualidad el test pasa verificando otra cosa. Se pide por nombre y tipo, que es lo que el test
/// de verdad quiere decir.
/// </summary>
public static class CatalogoDeCategorias
{
    /// <summary>El gasto con el que se carga un movimiento cuando da igual cuál sea.</summary>
    public const string GastoCualquiera = "Comida";

    /// <summary>El ingreso con el que se carga un movimiento cuando da igual cuál sea.</summary>
    public const string IngresoCualquiera = "Sueldo";

    /// <summary>
    /// El identificador de la categoría activa de esa cuenta con ese nombre y ese tipo.
    ///
    /// Falla ruidosamente si no existe, y eso es a propósito: un test que pide "el Comida de gasto
    /// de esta cuenta" y no lo encuentra tiene un problema real —la cuenta nació sin catálogo—, y
    /// devolverle un cero lo convertiría en un <c>400</c> desconcertante tres pasos más adelante.
    /// </summary>
    public static async Task<int> IdDeAsync(
        BaseDeDatosFixture baseDeDatos,
        long usuarioId,
        string nombre,
        TipoMovimiento tipo)
    {
        await using var contexto = baseDeDatos.CrearContexto();

        var id = await BuscarAsync(contexto, usuarioId, nombre, tipo);

        Assert.True(
            id is not null,
            $"La cuenta {usuarioId} no tiene ninguna categoría activa `{nombre}` de tipo {tipo}. " +
            "Toda cuenta recibe las diez del catálogo inicial al registrarse (FR-002): si falta, " +
            "el problema está en el alta y no en este test.");

        return id!.Value;
    }

    /// <summary>Un gasto cualquiera del catálogo de esa cuenta.</summary>
    public static Task<int> UnGastoAsync(BaseDeDatosFixture baseDeDatos, long usuarioId) =>
        IdDeAsync(baseDeDatos, usuarioId, GastoCualquiera, TipoMovimiento.Gasto);

    /// <summary>Un ingreso cualquiera del catálogo de esa cuenta.</summary>
    public static Task<int> UnIngresoAsync(BaseDeDatosFixture baseDeDatos, long usuarioId) =>
        IdDeAsync(baseDeDatos, usuarioId, IngresoCualquiera, TipoMovimiento.Ingreso);

    /// <summary>
    /// El catálogo inicial de esa cuenta con sus diez identificadores, resueltos de una vez.
    ///
    /// Es lo que reemplaza a los <c>const int Comida = 1</c> que había repartidos por media docena
    /// de archivos. Se resuelven las diez en una sola consulta y no de a una: un test que necesita
    /// tres categorías no tiene por qué pagar tres viajes a la base, y sobre todo no tiene por qué
    /// inventar su propia forma de encontrarlas.
    /// </summary>
    public static async Task<CategoriasDeLaCuenta> DeLaCuentaAsync(
        BaseDeDatosFixture baseDeDatos,
        long usuarioId)
    {
        await using var contexto = baseDeDatos.CrearContexto();

        async Task<int> Id(string nombre, TipoMovimiento tipo)
        {
            var id = await BuscarAsync(contexto, usuarioId, nombre, tipo);

            Assert.True(
                id is not null,
                $"La cuenta {usuarioId} no tiene la categoría `{nombre}` de tipo {tipo} en su " +
                "catálogo inicial (FR-002).");

            return id!.Value;
        }

        return new CategoriasDeLaCuenta(
            await Id("Comida", TipoMovimiento.Gasto),
            await Id("Transporte", TipoMovimiento.Gasto),
            await Id("Vivienda", TipoMovimiento.Gasto),
            await Id("Servicios", TipoMovimiento.Gasto),
            await Id("Salud", TipoMovimiento.Gasto),
            await Id("Ocio", TipoMovimiento.Gasto),
            await Id("Otros", TipoMovimiento.Gasto),
            await Id("Sueldo", TipoMovimiento.Ingreso),
            await Id("Ingreso extra", TipoMovimiento.Ingreso),
            await Id("Otros", TipoMovimiento.Ingreso));
    }

    /// <summary>
    /// La búsqueda, escrita una sola vez.
    ///
    /// El ámbito es el mismo que el de <c>CategoriasConsulta.DelAmbito</c>: las de esa cuenta y
    /// ninguna más. Durante la migración llevó además un <c>|| c.UsuarioId == null</c> para
    /// encontrar las diez compartidas que todavía existían; se fue con ellas.
    /// </summary>
    private static async Task<int?> BuscarAsync(
        Api.Persistencia.GestionGastosDbContext contexto,
        long usuarioId,
        string nombre,
        TipoMovimiento tipo)
    {
        var categoria = await contexto.Categorias
            .Where(c => c.UsuarioId == usuarioId && c.Nombre == nombre && c.Tipo == tipo && c.Activa)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();

        return categoria?.Id;
    }
}

/// <summary>
/// Los diez identificadores del catálogo inicial de una cuenta.
///
/// Existe para que un test pueda escribir <c>catalogo.Comida</c> en lugar de un número: el número
/// dejó de ser el mismo para todas las cuentas.
///
/// "Otros" aparece dos veces porque son dos categorías distintas que comparten nombre y difieren en
/// tipo. Acá se distinguen por el sufijo, que es lo que el nombre solo no alcanza a hacer.
/// </summary>
public sealed record CategoriasDeLaCuenta(
    int Comida,
    int Transporte,
    int Vivienda,
    int Servicios,
    int Salud,
    int Ocio,
    int OtrosGasto,
    int Sueldo,
    int IngresoExtra,
    int OtrosIngreso)
{
    /// <summary>Las siete de gasto, para sembrar repartiendo entre todas.</summary>
    public int[] Gastos() => [Comida, Transporte, Vivienda, Servicios, Salud, Ocio, OtrosGasto];

    /// <summary>Las tres de ingreso.</summary>
    public int[] Ingresos() => [Sueldo, IngresoExtra, OtrosIngreso];
}

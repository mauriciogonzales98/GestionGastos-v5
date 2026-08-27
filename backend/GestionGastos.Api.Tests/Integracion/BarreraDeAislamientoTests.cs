using System.Text.RegularExpressions;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// La barrera del aislamiento (FR-004 del ticket 01c, reformulación de su AC-10).
///
/// Los tests cruzados de <see cref="AislamientoEntreCuentasTests"/> ya detectan que el acotado
/// **actual** desaparezca: si se borra, el listado de una cuenta devuelve los movimientos de la
/// otra y esos tests caen solos. Lo que no detectan es una consulta **nueva** que nadie acote —
/// no saben que existe—, y ése es el descuido que va a pasar el día que alguien agregue el séptimo
/// endpoint.
///
/// Por eso hay dos tests y no uno: el primero vigila la condición, el segundo vigila el canal.
/// Y por eso existe `backend/verificar-aislamiento.sh`, que le prueba a esta barrera que sabe
/// ponerse en rojo — una barrera que nunca se vio fallar no es una barrera (Principio V).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class BarreraDeAislamientoTests(BaseDeDatosFixture baseDeDatos)
{
    /// <summary>
    /// El único archivo de producción que puede LEER <c>contexto.Movimientos</c>.
    ///
    /// Se nombra, no se descubre: una barrera que deduce sus propias excepciones aprende a
    /// aceptarlas.
    /// </summary>
    private const string CanalDeLectura = "Movimientos/MovimientosConsulta.cs";

    /// <summary>
    /// La única escritura declarada. El alta agrega la fila y le pone el propietario de la sesión a
    /// mano; el acotado de lectura no aplica a un INSERT, así que este uso es legítimo y distinto.
    /// </summary>
    private const string EscrituraDeclarada = "Movimientos/MovimientosEndpoints.cs";

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// La consulta del listado acota por <c>usuario_id</c>, y se comprueba sobre el SQL que genera.
    ///
    /// Mirar el resultado no alcanza: el índice <c>(usuario_id, fecha, id)</c> hace que MySQL
    /// devuelva filas que parecen correctas por motivos que no son la consulta. Es el mismo motivo
    /// por el que `ListadoMovimientosTests` verifica el orden en dos capas.
    /// </summary>
    [Fact]
    public void La_Consulta_Del_Listado_Acota_Por_Cuenta_En_El_Sql()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var sql = MovimientosConsulta
            // El id no importa: este test sólo mira el SQL que se genera, no filas.
            .DelMes(contexto, usuarioId: 1, RangoDelMes.De(new DateOnly(2026, 8, 15)))
            .ToQueryString();

        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);

        var donde = sql[sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..];

        Assert.True(
            donde.Contains("usuario_id", StringComparison.OrdinalIgnoreCase),
            "La consulta del listado dejó de acotar por cuenta: su WHERE no nombra `usuario_id`. " +
            "Con eso, el listado de una cuenta devuelve los movimientos de todas.\n\nSQL:\n" + sql);
    }

    /// <summary>
    /// Ningún archivo de producción lee <c>contexto.Movimientos</c> fuera del canal único.
    ///
    /// Es la mitad de la barrera que protege lo que todavía no se escribió. Vigilar la condición
    /// cuida la consulta que existe; vigilar el canal cuida la que alguien agregue el mes que viene
    /// sin acordarse de acotarla, que es la que va a fallar porque nadie la va a estar mirando.
    ///
    /// **Si este test te frena al agregar una consulta legítima, la salida es agregar el método al
    /// canal, no ampliar la lista de excepciones.**
    /// </summary>
    [Fact]
    public void Ninguna_Lectura_De_Movimientos_Vive_Fuera_Del_Canal()
    {
        var raiz = RaizDelProyectoDeProduccion();

        var infractores = Directory
            .EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(archivo => !EstaEnMigraciones(raiz, archivo))
            .Where(archivo => !EsRutaDeclarada(raiz, archivo))
            .Where(archivo => UsaElDbSetDeMovimientos(File.ReadAllText(archivo)))
            .Select(archivo => Path.GetRelativePath(raiz, archivo).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            infractores.Count == 0,
            "Estos archivos usan `contexto.Movimientos` fuera del canal único de lectura " +
            $"(`{CanalDeLectura}`) y de la escritura declarada (`{EscrituraDeclarada}`):\n  " +
            string.Join("\n  ", infractores) +
            "\n\nUna lectura de movimientos que no pase por el canal es una que nadie está " +
            "mirando, y el acotado por cuenta se olvida escribiéndola. La salida es agregar el " +
            "método a `MovimientosConsulta`, no sumar una excepción acá.");
    }

    /// <summary>
    /// El canal sigue existiendo y sigue siendo el que la otra mitad de la barrera vigila.
    ///
    /// Sin esto, borrar `MovimientosConsulta` y esparcir las consultas dejaría el test de arriba en
    /// verde por vacuidad: sin archivos que lo usen, no hay infractores.
    /// </summary>
    [Fact]
    public void El_Canal_De_Lectura_Existe_Y_Se_Usa()
    {
        var canal = Path.Combine(RaizDelProyectoDeProduccion(), CanalDeLectura);

        Assert.True(
            File.Exists(canal),
            $"No existe `{CanalDeLectura}`. La barrera del canal quedaría en verde sin vigilar nada.");

        Assert.True(
            UsaElDbSetDeMovimientos(File.ReadAllText(canal)),
            $"`{CanalDeLectura}` ya no lee `contexto.Movimientos`: el canal se vació y las " +
            "consultas se mudaron a algún lado que esta barrera no está mirando.");
    }

    /// <summary>
    /// <c>true</c> si el texto usa el <c>DbSet</c> de movimientos.
    ///
    /// Busca el acceso al DbSet, no la palabra "Movimientos": el nombre aparece en namespaces,
    /// comentarios y nombres de clase por todos lados, y una barrera que se dispara con eso es una
    /// que se termina apagando.
    /// </summary>
    private static bool UsaElDbSetDeMovimientos(string codigo) =>
        Regex.IsMatch(
            codigo,
            @"\b(contexto|context|db)\s*\.\s*Movimientos\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

    private static bool EsRutaDeclarada(string raiz, string archivo)
    {
        var relativa = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
        return relativa == CanalDeLectura || relativa == EscrituraDeclarada;
    }

    /// <summary>
    /// `Migrations/` queda fuera: lo genera EF, nadie lo escribe a mano, y no consulta movimientos
    /// en nombre de ninguna cuenta. Es la misma excepción que hace la barrera del linter.
    /// </summary>
    private static bool EstaEnMigraciones(string raiz, string archivo) =>
        Path.GetRelativePath(raiz, archivo).Replace('\\', '/').StartsWith("Migrations/", StringComparison.Ordinal);

    /// <summary>
    /// La carpeta de `GestionGastos.Api`, encontrada subiendo desde el binario de los tests.
    ///
    /// Se busca por el `.csproj` y no con una ristra de `..`: así el test no se rompe si cambia la
    /// profundidad de la carpeta de salida.
    /// </summary>
    private static string RaizDelProyectoDeProduccion()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null &&
               !File.Exists(Path.Combine(directorio.FullName, "GestionGastos.slnx")))
        {
            directorio = directorio.Parent;
        }

        Assert.NotNull(directorio);

        var api = Path.Combine(directorio.FullName, "GestionGastos.Api");

        Assert.True(Directory.Exists(api), $"No se encontró el proyecto de producción en {api}.");
        return api;
    }
}

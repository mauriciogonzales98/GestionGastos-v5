using System.Globalization;
using System.Text.RegularExpressions;

namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// Lee `frontend/src/api/tipos.ts`, que es la fuente de verdad del contrato (D-09).
///
/// Es la única excepción de estructura del proyecto, declarada en `AGENTS.md`: los tests del
/// backend leen un archivo del frontend. Es lectura y en una sola dirección — el frontend no lee
/// nada del backend, y eso no cambia.
///
/// El parseo es deliberadamente simple: no interpreta TypeScript, sólo extrae los nombres de campo
/// y los valores de una unión de literales. Si el archivo se vuelve más complejo que eso, el
/// parseo falla ruidosamente en vez de aprobar de más, que es lo que se quiere de una barrera.
/// </summary>
public static class TiposDelFrontend
{
    private const string RutaRelativa = "frontend/src/api/tipos.ts";

    public static string Ruta => Path.Combine(RaizDelRepositorio(), RutaRelativa);

    public static string Texto => File.ReadAllText(Ruta);

    /// <summary>
    /// Los nombres de campo de una interfaz, en orden de aparición. El `?` de los opcionales no
    /// forma parte del nombre.
    /// </summary>
    public static IReadOnlyList<string> CamposDeInterfaz(string nombreInterfaz)
    {
        var cuerpo = Regex.Match(
            Texto,
            $@"export\s+interface\s+{Regex.Escape(nombreInterfaz)}\s*\{{(?<cuerpo>[^}}]*)\}}",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        if (!cuerpo.Success)
        {
            throw new InvalidOperationException(
                $"No se encontró `export interface {nombreInterfaz}` en {RutaRelativa}. " +
                "O el contrato cambió de nombre, o el archivo dejó de tener la forma que esta " +
                "barrera sabe leer. Las dos cosas requieren mirar, no ajustar el regex.");
        }

        return Regex.Matches(
                cuerpo.Groups["cuerpo"].Value,
                @"^\s*(?<campo>[A-Za-z_][A-Za-z0-9_]*)\s*\??\s*:",
                RegexOptions.Multiline,
                TimeSpan.FromSeconds(5))
            .Select(m => m.Groups["campo"].Value)
            .ToList();
    }

    /// <summary>Los literales de una unión de cadenas, por ejemplo <c>'gasto' | 'ingreso'</c>.</summary>
    public static IReadOnlyList<string> LiteralesDeUnion(string nombreTipo)
    {
        var declaracion = Regex.Match(
            Texto,
            $@"export\s+type\s+{Regex.Escape(nombreTipo)}\s*=\s*(?<union>[^;]+);",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        if (!declaracion.Success)
        {
            throw new InvalidOperationException(
                $"No se encontró `export type {nombreTipo}` en {RutaRelativa}.");
        }

        return Regex.Matches(
                declaracion.Groups["union"].Value,
                @"'(?<valor>[^']*)'",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))
            .Select(m => m.Groups["valor"].Value)
            .ToList();
    }

    /// <summary>
    /// Sube desde el directorio del assembly hasta encontrar la raíz del repositorio. No se fija
    /// una ruta relativa a mano porque depende del framework y de la configuración de compilación.
    /// </summary>
    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, RutaRelativa)))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"No se encontró {RutaRelativa} subiendo desde {AppContext.BaseDirectory}."));
    }
}

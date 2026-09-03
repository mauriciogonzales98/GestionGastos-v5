using GestionGastos.Api.Dominio;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Categorias;

/// <summary>
/// Las reglas del nombre y del tipo de una categoría (FR-005, FR-006, FR-007).
///
/// **La usan el alta y el renombre, y es la misma para las dos** — mismo criterio que
/// `ValidacionDelMovimiento` desde FEAT-001b: una categoría no puede quedar, por vía de un
/// renombre, con un nombre que el alta habría rechazado. Dos validaciones parecidas divergen el día
/// que alguien toca una (Clarificación 1).
///
/// La clave de cada error es el nombre del campo de la petición, para que el formulario pueda poner
/// el mensaje al lado de su control en vez de volcar un texto suelto.
/// </summary>
public static class ValidacionDeLaCategoria
{
    /// <summary>El largo de la columna, que es el límite real. El PRD citó otro de memoria.</summary>
    public const int LargoMaximoDelNombre = 50;

    private const string ErrorDeLargo = "El nombre tiene que tener entre 1 y 50 caracteres.";

    /// <summary>
    /// El mensaje del nombre repetido.
    ///
    /// **No dice si la que choca es propia o predefinida** (D-06). No hace falta para corregirlo
    /// —hay que elegir otro nombre en los dos casos— y decirlo sería contar algo del ámbito ajeno
    /// sin necesidad.
    /// </summary>
    private const string ErrorDeNombreRepetido = "Ya tenés una categoría de ese tipo con ese nombre.";

    /// <summary>
    /// El rechazo del nombre repetido, listo para responder.
    ///
    /// Lo necesita el alta para el caso en que **el índice** atrapa el choque que la comprobación
    /// previa no llegó a ver (D-01): la respuesta tiene que ser la misma en las dos vías, o el
    /// cliente vería dos formas distintas del mismo rechazo según cuál de las dos lo atrapó. Está
    /// acá y no allá para que el mensaje siga escrito una sola vez.
    /// </summary>
    public static Dictionary<string, string[]> ErroresDeNombreRepetido() =>
        new(StringComparer.Ordinal) { ["nombre"] = [ErrorDeNombreRepetido] };

    /// <summary>
    /// Valida el alta: el tipo tiene que ser una de las dos cadenas, y el nombre tiene que pasar
    /// las mismas reglas que en el renombre.
    /// </summary>
    public static async Task<ValidacionDeCategoria> ValidarAltaAsync(
        GestionGastosDbContext contexto,
        long usuarioId,
        string? nombre,
        string? tipoTexto)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!TipoMovimientoTexto.TryDesdeTexto(tipoTexto, out var tipo))
        {
            errores["tipo"] = ["Elegí si la categoría es de gasto o de ingreso."];
        }

        var recortado = Recortar(nombre);

        // Si el tipo vino mal no se puede comprobar la unicidad contra nada: la unicidad es por
        // (nombre, tipo), y con un tipo inválido la consulta preguntaría por un ámbito que no
        // existe. El largo sí se valida igual, para que la respuesta junte los dos errores en una
        // sola pasada en vez de hacer corregir de a uno.
        await ValidarNombreAsync(
            contexto, usuarioId, recortado, tipo, errores, comprobarUnicidad: !errores.ContainsKey("tipo"), idPropio: null);

        return new ValidacionDeCategoria(errores, recortado, tipo);
    }

    /// <summary>
    /// Valida el renombre. El tipo **no viaja en la petición**: cambiarlo movería de tipo a todos
    /// los movimientos que la usan, así que se toma el de la categoría que se está editando.
    ///
    /// <paramref name="idPropio"/> es lo que hace que la categoría **no choque consigo misma**:
    /// renombrar "Gimnasio" a "Gimnasio" no es un error (Clarificación 1).
    /// </summary>
    public static async Task<ValidacionDeCategoria> ValidarRenombreAsync(
        GestionGastosDbContext contexto,
        long usuarioId,
        string? nombre,
        TipoMovimiento tipo,
        int idPropio)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var recortado = Recortar(nombre);

        await ValidarNombreAsync(
            contexto, usuarioId, recortado, tipo, errores, comprobarUnicidad: true, idPropio);

        return new ValidacionDeCategoria(errores, recortado, tipo);
    }

    /// <summary>
    /// Los espacios al principio y al final no forman parte del nombre (FR-006).
    ///
    /// Es la única mitad de FR-007 que hay que escribir: las mayúsculas y los acentos los ignora la
    /// collation `utf8mb4_0900_ai_ci` de la columna, en el índice y en la comparación.
    /// </summary>
    private static string Recortar(string? nombre) => (nombre ?? string.Empty).Trim();

    private static async Task ValidarNombreAsync(
        GestionGastosDbContext contexto,
        long usuarioId,
        string nombre,
        TipoMovimiento tipo,
        Dictionary<string, string[]> errores,
        bool comprobarUnicidad,
        int? idPropio)
    {
        if (nombre.Length is 0 or > LargoMaximoDelNombre)
        {
            // El largo primero y sin seguir: una consulta de unicidad con un nombre vacío o de 200
            // caracteres no aporta un segundo motivo, aporta ruido.
            errores["nombre"] = [ErrorDeLargo];
            return;
        }

        if (!comprobarUnicidad)
        {
            return;
        }

        var repetida = await CategoriasConsulta
            .Homonimas(contexto, usuarioId, nombre, tipo)
            .AnyAsync(c => idPropio == null || c.Id != idPropio);

        if (repetida)
        {
            errores["nombre"] = [ErrorDeNombreRepetido];
        }
    }
}

/// <summary>
/// Lo que devuelve una validación: los errores por campo y los valores ya interpretados.
///
/// El nombre viene recortado y el tipo ya convertido, para que quien llama no tenga que repetir esa
/// interpretación —y no pueda hacerla distinto.
/// </summary>
/// <param name="Errores">Vacío si no hay nada que objetar.</param>
/// <param name="Nombre">El nombre sin espacios al borde.</param>
/// <param name="Tipo">El tipo interpretado. Sin sentido si `Errores` trae la clave `tipo`.</param>
public sealed record ValidacionDeCategoria(
    Dictionary<string, string[]> Errores,
    string Nombre,
    TipoMovimiento Tipo)
{
    public bool HayErrores => Errores.Count > 0;
}

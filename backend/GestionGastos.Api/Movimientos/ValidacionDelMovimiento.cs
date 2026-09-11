using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Las reglas que el esquema no puede expresar con un motivo legible (D-08).
///
/// **La usan el alta y la edición, y por eso ya no se llama `ValidacionDelAlta`.** Que sea una sola
/// es FR-003 de FEAT-001b: un movimiento no puede quedar, por vía de una edición, en un estado que
/// el alta habría rechazado. Dos validaciones parecidas divergen el día que alguien toca una.
///
/// El CHECK de la base rechaza un monto negativo, pero devuelve un error genérico de
/// almacenamiento; el techo de FR-004b lo redondearía en silencio. La persona necesita saber qué
/// campo está mal y por qué, así que las reglas viven acá y no sólo en el esquema.
///
/// La clave de cada error es el nombre del campo de la petición: es lo que permite al frontend
/// poner el mensaje al lado de su control en vez de volcar un texto suelto.
///
/// **Además de validar, normaliza la nota** (<see cref="NotaNormalizada"/>), y el nombre de la clase
/// anuncia una sola de las dos cosas. Están juntas a propósito: las dos recortan los espacios de los
/// extremos, y separarlas obligaría a escribir ese criterio dos veces — que es peor que un nombre
/// incompleto. Si aparece un tercer llamador, ahí sí conviene moverla.
/// </summary>
public static class ValidacionDelMovimiento
{
    /// <summary>El techo de FR-004b. Entra exacto en decimal(11,2).</summary>
    public const decimal MontoMaximo = 999_999_999.99m;

    /// <summary>El techo de la nota (RF-33). Entra exacto en varchar(120).</summary>
    public const int NotaMaxima = 120;

    /// <summary>Valida el alta.</summary>
    public static Dictionary<string, string[]> Validar(
        NuevoMovimientoDto peticion,
        Categoria? categoria,
        Moneda? moneda,
        out TipoMovimiento tipo) =>
        Validar(peticion.Tipo, peticion.Monto, peticion.CategoriaId, categoria, peticion.MonedaId, moneda, peticion.Nota, out tipo);

    /// <summary>Valida la edición. Mismas reglas y mismas claves de error que el alta (FR-003).</summary>
    public static Dictionary<string, string[]> Validar(
        MovimientoEditadoDto peticion,
        Categoria? categoria,
        Moneda? moneda,
        out TipoMovimiento tipo) =>
        Validar(peticion.Tipo, peticion.Monto, peticion.CategoriaId, categoria, peticion.MonedaId, moneda, peticion.Nota, out tipo);

    private static Dictionary<string, string[]> Validar(
        string? tipoTexto,
        decimal? monto,
        int? categoriaId,
        Categoria? categoria,
        int? monedaId,
        Moneda? moneda,
        string? nota,
        out TipoMovimiento tipo)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var tipoValido = TipoMovimientoTexto.TryDesdeTexto(tipoTexto, out tipo);

        if (!tipoValido)
        {
            errores["tipo"] = ["Elegí si es un gasto o un ingreso."];
        }

        ValidarMonto(monto, errores);
        ValidarCategoria(categoriaId, categoria, tipoValido, tipo, errores);
        ValidarMoneda(monedaId, moneda, errores);
        ValidarNota(nota, errores);

        return errores;
    }

    /// <summary>
    /// La nota: hasta 120 **caracteres Unicode**, medidos después de recortar (RF-33, feature 012).
    ///
    /// **Se cuentan code points y no `Length`, y ésa es la decisión** (D-02). `string.Length` cuenta
    /// unidades UTF-16: un emoji fuera del BMP vale 2, así que una nota de 120 emoji se rechazaría por
    /// "superar los 120 caracteres" cuando la persona escribió exactamente 120 — un mensaje que no se
    /// puede entender ni corregir. `varchar(120)` en utf8mb4 cuenta caracteres, así que contar así es
    /// además acordar con el esquema: lo que esta validación acepta entra siempre en la columna.
    ///
    /// **Se mide después de recortar** (D-03). Midiendo antes, 120 caracteres con un espacio a cada
    /// lado se rechazarían — y una vez guardados entran exactos.
    ///
    /// **Ausente no es un error ACÁ, y eso no significa que se acepte.** En el alta, ausente significa
    /// "sin nota" y es correcto. En la edición **se rechaza**, con la clave `nota`, y el chequeo vive en
    /// el handler del PUT junto al de `Fecha` — porque es una regla de la edición y no del movimiento,
    /// que es el mismo reparto que tiene la fecha.
    ///
    /// Acá hubo un comentario que afirmaba que un cuerpo sin la nota "deserializa con `Fecha` nula y
    /// muere antes de llegar acá". Era falso en los dos tramos: omitir la nota no tiene relación con
    /// `Fecha`, y el cuerpo llegaba, pasaba y **borraba la nota en silencio con un 200**. Lo encontró la
    /// revisión del PR #29, y lo peligroso no era el error sino que documentaba una garantía inexistente:
    /// el próximo en leer el archivo buscando el chequeo faltante iba a concluir que no hacía falta.
    ///
    /// El mensaje **no repite la nota**. Es la única entrada de texto libre de la aplicación, y
    /// devolver el valor lo haría viajar de vuelta y aparecer donde termine el mensaje.
    /// </summary>
    private static void ValidarNota(string? nota, Dictionary<string, string[]> errores)
    {
        if (nota is null)
        {
            return;
        }

        if (nota.Trim().EnumerateRunes().Count() > NotaMaxima)
        {
            errores["nota"] = [$"La nota no puede superar los {NotaMaxima} caracteres."];
        }
    }

    /// <summary>
    /// La nota lista para guardar: recortada, y la cadena vacía convertida en ausencia de valor.
    ///
    /// Las dos formas significan "sin nota" y el esquema admite las dos, así que elegir una acá no
    /// cambia lo que la API devuelve —eso lo normaliza `MovimientoDto`, que es el único punto por el
    /// que pasan todas las lecturas (D-04)—. Se elige la ausencia de valor porque es la que un
    /// movimiento de antes de esta feature ya tiene: así el estado "sin nota" se escribe igual venga
    /// de donde venga.
    /// </summary>
    public static string? NotaNormalizada(string? nota)
    {
        var recortada = nota?.Trim();
        return string.IsNullOrEmpty(recortada) ? null : recortada;
    }

    /// <summary>
    /// La moneda, con la forma de la categoría y **una diferencia deliberada**: no hay regla de
    /// ámbito (FR-003, feature 009).
    ///
    /// Una categoría vale si es predefinida del sistema **o** propia de esta cuenta, y activa. Una
    /// moneda vale si está en el catálogo, punto: son del sistema, no tienen dueño, no hay bajas
    /// lógicas y no hay monedas "no elegibles". Escribirle un filtro de ámbito sería copiar una
    /// condición que no protege nada.
    ///
    /// **Ausente no es un error**: significa la predeterminada al dar de alta y "la que ya tenía"
    /// al editar. Quien llama resuelve cuál de las dos cosas es; lo único que se valida acá es que,
    /// si se pidió una, exista.
    /// </summary>
    private static void ValidarMoneda(
        int? monedaId,
        Moneda? moneda,
        Dictionary<string, string[]> errores)
    {
        if (monedaId is not null && moneda is null)
        {
            errores["monedaId"] = ["La moneda elegida no existe."];
        }
    }

    private static void ValidarMonto(decimal? monto, Dictionary<string, string[]> errores)
    {
        if (monto is not { } valor)
        {
            errores["monto"] = ["Ingresá un monto."];
            return;
        }

        if (valor <= 0 || decimal.Round(valor, 2) != valor)
        {
            errores["monto"] = ["El monto debe ser mayor a cero y tener hasta dos decimales."];
            return;
        }

        if (valor > MontoMaximo)
        {
            // Declarada, no un error genérico del almacenamiento: el esquema daría un fallo de
            // rango que no le dice nada a nadie.
            errores["monto"] = ["El monto no puede superar 999.999.999,99."];
        }
    }

    private static void ValidarCategoria(
        int? categoriaId,
        Categoria? categoria,
        bool tipoValido,
        TipoMovimiento tipo,
        Dictionary<string, string[]> errores)
    {
        if (categoriaId is null)
        {
            errores["categoriaId"] = ["Elegí una categoría."];
            return;
        }

        if (categoria is null)
        {
            errores["categoriaId"] = ["La categoría elegida no existe."];
            return;
        }

        // FR-011: es una regla entre dos tablas que ninguna clave foránea expresa. Si el tipo
        // vino mal, no se puede comparar contra nada: ese error ya se reportó en su propio campo.
        if (tipoValido && categoria.Tipo != tipo)
        {
            errores["categoriaId"] =
            [
                tipo == TipoMovimiento.Gasto
                    ? "Elegí una categoría de gasto."
                    : "Elegí una categoría de ingreso.",
            ];
        }
    }
}

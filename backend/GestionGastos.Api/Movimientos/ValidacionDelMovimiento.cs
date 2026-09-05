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
/// </summary>
public static class ValidacionDelMovimiento
{
    /// <summary>El techo de FR-004b. Entra exacto en decimal(11,2).</summary>
    public const decimal MontoMaximo = 999_999_999.99m;

    /// <summary>Valida el alta.</summary>
    public static Dictionary<string, string[]> Validar(
        NuevoMovimientoDto peticion,
        Categoria? categoria,
        Moneda? moneda,
        out TipoMovimiento tipo) =>
        Validar(peticion.Tipo, peticion.Monto, peticion.CategoriaId, categoria, peticion.MonedaId, moneda, out tipo);

    /// <summary>Valida la edición. Mismas reglas y mismas claves de error que el alta (FR-003).</summary>
    public static Dictionary<string, string[]> Validar(
        MovimientoEditadoDto peticion,
        Categoria? categoria,
        Moneda? moneda,
        out TipoMovimiento tipo) =>
        Validar(peticion.Tipo, peticion.Monto, peticion.CategoriaId, categoria, peticion.MonedaId, moneda, out tipo);

    private static Dictionary<string, string[]> Validar(
        string? tipoTexto,
        decimal? monto,
        int? categoriaId,
        Categoria? categoria,
        int? monedaId,
        Moneda? moneda,
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

        return errores;
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

namespace GestionGastos.Api.Monedas;

/// <summary>
/// Una moneda del catálogo como la ve el cliente (FR-004).
///
/// **`esPredeterminada` sale y `decimales` no**, y la diferencia no es un descuido:
///
/// · `esPredeterminada` responde la única pregunta que el formulario se hace sobre el catálogo
///   —cuál propongo—, y viaja como la respuesta ya calculada en vez del dato con el que calcularla.
///   Es el mismo criterio con el que `CategoriaDto` manda `esPropia` y no `usuarioId`.
///
/// · `decimales` existe en la tabla y **no lo consume nadie todavía**: el formato regional del monto
///   es el ticket 6. Un campo que nadie usa es un dato que salió a la red sin que nadie lo
///   decidiera, y `ContratoMonedasTests` se pone en rojo si alguien lo agrega "por completitud".
/// </summary>
/// <param name="Id">Lo que el cliente manda como <c>monedaId</c> al registrar o editar.</param>
/// <param name="Codigo">ISO 4217: <c>ARS</c>, <c>USD</c>.</param>
/// <param name="Nombre">Nombre visible, para no obligar a nadie a saber que <c>ARS</c> son pesos.</param>
/// <param name="Simbolo">El símbolo con el que se muestra el monto.</param>
/// <param name="EsPredeterminada">Exactamente una del catálogo la tiene en <c>true</c> (RF-25).</param>
public record MonedaDto(short Id, string Codigo, string Nombre, string Simbolo, bool EsPredeterminada);

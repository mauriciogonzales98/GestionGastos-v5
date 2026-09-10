namespace GestionGastos.Api.Monedas;

/// <summary>
/// Una moneda del catálogo como la ve el cliente (FR-004).
///
/// `esPredeterminada` responde la única pregunta que el formulario se hace sobre el catálogo —cuál
/// propongo—, y viaja como la respuesta ya calculada en vez del dato con el que calcularla. Es el
/// mismo criterio con el que `CategoriaDto` manda `esPropia` y no `usuarioId`.
///
/// **`decimales` empieza a viajar en la feature 011**, y hasta entonces no lo hacía por una regla
/// que sigue en pie: un campo que nadie usa es un dato que salió a la red sin que nadie lo
/// decidiera. Lo que cambió no es la regla sino el hecho — ahora tiene consumidor, que es el formato
/// del monto según la escala de su moneda (`PRD:RF-32`). El comentario que decía "el ticket 6" era
/// esta feature.
/// </summary>
/// <param name="Id">Lo que el cliente manda como <c>monedaId</c> al registrar o editar.</param>
/// <param name="Codigo">ISO 4217: <c>ARS</c>, <c>USD</c>.</param>
/// <param name="Nombre">Nombre visible, para no obligar a nadie a saber que <c>ARS</c> son pesos.</param>
/// <param name="Simbolo">El símbolo con el que se muestra el monto.</param>
/// <param name="EsPredeterminada">Exactamente una del catálogo la tiene en <c>true</c> (RF-25).</param>
/// <param name="Decimales">
/// Cuántos decimales usa la moneda. Le gana a lo que <c>Intl</c> deduzca del código ISO: si mandara
/// la deducción, agregar al catálogo una moneda con otra escala daría montos redondeados a una que
/// nadie eligió, en silencio.
/// </param>
public record MonedaDto(
    short Id,
    string Codigo,
    string Nombre,
    string Simbolo,
    bool EsPredeterminada,
    byte Decimales);

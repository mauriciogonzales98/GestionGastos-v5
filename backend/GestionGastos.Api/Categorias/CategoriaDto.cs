namespace GestionGastos.Api.Categorias;

/// <summary>
/// Una categoría como la ve el cliente. `tipo` viaja como cadena y no como el <c>tinyint</c> de la
/// base: el número obligaría al frontend a conocer el mapeo del esquema.
///
/// **Ni `activa`, ni `usuarioId`, ni `esPropia` salen**, cada uno por su motivo:
///
/// · `activa` no viaja porque el listado ya devuelve sólo activas — un campo que siempre vale lo
///   mismo no informa nada y sí invita a filtrar del lado del cliente por algo que ya vino filtrado.
///
/// · `usuarioId` no viaja porque el número de cuenta no le sirve a nadie de este lado: obligaría al
///   cliente a saber cuál es la suya para poder compararlo, y es un dato de más en la red.
///
/// · `esPropia` **viajaba hasta la feature 013** y se fue por el primero de esos motivos. Respondía
///   una sola pregunta —¿esto lo puedo renombrar y dar de baja, o es del sistema?— y desde que cada
///   cuenta recibe su propio catálogo la respuesta es siempre que sí. Un campo que siempre vale lo
///   mismo invita a escribir del lado del cliente una condición que ya no existe (FR-016).
/// </summary>
/// <param name="Id">Identificador de la categoría.</param>
/// <param name="Nombre">Nombre visible.</param>
/// <param name="Tipo">"gasto" o "ingreso".</param>
public record CategoriaDto(int Id, string Nombre, string Tipo);

/// <summary>
/// Lo que se manda al crear una categoría (FR-004).
///
/// Los dos campos son anulables aunque el contrato los pida obligatorios: si fueran `string` y
/// `required`, un cuerpo sin ellos fallaría al deserializar y respondería un 400 genérico del
/// framework, sin la clave del campo. La validación tiene que poder decir CUÁL falta.
/// </summary>
/// <param name="Nombre">1 a 50 caracteres después de recortar espacios.</param>
/// <param name="Tipo">"gasto" o "ingreso". Se fija al crear y no se puede cambiar después.</param>
public record NuevaCategoriaDto(string? Nombre, string? Tipo);

/// <summary>
/// Lo que se manda al renombrar (FR-007).
///
/// **Sólo el nombre.** El tipo no viaja: cambiarlo movería de tipo a todos los movimientos que la
/// usan, que es reescribir la historia por la puerta de atrás.
///
/// Tiene un solo campo y aun así es un tipo aparte de <see cref="NuevaCategoriaDto"/>, por el mismo
/// motivo que `NuevaCuenta` y `Credenciales`: son dos contratos que pueden divergir, y éste ya
/// diverge en que no lleva tipo.
/// </summary>
/// <param name="Nombre">El nombre nuevo. Mismas reglas que en el alta.</param>
public record CategoriaEditadaDto(string? Nombre);

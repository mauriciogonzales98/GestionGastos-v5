namespace GestionGastos.Api.Categorias;

/// <summary>
/// Una categoría como la ve el cliente. `tipo` viaja como cadena y no como el <c>tinyint</c> de la
/// base: el número obligaría al frontend a conocer el mapeo del esquema.
///
/// `activa` y `usuarioId` no salen: existen en la tabla como anticipo del ticket 3 (D-06) y
/// exponerlas ahora sería contrato que todavía no se decidió.
/// </summary>
/// <param name="Id">Identificador de la categoría.</param>
/// <param name="Nombre">Nombre visible.</param>
/// <param name="Tipo">"gasto" o "ingreso".</param>
public record CategoriaDto(int Id, string Nombre, string Tipo);

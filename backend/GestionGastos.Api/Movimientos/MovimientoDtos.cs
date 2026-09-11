namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Lo que llega en el alta. `fecha` es opcional: ausente o null significa "hoy", y ese valor lo
/// pone el servidor y no el cliente (AC-17) — es la única forma de que el test sea determinista.
///
/// **`monedaId` también es opcional, y ausente significa "la predeterminada del catálogo"**
/// (FR-002, feature 009). Hasta el ticket 4b este campo no existía y la moneda la decidía siempre
/// el servidor; que siga siendo opcional es `PRD:NFR-01` —quien opera en una sola moneda no agrega
/// ni un paso— y es la compatibilidad hacia atrás del contrato: todo cliente que ya andaba sin
/// mandarlo sigue andando.
/// </summary>
/// <param name="Tipo">"gasto" o "ingreso".</param>
/// <param name="Monto">Número con hasta dos decimales.</param>
/// <param name="CategoriaId">Categoría elegida, del mismo tipo que el movimiento (FR-011).</param>
/// <param name="MonedaId">
/// Moneda del catálogo. Ausente o null = la predeterminada.
///
/// Es <c>int?</c> y no <c>short?</c> aunque <see cref="Dominio.Moneda.Id"/> sea <c>short</c>: con
/// <c>short?</c>, un número fuera de rango falla al deserializar y responde un 400 genérico del
/// framework, sin decir qué campo está mal. Con <c>int?</c> llega hasta la validación y se rechaza
/// con la clave <c>monedaId</c>, que es lo que le permite al frontend poner el mensaje al lado del
/// selector. Es el mismo motivo por el que todos los campos de este DTO son anulables.
/// </param>
/// <param name="Fecha">Día del movimiento. Ausente o null = hoy.</param>
/// <param name="Nota">
/// La nota descriptiva, hasta 120 caracteres Unicode. **Opcional**: ausente, null y la cadena vacía
/// significan los tres lo mismo —sin nota— y se tiene que poder registrar un movimiento sin tocar el
/// campo (`PRD:AC-09`). Es además la compatibilidad hacia atrás del contrato, igual que `MonedaId`.
/// </param>
public record NuevoMovimientoDto(
    string? Tipo,
    decimal? Monto,
    int? CategoriaId,
    int? MonedaId,
    DateOnly? Fecha,
    string? Nota);

/// <summary>
/// Un movimiento como lo ve el cliente. Es la misma forma en el alta y en el listado: devolver el
/// movimiento entero al crearlo es lo que permite a la pantalla insertarlo sin volver a pedirlo
/// (FR-014).
///
/// `categoriaNombre` viaja junto al id para que el listado no cruce contra el catálogo. Es además
/// lo que hará funcionar RF-09 en el ticket 3: el nombre que se conserva en los movimientos ya
/// registrados es el que devuelve esta lectura.
/// </summary>
public record MovimientoDto(
    long Id,
    string Tipo,
    decimal Monto,
    int CategoriaId,
    string CategoriaNombre,
    string MonedaCodigo,
    DateOnly Fecha,
    string? Nota)
{
    /// <summary>
    /// La nota, **siempre presente y nunca nula**: "sin nota" es la cadena vacía.
    ///
    /// **Acá está la única normalización de lectura de la feature, y está acá a propósito** (D-04).
    /// El esquema admite DOS representaciones de "sin nota" —la ausencia de valor y la cadena vacía— y
    /// no normaliza al escribir, que fue una decisión tomada a sabiendas y anotada como deuda D12-08.
    /// Lo que sostiene la invariante de `FR-005` es entonces la lectura, y este tipo es el único punto
    /// por el que pasan TODAS las lecturas: se construye en cuatro lugares distintos de
    /// <see cref="MovimientosEndpoints"/> —el alta, el listado, la consulta individual y la edición—
    /// y los cuatro heredan la regla sin escribirla. El quinto lugar que aparezca también.
    ///
    /// Escribir la coalescencia en las cuatro proyecciones sería igual de correcto hoy y es
    /// exactamente la forma del problema que se está evitando: cuatro lugares que tienen que estar de
    /// acuerdo. Es el mismo argumento por el que `DeLaCuenta` es privado en `MovimientosConsulta` y
    /// por el que hay una sola `ValidacionDelMovimiento` para el alta y la edición.
    ///
    /// **Por qué funciona también en las dos rutas que son `IQueryable`**: EF Core traduce una
    /// proyección a un tipo que no es una entidad seleccionando las columnas y llamando al
    /// constructor EN MEMORIA, así que este inicializador corre igual. No es una suposición cómoda —
    /// `NotaDelMovimientoTests.Las_Cuatro_Rutas_No_Distinguen_Las_Dos_Formas_De_Sin_Nota_FR011`
    /// recorre las cuatro contra una fila guardada sin valor, y si EF alguna vez materializara de otra
    /// forma se pone en rojo en vez de dejar pasar dos formas de lo mismo.
    /// </summary>
    public string Nota { get; init; } = Nota ?? string.Empty;
}

/// <summary>
/// Lo que llega al modificar un movimiento (RF-14).
///
/// **`Fecha` es obligatoria acá y opcional en el alta, y la diferencia es a propósito.** Ausente
/// significa "hoy" al registrar, que es lo correcto; en una edición sería una trampa — quien mande
/// una modificación sin fecha vería su movimiento saltar a hoy en silencio. Un movimiento editado
/// conserva su fecha salvo que se pida cambiarla, y exigirla es la forma más simple de garantizarlo.
///
/// **`MonedaId` es opcional, y acá ausente significa "la que ya tenía"** — no "la predeterminada",
/// que es lo que significa en el alta. Parecen dos reglas y son una sola: **ausente nunca produce
/// un cambio que nadie pidió**. Es la misma regla que hace obligatoria a `Fecha`, porque ahí
/// ausente sí significaría un valor nuevo.
///
/// Con esto RF-14 queda entero: hasta la feature 009 se podía corregir todo de un movimiento menos
/// su moneda, y esa mitad faltante estaba anotada como deuda esperando el catálogo del ticket 4a.
///
/// No lleva propietario, y si llegara igual en el JSON se descarta al deserializar: el dueño lo
/// decide la sesión (INV-01).
/// </summary>
/// <param name="Tipo">"gasto" o "ingreso". Se valida contra el tipo de la categoría elegida.</param>
/// <param name="Monto">Número con hasta dos decimales.</param>
/// <param name="CategoriaId">Categoría elegida, del mismo tipo que el movimiento.</param>
/// <param name="MonedaId">Moneda del catálogo. Ausente o null = la que el movimiento ya tenía.</param>
/// <param name="Fecha">Día del movimiento. Obligatoria.</param>
/// <param name="Nota">
/// La nota descriptiva, hasta 120 caracteres Unicode. **OBLIGATORIA acá y opcional en el alta**, que
/// es la misma asimetría que <paramref name="Fecha"/> y por el mismo motivo: *ausente nunca puede
/// producir un cambio que nadie pidió*.
///
/// Si ausente significara "la que ya tenía", no habría forma de vaciarla sin inventar un valor
/// centinela; si significara "sin nota", un cliente que no la manda borraría en silencio lo que la
/// persona escribió. Exigirla saca las dos trampas (`FR-004`).
///
/// **Sigue siendo `string?` en el DTO y `null` se RECHAZA**, con la clave `nota`, en el handler del
/// PUT — igual que `Fecha`. En JSON no hay forma de distinguir "vino null" de "no vino", así que
/// aceptar `null` como vaciado dejaba la omisión indistinguible del vaciado explícito: mientras eso
/// fue así, un cuerpo sin la clave borraba la nota en silencio con un 200. Vaciarla es mandar la
/// cadena vacía, que es además lo que la API devuelve para un movimiento sin nota.
/// </param>
public record MovimientoEditadoDto(
    string? Tipo,
    decimal? Monto,
    int? CategoriaId,
    int? MonedaId,
    DateOnly? Fecha,
    string? Nota);

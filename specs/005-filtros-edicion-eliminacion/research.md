# Research — Filtros del listado, edición y eliminación

Las decisiones que el plan toma, con su motivo y lo que se descartó. Cada `D-NN` se cita desde
[plan.md](./plan.md), [data-model.md](./data-model.md) y las tareas.

---

## D-01 · La barrera de aislamiento tiene un agujero, y es exactamente donde esta feature va a escribir

**Decisión**: estrechar la excepción declarada de `BarreraDeAislamientoTests` antes de escribir el
primer endpoint. `Movimientos/MovimientosEndpoints.cs` deja de poder hacer **cualquier cosa** con
`contexto.Movimientos` y pasa a poder hacer sólo **escrituras**: `Add`, `Update`, `Remove`. Toda
lectura, también la de ese archivo, tiene que pasar por el canal.

**El hallazgo, comprobado y no razonado.** La barrera excluye tres rutas de su búsqueda: el canal
de lectura, la declaración del `DbSet` y `MovimientosEndpoints.cs` como *escritura declarada*. Esa
tercera exclusión es por archivo entero, no por operación. Se probó:

```csharp
// dentro de MovimientosEndpoints.cs
rutas.MapGet("/api/movimientos/colado", async (GestionGastosDbContext contexto) =>
    await contexto.Movimientos.ToListAsync());
```

Un endpoint que devuelve **los movimientos de todas las cuentas**. Resultado: el proyecto compila y
`BarreraDeAislamientoTests` pasa **4 de 4 en verde**. La barrera no lo ve.

**Por qué recién ahora importa.** Cuando 004 declaró esa excepción, el único acceso escrito a mano
en ese archivo era el INSERT del alta, y un INSERT no tiene a quién dejar de acotar. Esta feature
introduce **leer-modificar-guardar**: para editar hay que encontrar primero, y ese "encontrar" es
justo la lectura que puede nacer sin acotar. La excepción era correcta para el código que había y
deja de serlo para el que viene. No es un error de 004: es una condición que caducó.

**Cómo se estrecha**: la comprobación deja de preguntar *"¿este archivo toca `Movimientos`?"* y pasa
a preguntar *"¿este archivo toca `Movimientos` de una forma que no sea `Add`, `Update` o
`Remove`?"*. En los otros archivos no cambia nada: cualquier uso sigue siendo infracción.

**Y el Principio V obliga a probarlo.** `verificar-aislamiento.sh` gana un cuarto desarme: colar una
lectura sin acotar dentro de `MovimientosEndpoints.cs` tiene que dar **rojo**. Ese caso da verde con
la barrera de hoy —acabamos de verlo— y tiene que dar rojo con la de mañana. Sin ese paso, el
estrechamiento no está verificado y podría no estar haciendo nada.

**Alternativas descartadas**:

- *Sacar la excepción del todo y mudar también la escritura al canal.* El canal está definido y
  documentado como el canal único de **lectura**; meterle escrituras le cambia la naturaleza y
  vuelve confuso su nombre y su razón de ser. El acotado por cuenta es un problema de lecturas.
- *Dejarlo como está y confiar en escribir las lecturas acotadas.* Es literalmente lo que la
  barrera existe para no tener que hacer.

---

## D-02 · Las tres operaciones nuevas leen por el canal, y la barrera las cubre gratis

**Decisión**: agregar a `MovimientosConsulta` un método `PropioPorId(contexto, usuarioId, id)` que
devuelve `IQueryable<Movimiento>`, y que lo usen los tres endpoints nuevos —el GET individual para
devolver, el PUT y el DELETE para encontrar antes de tocar.

**El motivo es un beneficio ya pago.** `Todas_Las_Consultas_Del_Canal_Acotan_Por_Cuenta` descubre
los métodos del canal **por reflexión**, no por una lista: cualquier método público y estático que
devuelva `IQueryable<Movimiento>` entra solo en su radar y se le exige `usuario_id` en el `WHERE`.
Poner la consulta nueva en el canal la deja vigilada sin escribir una línea de barrera. Ésa era la
apuesta de 004 y esta feature es la primera que la cobra.

**Consecuencia que hay que respetar**: `PropioPorId` acota por cuenta **en la consulta**, no
filtrando después en memoria. Un `FirstOrDefault(m => m.Id == id)` seguido de un
`if (m.UsuarioId != actual)` daría el mismo resultado visible y dejaría el `WHERE` sin `usuario_id`,
o sea la barrera en rojo. Es intencional: se quiere que el acotado esté en la consulta.

---

## D-03 · "Indistinguible de inexistente" se verifica comparando dos respuestas, no dos constantes

**Decisión**: los AC-05, AC-06 y AC-09 se prueban obteniendo **dos** respuestas —una sobre un
identificador de otra cuenta y otra sobre un identificador que no existe— y comparándolas **entre
sí**: mismo código de estado, mismo cuerpo y mismo `Content-Type`.

**Por qué no alcanza afirmar `404` en cada una.** Un test que afirma `404` en los dos casos pasa en
verde aunque los cuerpos difieran: `"El movimiento no existe"` contra `"No tenés permiso sobre este
movimiento"` son los dos 404 y el segundo confirma que el identificador existe. El criterio no es
"responde 404", es "no se puede distinguir", y eso es una relación entre dos respuestas. Un test que
mira una sola no puede expresarla.

El identificador inexistente se consigue registrando un movimiento y eliminándolo, o usando uno muy
por encima del máximo. Se prefiere el primero: un id que **existió** es más parecido al caso real
que un número arbitrario.

**Alternativa descartada**: comparar contra un literal esperado. Ata el test a la redacción del
mensaje y se rompe cada vez que alguien la mejora, sin que el aislamiento haya cambiado.

---

## D-04 · `RangoDelMes` se generaliza a `RangoDeFechas`

**Decisión**: introducir `RangoDeFechas(Desde, Hasta)` como el concepto general, y dejar la
construcción del mes como una fábrica sobre él. El listado sin filtros sigue pidiendo el mes; el
listado con filtros pide un rango arbitrario.

**Motivo**: hoy `RangoDelMes` sólo se puede construir con `De(hoy)`, que devuelve el mes calendario
de esa fecha. Es exactamente lo que FR-007 necesitaba y no sirve para FR-012. El invariante que hay
que conservar es que el rango lleva **sus dos extremos incluidos**, que es lo que AC-14 exige y lo
que un par de `DateOnly` sueltos no dice en ningún lado.

**El invariante `Desde <= Hasta` vive en el tipo**, y es lo que hace que FR-015 —rechazar el rango
invertido— sea una validación de borde y no una condición dispersa por la consulta.

**Detalle que la barrera obliga a no olvidar**: `ArgumentosDePrueba`, en
`BarreraDeAislamientoTests`, mapea tipo de parámetro a valor de prueba, y **lanza a propósito** si
encuentra uno que no conoce, con el mensaje *"esa consulta queda sin vigilar"*. Al cambiar el tipo
del parámetro del canal hay que registrarlo ahí. No es un obstáculo: es la barrera haciendo su
trabajo, y el test lo dice con esas palabras.

---

## D-05 · La edición tiene su propio DTO, y la validación se comparte

**Decisión**: `MovimientoEditadoDto` aparte de `NuevoMovimientoDto`, con `fecha` **obligatoria**. La
validación se comparte: `ValidacionDelAlta` pasa a llamarse `ValidacionDelMovimiento` y la usan los
dos caminos.

**El motivo de separar los DTO** es un campo: en el alta, `fecha` ausente significa "hoy" y lo pone
el servidor (AC-17 de la feature 001). En una edición eso es una trampa — quien mande una
modificación sin fecha vería su movimiento saltar a hoy en silencio. Un `Movimiento` editado
conserva su fecha salvo que se pida cambiarla, y la forma más simple de garantizarlo es exigirla.

Hay precedente en el repositorio: `NuevaCuenta` y `Credenciales` tienen hoy exactamente la misma
forma y son tipos separados a propósito, porque son dos contratos que pueden divergir. Éste ya
diverge.

**El motivo de compartir la validación** es FR-003: un movimiento no puede quedar, por vía de una
edición, en un estado que el alta habría rechazado. Dos validaciones paralelas es la forma segura de
que eso pase el día que una de las dos cambie.

**El nombre cambia porque la clase deja de ser del alta.** Dejarla llamándose `ValidacionDelAlta`
mientras la usa la edición es la clase de mentira chica que después cuesta cara.

---

## D-06 · Todo lo que no es tuyo responde igual que lo que no existe: `404`, nunca `403`

**Decisión**: un `404` con el mismo cuerpo para los tres casos: el movimiento no existe, es de otra
cuenta, o ya fue eliminado.

**Motivo**: es FR-008 escrito en HTTP. Un `403` sobre un movimiento ajeno confirma que ese
identificador existe, y como los identificadores son autoincrementales y contiguos, confirmarlo
permite recorrerlos y contar los movimientos de otra cuenta sin ver ninguno. La existencia también
es información.

El `404` de "ya eliminado" cae del mismo lado sin esfuerzo: después de borrarlo, el movimiento no
existe, y no hay nada que distinguir. AC-10 es casi una consecuencia.

**Alternativa descartada**: `403` para lo ajeno y `404` para lo inexistente, que es lo que muchas
APIs hacen. Es más informativo para quien depura y es exactamente la fuga que AC-05 prohíbe.

---

## D-07 · El filtro por categoría no lleva índice nuevo

**Decisión**: no se agrega ningún índice. El índice `(usuario_id, fecha DESC, id DESC)` sigue
cubriendo el acotado por cuenta y el rango de fechas; el filtro por categoría se resuelve sobre las
filas que ese índice ya redujo.

**Motivo**: el orden de selectividad es el correcto. La cuenta y el rango de fechas recortan
primero, y sobre lo que queda —los movimientos de una persona en un período— filtrar por categoría
es barato. Un índice por categoría ayudaría en un escenario que este producto no tiene: una cuenta
con cientos de miles de movimientos en un solo mes.

**Queda anotado** por si algún día lo justifica, pero agregarlo ahora sería una migración sin
evidencia, y las migraciones son el tipo de cosa que conviene deberle al futuro y no al revés.

---

## D-08 · Los filtros viajan como parámetros de consulta y **no** entran en `tipos.ts`

**Decisión**: los filtros son parámetros de la URL, no un cuerpo. Por eso no agregan ningún tipo al
contrato, y la verificación del contrato no los cubre.

**Motivo, y su límite, dicho de frente**: los tests de `Contrato/` comparan las interfaces de
`tipos.ts` contra el JSON que la API emite y acepta. Un parámetro de consulta no es JSON y no tiene
dónde compararse. Eso significa que **los filtros quedan fuera de la red que atrapa las
desalineaciones del contrato**, y que un rename de `categoriaId` a `categoria` en el backend no
haría ruido en ningún lado.

No se inventa un mecanismo nuevo para cubrirlos: sería una barrera sin cicatriz que la justifique, y
este repositorio ya tiene cuatro. Se anota como límite conocido. Lo que **sí** entra al contrato es
`MovimientoEditado`, que es un cuerpo y sí se compara.

---

## D-09 · El borrado es físico, y la carrera se resuelve sola

**Decisión**: `DELETE` borra la fila. No hay baja lógica, ni columna de estado, ni migración.

**Motivo**: está en las *Assumptions* de la spec — el PRD usa "baja lógica" explícitamente para las
categorías (RF-09) y no para los movimientos (RF-15), y el contraste parece deliberado.

**La carrera**: dos operaciones sobre el mismo movimiento a la vez. La segunda encuentra que la
consulta acotada no devuelve nada y responde `404`, que es lo que AC-10 pide. No hace falta control
de concurrencia optimista: no hay ningún estado que se pueda corromper, sólo una operación que llega
tarde a un movimiento que ya no está.

---

## Riesgos de esta feature

| Riesgo | Por qué es real | Qué lo contiene |
|---|---|---|
| **La superficie nueva nace sin aislar** | Es código nuevo, y el acotado se escribe a mano | D-01 + D-02: la barrera estrechada, y el canal que vigila por reflexión |
| **Los tests de filtros dependen del día en que corren** | El listado sin filtros recorta al mes en curso del servidor | Reloj clavado con `FactoriaConReloj` en todo test que toque el listado, como en 004 |
| **La barrera se estrecha después de los endpoints** | Sería escribirla sabiendo qué tiene que dejar pasar | El plan la pone en su propia fase, primero, y exige ver su rojo antes |
| **El contrato se desalinea sin que nadie lo note** | Los filtros no están cubiertos por la verificación (D-08) | Nada, y está dicho. Es un límite conocido, no una omisión |
| **La edición deja pasar lo que el alta rechaza** | Dos caminos de validación divergen con el tiempo | D-05: una sola validación compartida, no dos parecidas |

# Contrato HTTP — Aislamiento entre cuentas verificado

**El contrato no cambia, y eso es parte del entregable.**

No es una nota al pie. Un aislamiento que se arregla cambiando una respuesta no es este ticket: si
al verificar apareciera que hace falta un código nuevo, un campo nuevo o un mensaje distinto,
significaría que el aislamiento **no** estaba y que esta feature dejó de ser de verificación. La
barrera del contrato (`verificar-contrato.sh`) corre en la puerta de cierre justamente para
comprobar que nada de eso pasó.

---

## Endpoints alcanzados

Los dos que existen. Los otros cuatro que el PRD nombra no están en este repositorio
([D-01](../research.md)).

### `POST /api/movimientos`

Registra un movimiento **a nombre de la cuenta de la sesión**.

- **Petición**: `NuevoMovimientoDto` — `tipo`, `monto`, `categoriaId`, `fecha` (opcional).
- **No hay campo de propietario, y no se agrega ninguno.** Un `usuarioId` que llegue en el JSON es
  un campo desconocido: se descarta al deserializar y no influye sobre nada.
- **Respuesta**: `201` con el movimiento creado. Sin encabezado `Location`, porque no existe
  `GET /api/movimientos/{id}` al que apuntar.
- **Lo que esta feature verifica**: que el movimiento quede a nombre de quien lo registró, aunque la
  petición diga otra cosa, y que el listado de las demás cuentas no cambie.

### `GET /api/movimientos`

Devuelve los movimientos **de la cuenta de la sesión**, del mes en curso del servidor.

- **Petición**: sin parámetros. El recorte al mes actual no es un control del cliente; los filtros
  de rango llegan con FEAT-001b.
- **Respuesta**: `200` con un arreglo de `MovimientoDto`. Un mes sin movimientos es un arreglo
  vacío, no un `404`.
- **Lo que esta feature verifica**: que el arreglo no contenga ningún movimiento de otra cuenta, ni
  siquiera cuando las dos cuentas tienen movimientos en el mismo mes, en la misma fecha y con la
  misma categoría.

---

## Sobre la indistinguibilidad de las respuestas

NFR-01 del PRD pide que acceder a un dato ajeno responda igual que pedir uno que no existe. **En
esta feature no hay dónde aplicarlo**, y conviene decir por qué en lugar de darlo por cumplido:

- El listado nunca recibe un identificador, así que no puede confirmar ni desmentir la existencia de
  nada ajeno. Simplemente no lo devuelve.
- El alta tampoco: si la categoría no es del sistema ni de la cuenta, responde el mismo error de
  validación que ante una categoría inexistente — y eso ya lo verifica `001`.

Los tres endpoints que reciben un identificador de movimiento —`GET`, `PUT` y `DELETE` por `{id}`—
son exactamente los que faltan. NFR-01 queda entero en la tabla de *Deuda registrada* de la spec.

## Endpoints explícitamente fuera

- **`GET /api/categorias`**: catálogo global sin propietario. Su aislamiento es el ticket 3.
- **`/api/cuentas` y `/api/sesion`**: son `01a` y `01b`. Su protección es la barrera de
  autorización, que ya existe y corre en la misma puerta.

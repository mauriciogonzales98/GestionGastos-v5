# Contrato HTTP — Movimientos

Los cinco endpoints después de esta feature. Los dos primeros ya existen y se documentan para que
el contrato se lea entero; **el listado cambia**, los tres últimos son nuevos.

Todos exigen sesión. Sin cookie válida responden `401` y no ejecutan su efecto — lo garantiza
`verificar-autorizacion.sh`, y los tres endpoints nuevos entran en su radar sin hacer nada.

---

## Tipos del contrato

Definidos en `frontend/src/api/tipos.ts` y comparados contra el JSON real por los tests de
`backend/GestionGastos.Api.Tests/Contrato/`.

**`Movimiento`** *(sin cambios)* — la forma con la que un movimiento se devuelve, la misma en los
cuatro lugares donde aparece: alta, listado, consulta individual y respuesta de la edición.

**`NuevoMovimiento`** *(sin cambios)* — lo que se manda al registrar. `fecha` opcional = hoy.

**`MovimientoEditado`** *(nuevo)* — lo que se manda al modificar:

```ts
export interface MovimientoEditado {
  tipo: TipoMovimiento;
  monto: number;
  categoriaId: number;
  /** `YYYY-MM-DD`. **Obligatoria**, a diferencia del alta: una edición sin fecha movería el
      movimiento a hoy en silencio (D-05). */
  fecha: string;
}
```

**`ProblemDetails`** *(sin cambios)* — el formato único de error, con `errors` por campo.

---

## `POST /api/movimientos` *(existente, sin cambios)*

Registra un movimiento. `201` con el `Movimiento` creado.

**Cambio menor pero real**: hoy responde sin encabezado `Location`, con este comentario en el
código — *"Sin Location: no existe GET /api/movimientos/{id}, así que la URL apuntaría a un 404.
Cuando FEAT-001b agregue la lectura individual, vuelve con su ruta de verdad"*. Esa condición se
cumple en esta feature, así que **el `Location` vuelve**, apuntando a la ruta nueva.

---

## `GET /api/movimientos` *(existente, **cambia**)*

Devuelve los movimientos propios que pasan los filtros, ordenados por `fecha DESC, id DESC`.

### Parámetros de consulta *(todos opcionales)*

| Parámetro | Formato | Ausente significa |
|---|---|---|
| `desde` | `YYYY-MM-DD` | El primer día del mes en curso **del servidor** |
| `hasta` | `YYYY-MM-DD` | El último día del mes en curso **del servidor** |
| `categoriaId` | entero | Todas las categorías |

Los tres se combinan con **y**: un movimiento sale si cumple todos los que se pidieron.

Los extremos del rango **se incluyen**: un movimiento fechado exactamente en `desde` o en `hasta`
entra.

> **`desde` y `hasta` van juntos.** Pedir uno solo se rechaza. Media condición de rango invita a
> suponer un extremo abierto que nadie declaró, y ese supuesto es distinto para cada quien.

### Respuestas

| Código | Cuándo | Cuerpo |
|---|---|---|
| `200` | Siempre que los filtros sean válidos | `Movimiento[]`, posiblemente **vacío** |
| `400` | `desde > hasta`, sólo uno de los dos, o una fecha mal formada | `ProblemDetails` con `errors` |

**El arreglo vacío no es un `404`.** Un filtro que no deja pasar nada devuelve `[]` — es FR-016, y
es coherente con lo que el listado ya hacía con un mes sin movimientos.

**Un `categoriaId` inexistente o ajeno no es un error**: devuelve `[]`. Rechazarlo con `400`
confirmaría cuáles existen, que es la misma fuga que D-06 cierra en las rutas por identificador.

---

## `GET /api/movimientos/{id}` *(nuevo)*

Devuelve un movimiento propio.

| Código | Cuándo | Cuerpo |
|---|---|---|
| `200` | El movimiento existe y es de quien pide | `Movimiento` |
| `404` | No existe, **o es de otra cuenta**, o ya fue eliminado | `ProblemDetails`, **el mismo en los tres casos** |

---

## `PUT /api/movimientos/{id}` *(nuevo)*

Reemplaza los campos editables de un movimiento propio. No es un parche: se manda el estado final
completo.

**Cuerpo**: `MovimientoEditado`.

| Código | Cuándo | Cuerpo |
|---|---|---|
| `200` | Se modificó | El `Movimiento` con sus valores nuevos |
| `400` | El cuerpo no pasa la validación | `ProblemDetails` con `errors` por campo |
| `404` | No existe, **o es de otra cuenta**, o ya fue eliminado | `ProblemDetails`, el mismo que el `GET` |

**Lo que el cuerpo no puede tocar**, dicho para que quede escrito:

- **El propietario.** No es un campo del contrato, y si llegara igual se descarta. Lo decide la
  sesión (INV-01).
- **La moneda.** Fuera de alcance en esta feature.
- **El identificador.** Está en la ruta, no en el cuerpo.

**El `tipo` viaja y a la vez se deriva.** Se manda —igual que en el alta— y se valida contra el tipo
de la categoría elegida; si no coinciden, es un `400`. No es redundante: es lo que evita que un
cambio de categoría convierta un gasto en ingreso sin que quien lo pidió lo supiera.

**Orden de comprobación**: primero se busca el movimiento acotado por cuenta, después se valida el
cuerpo. Un movimiento ajeno con un cuerpo inválido responde `404` y no `400`: un `400` confirmaría
que el identificador existe y que se llegó a mirar el cuerpo.

---

## `DELETE /api/movimientos/{id}` *(nuevo)*

Elimina un movimiento propio. Definitivo, sin baja lógica ([D-09](../research.md)).

| Código | Cuándo | Cuerpo |
|---|---|---|
| `204` | Se eliminó | Vacío |
| `404` | No existe, **o es de otra cuenta**, o ya fue eliminado | `ProblemDetails`, el mismo que el `GET` |

**No es idempotente en el código de respuesta, y es a propósito.** Borrar dos veces da `204` y
después `404`. Podría devolver `204` siempre —sería más idempotente— pero eso convertiría al `DELETE`
en el único endpoint que responde distinto que los otros dos ante lo inexistente, y esa asimetría es
observable: mandar `DELETE` a un identificador ajeno daría `204` y confirmaría nada, pero mandarlo a
uno inexistente también, y entonces el `404` de las otras dos rutas quedaría solo delatando. La
uniformidad vale más que la idempotencia acá.

---

## La regla que atraviesa las tres rutas nuevas

> Un movimiento que no es tuyo responde **exactamente igual** que uno que no existe. Mismo código,
> mismo cuerpo, mismo `Content-Type`.

No es una recomendación: es FR-008, y se verifica comparando las dos respuestas **entre sí**, no
cada una contra un `404` esperado ([D-03](../research.md)). Los identificadores son
autoincrementales y contiguos, así que cualquier diferencia observable permite recorrerlos y contar
los movimientos de otra cuenta sin ver ninguno.

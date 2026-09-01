# Data Model — Filtros del listado, edición y eliminación

**No hay migración.** La entidad `Movimiento` no cambia de forma: no gana ni pierde columnas, y sus
tipos y restricciones quedan como están. Lo que cambia es su **ciclo de vida**. Si al implementar
aparece una migración, algo se salió del alcance.

---

## Entidades

### Movimiento *(existente, sin cambios de forma)*

El hecho registrado. Su comentario de código dice hoy *"Se crea y no cambia. La edición y la baja
llegan en FEAT-001b"*: esta feature es la que lo desactualiza, y ese comentario hay que corregirlo.

| Campo | Tipo | Nota para esta feature |
|---|---|---|
| `Id` | `long` | No cambia nunca. Es lo que identifica al movimiento en las tres rutas nuevas |
| `UsuarioId` | `long` | **Nunca cambia.** Ni por edición, ni por nada — INV-01 |
| `Tipo` | `TipoMovimiento` | **Se deriva de la categoría**, no se envía. Cambiar la categoría puede cambiarlo — INV-03 |
| `Monto` | `decimal(11,2)` | Editable |
| `MonedaId` | `short` | **No editable en esta feature.** Ver *Deuda registrada* de la spec |
| `CategoriaId` | `int` | Editable, con el mismo criterio de búsqueda que el alta — INV-04 |
| `Fecha` | `DateOnly` | Editable, y obligatoria al editar — D-05 |

### RangoDeFechas *(nuevo, no se persiste)*

Un período con **sus dos extremos incluidos**. Generaliza a `RangoDelMes`, que sólo sabía construir
meses calendario ([D-04](./research.md)).

| Campo | Tipo | Invariante |
|---|---|---|
| `Desde` | `DateOnly` | `Desde <= Hasta` — INV-05 |
| `Hasta` | `DateOnly` | Inclusivo: un movimiento fechado exactamente en `Hasta` entra |

Vive en el dominio, no en la petición: el listado sin filtros lo construye a partir del mes en curso
del servidor, y el listado con filtros a partir de lo que se pidió. Quien consume la consulta no
distingue de dónde vino, y ése es el punto.

### FiltroDelListado *(nuevo, no se persiste)*

Lo que acota el listado. No es una entidad de dominio: vive en la petición y se traduce a
condiciones de consulta.

| Campo | Ausente significa |
|---|---|
| Rango de fechas | El mes en curso **del servidor** — FR-013 |
| Categoría | Todas las categorías — FR-011 |

---

## Invariantes

Cada una tiene su AC, y cada AC su test. Una invariante sin test es una intención.

| # | Invariante | Se rompe si… | AC |
|---|---|---|---|
| **INV-01** | El propietario de un movimiento no cambia nunca | La edición toma `usuarioId` de la petición en lugar de la sesión | AC-04 |
| **INV-02** | Ninguna operación por identificador alcanza un movimiento ajeno | La consulta busca por `Id` sin acotar por cuenta, y el chequeo del dueño queda en memoria | AC-05, AC-06, AC-09 |
| **INV-03** | El tipo del movimiento coincide siempre con el de su categoría | La edición acepta una categoría de un tipo y deja el tipo viejo | AC-02 |
| **INV-04** | La categoría de un movimiento es siempre una que su dueño puede usar | La edición busca la categoría sólo por identificador | AC-07 |
| **INV-05** | Un rango tiene `Desde <= Hasta` | Se acepta un rango invertido y se devuelve una lista vacía | FR-015 |
| **INV-06** | Un movimiento editado no queda en un estado que el alta habría rechazado | Edición y alta validan por caminos distintos | AC-07 |

**INV-02 merece un párrafo.** Su forma correcta es *"la consulta no lo devuelve"*, no *"lo
devolvemos y después chequeamos"*. Las dos producen el mismo `404`, y sólo la primera deja el
`usuario_id` en el `WHERE`, que es lo que `BarreraDeAislamientoTests` exige por reflexión
([D-02](./research.md)). Es un caso donde la barrera fuerza la forma buena en vez de sólo detectar
la mala.

---

## Transiciones de estado

Hasta hoy un movimiento tenía un estado: **existe**. Se creaba y no cambiaba.

```text
                 alta
        (nada) ────────► existe ──┐
                          │       │ edición  (misma identidad, mismo dueño,
                          │       │           otros valores)
                          │◄──────┘
                          │
                          │ eliminación
                          ▼
                       (nada)
```

Tres cosas que este diagrama dice y conviene leer despacio:

1. **La edición no crea ni destruye.** Es la misma fila, el mismo `Id` y el mismo `UsuarioId`. Es
   por eso que INV-01 no es una regla de negocio sino la definición de qué es editar.
2. **De "(nada)" no se vuelve.** No hay deshacer ([D-09](./research.md)), así que la eliminación es
   la única transición irreversible del modelo.
3. **No hay estado intermedio.** Sin baja lógica no hay "eliminado pero presente", y por eso no hace
   falta que ninguna consulta filtre por estado. Es la simplicidad que se compra al no hacer baja
   lógica, y el precio es el punto 2.

---

## Lo que NO cambia

- **El esquema.** Cero migraciones.
- **La forma de `MovimientoDto`.** La consulta individual devuelve exactamente lo mismo que ya
  devuelven el alta y el listado. Una forma sola para el mismo concepto.
- **El orden del listado.** Sigue siendo `fecha DESC, id DESC`, pedido explícitamente por la
  consulta y no heredado del índice — la razón está en la D-04 de la feature 001 y sigue vigente.
- **El catálogo de categorías.** Sigue siendo el global; las propias son el ticket 3.
- **La moneda.** No se elige al registrar y tampoco al editar.

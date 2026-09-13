# Data Model: Cada cuenta con sus propias categorías predefinidas

**Fecha**: 2026-09-12 | **Spec**: [spec.md](./spec.md) | **Decisiones**: [research.md](./research.md)

---

## `categoria` — el dueño deja de ser opcional

| Columna | Antes | Después | Nota |
|---|---|---|---|
| `id` | `int`, PK | sin cambios | |
| `nombre` | `varchar(50)`, `utf8mb4_0900_ai_ci` | sin cambios | |
| `tipo` | `tinyint` | sin cambios | |
| `usuario_id` | `bigint` **NULL** | `bigint` **NOT NULL** | `NULL` significaba "predefinida del sistema". Deja de existir |
| `activa` | `tinyint(1)` | sin cambios | |
| `discriminador` | `bigint` | sin cambios | Sigue siendo lo que deja que un nombre dado de baja libere su casillero |

**Restricciones**

| Nombre | Antes | Después |
|---|---|---|
| `ux_categoria_ambito_nombre_tipo` | `UNIQUE (usuario_id, nombre, tipo, discriminador)` | **Sin cambios en su forma, pero cambia lo que garantiza**: sin `NULL` en `usuario_id`, ahora cubre de verdad la unicidad dentro del ámbito. Antes dejaba pasar una propia homónima de una predefinida, porque para MySQL `NULL` y `7` son claves distintas (D-02 de la 007) |
| `FK_categoria_usuario_usuario_id` | existe | sin cambios |
| **clave alternativa** `(id, usuario_id)` | no existe | **nueva**. Es lo que la foránea compuesta de `movimiento` necesita como destino. `id` ya es único por sí solo, así que no restringe nada nuevo: existe para poder ser referenciada |

**Regla de negocio que cambia**: ya no hay categorías de solo lectura. Toda fila del catálogo de una
cuenta se puede renombrar y dar de baja (`FR-015`, `FR-016`).

**Regla de negocio que NO cambia**: la baja es lógica, es idempotente, y una categoría dada de baja
sigue clasificando los movimientos que ya la usaban.

---

## `movimiento` — no cambia de forma, gana un piso

| Columna | Cambio |
|---|---|
| todas | **ninguno** |

**Restricciones**

| Nombre | Antes | Después |
|---|---|---|
| `FK_movimiento_categoria_categoria_id` | `FOREIGN KEY (categoria_id) → categoria (id)` | **se elimina**: la compuesta la contiene |
| `fk_movimiento_categoria_del_ambito` | no existe | **nueva**: `FOREIGN KEY (categoria_id, usuario_id) → categoria (id, usuario_id)` |
| `FK_movimiento_usuario_usuario_id` | existe | sin cambios. `usuario_id` pasa a participar de **dos** foráneas, y eso hay que verificarlo con un test de esquema en vez de darlo por hecho |
| `ck_movimiento_monto_positivo`, `ck_movimiento_nota_sin_cadena_vacia` | existen | sin cambios |
| `IX_movimiento_categoria_id` | existe | probablemente lo reemplace el índice que la foránea compuesta necesita; hay que mirar lo que EF genere y no dejar dos índices que empiezan por la misma columna |

**La invariante que ahora vive en la base**

```
Para todo movimiento m:  existe categoria c  tal que  c.id = m.categoria_id  Y  c.usuario_id = m.usuario_id
```

Es la que hasta hoy sostenían sólo las dos comprobaciones de `MovimientosEndpoints`. Esas
comprobaciones **se conservan**: la foránea es el piso, no el reemplazo (`FR-007`).

**Lo que esta invariante habilita, y que era el verdadero motivo de D7-07**: `MovimientosConsulta`
proyecta `m.Categoria!.Nombre`, un `JOIN` contra `categoria` que no pasa por el canal único y que
`BarreraDeAislamientoTests` no puede ver —lo dice en su propio comentario: *"hoy es seguro y no por
construcción"*. A partir de esta feature lo es por construcción.

---

## `usuario` — sin cambios de forma

Gana una consecuencia al nacer: diez categorías propias, en la misma transacción que lo crea
(`FR-002`, `FR-003`).

---

## Transición de los datos existentes

Medido sobre `gestiongastos` el 2026-09-12: 9 cuentas, 10 predefinidas compartidas, 8 propias,
6 movimientos, **5 de ellos apuntando a una predefinida**.

| Estado inicial | Estado final |
|---|---|
| 10 categorías con `usuario_id IS NULL` | 0 |
| 8 categorías propias | **8, intactas**: mismo id, nombre, tipo y estado de baja (`FR-013`) |
| — | 90 copias nuevas (9 cuentas × 10), activas, con dueño |
| 5 movimientos apuntando a una compartida | 5 apuntando a la copia **de su propio dueño**, misma categoría por nombre y tipo (`FR-011`) |

El emparejamiento va por una columna temporal y no por nombre: una cuenta puede tener una categoría
propia **dada de baja** homónima de una predefinida, y emparejar por nombre reapuntaría movimientos a
la categoría equivocada en silencio (D-03 de research).

**Quién verifica que salió bien**: las restricciones mismas. `usuario_id NOT NULL` falla si quedó
una categoría sin dueño; la foránea compuesta falla si quedó un movimiento fuera de su ámbito. La
migración corre en transacción, así que un fallo deja la base como estaba (`FR-014`).

---

## Lo que NO se toca

- La semilla de `moneda`: las monedas siguen siendo del sistema y no se copian por cuenta.
- `intento_de_acceso`, `usuario.contrasena_hash` y todo lo de identidad.
- La forma del movimiento en el contrato.

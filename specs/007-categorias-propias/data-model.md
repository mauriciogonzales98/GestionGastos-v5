# Data Model — Categorías propias del usuario

Lo que cambia en el modelo. Casi todo ya existe: la feature 001 anticipó el ámbito y la baja lógica
(su D-06), así que esta feature agrega **una** columna y rehace **un** índice.

---

## `Categoria`

| Campo | Tipo | Estado | Regla |
|---|---|---|---|
| `Id` | `int`, PK | ya existe | Las diez predefinidas tienen ids fijos, sembrados, que no cambian nunca (SC-005) |
| `Nombre` | `string`, `varchar(50)` | ya existe | 1 a 50 caracteres después de recortar espacios (FR-006). El límite es el de la columna, no el que el PRD citó |
| `Tipo` | `TipoMovimiento`, `tinyint` | ya existe | Gasto o ingreso. **Inmutable**: no se puede cambiar después de crear (Assumptions) |
| `UsuarioId` | `long?` | ya existe | `NULL` = predefinida del sistema; con valor = propia de esa cuenta. FK a `usuarios` con `Restrict` |
| `Activa` | `bool`, `bit(1)` | ya existe | `false` = dada de baja. Camino de ida: no hay forma de reactivar |
| **`Discriminador`** | **`long`, `bigint NOT NULL DEFAULT 0`** | **nuevo** | `0` mientras está activa; su propio `Id` al darla de baja. Existe **sólo** para el índice único (D-01) |

### El índice

```
UNIQUE (usuario_id, nombre, tipo, discriminador)   ← ux_categoria_ambito_nombre_tipo
```

Reemplaza a `UNIQUE (usuario_id, nombre, tipo)`. Qué garantiza y qué no:

| Situación | ¿Choca? | Quién lo decide |
|---|---|---|
| Dos activas de la misma cuenta, mismo nombre y tipo | **Sí** | El índice: las dos tienen `discriminador = 0` |
| Una activa y una dada de baja, mismo nombre y tipo | No | El índice: `0` contra un `id`. Es FR-009 |
| Varias dadas de baja homónimas | No | El índice: cada una lleva su propio `id` |
| Dos cuentas distintas, mismo nombre y tipo | No | El índice: distinto `usuario_id`. Es AC-08, y ya funcionaba |
| Una propia contra una **predefinida** homónima | **No choca en el índice** | La **aplicación**, con la consulta de ámbito (D-02). Para MySQL `NULL` y `7` son claves distintas |

**Las mayúsculas y los acentos no hacen falta normalizarlos**: la collation `utf8mb4_0900_ai_ci` ya
los ignora, en el índice y en las comparaciones. Los espacios al borde sí: se recortan al recibir.

### Transiciones de estado

```
                 crear                    dar de baja
   (no existe) ─────────► Activa ─────────────────────────► Dada de baja
                            │  Discriminador = 0              Discriminador = Id
                            │                                        │
                            └──── renombrar (conserva el tipo) ◄──────┘ ✗ no hay vuelta
```

- **Crear** exige nombre y tipo. Nace activa y con `Discriminador = 0`.
- **Renombrar** cambia el nombre y **nada más**. Valida la misma unicidad que crear (Clarificación
  1). El `Id` no cambia, así que los movimientos siguen apuntando a la misma fila y ven el nombre
  nuevo sin que nadie los toque (AC-04).
- **Dar de baja** apaga `Activa` y escribe `Discriminador = Id`, en el mismo `UPDATE`. Idempotente:
  hacerlo dos veces devuelve lo mismo (D-06).
- **Reactivar no existe.** El camino de vuelta es crear una nueva con el mismo nombre (AC-09).

---

## Qué categorías ve una cuenta

Un solo predicado, escrito una sola vez en el canal (D-03), con dos variantes según para qué:

| Para qué | Predicado |
|---|---|
| **Ofrecer** (el catálogo, FR-002) | `(usuario_id IS NULL OR usuario_id = @yo) AND activa` |
| **Comprobar unicidad** (FR-005) | el mismo, más `nombre = @nombre AND tipo = @tipo` |
| **Modificar o dar de baja** (FR-012) | `usuario_id = @yo` — **sólo propias**, y ni siquiera las predefinidas que sí ve |
| **Validar el alta de un movimiento** (FR-021, FR-022) | `(usuario_id IS NULL OR usuario_id = @yo) AND activa` — ya existe |
| **Validar la edición de un movimiento** (FR-023) | el mismo, **o** que sea la categoría que ese movimiento ya tenía |

La diferencia entre la primera fila y la tercera es la que hace cumplir FR-008: una predefinida se
ve pero no se toca, y por eso la consulta que ofrece y la que modifica no pueden ser la misma.

---

## `Movimiento`

**No cambia.** Sigue apuntando a `CategoriaId` con una FK, y esa fila sigue existiendo aunque se dé
de baja — que es la razón de que la baja sea lógica y no un `DELETE`.

Lo único que cambia es alrededor: la consulta que valida la categoría en la edición (FR-023) y la
que **no** debe filtrar por `Activa` (`MovimientosConsulta.Agrupado`, D-05).

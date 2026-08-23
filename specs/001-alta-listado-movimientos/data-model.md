# Phase 1 — Data Model: Alta de movimientos y listado simple

Entidades derivadas de *Key Entities* de la [spec](./spec.md), con las restricciones que exigen
FR-004 a FR-011. Las decisiones de tipo y su motivo están en [research.md](./research.md).

Nombres de tabla y columna en `snake_case`; nombres de clase y propiedad en C# como manda el
lenguaje.

---

## `movimiento`

El hecho registrado: dinero que salió (**gasto**) o que entró (**ingreso**) de la cuenta.

| Columna | Tipo | Restricciones | Motivo |
|---------|------|---------------|--------|
| `id` | `bigint` | PK, autoincremental | También es el desempate del orden del listado ([D-04](./research.md)) |
| `usuario_id` | `bigint` | `NOT NULL`, FK → `usuario.id` | FR-010. Se asigna desde `IUsuarioActual` en cada `INSERT`, a mano |
| `tipo` | `tinyint` | `NOT NULL`, 0 = gasto, 1 = ingreso | Distingue las dos mitades del dominio (FR-001, FR-002) |
| `monto` | `decimal(11,2)` | `NOT NULL`, `CHECK (monto > 0)` | FR-004 y FR-004b. `decimal(11,2)` topa exactamente en 999.999.999,99 ([D-01](./research.md)) |
| `moneda_id` | `smallint` | `NOT NULL`, FK → `moneda.id` | FR-009. Dato del movimiento, no constante del código |
| `categoria_id` | `int` | `NOT NULL`, FK → `categoria.id` | FR-005. `NOT NULL` es lo que hace imposible un movimiento sin categoría |
| `fecha` | `date` | `NOT NULL` | FR-003. Sin hora ni zona horaria ([D-02](./research.md)) |

**Índice**: `ix_movimiento_usuario_fecha` sobre `(usuario_id, fecha DESC, id DESC)`.

Sirve al listado de FR-007 y FR-008, y es el que el ticket 5 va a necesitar con 10.000 movimientos
(RNF-01). **Cuidado al testear el orden**: este índice hace que MySQL devuelva las filas ya
ordenadas aunque la consulta no lo pida, así que un test que sólo mire el resultado pasa en verde
con el `OrderBy` borrado. Por eso [D-04](./research.md) exige verificarlo en doble capa.

**Reglas que no viven en el esquema** (van en la capa de aplicación, porque necesitan dar un motivo
legible — [D-08](./research.md)):

- El monto no puede tener más de dos decimales (FR-004). El esquema redondearía en silencio; la
  aplicación rechaza.
- El monto no puede superar 999.999.999,99 (FR-004b). El esquema daría un error genérico de
  almacenamiento; la aplicación da el motivo.
- La categoría tiene que ser del mismo tipo que el movimiento (FR-011). Es una regla entre dos
  tablas que ninguna clave foránea expresa.

**Ciclo de vida**: se crea y no cambia. La edición y la baja llegan en FEAT-001b.

---

## `categoria`

| Columna | Tipo | Restricciones | Motivo |
|---------|------|---------------|--------|
| `id` | `int` | PK, autoincremental | |
| `nombre` | `varchar(50)` | `NOT NULL` | |
| `tipo` | `tinyint` | `NOT NULL`, 0 = gasto, 1 = ingreso | FR-006: el formulario ofrece sólo las del tipo que se carga |
| `usuario_id` | `bigint` | **nullable**, FK → `usuario.id` | `NULL` = predefinida del sistema. Lleno = propia de esa cuenta |
| `activa` | `bit(1)` | `NOT NULL`, default `1` | Baja lógica de RF-09 |

`usuario_id` nullable y `activa` son **anticipos deliberados** del ticket 3 (*Categorías propias*).
No se usan en esta feature —todas las filas nacen con `usuario_id = NULL` y `activa = 1`— pero
estar desde el principio evita migrar la tabla y reescribir las consultas que la tocan
([D-06](./research.md)).

**Restricción**: `UNIQUE (usuario_id, nombre, tipo)`. Impide dos categorías con el mismo nombre y
tipo dentro del mismo ámbito.

**Semilla — las diez de FR-006, exactamente**:

| Tipo | Nombres |
|------|---------|
| Gasto | Comida, Transporte, Vivienda, Servicios, Salud, Ocio, Otros |
| Ingreso | Sueldo, Ingreso extra, Otros |

`Otros` aparece en los dos tipos: son dos filas distintas, y la restricción `UNIQUE` las admite
porque difieren en `tipo`.

---

## `moneda`

| Columna | Tipo | Restricciones | Motivo |
|---------|------|---------------|--------|
| `id` | `smallint` | PK | |
| `codigo` | `char(3)` | `NOT NULL`, `UNIQUE` | ISO 4217: `ARS`, `USD` |
| `nombre` | `varchar(30)` | `NOT NULL` | Lo que se muestra |
| `simbolo` | `varchar(5)` | `NOT NULL` | |
| `decimales` | `tinyint` | `NOT NULL`, default `2` | Los decimales admitidos son dato de la moneda, no constante (PRD, *Supuestos abiertos*) |
| `es_predeterminada` | `bit(1)` | `NOT NULL`, default `0` | RF-25. Exactamente una fila en `1` |

**Semilla**: `ARS` (pesos, predeterminada) y `USD` (dólares), como fija RF-31.

Ser tabla y no enum es lo que hace posible RF-32 —sumar una moneda **sin modificar el código**— y
es lo que el ticket 4a va a explotar. En esta feature el catálogo existe pero no se expone: todo
movimiento se registra en la predeterminada (FR-009).

---

## `usuario`

| Columna | Tipo | Restricciones |
|---------|------|---------------|
| `id` | `bigint` | PK, autoincremental |
| `email` | `varchar(254)` | `NOT NULL`, `UNIQUE` |

**Semilla**: una única fila, la que devuelve `IUsuarioActual` ([D-05](./research.md)).

La tabla existe ya para que `movimiento.usuario_id` sea una clave foránea real desde el primer día.
Las columnas de autenticación —hash de contraseña, contador de intentos fallidos— **no** se agregan
acá: son del ticket 1a, y el plan `DISC-001` decidió que los datos de desarrollo de esta fila se
descartan en esa migración.

> **RNF-03 no aplica todavía.** No hay contraseña que guardar en esta feature. Cuando la haya, va
> con hash seguro (bcrypt/argon2), como exige `AGENTS.md` en *What NOT to do*.

---

## Relaciones

```text
usuario 1 ──< movimiento >── 1 categoria
              │
              └── 1 moneda

usuario 1 ──< categoria        (usuario_id NULL = predefinida del sistema)
```

Ningún borrado en cascada: en esta feature nada se borra, y cuando FEAT-001b agregue la eliminación
será del movimiento, que es la punta de todas las relaciones.

---

## Lectura del listado (FR-007, FR-008)

```text
movimientos del usuario actual
  donde fecha entre RangoDelMes.De(hoy).Desde y .Hasta     -- extremos incluidos (AC-25)
  ordenado por fecha DESC, id DESC                          -- explícito, no por el índice
```

`RangoDelMes.De(DateOnly hoy)` es una función pura: recibe el día, devuelve el primero y el último
del mes. Es lo que hace verificables los bordes de mes con fechas fijas, sin depender de cuándo
corra la suite (Principio IV de la constitución).

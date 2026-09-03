# Contrato — los cuatro endpoints de categorías

La forma exacta de cada petición y cada respuesta. Lo que está acá tiene que estar también en
`frontend/src/api/tipos.ts`, y `verificar-contrato.sh` compara las dos definiciones contra el JSON
real en las dos direcciones.

Todos exigen sesión (RF-03). Sin ella: `401`.

---

## `GET /api/categorias`

Ya existe; cambia lo que devuelve.

```http
GET /api/categorias
Cookie: <sesión>
```

**Antes**: sólo las predefinidas activas. **Ahora**: las predefinidas activas **más** las propias
activas de la cuenta de la sesión, y ninguna de otra cuenta (FR-002).

```json
[
  { "id": 1, "nombre": "Supermercado", "tipo": "gasto",   "esPropia": false },
  { "id": 8, "nombre": "Sueldo",       "tipo": "ingreso", "esPropia": false },
  { "id": 43, "nombre": "Gimnasio",    "tipo": "gasto",   "esPropia": true }
]
```

| Campo | Tipo | Notas |
|---|---|---|
| `id` | `number` | |
| `nombre` | `string` | El nombre vigente |
| `tipo` | `"gasto" \| "ingreso"` | Cadena, no el `tinyint` del esquema |
| `esPropia` | `boolean` | **Nuevo.** `true` = la creó esta cuenta y puede renombrarla o darla de baja; `false` = predefinida, de solo lectura (FR-008). Es lo único que la pantalla de gestión necesita para saber qué ofrecer (D-07) |

`activa` y `usuarioId` **no** viajan: el listado ya devuelve sólo activas, y el número de cuenta no
le sirve a nadie del otro lado.

**Orden**: por tipo y después por identificador, como ya venía. Las propias caen después de las
predefinidas de su tipo por tener ids más altos, y eso alcanza: nadie pidió agruparlas aparte.

---

## `POST /api/categorias`

```http
POST /api/categorias
Content-Type: application/json
Cookie: <sesión>

{ "nombre": "Gimnasio", "tipo": "gasto" }
```

| Campo | Tipo | Obligatorio | Reglas |
|---|---|---|---|
| `nombre` | `string` | sí | 1 a 50 caracteres **después de recortar** espacios al principio y al final (FR-006, FR-007) |
| `tipo` | `"gasto" \| "ingreso"` | sí | Se fija al crear y no se puede cambiar después |

**`201 Created`** con la categoría creada, en la misma forma que devuelve el listado y con
`esPropia: true`. Lleva `Location` apuntando a ella.

**`400`** (`ValidationProblem`, clave `nombre`):

- nombre vacío, en blanco, o de más de 50 caracteres → *"El nombre tiene que tener entre 1 y 50 caracteres."*
- ya existe una categoría **activa** con ese nombre y tipo disponible para la cuenta, propia o
  predefinida → *"Ya tenés una categoría de ese tipo con ese nombre."* El mensaje **no** dice si la
  que choca es propia o predefinida: no hace falta y es una fuga menos (FR-005, D-06).

**`400`** (clave `tipo`) si `tipo` no es una de las dos cadenas.

Una categoría propia **dada de baja** con ese mismo nombre y tipo **no** impide la creación: es
FR-009, y es lo que la columna `discriminador` hace posible.

---

## `PUT /api/categorias/{id}`

```http
PUT /api/categorias/43
Content-Type: application/json
Cookie: <sesión>

{ "nombre": "Gimnasio y pileta" }
```

Sólo el nombre. **El tipo no viaja**: cambiarlo movería de tipo a todos los movimientos que la usan,
que es reescribir la historia por la puerta de atrás.

**`200`** con la categoría ya modificada.

**`400`** con la clave `nombre`: las mismas dos reglas del alta, unicidad incluida (Clarificación 1).
Al comprobar la unicidad **la categoría no choca consigo misma**: renombrar "Gimnasio" a "Gimnasio"
no es un error.

**`403`** si el `id` es de una categoría **predefinida**. Acá no va el `404` uniforme: la persona la
está viendo en su selector, y decirle que no existe es mentirle sobre algo que tiene a la vista. No
hay nada que ocultar, el catálogo predefinido es igual para todos (FR-008, D-06).

**`404`** si el `id` no existe **o** es de una categoría propia de otra cuenta. Mismo código y mismo
cuerpo en los dos casos: cualquier diferencia confirma que la fila existe (FR-013).

---

## `DELETE /api/categorias/{id}`

Baja lógica. **La fila no se borra**: se apaga `activa` y se le escribe el discriminador.

**`204`** sin cuerpo. **Es idempotente**: darle de baja a algo ya dado de baja devuelve `204`
también. El estado final es el mismo y obligar al cliente a distinguir dos situaciones idénticas no
le sirve a nadie.

**`403`** si es predefinida. **`404`** si no existe o es de otra cuenta. Igual que el `PUT`.

Después de esto:

- deja de aparecer en `GET /api/categorias` (FR-010);
- **sigue** apareciendo con su nombre en los movimientos que la usan, en el listado y en el desglose
  del resumen (FR-010, FR-011);
- **ningún** número del resumen cambia (FR-011, AC-06);
- se puede crear una nueva con el mismo nombre y tipo (FR-009).

---

## Lo que cambia en los movimientos

Ningún endpoint de movimientos cambia de forma. Cambia **una** regla, y sólo en la edición:

| | Categoría activa | Dada de baja, la que ya tenía | Dada de baja, otra | De otra cuenta |
|---|---|---|---|---|
| `POST /api/movimientos` | ✅ | ❌ `400` | ❌ `400` | ❌ `400` |
| `PUT /api/movimientos/{id}` | ✅ | **✅** *(cambia)* | ❌ `400` | ❌ `400` |

El único casillero nuevo es el del medio: editarle el monto o la fecha a un movimiento viejo no
puede obligar a reclasificarlo (FR-023). Los otros siete ya se comportan así desde FEAT-001b y
tienen tests que los defienden.

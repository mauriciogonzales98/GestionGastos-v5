# Contrato HTTP — Alta de movimientos y listado simple

**La fuente de verdad de este contrato es `frontend/src/api/tipos.ts`.** Los tests de
`backend/GestionGastos.Api.Tests/Contrato/` leen ese archivo y lo comparan contra el JSON que la
API emite de verdad, en las dos direcciones. Este documento describe el contrato; no lo reemplaza.

El motivo de esa asimetría está medido en [research.md D-09](../research.md): los tipos del
frontend están escritos a mano y no derivan del backend, así que un rename coherente del backend
deja en verde el build, `tsc`, ESLint y toda la suite, y hace llegar `undefined` a la pantalla.

Tres endpoints. Ninguno lleva autenticación: la cuenta sale de `IUsuarioActual` (FR-010), y el
ticket 1a reemplaza esa pieza.

---

## `GET /api/categorias`

Devuelve el catálogo que alimenta el selector del formulario (FR-006).

**Respuesta `200`**

```json
[
  { "id": 1, "nombre": "Comida",        "tipo": "gasto" },
  { "id": 8, "nombre": "Sueldo",        "tipo": "ingreso" },
  { "id": 7, "nombre": "Otros",         "tipo": "gasto" },
  { "id": 10, "nombre": "Otros",        "tipo": "ingreso" }
]
```

Diez elementos: siete de tipo `gasto` y tres de tipo `ingreso`. El cliente agrupa por `tipo` para
ofrecer sólo las del tipo que se está cargando — **AC-10 exige que el selector de gasto no muestre
ninguna de ingreso**, y viceversa.

`tipo` viaja como cadena (`"gasto"` / `"ingreso"`), no como el `tinyint` que guarda la base: el
número obligaría al frontend a conocer el mapeo y lo volvería frágil ante un cambio de esquema.

---

## `POST /api/movimientos`

Registra un gasto o un ingreso (FR-001, FR-002).

**Petición**

```json
{
  "tipo": "gasto",
  "monto": 1250.50,
  "categoriaId": 1,
  "fecha": "2026-08-23"
}
```

- `fecha` es opcional. Si viene ausente o `null`, el servidor usa el día actual — **AC-17**. El
  valor por defecto lo pone el servidor, no el cliente: es la única forma de que el test sea
  determinista.
- `monto` es un número JSON con hasta dos decimales.
- No hay campo de moneda: se registra en la predeterminada del catálogo (FR-009). El selector
  llega en el ticket 4b.

**Respuesta `201`** — el movimiento creado, con la misma forma que devuelve el listado:

```json
{
  "id": 42,
  "tipo": "gasto",
  "monto": 1250.50,
  "categoriaId": 1,
  "categoriaNombre": "Comida",
  "monedaCodigo": "ARS",
  "fecha": "2026-08-23"
}
```

Devolver el movimiento entero —y no sólo el `id`— es lo que permite a la pantalla insertarlo en el
listado sin volver a pedirlo (FR-014).

**Respuesta `400`** — validación. Ver *Formato de error* abajo. Cubre FR-004, FR-004b, FR-005 y
FR-011.

---

## `GET /api/movimientos`

Lista los movimientos del mes actual (FR-007, FR-008).

Sin parámetros. **El recorte al mes actual es del servidor y no se expone como control** — FR-007
lo fija así, y ponerlo en el cliente lo convertiría en algo que el cliente puede cambiar. Los
parámetros de rango llegan en FEAT-001b, cuando AC-25 pase a ser un valor por defecto y no una
constante.

**Respuesta `200`**

```json
[
  {
    "id": 42,
    "tipo": "gasto",
    "monto": 1250.50,
    "categoriaId": 1,
    "categoriaNombre": "Comida",
    "monedaCodigo": "ARS",
    "fecha": "2026-08-23"
  }
]
```

Ordenado por `fecha` descendente y, a igual fecha, por `id` descendente: el último cargado primero.
Arreglo vacío si no hay movimientos en el mes — **no** es un `404`, y la pantalla lo muestra como
listado vacío con su mensaje (FR-012).

`categoriaNombre` viaja junto al `id` para que el listado no tenga que cruzar contra el catálogo.
Es además lo que va a hacer que RF-09 funcione sin esfuerzo en el ticket 3: el nombre que se
conserva en los movimientos ya registrados es el que devuelve esta lectura.

---

## Formato de error

Uno solo para todas las validaciones, en las dos capas — [research.md D-07](../research.md).
`ProblemDetails` de validación (RFC 9457), que .NET produce de fábrica.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "monto": ["El monto debe ser mayor a cero y tener hasta dos decimales."],
    "categoriaId": ["Elegí una categoría."]
  }
}
```

**La clave de `errors` es el nombre del campo de la petición.** Eso es lo que permite al frontend
poner cada mensaje al lado de su control en vez de volcar un texto suelto, que es la mitad del
*Contrato de marcado de la UI* del [plan](../plan.md).

| Caso | Campo en `errors` | Requerimiento |
|------|-------------------|---------------|
| Monto ausente, `0`, negativo o con más de dos decimales | `monto` | FR-004 / AC-18 |
| Monto mayor a 999.999.999,99 | `monto` | FR-004b / AC-18 |
| Sin categoría | `categoriaId` | FR-005 / AC-40 |
| Categoría inexistente | `categoriaId` | FR-005 |
| Categoría de un tipo distinto al del movimiento | `categoriaId` | FR-011 |
| `tipo` ausente o distinto de `gasto`/`ingreso` | `tipo` | FR-001, FR-002 |

Un error que no corresponda a ningún campo —un fallo al persistir— sale como `500` con
`ProblemDetails` sin `errors`, y la pantalla lo muestra en la región de error del formulario
conservando lo cargado.

Los mensajes van en español: `AGENTS.md` fija el idioma de trabajo y estos textos los lee la
persona usuaria.

---

## Lo que este contrato NO tiene todavía

Anotado para que se note que es deliberado, no un olvido:

- `PUT` y `DELETE` de movimientos → FEAT-001b
- Parámetros de filtro (categoría, rango de fechas, moneda) → FEAT-001b y 4b
- Endpoint de resumen del mes → FEAT-001c
- Endpoints de alta, login y logout → ticket 1a
- `CRUD` de categorías propias → ticket 3
- Campo `monedaId` en la petición y filtro por moneda → 4a y 4b
- Campo `nota` → ticket 2

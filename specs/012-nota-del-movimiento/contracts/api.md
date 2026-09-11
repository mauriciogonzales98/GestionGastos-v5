# Contrato: Nota descriptiva del movimiento

Qué cambia del contrato HTTP y qué no. La fuente de verdad del lado del cliente es
`frontend/src/api/tipos.ts`; los tests de `backend/GestionGastos.Api.Tests/Contrato/` lo leen y lo
comparan contra el JSON que la API emite de verdad, en las dos direcciones
([ADR-001](../../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md)).

**Ningún endpoint nuevo.** Cambian las tres formas del movimiento y nada más.

---

## 1 · Lo que se devuelve: `nota` viaja siempre y nunca es nula

Afecta a `POST /api/movimientos`, `GET /api/movimientos`, `GET /api/movimientos/{id}` y
`PUT /api/movimientos/{id}` — los cuatro devuelven la **misma** forma, que es la propiedad que el test
de contrato ya verifica y que no se afloja.

**Antes**

```json
{ "id": 7, "tipo": "gasto", "monto": 8500.00, "categoriaId": 3, "categoriaNombre": "Transporte", "monedaCodigo": "ARS", "fecha": "2026-09-10" }
```

**Después**

```json
{ "id": 7, "tipo": "gasto", "monto": 8500.00, "categoriaId": 3, "categoriaNombre": "Transporte", "monedaCodigo": "ARS", "fecha": "2026-09-10", "nota": "viaje al aeropuerto" }
```

Y un movimiento **sin** nota:

```json
{ "id": 8, "tipo": "gasto", "monto": 1200.00, "categoriaId": 3, "categoriaNombre": "Transporte", "monedaCodigo": "ARS", "fecha": "2026-09-10", "nota": "" }
```

- **El campo viaja siempre** y **nunca es `null`**: "sin nota" es la cadena vacía. Es lo decidido en
  *Clarifications* y lo que hace que la pantalla no tenga que saber que el almacenamiento admite dos
  formas de representar la ausencia.
- **La normalización ocurre una sola vez**, al construirse el DTO, y no en los cuatro lugares que lo
  construyen (D-04). El test de `FR-011` recorre **las cuatro rutas** contra una fila guardada sin
  valor: si alguna devolviera `null`, rojo.
- `nota` va **última** en las dos definiciones, después de `fecha`. Los tests de contrato comparan
  conjuntos de nombres en las dos direcciones, así que el orden no los afecta — mantenerlo alineado
  cuesta nada y evita la discusión sobre cuál de las dos cosas hacen.

---

## 2 · Lo que se manda al registrar: `nota` es **opcional**

`POST /api/movimientos`

```json
{ "tipo": "gasto", "monto": 8500, "categoriaId": 3, "monedaId": 1, "fecha": "2026-09-10", "nota": "viaje al aeropuerto" }
```

```json
{ "tipo": "gasto", "monto": 1200, "categoriaId": 3 }
```

Los dos son válidos. **Ausente, `null` y la cadena vacía significan lo mismo: sin nota.**

Que sea opcional no es una comodidad: es `PRD:AC-09` —se tiene que poder registrar un movimiento sin
tocar el campo— y es la compatibilidad hacia atrás del contrato, igual que pasó con `monedaId` en la
feature 009. Todo cliente que ya andaba sigue andando sin mandarla.

---

## 3 · Lo que se manda al modificar: `nota` es **obligatoria**

`PUT /api/movimientos/{id}`

```json
{ "tipo": "gasto", "monto": 8500, "categoriaId": 3, "monedaId": 1, "fecha": "2026-09-10", "nota": "viaje al aeropuerto ida y vuelta" }
```

Y para **vaciarla**:

```json
{ "tipo": "gasto", "monto": 8500, "categoriaId": 3, "monedaId": 1, "fecha": "2026-09-10", "nota": "" }
```

**Es la misma asimetría que `fecha`, y por el mismo motivo.** La regla que el contrato ya declara es
*ausente nunca produce un cambio que nadie pidió*:

| Campo | En el alta, ausente significa | En la edición |
|---|---|---|
| `fecha` | hoy | **obligatoria** — ausente significaría "hoy" y el movimiento saltaría de fecha en silencio |
| `monedaId` | la predeterminada | opcional — ausente significa "la que ya tenía", que no es un cambio |
| `nota` | sin nota | **obligatoria, y no admite `null`** — ausente tendría que significar "la que ya tenía" (y entonces no habría forma de vaciarla) o "sin nota" (y entonces un cliente que no la manda borra en silencio). Vaciar es mandar `""` |

**La cadena vacía es la forma de decir "sin nota", y `null` se rechaza igual que la omisión.** En JSON
no hay forma de distinguir "vino `null`" de "no vino", así que aceptar `null` como vaciado dejaba la
omisión indistinguible del vaciado explícito — y mientras eso fue así, un cuerpo sin la clave borraba
la nota en silencio con un `200`. Lo encontró la revisión del PR #29.

Que la forma de vaciar sea `""` tiene además una simetría que `null` no tenía: **es exactamente lo que
la API devuelve** para un movimiento sin nota. Se lee y se escribe igual.

El rechazo viene con la clave `nota`, como el de `fecha`:

```json
{ "errors": { "nota": ["Mandá la nota del movimiento, vacía si no tiene."] } }
```

---

## 4 · El rechazo por largo

Una nota de más de 120 caracteres Unicode se rechaza con el formato único de error del proyecto
(RFC 9457), con la clave del campo:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "nota": ["La nota no puede superar los 120 caracteres."] }
}
```

- **La clave es `nota`**, el nombre del campo, que es lo que permite a la pantalla poner el mensaje al
  lado de su control. Del lado del cliente hay que agregar `nota` a la lista de campos que tienen lugar
  donde mostrar su error, o el mensaje llega y cae en la región general (D-05).
- **El mensaje no repite la nota.** Es la única entrada de texto libre de la aplicación: devolver el
  valor lo haría viajar de vuelta y aparecer donde termine el mensaje.
- **El largo se mide después de recortar los espacios de los extremos** (D-03), así que 120 caracteres
  con un espacio a cada lado se aceptan.
- Se rechaza **sin crear ni alterar nada** (`PRD:AC-03`), igual que cualquier otro error de validación:
  se valida todo antes de tocar la base.

---

## 5 · Lo que NO cambia

- **Ningún parámetro de consulta nuevo.** `GET /api/movimientos` sigue acotando por `desde`, `hasta`,
  `categoriaId` y `monedaId`, y **nunca por la nota** (`FR-007`). `BarreraDeLaNotaTests` y
  `verificar-nota.sh` existen para que eso se ponga en rojo si alguien lo agrega.
- **`GET /api/resumen` no cambia en nada.** Ni un campo, ni un número. La nota no entra en la consulta
  que agrupa, así que los totales no pueden moverse (`NFR-002`).
- **`GET /api/monedas` no cambia.** `FR-010` toca el esquema, no el contrato: `codigo` ya viajaba y
  sigue siendo tres caracteres. Lo único que cambia es que ahora la base exige que sean **letras**.
- **Ningún código de estado nuevo.** El `404` del movimiento ajeno o inexistente sigue siendo uniforme
  y sigue sin distinguir los dos casos.

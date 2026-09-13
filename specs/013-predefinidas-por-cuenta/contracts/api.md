# Contrato: Cada cuenta con sus propias categorías predefinidas

Qué cambia del contrato HTTP y qué no. La fuente de verdad del lado del cliente es
`frontend/src/api/tipos.ts`; los tests de `backend/GestionGastos.Api.Tests/Contrato/` lo leen y lo
comparan contra el JSON que la API emite de verdad, en las dos direcciones
([ADR-001](../../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md)).

**Ningún endpoint nuevo, y ningún endpoint que desaparezca.** Cambia un campo de la categoría y
desaparece una respuesta de error.

---

## 1 · `esPropia` se elimina de la categoría

Afecta a `GET /api/categorias`, `POST /api/categorias` y `PUT /api/categorias/{id}` — los tres
devuelven la misma forma.

**Antes**

```json
{ "id": 3, "nombre": "Transporte", "tipo": "gasto", "esPropia": false }
```

**Después**

```json
{ "id": 3, "nombre": "Transporte", "tipo": "gasto" }
```

**Por qué**: el campo respondía una sola pregunta —"¿esta fila la puedo renombrar y dar de baja, o es
del sistema?"— y a partir de esta feature la respuesta es siempre que sí. Un campo que siempre vale
lo mismo no informa nada e invita a escribir del lado del cliente una condición que ya no existe.

**Es un cambio incompatible**, y se hace en las dos pilas en la misma tarea: los tests de contrato
comparan en las dos direcciones y quedan en rojo mientras sólo una de las dos haya cambiado. Ese
rojo es el esperado, no un fallo a investigar.

`activa` y `usuarioId` siguen sin viajar, por los mismos motivos de siempre (D-07 de la 007).

---

## 2 · El `403` sobre una categoría predefinida desaparece

Afecta a `PUT /api/categorias/{id}` y `DELETE /api/categorias/{id}`.

| Situación | Antes | Después |
|---|---|---|
| La categoría es propia de la cuenta | `200` / `204` | **Sin cambios**: `200` / `204` |
| La categoría es una predefinida del sistema | `403` | **Ya no existe esa situación**: toda categoría del ámbito es propia, y responde `200` / `204` |
| La categoría es propia de otra cuenta | `404` | **Sin cambios**: `404` |
| El identificador no existe | `404` | **Sin cambios**: `404` |

**Por qué**: el `403` distinguía "la ves pero no la podés tocar" de "no existe". Sin categorías del
sistema, esa distinción se queda sin casos: lo que no es tuyo no lo ves, y lo que ves es tuyo.

**Lo que NO cambia y conviene decirlo**: una categoría **propia de otra cuenta** sigue respondiendo
`404` y no `403`. Confirmar su existencia sería exactamente la filtración que `FR-013` de la 007
evita, y esa regla queda intacta.

---

## 3 · Lo que no cambia

- `GET /api/categorias` sigue devolviendo sólo las **activas** del ámbito, ordenadas por tipo y
  después por identificador.
- `NuevaCategoria` y `CategoriaEditada` no cambian de forma.
- El movimiento no cambia de forma: sigue viajando con `categoriaId` y `categoriaNombre`.
- El error de validación por clasificar con una categoría fuera del ámbito sigue siendo un `400`
  con la clave del campo, y no el fallo crudo de la restricción nueva (`FR-007`).
- Los identificadores de categoría dejan de ser estables entre cuentas: `3` es "Transporte" para
  una cuenta y puede ser cualquier otra cosa para otra. Ya era así para las propias desde la 007;
  ahora vale para todas. **Ningún cliente debe cablear un identificador de categoría.**

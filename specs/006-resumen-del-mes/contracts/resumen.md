# Contrato — `GET /api/resumen`

La forma exacta de la petición y de la respuesta. Lo que está acá tiene que estar también en
`frontend/src/api/tipos.ts`, y `verificar-contrato.sh` compara las dos definiciones contra el JSON
real en las dos direcciones.

---

## Petición

```http
GET /api/resumen?desde=2026-08-01&hasta=2026-08-31
Cookie: <sesión>
```

| Parámetro | Tipo | Obligatorio | Significado |
|---|---|---|---|
| `desde` | `YYYY-MM-DD` | no | Primer día del período. **Incluido** |
| `hasta` | `YYYY-MM-DD` | no | Último día del período. **Incluido** |

**Las reglas son las mismas del listado, y salen del mismo código**
([D-03](../research.md#d-03--el-período-se-valida-igual-que-en-el-listado-y-en-un-solo-lugar)):

- Sin ninguno de los dos → el **mes en curso, decidido por el servidor** (FR-002).
- Con uno solo → `400`. Suponer el extremo que falta es inventar un supuesto que nadie declaró.
- Con `desde > hasta` → `400`. Devolver todo en cero escondería que la pregunta estaba mal formada.

Exige sesión, como todo endpoint (RF-03). Sin ella: `401`.

---

## Respuesta `200`

```json
{
  "desde": "2026-08-01",
  "hasta": "2026-08-31",
  "monedas": [
    {
      "monedaId": 1,
      "monedaCodigo": "ARS",
      "totalIngresado": 450000.00,
      "totalGastado": 182500.50,
      "balance": 267499.50,
      "gastosPorCategoria": [
        { "categoriaId": 3, "categoriaNombre": "Supermercado", "total": 120000.00 },
        { "categoriaId": 7, "categoriaNombre": "Transporte",   "total":  62500.50 }
      ]
    },
    {
      "monedaId": 2,
      "monedaCodigo": "USD",
      "totalIngresado": 0,
      "totalGastado": 0,
      "balance": 0,
      "gastosPorCategoria": []
    }
  ]
}
```

### `Resumen`

| Campo | Tipo | Notas |
|---|---|---|
| `desde` | `string` | `YYYY-MM-DD`. **Siempre viaja**, también cuando el cliente no lo mandó: es el único modo de saber qué mes eligió el servidor ([D-06](../research.md#d-06--la-respuesta-devuelve-el-período-que-se-usó)) |
| `hasta` | `string` | `YYYY-MM-DD` |
| `monedas` | `ResumenPorMoneda[]` | **Una por cada moneda del catálogo, siempre.** Nunca vacío |

### `ResumenPorMoneda`

| Campo | Tipo | Notas |
|---|---|---|
| `monedaId` | `number` | |
| `monedaCodigo` | `string` | ISO 4217. Viaja junto al id para que quien lo muestre no cruce contra un catálogo |
| `totalIngresado` | `number` | Suma de los ingresos **de esta moneda** en el período. `0` si no hubo |
| `totalGastado` | `number` | Suma de los gastos **de esta moneda**. `0` si no hubo |
| `balance` | `number` | `totalIngresado - totalGastado`. **Puede ser negativo**, y eso es un resultado, no un error |
| `gastosPorCategoria` | `TotalPorCategoria[]` | Sólo las categorías con al menos un gasto. `[]` es normal. **Ordenado**: de mayor a menor total, y el empate lo desempata el `categoriaId` ascendente |

### El orden del desglose

`gastosPorCategoria` viene **de mayor a menor total, desempatado por `categoriaId` ascendente**, y
es parte del contrato: quien lo grafique puede confiar en él sin reordenar.

El desempate está escrito porque hace falta. La consulta agregada no lleva `ORDER BY` —el orden es
un requisito del listado, no del acotado—, así que dos categorías con el mismo total llegan en el
orden que el motor elija: comprobado, el de carga. Sin desempatar, dos pedidos idénticos devuelven
las barras intercambiadas.

`monedas` también viene ordenado, por `monedaId` ascendente, y por el mismo motivo.

### `TotalPorCategoria`

| Campo | Tipo | Notas |
|---|---|---|
| `categoriaId` | `number` | |
| `categoriaNombre` | `string` | El nombre **vigente** de la categoría, no una copia del momento del alta (AC-13) |
| `total` | `number` | Suma de los gastos de esa categoría, en esa moneda, en el período |

**Nada se convierte entre monedas, en ningún campo** (FR-011, RF-29). Dos entradas de `monedas` son
dos universos separados que no se suman jamás.

---

## Respuesta `400`

`ProblemDetails` con la clave `rango` en `errors`, exactamente igual que el listado — porque sale
del mismo intérprete:

```json
{
  "type": "...", "title": "...", "status": 400,
  "errors": { "rango": ["La fecha de inicio no puede ser posterior a la de fin."] }
}
```

---

## Lo que este contrato **no** tiene, y por qué

- **No hay filtro de categoría.** El listado lo tiene porque muestra filas; el resumen existe para
  comparar categorías entre sí, y filtrarlo por una dejaría una sola barra que compararse con nada.
- **No hay filtro de moneda** (RF-30). Depende de que haya más de una en uso, que es el ticket 4a.
  Acá se discriminan; filtrarlas es otro ticket.
- **No hay desglose de ingresos.** RF-19 desglosa gastos; los ingresos entran en `totalIngresado` y
  en `balance`. Decidido con el usuario el 2026-09-01 y asentado en *Assumptions* de la spec.
- **No hay paginación.** El resultado está acotado por el catálogo de monedas y por las categorías
  con movimientos: decenas de filas, no un conjunto que crezca con el uso.
- **No hay campo de "cantidad de movimientos".** Nadie lo pidió, y un campo que nadie pidió es un
  campo que hay que mantener alineado para siempre.

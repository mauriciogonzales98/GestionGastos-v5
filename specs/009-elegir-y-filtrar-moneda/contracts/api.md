# Contrato — Elegir y filtrar la moneda de un movimiento

**Feature**: 009 · **Fecha**: 2026-09-04

La fuente de verdad del contrato es **`frontend/src/api/tipos.ts`**, y los tests de
`backend/GestionGastos.Api.Tests/Contrato/` lo leen y lo comparan contra el JSON que la API emite de
verdad, en las dos direcciones. Este documento describe el cambio; el archivo lo declara.

**Esta feature toca el contrato en cuatro lugares**, más que ninguna desde la 007. `verificar-contrato.sh`
es, por eso, la barrera que más trabajo va a hacer.

---

## 1. `GET /api/monedas` — nuevo

El catálogo que alimenta el selector del formulario y el control de acotado (FR-004).

**Requiere sesión**, como todo endpoint del proyecto. `verificar-autorizacion.sh` se pone en rojo si
naciera abierto.

**Respuesta `200`**: un arreglo, ordenado por identificador, con **una entrada por fila del
catálogo**. Nunca vacío: la migración siembra al menos una.

```json
[
  { "id": 1, "codigo": "ARS", "nombre": "Peso argentino", "simbolo": "$",   "esPredeterminada": true  },
  { "id": 2, "codigo": "USD", "nombre": "Dólar",          "simbolo": "US$", "esPredeterminada": false }
]
```

**Tipo nuevo en `tipos.ts`**:

```ts
export interface Moneda {
  id: number;
  codigo: string;
  nombre: string;
  simbolo: string;
  esPredeterminada: boolean;
}
```

**Por qué viaja `esPredeterminada` y no viaja `decimales`**: `esPredeterminada` responde la única
pregunta que el formulario se hace sobre el catálogo —cuál propongo— y es la respuesta ya calculada,
no el dato con el que calcularla. Es el mismo criterio que `esPropia` en `CategoriaDto`.
`decimales` no viaja porque **hoy no lo usa nadie**: es del formato regional, que es el ticket 6
(D9-05). Un campo que nadie consume es un dato que salió a la red sin que nadie lo decidiera, y la
barrera del contrato lo trata como error en la dirección "sobra en la API".

---

## 2. `POST /api/movimientos` — un campo opcional más

```ts
export interface NuevoMovimiento {
  tipo: TipoMovimiento;
  monto: number;
  categoriaId: number;
  monedaId?: number | null;   // ← nuevo. Ausente o null = la predeterminada del catálogo
  fecha?: string | null;
}
```

| Cuerpo | Resultado |
|---|---|
| Sin `monedaId`, o con `null` | Se registra en la predeterminada. **Es el comportamiento de hoy, sin cambios** (FR-002, `PRD:NFR-01`) |
| Con un `monedaId` del catálogo | Se registra en esa moneda (FR-001) |
| Con un `monedaId` que no está en el catálogo | `400` con `errors.monedaId`, y **no se crea nada** (FR-003) |

La respuesta `201` no cambia de forma: sigue siendo `Movimiento`, con su `monedaCodigo`.

## 3. `PUT /api/movimientos/{id}` — el mismo campo, con otra ausencia

```ts
export interface MovimientoEditado {
  tipo: TipoMovimiento;
  monto: number;
  categoriaId: number;
  monedaId?: number | null;   // ← nuevo. Ausente o null = la que el movimiento ya tenía
  fecha: string;
}
```

**Ausente significa "no la cambies", no "poné la predeterminada"** — y ésa es la diferencia con el
alta, deliberada y explicada en [research.md D-02](../research.md). La regla común es que **ausente
nunca produce un cambio que nadie pidió**; `fecha` es obligatoria acá por esa misma regla, no por
una distinta.

| Cuerpo | Resultado |
|---|---|
| Sin `monedaId` | El movimiento conserva su moneda. Todo lo demás se edita normalmente |
| Con un `monedaId` del catálogo | Cambia la moneda, y **el monto, la categoría y la fecha quedan como estaban** (FR-012) |
| Con un `monedaId` que no está en el catálogo | `400` con `errors.monedaId`, y el movimiento **queda como estaba** |
| Sobre un movimiento de otra cuenta | `404`, igual que hoy, sin distinguir "no existe" de "no es tuyo" |

## 4. `GET /api/movimientos` — un parámetro de acotado más

`GET /api/movimientos?desde=&hasta=&categoriaId=&monedaId=`

| `monedaId` | Resultado |
|---|---|
| Ausente | Todas las monedas. **Es el comportamiento de hoy** (FR-008) |
| Del catálogo | Sólo los movimientos en esa moneda |
| Inexistente | Arreglo vacío. **No es un error** (FR-015), mismo criterio que la categoría inexistente |

Se combina con **y** con `desde`, `hasta` y `categoriaId` (FR-009).

---

## Lo que NO cambia

- **`Movimiento`**: ya lleva `monedaCodigo` desde FEAT-001a. El listado no necesita un campo nuevo
  para mostrar el código en cada fila (FR-007) — lo necesita para *mostrarlo*, y eso es maquetación
  del lado del cliente, no contrato.
- **`Resumen`, `ResumenPorMoneda`, `TotalPorCategoria`**: intactos. `GET /api/resumen` no cambia ni
  de forma ni de comportamiento; lo que cambia es en qué moneda cae cada movimiento que suma.
  `verificar-contrato.sh` en verde es la prueba.
- **`ProblemDetails`**: intacto. `monedaId` es una clave más dentro de `errors`, que es un
  diccionario abierto por diseño.

---

## Lo que las barreras del contrato van a exigir

`ContratoMovimientosTests` arma el cuerpo del `POST` y del `PUT` **con los nombres que declara el
contrato**, y su `switch` **lanza** ante un campo que no sabe con qué valor ejercitar:

> "El contrato declara el campo `{campo}` de NuevoMovimiento y este test no sabe con qué valor
> ejercitarlo. Agregalo acá: un campo del contrato sin ejercitar es un campo sin barrera."

O sea: **agregar `monedaId` a `tipos.ts` pone esos dos tests en rojo antes de que exista una línea de
implementación.** Ese rojo es el primero que esta feature va a ver, y es el rojo correcto — el
Principio I con la barrera empujando en la misma dirección. Además hace falta un
`ContratoMonedasTests` para el tipo nuevo, con la misma comparación en las dos direcciones que ya
tienen categorías, movimientos y resumen.

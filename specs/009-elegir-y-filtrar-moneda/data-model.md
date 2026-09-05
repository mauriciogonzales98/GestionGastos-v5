# Data Model — Elegir y filtrar la moneda de un movimiento

**Feature**: 009 · **Fecha**: 2026-09-04

## Lo más importante de este documento

**El esquema no cambia. No hay migración.**

`movimiento.moneda_id` existe desde la migración `Inicial`, es `NOT NULL`, tiene su clave foránea a
`moneda(id)` y su índice. La tabla `moneda` y su semilla también, y la migración
`UnicaMonedaPredeterminada` sostiene la invariante de que exactamente una fila tiene
`es_predeterminada = 1`. Todo eso lo verificó la feature 008.

Lo único que cambia es **quién decide el valor de `moneda_id`**: hasta hoy el servidor, con la
predeterminada; desde acá el usuario, con el servidor poniendo la predeterminada cuando no elige.

Que una feature de este tamaño no toque el esquema es la consecuencia directa de que el catálogo se
haya modelado como tabla y no como enum. Es `RF-32` cobrando.

---

## Entidades

### `Moneda` — se lee, no se escribe

`backend/GestionGastos.Api/Dominio/Moneda.cs`, sin cambios.

| Campo | Tipo | Qué es en esta feature |
|---|---|---|
| `Id` | `short` | **Lo que viaja en la petición** como `monedaId` (D-01) |
| `Codigo` | `string` | ISO 4217. Lo que viaja en la respuesta y lo que se muestra en cada fila del listado (FR-007) |
| `Nombre` | `string` | Lo que se lee en el selector, para no obligar a nadie a saber que `ARS` son pesos |
| `Simbolo` | `string` | Ya lo usa el formateo del monto. No lo consume nada nuevo |
| `Decimales` | `byte` | **Sigue sin usarse.** Es D9-05, del ticket 6 |
| `EsPredeterminada` | `bool` | Exactamente una en `true`. Es lo que el selector propone por defecto (FR-006) y lo que el alta usa cuando no se elige (FR-002) |

**Ninguna operación de esta feature escribe en esta tabla.** El catálogo se administra como dato, y
`verificar-monedas.sh` existe para que siga siendo así (D9-03).

### `Movimiento` — cambia quién decide su moneda

`backend/GestionGastos.Api/Dominio/Movimiento.cs`, sin cambios de forma.

| Campo | Antes | Ahora |
|---|---|---|
| `MonedaId` | Lo escribía el alta con `SingleAsync(m => m.EsPredeterminada)`, siempre | Lo escribe el alta con lo que el usuario eligió, o con la predeterminada si no eligió. **La edición ahora también lo escribe**, cosa que hasta hoy no hacía |

---

## Reglas de validación

Todas viven en `ValidacionDelMovimiento` y valen igual para el alta y para la edición (D-04).

| Regla | Clave del error | Mensaje | Origen |
|---|---|---|---|
| `monedaId` ausente o `null` | — | No es error: alta ⇒ la predeterminada; edición ⇒ la que ya tenía (D-02) | FR-002, `PRD:NFR-01` |
| `monedaId` no identifica ninguna fila del catálogo | `monedaId` | "La moneda elegida no existe." | FR-003, `PRD:AC-11`, deuda **D8-01** |

**No hay regla de ámbito, y ésa es la diferencia con la categoría.** Una categoría vale si es
predefinida del sistema **o** propia de esta cuenta, y activa; una moneda vale si está en el
catálogo, punto. No hay monedas de nadie ni monedas dadas de baja: son del sistema y son todas
elegibles. Escribirle un filtro de ámbito a la moneda sería copiar una condición que no protege nada
(D-03).

**Tampoco se distingue la predeterminada del resto a la hora de elegir.** El catálogo no tiene
monedas "elegibles" y "no elegibles": tiene una que se propone.

---

## Consultas

| Consulta | Cambio |
|---|---|
| `MovimientosConsulta.Filtrado` | Recibe un `monedaId` opcional más. La condición se escribe en `DeLaCuenta`, junto a la de categoría, con la misma forma: `monedaId == null` deja pasar todo (D-05, FR-008, FR-009) |
| `MovimientosConsulta.Agrupado` | **Pasa `monedaId: null` explícito.** El resumen no se filtra por moneda en esta feature: eso es `RF-30` y es del ticket 5 (D9-02). El `null` va escrito y comentado, no heredado por omisión (D-05) |
| `MovimientosConsulta.PropioPorId` | Sin cambios |
| El catálogo de monedas | Lectura nueva, directa contra `contexto.Monedas`, ordenada. **Sin canal**: la moneda no tiene dueño, así que no hay nada que aislar (D-03) |

Una moneda inexistente en el acotado **no es un error**: no deja pasar nada (FR-015). Es el mismo
criterio que ya rige para la categoría inexistente, y por la misma razón — rechazarla confirmaría
cuáles existen.

---

## Lo que sigue igual, y hay que verificar que siga

- **El resumen.** Ya separa por moneda desde FEAT-001c. Lo que esta feature cambia es en qué moneda
  cae cada movimiento, no cómo se suman: `CalculoDelResumen` no se toca.
- **El aislamiento.** El acotado por cuenta sigue saliendo de `DeLaCuenta` y de `PropioPorId`.
  Agregar un `Where` más no lo debilita, y `verificar-aislamiento.sh` lo comprueba desarmándolo de
  siete formas.
- **La invariante de una sola predeterminada.** La sostiene la migración. El alta la da por cierta
  con `SingleAsync`, que revienta ruidosamente si dejara de valer — que es lo correcto ante una
  invariante rota.

# Data model: Dashboard con gráficos

**Feature**: `010-dashboard-con-graficos` · **Fecha**: 2026-09-05

## Lo primero, porque cambia cómo se lee el resto

**Esta feature no toca la base de datos.** Ninguna tabla nueva, ninguna columna nueva, ningún índice
nuevo, **ninguna migración**. Tampoco toca ningún DTO ni ningún endpoint: el contrato queda idéntico
(ver [contracts/api.md](./contracts/api.md)).

Todo lo que el dashboard muestra ya se calcula y ya viaja. Lo que esta feature construye es **quién
lo pinta**.

Que una feature entera de producto no toque el modelo es el resultado de tres decisiones anteriores
que conviene nombrar, porque son las que la hacen barata:

- FEAT-001c calculó el resumen **por período**, no por mes fijo, aunque la pantalla de entonces sólo
  pidiera el mes en curso.
- FEAT-001c compuso las monedas desde el **catálogo** y no desde el agregado, así que un período
  vacío ya devuelve ceros.
- FEAT-001c dejó **un solo endpoint** para el resumen y el dashboard, con el argumento escrito.

---

## Lo que se lee

### `Resumen` — el período entero

| Campo | Tipo | Qué es |
|---|---|---|
| `desde` | `DateOnly` / `string` `YYYY-MM-DD` | Primer día del período, **incluido**. Viaja siempre, también cuando no se pidió |
| `hasta` | `DateOnly` / `string` | Último día, **incluido** |
| `monedas` | `ResumenPorMoneda[]` | Una entrada por **cada moneda del catálogo**, tenga o no movimientos. Nunca vacío |

`desde` y `hasta` son lo que le permite al dashboard titular el período que está mostrando sin
calcular el mes en curso por su cuenta. Es la decisión D-06 de la feature 006, y esta feature es la
primera que la usa: hasta hoy nadie mostraba el título.

### `ResumenPorMoneda` — la unidad indivisible

| Campo | Tipo | Qué es |
|---|---|---|
| `monedaId` | `short` / `number` | Identificador en el catálogo |
| `monedaCodigo` | `string` | ISO 4217, junto al id para no cruzar contra el catálogo |
| `totalIngresado` | `decimal` / `number` | Cero si no hubo |
| `totalGastado` | `decimal` / `number` | Cero si no hubo |
| `balance` | `decimal` / `number` | Ingresado menos gastado. **Puede ser negativo, y eso se muestra** |
| `gastosPorCategoria` | `TotalPorCategoria[]` | Sólo las categorías con al menos un gasto. `[]` es normal |

Es la unidad sobre la que opera el filtro de moneda del dashboard (D-05): filtrar es quedarse con
una de estas entradas, no recalcular nada.

`RF-29` la vuelve indivisible: **nada se suma nunca a través de dos de éstas**, no hay conversión y
no va a haberla.

### `TotalPorCategoria` — una barra

| Campo | Tipo | Qué es |
|---|---|---|
| `categoriaId` | `int` / `number` | Identificador |
| `categoriaNombre` | `string` | El nombre **vigente**, no una copia del alta: renombrar se refleja solo |
| `total` | `decimal` / `number` | Lo gastado en esa categoría, esa moneda y ese período |

Es exactamente una fila del gráfico (D-03): el nombre es la etiqueta, el total es el valor, y el
ancho de la barra es `total / mayor total de la moneda`.

---

## Invariantes que la pantalla hereda y no puede romper

| # | Invariante | Quién lo garantiza |
|---|---|---|
| INV-01 | Ningún total mezcla monedas | `MovimientosConsulta.Agrupado` agrupa por moneda; nada suma a través |
| INV-02 | La suma del desglose es el `totalGastado` de esa moneda | Los cuatro números salen de **las mismas filas** (D-04 de la 006) |
| INV-03 | Toda moneda del catálogo aparece, con ceros si no tuvo movimientos | El catálogo se lee aparte del agregado (D-05 de la 006) |
| INV-04 | Sólo gastos en el desglose | El filtro por `TipoMovimiento.Gasto`, sobre las mismas filas |
| INV-05 | Las categorías dadas de baja que conservan movimientos siguen sumando | `verificar-desglose.sh` |
| INV-06 | El orden del desglose es estable entre pedidos idénticos | Mayor a menor, con desempate por id |
| INV-07 | Ninguna cuenta ve datos de otra | `MovimientosConsulta.DeLaCuenta`, `verificar-aislamiento.sh` |

**La pantalla no verifica ninguno de estos: los usa.** Es la diferencia entre heredar una garantía y
volver a comprobarla. Lo único que esta feature tiene que cuidar es no introducir un cálculo propio
que pueda contradecirlos — que es `FR-014`, y por eso está escrito.

---

## Consultas

**Ninguna nueva.** El dashboard usa `GET /api/resumen` con `desde` y `hasta`; la pantalla principal
lo usa sin parámetros. Las dos llegan a `MovimientosConsulta.Agrupado`, que es una sola consulta
agrupada por moneda, tipo y categoría.

`NFR-002` —a lo sumo una fila por par de moneda y categoría, más los tres totales— se cumple por la
forma del agregado, no por una restricción que haya que agregar.

---

## Rendimiento

Sin cambios que evaluar: la consulta es la misma que ya se mide.

`RendimientoResumenTests` cubre los dos escalones de `RNF-01` —1000 movimientos en < 2 s y 10000 en
< 4 s, p95 sobre 100 ejecuciones— más el caso repartido en dos monedas. Los números anotados al
escribir esos tests: **6 ms con 1000 filas en una moneda, 9 ms en dos**, dos órdenes de magnitud
debajo del techo.

El índice por `categoria_id` que la feature 006 dejó anotado como deuda **D6-05** sigue sin
justificarse, y esta feature no lo agrega: un índice de más se paga en cada `INSERT`, y la decisión
era tomarlo con el número en la mano.

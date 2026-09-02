# Data Model: Resumen del mes con desglose por categoría

## Lo primero: acá no se persiste nada

**Esta feature no agrega ni cambia ninguna tabla, y no lleva migración.** Todo lo que sigue son
formas que existen mientras se responde una petición y después se tiran.

Vale decirlo fuerte porque el reflejo con un dashboard es guardar totales precalculados, y ése es el
camino a que el resumen y el listado se contradigan: un total guardado es un dato más que puede
quedar viejo. Acá el resumen se deriva de los movimientos **cada vez**, y por eso AC-19, AC-20 y
AC-21 —editar y borrar se reflejan en los totales— no necesitan ninguna invalidación: no hay nada
que invalidar.

Si esta feature produce una migración, algo se salió del alcance
([D-10](./research.md#d-10--sin-migración-y-el-índice-se-deja-como-está)).

---

## Lo que ya existe y se usa

| Entidad | Qué aporta acá |
|---|---|
| `Movimiento` | La materia prima. Se leen `Monto`, `Tipo`, `MonedaId`, `CategoriaId` y `Fecha`; ninguno cambia |
| `Categoria` | El **nombre vigente** del desglose (FR-010). Se toma de la categoría, no de una copia guardada en el movimiento — por eso AC-13 funciona sin hacer nada |
| `Moneda` | El catálogo, que es **la lista de entradas de la respuesta** y no una consecuencia de los movimientos (FR-013, [D-05](./research.md#d-05--las-monedas-salen-del-catálogo-no-de-los-movimientos)) |
| `RangoDeFechas` | El período, con sus dos extremos incluidos y el invariante `Desde <= Hasta` en el tipo |

---

## Formas nuevas

### `PeriodoPedido` — el intérprete del período (`Dominio/`)

Traduce lo que llega por la URL a un `RangoDeFechas`, o a un rechazo. Es lo único que sabe las tres
reglas, y lo usan el listado y el resumen
([D-03](./research.md#d-03--el-período-se-valida-igual-que-en-el-listado-y-en-un-solo-lugar)):

| Entra | Sale | Regla |
|---|---|---|
| nada | el mes en curso del servidor | FR-002 |
| `desde` y `hasta`, en orden | ese rango, extremos incluidos | FR-003 |
| `desde > hasta` | rechazo, con su mensaje | FR-004 |
| sólo uno de los dos | rechazo, con su mensaje | FR-004 |

No es una entidad: no tiene identidad ni se guarda. Es la regla escrita una sola vez.

### `MontoAgrupado` — la fila que devuelve la agregación (`Movimientos/`)

Lo que el motor devuelve por cada grupo de la consulta única
([D-04](./research.md#d-04--una-sola-consulta-agregada-y-la-composición-en-memoria)):

| Campo | De dónde sale |
|---|---|
| `MonedaId`, `MonedaCodigo` | la moneda del movimiento |
| `Tipo` | gasto o ingreso |
| `CategoriaId`, `CategoriaNombre` | la categoría, con su nombre **vigente** |
| `Total` | `SUM(monto)` del grupo |

**No es lo que se devuelve por HTTP.** Es la materia intermedia de la que se componen los cuatro
números de la respuesta, y ésa es toda la gracia: al salir todos de las mismas filas, la suma del
desglose **no puede** diferir del total gastado.

Sobre `Total`: **no es un `decimal(11,2)`**. El techo de un movimiento no es el techo de una suma de
movimientos ([D-11](./research.md#d-11--el-techo-del-monto-agregado-no-es-el-del-movimiento)).

---

## Formas de la respuesta

Tres niveles, cada uno con su tipo y su nombre, sin objetos anidados anónimos
([D-07](./research.md#d-07--el-contrato-se-declara-con-interfaces-con-nombre-sin-objetos-anidados)).
El detalle campo por campo está en [contracts/resumen.md](./contracts/resumen.md).

```text
Resumen                       ← el período que se usó + una entrada por moneda del catálogo
└── ResumenPorMoneda[]        ← lo ingresado, lo gastado, el balance
    └── TotalPorCategoria[]   ← el desglose, SÓLO de gastos, sólo categorías con movimientos
```

**Las tres cardinalidades, que son requisitos y no detalles**:

- `monedas`: **una por cada fila del catálogo, siempre.** Nunca vacío, ni siquiera sin movimientos
  (FR-013, FR-014).
- `gastosPorCategoria`: **sólo las categorías con al menos un gasto en el período** (FR-009). Vacío
  es un resultado normal, no un caso especial.
- `Resumen`: uno. No hay listado de resúmenes ni identificador: el resumen no es una cosa que exista
  fuera de la pregunta que lo produjo.

---

## Invariantes

Los que un test tiene que poder romper. Los tres primeros son igualdades entre números, que es
justamente lo que no se ve mirando la pantalla.

- **INV-01** — `balance == totalIngresado - totalGastado`, por moneda. *(FR-007)*
- **INV-02** — la suma de `gastosPorCategoria[].total` es igual a `totalGastado`, por moneda.
  *(FR-009)*
- **INV-03** — los totales del resumen son iguales a los que se obtienen sumando el listado filtrado
  con el mismo período. *(FR-005)*
- **INV-04** — ningún monto de otra cuenta suma en ningún total, subtotal ni balance. *(FR-015,
  AC-02 de la deuda de la feature 004)*
- **INV-05** — ningún total mezcla montos de monedas distintas, y no hay conversión en ningún lado.
  *(FR-011)*
- **INV-06** — `monedas` tiene tantas entradas como el catálogo, siempre. *(FR-013)*
- **INV-07** — ninguna categoría de ingreso aparece en `gastosPorCategoria`. *(FR-008)*

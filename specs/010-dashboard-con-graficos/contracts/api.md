# Contrato: Dashboard con gráficos

**Feature**: `010-dashboard-con-graficos` · **Fecha**: 2026-09-05

## El contrato no cambia

Es la única feature de producto del proyecto que no toca el contrato. Ni un endpoint nuevo, ni un
campo nuevo, ni un tipo nuevo en `frontend/src/api/tipos.ts`.

Lo que el dashboard necesita está declarado en las dos pilas desde FEAT-001c y **verificado desde
entonces** por `Contrato/ContratoResumenTests.cs`, que compara `Resumenes/ResumenDtos.cs` contra
`frontend/src/api/tipos.ts` en las dos direcciones. Esta feature no le agrega trabajo a esa
verificación: le da el primer consumidor.

---

## Lo que se consume

### `GET /api/resumen`

| | |
|---|---|
| **Sesión** | Obligatoria. Sin cookie válida, `401` — como todo endpoint del proyecto |
| **Query** | `desde` y `hasta`, `YYYY-MM-DD`, **opcionales y solidarios**: van los dos o ninguno |
| **200** | `Resumen` — ver [data-model.md](../data-model.md) |
| **400** | `ProblemDetails` con la clave **`rango`** en `errors` |

**Quién lo llama, y cómo:**

| Quién | Llamada | Por qué así |
|---|---|---|
| Pantalla principal | `GET /api/resumen` **sin parámetros** | El mes en curso lo decide el servidor. Que el filtro exista no convierte al valor por omisión en algo que el cliente elige |
| Dashboard | `GET /api/resumen?desde=…&hasta=…` | El período lo elige la persona (`FR-004`) |
| Dashboard sin rango puesto | `GET /api/resumen` sin parámetros | Dos campos vacíos son *sin período pedido*, no un rango que haya que armar |

**El filtro de moneda no aparece en esta tabla, y eso es la decisión D-05**: es de presentación y no
viaja. `GET /api/resumen` sigue informando sobre todas las monedas del catálogo, siempre, y
`ResumenDelPeriodoTests.El_Resumen_No_Hereda_El_Acotado_Por_Moneda_Del_Listado` sigue significando
exactamente lo que significaba.

### Los dos rechazos del período

Los emite `Dominio/PeriodoPedido.cs`, el único intérprete, y los dos existen desde FEAT-001b:

| Qué llegó | Mensaje bajo `errors.rango` |
|---|---|
| Una sola de las dos fechas | `Indicá las dos fechas del rango, o ninguna.` |
| `desde` posterior a `hasta` | `La fecha de inicio no puede ser posterior a la de fin.` |

La pantalla los muestra tal cual, junto al control (`FR-005`). No los reescribe ni los traduce: son
el mensaje del servidor, y la clave `rango` existe —lo dice su propio comentario— *"porque el
frontend la usa para poner el mensaje al lado del control"*.

### `GET /api/monedas`

El selector de moneda del dashboard se llena del catálogo que `App.tsx` ya pide una vez por sesión
(D-06 de la feature 009). **No se agrega una petición**: el catálogo baja por props, igual que a los
otros dos consumidores que ya tiene.

---

## Qué verifica la barrera del contrato acá

`verificar-contrato.sh` se corre igual en el cierre de feature, aunque el contrato no cambie. Dos
razones:

1. `tipos.ts` se toca —aunque más no sea para nada— y la barrera existe justamente para el día en
   que alguien lo toca sin querer.
2. Es la puerta de cierre de feature que la constitución fija, y no se saltea porque *"esta vez no
   hacía falta"*.

Tarda ~2,5 min: corre `dotnet test` cinco veces desalineando un caso por forma de comparación.

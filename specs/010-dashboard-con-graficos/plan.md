# Implementation Plan: Dashboard con gráficos

**Branch**: `010-dashboard-con-graficos` | **Date**: 2026-09-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/010-dashboard-con-graficos/spec.md`

## Summary

Los totales que el servidor calcula bien desde FEAT-001c pasan a verse. Es lo único que esta feature
cambia, y de esa frase salen las dos piezas: **el resumen del mes en curso aparece en la pantalla
principal** —arriba del formulario y del listado, deuda D9-06— y **el dashboard nace como pantalla
propia**, con su rango de fechas, su filtro de moneda y su gráfico.

**No hay backend.** Ni migración, ni endpoint, ni DTO, ni un campo del contrato: `GET /api/resumen`
ya devuelve exactamente lo que el dashboard necesita, agregado en la base, con una entrada por cada
moneda del catálogo y el desglose ordenado de forma estable. Es la feature con el reparto más
desparejo de las diez: casi todo es `frontend/`.

Y hay una corrección que la reconciliación produjo y que cambia el orden del trabajo: **el riesgo
que el PRD marcaba como el más grande no existe.** El PRD dice que el volumen de 10000 movimientos
*"nunca se midió"* y pide atacarlo temprano; `RendimientoResumenTests` lo mide desde la feature 006,
con `[InlineData(10_000, 4000)]` escrito, y la 008 le sumó el caso repartido en dos monedas. Lo que
queda ahí es de forma —citar `PRD:AC-11` y `PRD:AC-12` en un test que ya los cubre— y no de fondo.

El enfoque técnico está en [research.md](./research.md), trece decisiones (D-01 a D-13). Las tres que
más condicionan el resto:

- **El gráfico se dibuja sin ninguna dependencia** (D-01, [ADR-002](../../docs/adr/ADR-002-el-grafico-sin-dependencia.md)), y **el equivalente textual no acompaña al gráfico: es el gráfico** (D-03). Una sola estructura, no dos representaciones que puedan discrepar.
- **El resumen no se iza a `App`** (D-06), a diferencia de los catálogos. Izarlo haría que el rango del dashboard moviera los números de la pantalla principal — que es literalmente lo que `FR-012` prohíbe.
- **El filtro de moneda es de presentación** (D-05): recorta lo que se ve, no lo que se pide. La garantía que la 009 blindó queda intacta y sin reabrirse.

## Technical Context

**Language/Version**: TypeScript 6.x (frontend) · C# / .NET 10 (SDK 10.0.301) (backend)

**Primary Dependencies**: React 19 + Vite 8 · EF Core 9.0.18 + Pomelo.MySQL 9.0.0. **Ninguna
dependencia nueva**: el gráfico se dibuja a mano (D-01, ADR-002)

**Storage**: MySQL 8.4.10, esquema `gestiongastos`; los tests contra `gestiongastos_test`. **Sin
migración y sin ningún cambio de modelo** (ver [data-model.md](./data-model.md))

**Testing**: xUnit (backend) · Vitest sobre happy-dom 20.11.15 (frontend)

**Target Platform**: navegador moderno + API HTTP sobre Linux/Windows

**Performance Goals**: p95 del resumen < 2 s con 1000 movimientos y < 4 s con 10000, sobre 100
ejecuciones (`PRD:AC-11`, `PRD:AC-12`). **Ya en verde**; la referencia de la 006 es 6 ms y 9 ms

**Constraints**: nada se suma en el cliente (`FR-014`) · el resumen se pide a lo sumo una vez por
pantalla y por período · cambiar de moneda cuesta **cero peticiones** (D-05) · contraste AA y
ninguna categoría distinguible sólo por color (`NFR-003`) · sumar una moneda al catálogo cuesta 0
líneas en las dos pilas

**Scale/Scope**: 0 endpoints tocados · 0 tipos del contrato tocados · 1 test de backend documentado
· ~6 componentes de frontend, 2 nuevos en `frontend/src/resumen/` y la pantalla del dashboard ·
catálogo de categorías de tamaño arbitrario, porque las propias no tienen tope (007)

## Constitution Check

*GATE: se evalúa antes de Phase 0 y otra vez después de Phase 1.*

| Principio | Cómo lo cumple este plan | Estado |
|---|---|---|
| **I · Test-First** | Cada tarea de implementación lleva su test antes y su rojo mostrado. Acá el rojo es fácil de conseguir y difícil de falsear: la pantalla principal no pide el resumen, así que el primer test —*el resumen del mes aparece arriba del formulario*— falla por ausencia del elemento, no por compilación | ✅ |
| **II · Cada AC tiene su test, y el test lo nombra** | Los 20 FR y 4 NFR de la spec y los 13 AC del PRD se reparten en tareas que citan su identificador. **Dos casos especiales**: `PRD:AC-11` y `PRD:AC-12` ya están medidos por un test que no los nombra — se agrega la cita, no la medición (D-11); y `PRD:AC-13` (contraste) estrena una forma de test que el proyecto no tenía (D-12) | ✅ |
| **III · VERIFY es una fase con puerta** | Una tarea VERIFY al cierre de cada historia con la puerta del stack tocado —casi siempre sólo frontend— y al cierre de la feature las dos pilas, cobertura y las **seis** barreras | ✅ |
| **IV · Tests deterministas y aislados** | El "hoy" entra por prop (`PropsApp.hoy`) como ya hace toda la app, y el `desde`/`hasta` del resumen viene del servidor, así que ningún test del frontend depende de la fecha real. Los fixtures del catálogo ya existen (`monedas.fixture.ts`, `categorias.fixture.ts`). **Se hereda la regla D-10 de la 009: ningún número fijo sobre el tamaño del catálogo de monedas** | ✅ |
| **V · Las barreras se verifican a sí mismas** | Esta feature **no agrega ninguna barrera** (D-13) y no toca ninguna: `verificar-monedas.sh` ya protege `FR-007` en las dos pilas y `verificar-desglose.sh` ya protege `FR-015`. Lo que sí estrena es un verificador de contraste, y se le aplica el mismo criterio: **se prueba contra un par que tiene que dar por debajo del umbral**, o no sabemos que sabe fallar (D-12) | ✅ |

**Resultado**: sin violaciones. *Complexity Tracking* queda vacío.

**Tres cosas que la puerta va a exigir y conviene saber antes de empezar:**

1. **La puerta de esta feature es más barata que la de las últimas tres.** Sin backend nuevo, el
   ciclo por tarea es `lint` + `tsc --noEmit` + `test` del frontend, que corre en segundos. El costo
   se concentra al cierre, cuando se corren las seis barreras (~11 min entre todas).
2. **`RendimientoResumenTests` no corre en CI.** Está excluida por `FullyQualifiedName!~Rendimiento`,
   así que los números de `AC-11` y `AC-12` salen de la puerta local y van anotados en el quickstart.
3. **`verificar-monedas.sh` exige los dos árboles limpios antes de empezar**, o no puede distinguir
   lo que ensució ella de lo que ya estaba sucio. Commitear antes de correrla.

### Re-evaluación después de Phase 1

Los artefactos de diseño no introdujeron ninguna violación. Tres puntos que el diseño **agregó** y
que refuerzan la constitución en vez de tensionarla:

- **D-03 elimina una clase entera de bug en vez de testearla.** Un gráfico y una tabla con los mismos
  números son dos representaciones que pueden discrepar; una sola fila con nombre, total y una barra
  dimensionada no puede contradecirse. Es el mismo criterio de *"es un endpoint y no dos"*, una capa
  más arriba, y hace que `FR-008` no cueste nada.
- **D-06 se escribió porque el atajo era tentador y estaba mal.** Izar el resumen a `App.tsx` es lo
  que la feature 007 hizo con los catálogos y lo que cualquiera copiaría por analogía. Acá produce
  exactamente el bug que `FR-012` prohíbe, y el bug sería invisible en la pantalla donde se produce.
  La regla queda escrita en las dos mitades: se iza lo que es el mismo dato para todos, no se iza lo
  que cada pantalla parametriza distinto.
- **D-09 hereda tres cicatrices de la 009 en vez de volver a pisarlas.** La respuesta vieja que pisa
  a la vigente, el cartel de error que sobrevive a una carga buena, y el catch silencioso. Las tres
  se encontraron en revisión hace un día y las tres tienen la misma forma acá — con una ventana más
  ancha, porque un rango de un año sobre 10000 movimientos tarda más que un acotado del listado.

## Project Structure

### Documentation (this feature)

```text
specs/010-dashboard-con-graficos/
├── plan.md              # Este archivo
├── spec.md              # Qué y por qué, con la reconciliación contra el código
├── research.md          # D-01 a D-13: las decisiones de diseño
├── data-model.md        # Lo que se lee. Sin modelo nuevo y sin migración
├── contracts/api.md     # El contrato NO cambia, y por qué la barrera se corre igual
├── quickstart.md        # Trece pasos a mano, más la medición y las seis barreras
├── checklists/
│   └── requirements.md  # 16/16 en verde
└── tasks.md             # Lo genera /speckit-tasks
```

Y fuera de la carpeta de la feature:

```text
docs/adr/ADR-002-el-grafico-sin-dependencia.md   # NUEVO: la decisión que el PRD pidió registrar
```

### Source Code (repository root)

```text
backend/
└── GestionGastos.Api.Tests/
    └── Rendimiento/RendimientoResumenTests.cs   # ÚNICO cambio de backend: cita AC-11 y AC-12 (D-11)

frontend/
├── src/
│   ├── api/cliente.ts                           # obtenerResumen(desde?, hasta?)
│   ├── api/tipos.ts                             # sin cambios: Resumen ya está declarado y verificado
│   ├── App.tsx                                  # Vista suma 'dashboard' (D-07)
│   ├── estilos/componentes.css                  # las barras y el bloque de totales
│   ├── resumen/
│   │   ├── ResumenDelPeriodo.tsx                # NUEVO: los totales de todas las monedas
│   │   ├── TotalesDeUnaMoneda.tsx               # NUEVO: ingresado, gastado, balance
│   │   └── GastosPorCategoria.tsx               # NUEVO: el gráfico, que es la tabla (D-03)
│   ├── dashboard/
│   │   ├── PantallaDashboard.tsx                # NUEVO: el período, el filtro y el resumen
│   │   └── ControlesDelPeriodo.tsx              # NUEVO: las dos fechas y el error del servidor
│   └── movimientos/
│       └── PantallaMovimientos.tsx              # el resumen del mes arriba, y su recarga al registrar
└── tests/
    ├── ResumenDelPeriodo.test.tsx               # NUEVO
    ├── GastosPorCategoria.test.tsx              # NUEVO
    ├── PantallaDashboard.test.tsx               # NUEVO
    ├── Contraste.test.ts                        # NUEVO: el verificador, y su caso que falla (D-12)
    ├── PantallaMovimientos.test.tsx             # el resumen arriba, y que se recarga
    └── App.test.tsx                             # la tercera vista y la vuelta
```

**Structure Decision**: se mantiene la separación `backend/` / `frontend/` de `AGENTS.md`, con su
única excepción declarada (ADR-001), que esta feature **no ejercita**: al no cambiar el contrato, los
tests de `Contrato/` no tienen nada nuevo que leer.

Dos carpetas nuevas de frontend y ninguna capa nueva. `resumen/` y `dashboard/` siguen la forma de
`movimientos/` y `categorias/`. La separación entre las dos no es cosmética: **`resumen/` es lo que
pinta un `Resumen` y lo usan las dos pantallas; `dashboard/` es lo que elige qué `Resumen` pedir**, y
sólo lo usa una. Es la misma frontera que D-06 traza en el estado.

## Complexity Tracking

Sin violaciones de la constitución que justificar.

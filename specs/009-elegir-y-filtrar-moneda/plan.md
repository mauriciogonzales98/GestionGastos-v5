# Implementation Plan: Elegir y filtrar la moneda de un movimiento

**Branch**: `009-elegir-y-filtrar-moneda` | **Date**: 2026-09-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/009-elegir-y-filtrar-moneda/spec.md`

## Summary

La moneda deja de ser una decisión del servidor y pasa a ser una elección del usuario. Es lo único
que esta feature cambia, y de esa frase salen las cuatro piezas: **la moneda viaja en la petición**
(alta y edición), **hay una entrada nueva que validar** contra el catálogo, **el catálogo se
expone** para que el selector y el acotado salgan de él y no de una constante, y **el listado se
puede acotar** por moneda.

El esquema no cambia y no hay migración: `movimiento.moneda_id` y la tabla `moneda` existen desde la
migración `Inicial`, y la feature 008 los verificó. Que una feature de este tamaño no toque la base
es `RF-32` cobrando — el catálogo se modeló como tabla y no como enum precisamente para esto.

En el frontend entran el selector, la columna con el código, el control de acotado por moneda y la
ventana emergente de edición. **No** entran la barra de filtros de categoría y fecha ni la vista de
totales: el corte está registrado en *Clarifications* de la spec y lo que queda afuera está en
*Deuda registrada* con su ticket.

El enfoque técnico está en [research.md](./research.md), once decisiones (D-01 a D-11). Las tres que
más condicionan el resto: la moneda viaja por identificador y es **opcional en las dos peticiones**
(D-01, D-02); el acotado entra en `DeLaCuenta` y el resumen recibe `null` explícito (D-05); y
`verificar-monedas.sh` se extiende al frontend, porque hasta hoy la promesa de "sumar una moneda
cuesta 0 líneas" sólo estaba protegida del lado del backend (D-11).

## Technical Context

**Language/Version**: TypeScript 6.x (frontend) · C# / .NET 10 (SDK 10.0.301) (backend)

**Primary Dependencies**: React 19 + Vite 8 · EF Core 9.0.18 + Pomelo.MySQL 9.0.0. **Ninguna
dependencia nueva**: la ventana emergente es un `<dialog>` nativo (D-07).

**Storage**: MySQL 8.4.10, esquema `gestiongastos`; los tests contra `gestiongastos_test`. **Sin
migración**: el esquema no cambia (ver [data-model.md](./data-model.md)).

**Testing**: xUnit (backend) · Vitest sobre **happy-dom 20.11.15** (frontend)

**Target Platform**: navegador moderno + API HTTP sobre Linux/Windows

**Project Type**: web — `backend/` y `frontend/` separados, como manda `AGENTS.md`

**Performance Goals**: p95 del guardado < 1 s sobre 100 ejecuciones, con la moneda elegida
(`PRD:NFR-03`, SC-008)

**Constraints**: cero interacciones adicionales para quien usa una sola moneda (`PRD:NFR-01`) ·
el catálogo se pide a lo sumo 1 vez por carga (`PRD:NFR-02`) · sumar una moneda al catálogo cuesta
**0 líneas de código en las dos pilas** (`PRD:AC-04`)

**Scale/Scope**: catálogo de monedas de tamaño arbitrario —ningún test puede suponer cuántas hay
(D-10)— · 4 endpoints tocados, 1 nuevo · 4 tipos del contrato · 4 componentes de frontend

## Constitution Check

*GATE: se evalúa antes de Phase 0 y otra vez después de Phase 1.*

| Principio | Cómo lo cumple este plan | Estado |
|---|---|---|
| **I · Test-First** | Cada tarea de implementación lleva su tarea de test antes, y su rojo mostrado. Acá el primer rojo llega solo: agregar `monedaId` a `tipos.ts` pone en rojo `ContratoMovimientosTests`, porque su `switch` lanza ante un campo del contrato que no sabe ejercitar (ver [contracts/api.md](./contracts/api.md)) | ✅ |
| **II · Cada AC tiene su test, y el test lo nombra** | Los 15 FR de la spec y los 13 AC del PRD se reparten en tareas que citan su identificador en el nombre del test. `PRD:AC-11` es el que salda D8-01 y no tenía cómo probarse hasta hoy | ✅ |
| **III · VERIFY es una fase con puerta** | Una tarea VERIFY al cierre de cada historia, con la puerta del stack tocado y su salida a la vista. Al cierre de la feature, las dos pilas más las **seis** barreras más cobertura | ✅ |
| **IV · Tests deterministas y aislados** | Reloj fijo vía `FactoriaConReloj`, como el resto. La moneda extra se agrega con el helper `ConLaMonedaAsync` de `MonedaComoDatoTests`, que la borra en un `finally`. **Y la regla D-10: ningún número fijo sobre el tamaño del catálogo** — es la que ya se rompió una vez | ✅ |
| **V · Las barreras se verifican a sí mismas** | Esta feature **no agrega una séptima barrera**: extiende la sexta. `verificar-monedas.sh` pasa a vigilar también `frontend/src/`, porque la superficie que protege creció (D-11). Esa extensión hay que verla fallar —dejar sucio un archivo del frontend y exigir el rojo—, igual que la 008 hizo con las dos mitades originales | ✅ |

**Resultado**: sin violaciones. *Complexity Tracking* queda vacío.

**Dos cosas que la puerta va a exigir y conviene saber antes de empezar:**

1. **`verificar-contrato.sh` es la barrera que más trabajo hace acá.** El contrato cambia en cuatro
   lugares —un tipo nuevo y tres tipos con un campo más—, y es la primera feature desde la 007 que
   toca las dos pilas. Tarda ~2,5 min porque corre `dotnet test` cinco veces.
2. **`GET /api/monedas` nace protegido o no nace.** `verificar-autorizacion.sh` agrega un endpoint
   desprotegido a propósito para comprobar que la barrera sabe fallar; un endpoint nuevo sin sesión
   la pone en rojo de verdad.

### Re-evaluación después de Phase 1

Los artefactos de diseño no introdujeron ninguna violación. Dos puntos que el diseño **agregó** y
que refuerzan la constitución en vez de tensionarla:

- **D-05 obliga a escribir `monedaId: null` explícito** en la llamada que alimenta el resumen. Es el
  mismo daño silencioso que `verificar-desglose.sh` vigila para `categoria.activa`, y por el mismo
  mecanismo: los totales de un período cerrado cambiarían sin que nadie tocara un movimiento.
- **D-07 se verificó en vez de suponerse.** Que happy-dom implemente `<dialog>.showModal()` era la
  premisa de la que colgaba toda la ventana emergente; se comprobó corriéndolo antes de escribir el
  plan, no a mitad de la implementación. Es el Principio V aplicado a una decisión de diseño.

## Project Structure

### Documentation (this feature)

```text
specs/009-elegir-y-filtrar-moneda/
├── plan.md              # Este archivo
├── spec.md              # Qué y por qué, con la reconciliación contra el código
├── research.md          # D-01 a D-11: las decisiones de diseño
├── data-model.md        # Entidades, validación y consultas. Sin migración
├── contracts/api.md     # El cambio del contrato, en cuatro lugares
├── quickstart.md        # Ocho pasos a mano, más la medición y las seis barreras
├── checklists/
│   └── requirements.md  # 16/16 en verde
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/
│   ├── Dominio/Moneda.cs                       # sin cambios: se lee, no se escribe
│   ├── Monedas/                                # NUEVO
│   │   ├── MonedaDto.cs
│   │   └── MonedasEndpoints.cs                 # GET /api/monedas, con sesión (D-03)
│   ├── Movimientos/
│   │   ├── MovimientoDtos.cs                   # monedaId opcional en alta y edición (D-02)
│   │   ├── ValidacionDelMovimiento.cs          # la moneda, con la forma de la categoría (D-04)
│   │   ├── MovimientosConsulta.cs              # el acotado en DeLaCuenta; null al resumen (D-05)
│   │   └── MovimientosEndpoints.cs             # alta y edición eligen; el listado acota
│   └── Program.cs                              # MapMonedas
└── GestionGastos.Api.Tests/
    ├── Contrato/ContratoMonedasTests.cs        # NUEVO
    ├── Contrato/ContratoMovimientosTests.cs    # el switch aprende monedaId
    ├── Integracion/MonedaElegidaTests.cs       # NUEVO: FR-001 a FR-003, FR-011, FR-012
    ├── Integracion/FiltrosDelListadoTests.cs   # el tercer acotado y la combinación
    └── Rendimiento/RendimientoAltaTests.cs     # un caso más; el existente intacto (D-09)

frontend/
├── src/
│   ├── api/tipos.ts                            # Moneda + monedaId en las dos peticiones
│   ├── api/cliente.ts                          # obtenerMonedas, editarMovimiento
│   ├── App.tsx                                 # el catálogo se pide una vez acá (D-06)
│   └── movimientos/
│       ├── CamposDelMovimiento.tsx             # NUEVO: los campos, compartidos (D-08)
│       ├── FormularioMovimiento.tsx            # los usa; suma el selector
│       ├── VentanaDeEdicion.tsx                # NUEVO: <dialog> nativo (D-07)
│       ├── ListadoMovimientos.tsx              # el código de la moneda + abrir la edición
│       └── PantallaMovimientos.tsx             # el acotado por moneda y el estado de la ventana
└── tests/                                      # un archivo por pieza, como ya está organizado

backend/verificar-monedas.sh                    # extiende su vigilancia a frontend/src/ (D-11)
```

**Structure Decision**: se mantiene la separación `backend/` / `frontend/` de `AGENTS.md`, con su
única excepción declarada —los tests de `Contrato/` leen `frontend/src/api/tipos.ts`, ADR-001—, que
esta feature usa más que ninguna desde la 007 pero no ensancha: sigue siendo lectura en una sola
dirección.

Dos carpetas nuevas y ninguna capa nueva. `Monedas/` sigue la forma de `Categorias/` con una
diferencia deliberada: **no tiene `MonedasConsulta`**, porque la moneda no tiene dueño y un canal
que no acota nada sugeriría que hay algo que aislar (D-03).

## Complexity Tracking

Sin violaciones de la constitución que justificar.

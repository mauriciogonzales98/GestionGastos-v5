# Implementation Plan: Categorías propias del usuario

**Branch**: `007-categorias-propias` | **Date**: 2026-09-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-categorias-propias/spec.md`

## Summary

El catálogo de categorías deja de ser diez filas fijas de todo el mundo y pasa a ser dos cosas a la
vez: una parte compartida e inmutable y una parte privada y editable de cada cuenta. Se agregan tres
endpoints —alta, renombre y baja lógica—, el `GET` existente empieza a devolver también las propias,
y aparece la pantalla que los usa.

El diseño se apoya en algo que ya estaba: la feature 001 anticipó `usuario_id` y `activa` en la
tabla, y las 001b/005 ya acotan por ámbito la categoría de un movimiento. **La única migración es
una columna `discriminador` que le permite al índice único convivir con la baja lógica**
([D-01](./research.md#d-01--la-unicidad-tiene-que-dejar-de-chocar-contra-una-fila-dada-de-baja)),
que es la pieza que el PRD no anticipó porque creía que el índice todavía era global.

Y hay una cosa que esta feature puede romper sin que se note: el resumen. El desglose por categoría
sale de un `JOIN` contra `categorias`, y filtrarlo por `activa` —el reflejo natural al agregar la
columna a las consultas— cambiaría totales históricos en silencio. Es la deuda D6-04 que dejó la
feature 006 y acá se salda con dos capas, no con una
([D-05](./research.md#d-05--el-desglose-del-resumen-no-puede-empezar-a-filtrar-por-activa)).

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.301) en el backend; TypeScript + React 19 + Vite en el frontend

**Primary Dependencies**: Entity Framework Core 9.0.18 + Pomelo.MySQL 9.0.0. **Ninguna nueva**: la
navegación a la pantalla de gestión usa el mismo mecanismo de estado que ya alterna login ↔
movimientos ([D-09](./research.md#d-09--la-pantalla-de-gestión-sin-enrutador)), decidido así en la
sesión de clarificación y sujeto a la regla de `AGENTS.md`

**Storage**: MySQL 8.4.10, schema `gestiongastos`, collation `utf8mb4_0900_ai_ci` — accent- y
case-insensitive, que es la mitad de FR-007 resuelta sin escribir nada

**Testing**: xUnit contra `gestiongastos_test`; Vitest en el frontend

**Target Platform**: aplicación web, backend en Linux

**Project Type**: web — backend y frontend separados, como fija `AGENTS.md`

**Performance Goals**: ninguno propio. RNF-01 (dashboard < 2 s con 1000 movimientos, < 4 s con
10000) sigue vigente y esta feature no debe empeorarlo: el catálogo de una cuenta son decenas de
filas y el `JOIN` del resumen no cambia de forma

**Constraints**: sin librerías nuevas; las diez predefinidas conservan sus ids, nombres y tipos
después de migrar (SC-005); ningún número del resumen cambia ante una baja (FR-011)

**Scale/Scope**: 4 endpoints de categorías (1 modificado, 3 nuevos), 1 migración, 1 canal de lectura
nuevo con su barrera, **1 barrera nueva con su propio script** —la quinta del proyecto—, 1 pantalla
nueva, y un cambio de una condición en la edición de movimientos

## Constitution Check

*GATE: antes de Phase 0, y otra vez después de Phase 1.*

### Antes del diseño

| Principio | Estado | Cómo se cumple |
|---|---|---|
| **I · Test-First** | ✅ | `/speckit-tasks` genera la tarea `[TEST]` antes que la de código para cada AC. Dos rojos son estructurales acá: la barrera del canal de categorías (D-03) y la del desglose (D-05) se ven fallar antes de que exista lo que vigilan |
| **II · Cada AC tiene su test que lo nombra** | ✅ | AC-01 a AC-13 del PRD, más FR-021/022/023 de la clarificación. La trazabilidad va en el nombre del test |
| **III · VERIFY es una fase con puerta** | ✅ | Puerta por historia y puerta completa al cierre, con las cuatro barreras |
| **IV · Tests deterministas y aislados** | ⚠️ | El riesgo real de esta feature: los tests van a crear categorías propias en una base compartida por la suite. Se resuelve en el diseño — ver abajo |
| **V · Las barreras se verifican a sí mismas** | ✅ | Dos barreras nuevas, cada una con su desarme en `verificar-aislamiento.sh` |

**Sobre el ⚠️ del Principio IV.** Hasta hoy las categorías eran diez filas inmutables: ningún test
las creaba y por eso ninguno podía ensuciar a otro. Desde esta feature sí, y
`ValidacionMovimientoTests` ya muestra la forma de hacerlo bien —crea su categoría con un id fijo
alto, la borra en un `finally` y **también antes** de crearla, porque una corrida interrumpida deja
la fila puesta—. El diseño adopta esa forma para todos los tests nuevos, y `LimpiarCuentasAsync`
tiene que llevarse también las categorías propias, que hoy no existen y por lo tanto no limpia.

### Después del diseño (Phase 1)

| Principio | Estado | Qué cambió al diseñar |
|---|---|---|
| **I** | ✅ | Sin cambios |
| **II** | ✅ | El diseño agregó FR-019 a FR-023; los cinco tienen escenario de aceptación en la spec |
| **III** | ✅ | Sin cambios |
| **IV** | ✅ | Resuelto: ids fijos altos, limpieza antes y después, y `LimpiarCuentasAsync` ampliado |
| **V** | ✅ | Las dos barreras quedaron especificadas con su desarme. La del canal de categorías **no** generaliza la de movimientos: el ámbito de una categoría es otro predicado, así que se comparte la vigilancia y no el acotado ([D-03](./research.md#d-03--un-canal-único-de-lectura-de-categorías-con-su-barrera)) |

**Sin violaciones que justificar.** La sección *Complexity Tracking* queda vacía a propósito.

## Project Structure

### Documentation (this feature)

```text
specs/007-categorias-propias/
├── plan.md              # Este archivo
├── research.md          # Phase 0 — las diez decisiones, con lo descartado
├── data-model.md        # Phase 1 — la columna nueva, el índice y las transiciones
├── quickstart.md        # Phase 1 — los ocho pasos a mano
├── contracts/
│   └── categorias.md    # Phase 1 — los cuatro endpoints
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 — lo genera /speckit-tasks
```

### Source Code (repository root)

```text
backend/GestionGastos.Api/
├── Dominio/
│   └── Categoria.cs                    # + Discriminador
├── Persistencia/
│   └── GestionGastosDbContext.cs       # índice rehecho con Discriminador
├── Migrations/                         # 1 migración: columna + índice
├── Categorias/
│   ├── CategoriasConsulta.cs           # NUEVO — el canal único (D-03)
│   ├── CategoriasEndpoints.cs          # GET ampliado + POST, PUT, DELETE
│   ├── CategoriaDto.cs                 # + esPropia
│   └── ValidacionDeLaCategoria.cs      # NUEVO — nombre y unicidad
└── Movimientos/
    └── MovimientosEndpoints.cs         # una condición más en la edición (FR-023)

backend/GestionGastos.Api.Tests/
├── Integracion/
│   ├── CategoriasPropiasTests.cs       # NUEVO — US1, US2, US3
│   ├── AislamientoDeCategoriasTests.cs # NUEVO — FR-012, FR-013, FR-014
│   ├── BarreraDeAislamientoTests.cs    # + el canal de categorías
│   ├── BarreraDelDesgloseTests.cs      # NUEVO — el resumen no filtra por activa (D-05)
│   └── ValidacionMovimientoTests.cs    # + el espejo de FR-023
└── Contrato/
    └── ContratoCategoriasTests.cs      # NUEVO — los cuatro endpoints

frontend/src/
├── App.tsx                             # el catálogo sube acá (D-08) + la vista nueva
├── api/
│   ├── tipos.ts                        # + esPropia, NuevaCategoria, CategoriaEditada
│   └── cliente.ts                      # + crear, renombrar, dar de baja
├── movimientos/PantallaMovimientos.tsx # recibe el catálogo por props
└── categorias/
    └── PantallaCategorias.tsx          # NUEVO — la gestión (FR-017)

backend/
├── verificar-aislamiento.sh            # + 1 desarme: el canal de categorías
└── verificar-desglose.sh               # NUEVO — la quinta barrera (D-05)
```

**Structure Decision**: se respeta la separación `backend/` ↔ `frontend/` de `AGENTS.md`, sin
excepciones nuevas. Las categorías estrenan carpeta propia en el frontend (`src/categorias/`),
espejo de `src/movimientos/`, porque la pantalla de gestión no es una pantalla de movimientos.

## Orden de ejecución, y por qué ése

1. **La migración y el índice** (D-01). Sin ellos, FR-009 no se puede ni testear: el alta choca.
2. **Las dos barreras, vistas fallar** (D-03, D-05). Antes de que exista el canal que vigilan y
   antes de tocar la consulta del resumen. Es la tercera vez en este proyecto que una barrera se
   escribe mirando código que ya está bien, y las dos anteriores caducaron en silencio.
3. **US1 (crear)**, con el canal y la validación de unicidad. Es el MVP: sin crear no hay nada que
   renombrar ni que dar de baja.
4. **US2 (renombrar)**, que reusa la validación de US1 entera.
5. **US3 (dar de baja)**, la que más cuidado necesita: toda su dificultad es **no** romper lo que
   las dos anteriores dejaron andando, y ahí es donde AC-06 y FR-011 se ganan.
6. **US4 (la pantalla)**, que es lo único que vuelve observable el resto desde la aplicación.
7. **Cierre**: la puerta completa, las cuatro barreras y el contrato.

FR-023 —la edición que acepta la categoría que ya tenía— va con US3, no antes: hasta que no se pueda
dar de baja, no hay forma de armarle el escenario.

## Complexity Tracking

*Sin violaciones de la constitución que justificar.*

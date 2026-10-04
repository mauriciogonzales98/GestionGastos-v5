# Implementation Plan: Identidad visual, navegación y pantallas más compactas

**Branch**: `025-identidad-visual` | **Date**: 2026-09-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/014-identidad-visual/spec.md`

## Summary

La app pasa de un estilo mínimo —siete colores, botones todos iguales, pantallas apiladas— a un
sistema visual con nombre: una paleta violeta sobria en dos modos que siguen al dispositivo,
tipografía y espaciado en escala, botones principal, secundario y de sólo ícono, y mensajes con
aspecto propio. Se estrena completo en la pantalla de acceso; la parte con sesión gana una barra de
navegación —lateral en escritorio, inferior en el teléfono—, un formulario de movimiento y un resumen
más compactos, y lápiz y tacho en las filas. Todo es frontend: el backend y la API no cambian.

Técnicamente: el sistema visual se llama **Lila**. La paleta sigue viviendo en CSS, con el modo
oscuro como las mismas variables redefinidas en `prefers-color-scheme` (sin JavaScript, así que no
hay destello). La navegación es un
componente `MarcoDeLaApp` con un único `<nav>` que CSS reubica según el ancho. Los íconos son SVG
propios. Y se suma **el modo navegador de Vitest con Chromium** para medir lo que un DOM simulado no puede:
anchos, alturas, áreas tocables y el modo oscuro aplicado.

## Technical Context

**Language/Version**: TypeScript 6 (frontend). C# / .NET 10 no se toca.

**Primary Dependencies**: React 19, Vite 8. **Nuevas, sólo de desarrollo**: `@vitest/browser-playwright`
`5.0.2` y `playwright` `1.63.0` ([research D-01](./research.md#d-01--el-navegador-de-pruebas-vitest-en-modo-navegador-con-chromium-vía-playwright)).
Ninguna dependencia nueva llega a la app (`NFR-002`).

**Storage**: N/A — ningún cambio de datos ([data-model.md](./data-model.md)).

**Testing**: Vitest 5 en dos proyectos: el actual, que corre en **happy-dom** —no en jsdom: el
repositorio vive en `/mnt/c` y jsdom no arranca dentro del límite que Vitest espera por un worker; el
motivo está escrito en `vite.config.ts`—, y `navegador` (Chromium sin ventana, archivos
`*.navegador.test.tsx`). El proyecto actual **conserva su entorno, su `pool` y su `setupFiles`**, pero
su configuración sí se toca: pasar a `test.projects` reescribe el bloque, y el proyecto nuevo necesita
su propio `setupFiles` porque el de hoy importa `jest-dom/vitest`, que espera el DOM del entorno y no
el de un navegador de verdad.

**Target Platform**: navegadores de escritorio y de teléfono, desde 360 px de ancho.

**Project Type**: aplicación web (frontend + backend); esta feature toca sólo `frontend/` y el CI.

**Performance Goals**: sin metas nuevas. El modo oscuro y la navegación son CSS; no agregan trabajo
en tiempo de ejecución.

**Constraints**: contraste AA en los dos modos; área tocable ≥ 44 × 44 px; sin desplazamiento
horizontal entre 360 y 1440 px; sin destello claro en modo oscuro; sin dependencias de estilo.

**Scale/Scope**: 3 pantallas con sesión y 1 sin sesión; 7 íconos; ~12 tokens de color por modo.
El **corte de disposición es 48rem (768 px)**, que es lo que `FR-023` delega en este plan: por debajo
la barra va abajo, desde ahí al costado. Medido y justificado en
[research D-05](./research.md#d-05--la-navegación-un-solo-nav-reubicado-por-css).

## Constitution Check

*GATE: antes de la Fase 0 y otra vez después de la Fase 1.*

| Principio | Cómo se cumple | Estado |
|-----------|----------------|--------|
| **I. Test-First** | Cada requisito visual se expresa primero como prueba que falla: una prueba en navegador que mide, por ejemplo, que la barra está abajo a 360 px, corre en rojo contra el código de hoy (no hay barra), y recién ahí se escribe el CSS. Las pruebas de medición **son** las que permiten TDD en una feature visual: con un DOM simulado sólo se podía probar que la regla existiera | ✅ |
| **II. Cada AC tiene su test** | Los escenarios de aceptación de las 9 historias y los `FR`/`SC` medibles tienen prueba que los nombra. Los que sólo pueden mirarse con los ojos (que el violeta "se lea sobrio") no son AC: son la dirección de `FR-021`, y el quickstart los recorre a mano | ✅ |
| **III. VERIFY con puerta** | La puerta del frontend no cambia de comandos: `pnpm test` pasa a correr los dos proyectos. El CI agrega la instalación de Chromium antes de `pnpm test` | ✅ |
| **IV. Deterministas y aislados** | Cada prueba fija su ancho de ventana y su esquema de color; ninguna depende de otra. **Riesgo identificado**: la fuente del sistema no es la misma en WSL que en el runner del CI, así que un ancho de texto puede diferir en unos píxeles. Por eso las pruebas afirman **invariantes** —"no desborda", "≥ 44 px", "está abajo", "la tarjeta mide lo mismo antes y después"— y nunca un número de píxeles exacto que dependa del texto | ✅ con la regla anotada |
| **V. Las barreras se verifican a sí mismas** | La barrera nueva es "la puerta mide en un navegador de verdad". Si Chromium no arranca, el proyecto `navegador` tiene que dar **rojo**, no saltearse. Una tarea lo comprueba rompiendo la ruta del navegador a propósito, igual que las ocho barreras del backend | ✅ con tarea |

**Violaciones**: una, justificada en *Complexity Tracking*: dependencias nuevas de desarrollo, que la
feature 011 había prohibido para sí misma (su `NFR-004`).

**Re-check después de la Fase 1**: el diseño no agregó nada que cambie la tabla. El contrato
([contracts/sistema-visual.md](./contracts/sistema-visual.md)) agregó una invariante —el
`outline-offset` del foco— que tiene prueba propia, y [research D-11](./research.md#d-11--qué-pruebas-existentes-cambian-y-por-qué)
lista qué pruebas existentes cambian y por qué requisito. ✅

## Project Structure

### Documentation (this feature)

```text
specs/014-identidad-visual/
├── spec.md
├── plan.md                      # este archivo
├── research.md                  # D-01 a D-11
├── data-model.md                # sin datos persistidos; tokens, pares, secciones
├── quickstart.md                # paso 0 (Chromium), puerta, recorrido a mano
├── contracts/
│   └── sistema-visual.md        # tokens, botones, BotonIcono, MarcoDeLaApp
├── checklists/
│   └── requirements.md
└── tasks.md                     # lo genera /speckit-tasks
```

### Source Code (repository root)

```text
frontend/
├── index.html                          # meta color-scheme; viewport-fit=cover
├── package.json                        # + @vitest/browser-playwright, playwright (dev)
├── vite.config.ts                      # projects: el actual (happy-dom) + navegador
├── src/
│   ├── App.tsx                         # envuelve las pantallas con sesión en MarcoDeLaApp
│   ├── estilos/
│   │   ├── base.css                    # tokens claro + oscuro, botones, campos, mensajes, foco
│   │   ├── disposicion.css             # l-marco, l-par, l-centrado
│   │   └── componentes.css             # tarjeta, conmutador, segmentado, barra, resumen, filas
│   ├── ui/
│   │   ├── iconos.tsx                  # NUEVO: 7 íconos SVG
│   │   ├── BotonIcono.tsx              # NUEVO
│   │   └── MarcoDeLaApp.tsx            # NUEVO: marca, nav, cuenta, main
│   ├── acceso/FormularioAcceso.tsx     # tarjeta, marca, clases; sin cambio de comportamiento
│   ├── movimientos/
│   │   ├── PantallaMovimientos.tsx     # pierde la cabecera; resumen sin desglose
│   │   ├── CamposDelMovimiento.tsx     # moneda después del monto; l-par
│   │   └── ListadoMovimientos.tsx      # lápiz y tacho
│   ├── categorias/PantallaCategorias.tsx   # lápiz y tacho; sin "Volver"
│   ├── dashboard/PantallaDashboard.tsx     # sin "Volver"
│   └── resumen/
│       ├── ResumenDelPeriodo.tsx       # conDesglose; encabezado en un renglón
│       └── TotalesDeUnaMoneda.tsx      # renglón de tres cifras; línea "sin movimientos"
└── tests/
    ├── fuentes.ts                      # colores por modo
    ├── Paleta.test.ts                  # cada par, en los dos modos
    └── *.navegador.test.tsx            # NUEVOS: las mediciones

.github/
├── workflows/ci.yml                    # + playwright install --with-deps chromium
└── dependabot.yml                      # un grupo para los majors de vitest y su proveedor
```

**Structure Decision**: la estructura existente de `frontend/`. Los tres componentes nuevos van en
`ui/`, que es donde viven las piezas que comparten varias pantallas (`CampoConError`, `contraste`).
`backend/` no se toca.

## Orden de trabajo sugerido para `/speckit-tasks`

1. **El navegador de pruebas** y su barrera (sin él, nada de lo visual se puede probar primero), y la
   **medición de la altura actual** del formulario y del resumen (D-07), antes de tocarlos.
2. **Tokens y modo oscuro** (US9 junto con la base): la paleta nueva, `Paleta.test.ts` por modo.
3. **Pantalla de acceso** (US1, US2, US3) — el MVP.
4. **Marco y navegación** (US5), y con él US4 (el resto hereda sin romperse).
5. **Formulario compacto** (US6), **resumen** (US7), **íconos** (US8).
6. Puerta de cierre completa, incluidas las barreras del backend aunque no se toque.

## Complexity Tracking

| Violación | Por qué hace falta | Alternativa más simple descartada porque |
|-----------|--------------------|------------------------------------------|
| Dos dependencias de desarrollo nuevas (`@vitest/browser-playwright`, `playwright`) y la descarga de Chromium en el CI | La feature es entera visual y la spec (`NFR-003`) exige **medir**: ni jsdom ni happy-dom calculan tamaños, así que sin un navegador cada requisito de ancho, alto, área tocable y modo oscuro quedaría verificado por regla escrita, que es la deuda D11-01 que lleva tres features abierta | Verificar por reglas como la 011: no mide nada, y lo decidió el usuario. Playwright Test end-to-end: mide lo mismo con la API y MySQL levantadas en el job del frontend y un segundo runner |
| Se levanta para esta feature el `NFR-004` de la 011 ("ninguna dependencia") | Era una restricción **de esa feature**, no del proyecto; `AGENTS.md` permite dependencias justificadas en la spec, y ésta lo está | — |

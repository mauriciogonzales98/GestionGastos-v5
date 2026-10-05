---
description: "Tareas de implementación — feature 014, identidad visual"
---

# Tasks: Identidad visual, navegación y pantallas más compactas

**Input**: documentos de diseño en `specs/014-identidad-visual/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/sistema-visual.md](./contracts/sistema-visual.md),
[quickstart.md](./quickstart.md)

**Tests**: obligatorios. La constitución fija TDD (principio I) y un test por AC (principio II), y
cada grupo cierra con una tarea VERIFY explícita (principio III).

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: se puede hacer en paralelo (archivo distinto, sin depender de una tarea incompleta)
- **[Story]**: a qué historia de usuario pertenece (US1…US9)
- Toda tarea nombra el archivo exacto

## Convenciones de esta feature

- **Dos proyectos de Vitest.** `dom` corre lo de hoy; `navegador` corre los `*.navegador.test.tsx`
  en Chromium. `pnpm --dir frontend test` corre los dos.
- **Anchos de referencia.** Toda prueba que mida disposición recorre **360 · 480 · 767 · 768 ·
  1024 · 1440 px**, salvo que la tarea diga otra cosa. 360 y 1440 son los extremos que fija la spec;
  480 y 1024 son el medio, que es donde una disposición pensada para los extremos se rompe sin que
  nadie la mire; y **767/768 son los dos lados del corte de 48rem**, el único ancho donde la
  navegación cambia de forma. Un corte sólo probado de un lado no está probado.
- **Las pruebas que miden afirman invariantes, nunca un número de píxeles que dependa del texto**
  (Constitution Check, principio IV): "no desborda", "≥ 44 px", "está abajo", "mide lo mismo antes
  y después". La fuente del sistema no es la misma en WSL que en el runner del CI.
- **Rojo real antes de verde.** Toda tarea `RED:` termina con la salida del fallo a la vista, y el
  fallo tiene que ser por la razón esperada, no por un import que no existe todavía.
- **Cada prueba cita su identificador**, y cuando el requisito viene del PRD cita **los dos**
  (`FR-038`, `PRD:AC-31`). Es el principio II de la constitución, y las pruebas de hoy ya lo hacen
  (`PRD:RNF-06`, `PRD-06:AC-06`, `006:AC-31`). Un `FR` de la spec sin el `AC` del PRD detrás deja la
  trazabilidad cortada justo donde hace falta: cuando alguien cambia el PRD y quiere saber qué
  pruebas toca.
- **Puerta por tarea** (`AGENTS.md`): `pnpm --dir frontend lint` + `format` + `exec tsc --noEmit` +
  `test`. El backend no se toca en ninguna tarea; sólo lo corre la puerta de cierre.

---

## Phase 1: Setup — el navegador que mide

**Purpose**: sin un navegador que calcule tamaños, ningún requisito visual de esta feature se puede
escribir primero como prueba que falla. Es el habilitador de todo lo demás.

- [X] T001 Agregar `@vitest/browser-playwright` en `5.0.2` y `playwright` en `1.63.0` como
      `devDependencies` **a versión exacta, sin `^`** (research D-01: el proveedor pide `vitest`
      `5.0.2` exacto, y cada `playwright` trae su propio Chromium) en `frontend/package.json`, y
      regenerar `frontend/pnpm-lock.yaml` con `pnpm --dir frontend install`
- [X] T002 Dejar Chromium utilizable en la máquina de desarrollo siguiendo el paso 0 de
      `specs/014-identidad-visual/quickstart.md` (`playwright install chromium` +
      `sudo … playwright install-deps chromium`), y **corregir en ese archivo lo que la instalación
      haya revelado**: research D-01 midió que falta `libnspr4.so` y no llegó a correr una prueba
- [X] T003 Convertir `frontend/vite.config.ts` a `test.projects` con dos proyectos: el actual
      —entorno `happy-dom`, `pool: 'threads'`, `setupFiles`, `include: tests/**/*.{test,spec}.{ts,tsx}`
      **excluyendo** `tests/**/*.navegador.test.tsx`— y uno nuevo llamado `navegador`, con el
      proveedor de Playwright, instancia `chromium` sin ventana e `include:
      tests/**/*.navegador.test.tsx`. Conservar textuales los comentarios que explican por qué el
      proyecto actual usa happy-dom y `threads` en `/mnt/c`
- [X] T004 [P] RED→GREEN: `frontend/tests/Medicion.navegador.test.tsx` — renderiza un bloque de
      ancho conocido y afirma que `getBoundingClientRect()` devuelve ese ancho y no cero. Es la
      prueba de que el proyecto `navegador` **mide**, que es justo lo que jsdom y happy-dom no
      pueden (deuda D11-01). Correrla primero contra el proyecto `dom` para verla fallar
- [X] T005 Darle al proyecto `navegador` su propio `setupFiles`. El actual
      (`frontend/tests/setup.ts`) importa `@testing-library/jest-dom/vitest`, que espera el DOM
      inyectado por el entorno y no el de un navegador de verdad; el modo navegador trae sus propios
      matchers. Crear `frontend/tests/setup.navegador.ts` con lo que ese proyecto necesite y dejar
      `setup.ts` intacto para el proyecto `dom`
- [X] T006 [P] Crear `frontend/tests/anchos.ts` con los seis anchos de referencia y un ayudante que
      corra un cuerpo de prueba en cada uno (`para cada ancho(...)`). **Es una constante compartida y
      no una lista repetida en cada archivo**: el día que el producto agregue un ancho objetivo, se
      agrega acá y lo heredan todas las pruebas que miden. Es la misma promesa que `tests/fuentes.ts`
      protege del lado del CSS — una comprobación que depende de que alguien se acuerde de agregar
      una fila no es una comprobación
- [X] T007 [P] RED→GREEN: `frontend/tests/SinDependenciasDeEstilo.test.ts` — lee
      `frontend/package.json` y afirma que (a) `playwright` y `@vitest/browser-playwright` están en
      `devDependencies` y **no** en `dependencies`, y (b) `dependencies` sigue teniendo exactamente
      `react` y `react-dom`: ninguna librería de componentes, framework de CSS ni paquete de fuentes
      (`NFR-002`, `SC-007`). **Es la única tarea que verifica la restricción que esta feature se
      levantó a sí misma**, y va acá y no al final porque el riesgo se crea en T001: el momento de
      poner la barrera es cuando se abre la puerta que vigila
- [X] T008 Barrera del navegador (principio V): comprobar que con `PLAYWRIGHT_BROWSERS_PATH`
      apuntando a un directorio vacío `pnpm --dir frontend test` termina en **rojo** y no en verde
      con el proyecto `navegador` salteado, y anotar el comando y la salida en el paso 0 de
      `specs/014-identidad-visual/quickstart.md`. Si Vitest lo saltea en silencio, configurarlo para
      que falle: una puerta que pasa sin Chromium afirma haber medido algo que no midió
- [X] T009 [P] Agregar el paso `pnpm exec playwright install --with-deps chromium` antes de `Tests`
      en el job `frontend` de `.github/workflows/ci.yml`, con el comentario de por qué (NFR-005: las
      pruebas que miden tamaños no pueden excluirse del CI)
- [X] T010 [P] Agregar en `.github/dependabot.yml` un grupo para los **majors** de `vitest` y
      `@vitest/*`, con el motivo escrito: hoy los majors llegan de a una dependencia por PR, y un
      major de `vitest` sin el de `@vitest/browser-playwright` rompe la instalación (research D-01)
- [X] T011 VERIFY del setup: `pnpm --dir frontend lint`, `format`, `exec tsc --noEmit`, `test` — los
      dos proyectos en verde, con la salida a la vista

**Checkpoint**: se puede escribir una prueba que mida y verla fallar.

---

## Phase 2: Foundational — la línea base medida y el sistema de tokens

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta que esta fase esté completa. Los tokens son de
toda la app y la línea base hay que medirla **antes** de tocar nada.

- [X] T012 Medir la **altura de hoy** del formulario de movimiento a 360 px y del resumen del mes
      con dos monedas (una sin movimientos), en
      `frontend/tests/LineaBase.navegador.test.tsx`, y anotar los números medidos en
      `specs/014-identidad-visual/research.md` (D-07). Es la referencia de `FR-032` y `SC-011`, y
      tiene que tomarse antes de modificar `CamposDelMovimiento.tsx` y `ResumenDelPeriodo.tsx`:
      comparar contra un número estimado es la trampa que la memoria del proyecto ya documentó
- [X] T013 RED: reescribir `frontend/tests/Paleta.test.ts` para medir **por modo** — extraer los
      colores de `:root` y los del bloque `@media (prefers-color-scheme: dark)` y correr la tabla
      `PARES` contra los dos (NFR-001, research D-03). Agregar los pares nuevos del contrato
      (superficie, texto secundario, sobre acento, éxito) y el caso que exige que un color declarado
      en un modo y no en el otro ponga la prueba en rojo. Falla contra la hoja de hoy
- [X] T014 Darle al sistema visual **su nombre** y escribirlo donde vive: un comentario de cabecera
      en `frontend/src/estilos/base.css` que lo nombre **Lila** y diga que es la única fuente de
      cualquier aspecto visual de la app, y el mismo nombre en
      [contracts/sistema-visual.md](./contracts/sistema-visual.md) y en el glosario de `AGENTS.md`.
      `FR-001` pide un sistema "único y **con nombre**", y hasta acá nadie lo nombró: sin nombre no
      hay a qué referirse cuando la próxima feature tenga que decir "usá Lila" en lugar de inventar
      un color
- [X] T015 Extender `frontend/tests/fuentes.ts` con la lectura de colores **por modo**
      (`coloresDeclarados` hoy aplana toda la hoja y no distingue `:root` del bloque oscuro), sin
      romper a `Contraste.test.ts` ni a los otros consumidores
- [X] T016 Escribir la paleta de los dos modos en `frontend/src/estilos/base.css` con los valores
      exactos de [contracts/sistema-visual.md](./contracts/sistema-visual.md): los doce
      `--color-*` en `:root`, los mismos doce redefinidos en `@media (prefers-color-scheme: dark)`,
      y `color-scheme: light dark` en `:root`. El violeta reemplaza al azul en acento, foco y barra
      del dashboard (`FR-021`). T013 pasa a verde. **Las barras del dashboard no llevan tarea
      propia**: desde la feature 011 `GastosPorCategoria.tsx` no declara colores —usa
      `var(--color-barra)` y `var(--color-riel)`—, así que cambiar el token acá les da el violeta
      solo. Y el criterio no cambia: siguen de un solo color (D11-04), no codifican categorías por
      color
- [X] T017 Agregar en `frontend/src/estilos/base.css` los tokens de tipografía, espaciado, radio y
      `--alto-tactil` del contrato, y la familia tipográfica del sistema (`FR-006`); el tamaño base
      y el de los campos quedan en 1rem (`FR-007`, research D-04)
- [X] T018 [P] Agregar en `frontend/index.html` `<meta name="color-scheme" content="light dark">` y
      `viewport-fit=cover` en la etiqueta `viewport` (research D-03 y D-05: sin `viewport-fit`,
      `env(safe-area-inset-bottom)` devuelve cero y `FR-026` no se puede cumplir)
- [X] T019 RED→GREEN: `frontend/tests/MovimientoReducido.test.ts` — toda transición declarada en
      `frontend/src/estilos/` vive dentro de `@media (prefers-reduced-motion: no-preference)`
      (`FR-016`, research D-10); después, envolver las transiciones en ese bloque
- [X] T020 Aspecto base de botones, campos y mensajes en `frontend/src/estilos/base.css`: variante
      principal (`c-boton--principal`) y secundaria (el `<button>` pelado), estados de campo normal
      / foco / error / deshabilitado, y `:focus-visible` con **`outline-offset` de al menos 2px**
      (`FR-003`, `FR-004`, invariante del foco del contrato)
- [X] T021 RED→GREEN: `frontend/tests/Foco.test.ts` — la regla de `:focus-visible` declara
      `outline-offset` ≥ 2px. Es la invariante del contrato: sin esa separación el anillo queda
      contra el acento, que da 1,58:1, y el par que se mide deja de ser el que se ve
- [X] T022 Conservar y extender en `frontend/tests/Paleta.test.ts` el bloque que comprueba que **el
      verificador sabe fallar** (hoy `describe('D-03 · el verificador de contraste sabe fallar')`):
      un color nuevo sin par declarado tiene que poner la prueba en rojo, **y ahora también un color
      declarado en un solo modo** (`FR-002`, `NFR-001`, principio V). Es la barrera que hace que
      agregar un color obligue a decidir su versión oscura y contra qué se mide
- [X] T023 VERIFY de la fase: la puerta del frontend completa, con la suite existente en verde

**Checkpoint**: la paleta y los controles básicos existen en los dos modos y están medidos.

---

## Phase 3: User Story 1 — Una pantalla de acceso que se ve terminada (P1) 🎯 MVP

**Goal**: marca, tarjeta única centrada, conmutador con una opción elegida y un solo botón
principal.

**Independent Test**: abrir la pantalla de acceso sin sesión y comprobar marca, contenedor,
conmutador con el modo activo distinguible y botón principal con aspecto de acción principal.

### Tests (primero, en rojo)

- [X] T024 [P] [US1] RED: `frontend/tests/PantallaDeAcceso.navegador.test.tsx` — la marca y el
      formulario están dentro de **un solo** bloque, el bloque está centrado horizontalmente y su
      ancho está acotado en 1440 px (AC1 y AC4 de US2, `FR-008`)
- [X] T025 [P] [US1] RED: en el mismo archivo, el ancho del bloque **no cambia** al alternar entre
      "Iniciar sesión" y "Crear cuenta" ni al pasar el botón a "Creando…" (`FR-012`, AC4 de US1)
- [X] T026 [P] [US1] RED: `frontend/tests/ConmutadorDeAcceso.test.tsx` — la opción elegida se
      distingue por algo más que el color (peso de letra y relleno) y su estilo cuelga de
      `[aria-pressed='true']`, el mismo atributo que ya la anuncia (`FR-009`, AC2 de US1)
- [X] T027 [P] [US1] RED: en el mismo archivo, el botón de envío es el **único** con la clase de
      acción principal en la pantalla (`FR-003`, AC3 de US1)

### Implementación

- [X] T028 [US1] Agregar en `frontend/src/estilos/componentes.css` la tarjeta (`c-tarjeta`, sobre
      superficie, `--radio-grande`, borde de 1px, ancho máximo 24rem) y el control segmentado
      (`c-segmentado`), con la opción elegida colgando de `[aria-pressed='true']` (research D-09)
- [X] T029 [US1] Agregar en `frontend/src/estilos/disposicion.css` la clase `l-centrado` que centra
      el bloque en la ventana
- [X] T030 [US1] Aplicar en `frontend/src/acceso/FormularioAcceso.tsx` la marca ("Gestión de gastos"
      a `--texto-marca`, en acento), la tarjeta, el conmutador segmentado y el botón principal a todo
      el ancho de la tarjeta — **sin tocar textos, roles ni atributos**: `NFR-004` exige que
      `frontend/tests/FormularioAcceso.test.tsx` siga en verde sin editar una línea
- [X] T031 [US1] Verificar que **las aserciones** de `frontend/tests/Accesibilidad.test.tsx` siguen
      en verde: foco visible en todo control, etiqueta asociada a todo campo y uso completo con el
      teclado (`FR-014`, `PRD:RNF-06`, `PRD:AC-55`). Es lo que la feature 011 dejó puesto, y una
      feature que repinta todos los controles es exactamente la que lo puede perder sin darse cuenta.
      **El archivo sí se va a editar**, en la tarea de limpieza de props de US5, y no por
      accesibilidad: le pasa a las tres pantallas las cuatro props de navegación que `FR-022`
      elimina, y son obligatorias. Lo que esta tarea vigila es que de ahí se borren props y nada más
- [X] T032 [US1] VERIFY de US1: la puerta del frontend completa; `FormularioAcceso.test.tsx` sin
      modificar y en verde (`SC-005`)

**Checkpoint**: la pantalla de acceso se ve terminada en escritorio.

---

## Phase 4: User Story 2 — Crear la cuenta desde el celular (P1)

**Goal**: el alta completa a 360 px, sin zoom y sin desplazamiento de costado.

**Independent Test**: recorrer el alta a 360 px midiendo cada control tocable, el tamaño del texto de
los campos y la ausencia de desplazamiento horizontal.

### Tests (primero, en rojo)

- [X] T033 [P] [US2] RED: en `frontend/tests/PantallaDeAcceso.navegador.test.tsx`, a 360, 768 y
      1440 px `document.documentElement.scrollWidth` no supera al ancho de la ventana, también con
      el texto al 200 % (`FR-011`, `NFR-003`, AC1)
- [X] T034 [P] [US2] RED: en el mismo archivo, **todo** control tocable de la pantalla —cada
      `button`, `input` y `select`— mide al menos 44 × 44 px. Se recorre el DOM, no una lista
      escrita a mano: un control nuevo queda cubierto solo (`FR-010`, `SC-003`, AC3)
- [X] T035 [P] [US2] RED: en el mismo archivo, el tamaño de letra calculado de cada campo es de al
      menos 16 px (`FR-007`, AC2)

### Implementación

- [X] T036 [US2] Aplicar `--alto-tactil` como `min-height` (y `min-width` en los de sólo ícono) a
      botones, campos y selects en `frontend/src/estilos/base.css`, y el margen lateral de la
      tarjeta a 360 px en `frontend/src/estilos/componentes.css`
- [X] T037 [US2] VERIFY de US2: la puerta completa. Con T033–T035 en verde quedan medidos
      —no deducidos de las reglas— los puntos de la deuda **D11-01** en la pantalla de acceso

**Checkpoint**: MVP entregable — la pantalla de acceso terminada y medida en teléfono y escritorio.

---

## Phase 5: User Story 5 — Moverse por la app desde una barra de navegación (P1)

**Goal**: las tres secciones siempre a la vista, al costado en escritorio y abajo en el teléfono, con
la cuenta y el cierre de sesión.

**Independent Test**: iniciar sesión y recorrer las tres secciones desde la barra, en escritorio y a
360 px, comprobando la marca de la sección actual y el cierre de sesión en los dos anchos.

### Tests (primero, en rojo)

- [X] T038 [P] [US5] RED: `frontend/tests/MarcoDeLaApp.test.tsx` — hay **un solo** `<nav>` en el
      DOM, con las tres secciones, y la actual lleva `aria-current="page"` (`FR-024`, AC3;
      research D-05: dos `<nav>` son dos puntos de referencia que pueden desincronizarse)
- [X] T039 [P] [US5] RED: en el mismo archivo, desde cada sección se llega a cualquier otra con un
      solo clic, incluido dashboard → categorías (`SC-009`, AC4)
- [X] T040 [P] [US5] RED: en el mismo archivo, el orden de tabulación es marca, secciones, cuenta,
      contenido, y el cierre de sesión se alcanza y se activa con el teclado (`FR-025`, AC5)
- [X] T041 [P] [US5] RED: `frontend/tests/Navegacion.navegador.test.tsx` — a 1440 y 1024 px la barra
      está al costado (su borde derecho queda a la izquierda del `<main>`) y a 360 y 480 px está
      abajo (su borde superior queda por debajo del `<main>`) (`FR-023`, AC1 y AC2)
- [X] T042 [P] [US5] RED: en el mismo archivo, **el corte probado de los dos lados**: a 767 px la
      barra está abajo y a 768 px al costado. Es el único ancho donde la navegación cambia de forma,
      y un corte probado de un solo lado deja pasar que el valor del `@media` se escriba mal
      (`FR-023`, research D-05)
- [X] T043 [P] [US5] RED: en el mismo archivo, cada sección mide al menos 44 × 44 px en **todos** los
      anchos de referencia, y en ninguno hay desplazamiento horizontal de la página (`FR-025`)
- [X] T044 [P] [US5] RED: en el mismo archivo, a 360 px con contenido largo el final del `<main>`
      queda **por encima** de la barra inferior y no debajo (`FR-026`, AC6)
- [X] T045 [P] [US5] RED: en el mismo archivo, un email largo en la barra se corta con puntos
      suspensivos y no ensancha la barra ni produce desplazamiento horizontal (caso borde)

### Implementación

- [X] T046 [US5] Crear `frontend/src/ui/iconos.tsx` con `IconoMovimientos`, `IconoDashboard` e
      `IconoCategorias`: `<svg viewBox="0 0 24 24">` de trazo, `stroke="currentColor"`,
      `aria-hidden="true"`, `focusable="false"` — toman el color del texto y por eso siguen a los
      dos modos sin declarar color propio (research D-06)
- [X] T047 [US5] Crear `frontend/src/ui/MarcoDeLaApp.tsx` con la interfaz de
      [contracts/sistema-visual.md](./contracts/sistema-visual.md) y el orden del DOM que fija
      research D-05: marca · `<nav aria-label="Secciones">` con tres botones · bloque de cuenta
      (email y "Cerrar sesión") · `<main>`. Botones con `aria-current="page"`, **no enlaces**: la app
      no tiene rutas
- [X] T048 [US5] Agregar `l-marco` en `frontend/src/estilos/disposicion.css` y la barra en
      `frontend/src/estilos/componentes.css`: desde 48rem, grilla de dos columnas con barra de
      `--ancho-barra-lateral` y `position: sticky`; por debajo, franja superior con cuenta y cierre
      de sesión, barra `position: fixed` abajo con `padding-bottom: env(safe-area-inset-bottom)`, y
      relleno inferior del `<main>` igual al alto de la barra. La sección actual se distingue por
      relleno, marca lateral o superior y peso de letra (`FR-024`)
- [X] T049 [US5] Envolver en `frontend/src/App.tsx` las tres pantallas con sesión en `MarcoDeLaApp`,
      pasándole `seccion`, `email`, `onIrA` y `onCerrarSesion`. La sección inicial sigue siendo
      movimientos (`FR-028`, la constante `VISTA_INICIAL` no cambia)
- [X] T050 [US5] Quitar la cabecera de `frontend/src/movimientos/PantallaMovimientos.tsx` (email y
      los tres botones) y su prop `onGestionarCategorias` / `onVerDashboard` / `onCerrarSesion`, que
      ahora viven en el marco (`FR-022`)
- [X] T051 [P] [US5] Quitar el botón "Volver a movimientos" y la prop `onVolver` de
      `frontend/src/categorias/PantallaCategorias.tsx` (`FR-022`)
- [X] T052 [P] [US5] Quitar el botón "Volver a movimientos" y la prop `onVolver` de
      `frontend/src/dashboard/PantallaDashboard.tsx` (`FR-022`)
- [X] T053 [US5] Borrar las props de navegación que quedan de más en los **seis archivos que cambian
      sólo para compilar**, sin tocar ninguna de sus aserciones:
      `frontend/tests/Accesibilidad.test.tsx` (las cuatro props, 5 usos),
      `frontend/tests/CargaInicial.test.tsx` (3 props, 12 usos),
      `frontend/tests/EliminarMovimiento.test.tsx` (6 usos),
      `frontend/tests/FiltrosDelListado.test.tsx` (3 usos),
      `frontend/tests/PantallaDashboard.test.tsx` (`onVolver`, 3 usos) y
      `frontend/tests/PantallaMovimientos.test.tsx` (6 usos). Son props **obligatorias**
      (`: () => void`, sin `?`), así que al salir de la interfaz cada una deja un error de TypeScript,
      y la puerta incluye `tsc --noEmit`: sin esta tarea las tres tareas anteriores dejan la puerta en
      rojo por seis archivos que la spec daba por intactos (`FR-022`, `SC-006` grupo b, research
      D-11). Las mismas props hay que borrarlas de los tres archivos de las dos tareas siguientes,
      que además cambian aserciones
- [X] T054 [US5] Actualizar `frontend/tests/App.test.tsx` y `frontend/tests/PantallaCategorias.test.tsx`
      (el caso de la línea 170): "Volver a movimientos" pasa a ser la sección "Movimientos" de la
      barra. Cada cambio queda justificado por `FR-022` en un comentario, como pide `SC-006`
- [X] T055 [US5] Actualizar en `frontend/tests/TecladoFormulario.test.tsx` el orden de tabulación:
      los tres botones de cabecera pasan a ser Movimientos, Dashboard, Categorías y Cerrar sesión
      (`FR-022`). El orden de los campos lo cambia US6, en T068
- [X] T056 [US5] VERIFY de US5: la puerta completa, con los dos proyectos en verde

**Checkpoint**: la navegación funciona en los dos anchos y el resto de la app vive dentro del marco.

---

## Phase 6: User Story 3 — Que cada situación se vea como lo que es (P2)

**Goal**: error de campo, error general, confirmación y envío en curso, cada uno con aspecto propio
y distinguibles también sin color.

**Independent Test**: provocar los cuatro estados en la pantalla de acceso y comprobar que cada uno
tenga su aspecto y que se distingan entre sí vistos en escala de grises.

### Tests (primero, en rojo)

- [X] T057 [P] [US3] RED: `frontend/tests/EstadosDelAcceso.test.tsx` — con un email inválido el
      campo queda marcado (`aria-invalid='true'` más borde y grosor de error) y el motivo aparece
      pegado a él (`FR-004`, AC1)
- [X] T058 [P] [US3] RED: en el mismo archivo, con credenciales incorrectas el error aparece como
      mensaje del formulario y **ningún campo queda marcado** (`FR-013`, AC2)
- [X] T059 [P] [US3] RED: en el mismo archivo, la confirmación del alta y el error general se
      distinguen por algo más que el color —ícono propio y borde lateral propio—, y el formulario
      queda en "Iniciar sesión" con el email puesto (`FR-005`, `SC-004`, AC3)
- [X] T060 [P] [US3] RED: en el mismo archivo, con el envío en curso el botón se ve ocupado y no se
      puede volver a enviar (AC4)

### Implementación

- [X] T061 [US3] Agregar `IconoError` e `IconoExito` en `frontend/src/ui/iconos.tsx`
- [X] T062 [US3] Agregar en `frontend/src/estilos/componentes.css` el aspecto de los mensajes: borde
      lateral, fondo tenue y hueco para el ícono, uno con `--color-error` y otro con `--color-exito`
      (research D-09)
- [X] T063 [US3] Mostrar los íconos junto al error general y la confirmación en
      `frontend/src/acceso/FormularioAcceso.tsx`, **sin cambiar ningún texto ni rol** (`FR-013`,
      `NFR-004`)
- [X] T064 [US3] VERIFY de US3: la puerta completa; `FormularioAcceso.test.tsx` sigue sin modificar

**Checkpoint**: los cuatro estados del acceso se reconocen de un vistazo.

---

## Phase 7: User Story 4 — El resto de la app hereda los controles sin romperse (P2)

**Goal**: movimientos, categorías y dashboard con el aspecto nuevo, su disposición de siempre, y
nada desbordado, tapado ni ilegible.

**Independent Test**: recorrer las tres pantallas a 360 px y en escritorio; la suite existente de
esas pantallas sigue en verde salvo lo que `SC-006` autoriza.

### Tests (primero, en rojo)

- [X] T065 [P] [US4] RED: `frontend/tests/PantallasConSesion.navegador.test.tsx` — en **cada uno de
      los seis anchos de referencia** (360, 480, 767, 768, 1024, 1440) ninguna de las tres pantallas
      produce desplazamiento horizontal de la **página**; el de la tabla del listado sigue confinado
      a su contenedor. Se recorre el producto pantallas × anchos, no una lista a mano: una pantalla
      nueva queda cubierta sola (`FR-015`, AC2, `SC-006`)
- [X] T066 [P] [US4] RED: en el mismo archivo, en cada ancho de referencia ningún elemento queda
      **tapado por la barra ni cortado por el borde**: el `<main>` empieza después de la barra
      lateral y termina antes de la inferior, y ningún control tocable queda fuera de la ventana.
      Es la mitad del "responsive" que la ausencia de desplazamiento horizontal no cubre — una
      pantalla puede no desbordar y tener el botón de guardar debajo de la barra (`FR-015`, `FR-026`)
- [X] T067 [P] [US4] RED: en el mismo archivo, ninguna de las tres pantallas tiene más de un botón
      con la clase de acción principal por formulario (`FR-003`, AC3)

### Implementación

- [X] T068 [US4] Ajustar en `frontend/src/estilos/` lo que haga falta para que T065 y T067 queden en
      verde **sin cambiar la disposición del contenido** de esas pantallas: el alcance de esta
      historia es heredar los controles, no recomponer (deuda D14-01)
- [X] T069 [US4] VERIFY de US4: la puerta completa. Dejar anotado en el PR qué pruebas existentes
      cambiaron y por qué requisito, contra la tabla de research D-11

**Checkpoint**: con la deuda D11-01 y la parte medible de D12-01 saldadas en las cuatro pantallas.

---

## Phase 8: User Story 6 — Registrar un movimiento con un formulario compacto (P2)

**Goal**: tipo en un renglón; monto y moneda juntos; categoría y fecha juntas; nota a lo ancho; y
menos alto que hoy a 360 px.

**Independent Test**: abrir el formulario de alta a 360 px y en escritorio, comprobar la agrupación
y la altura total, y registrar un movimiento completo con el teclado.

### Tests (primero, en rojo)

- [X] T070 [P] [US6] RED: `frontend/tests/FormularioCompacto.navegador.test.tsx` — en escritorio,
      monto y moneda comparten renglón (mismo `top`), categoría y fecha comparten otro, y la nota
      ocupa el ancho completo (`FR-029`, AC1)
- [X] T071 [P] [US6] RED: en el mismo archivo, **en cada ancho de referencia** monto y moneda
      siguen en el mismo renglón —es la invariante dura de `FR-030`, vale en los seis—, categoría y
      fecha comparten renglón sólo si cada una conserva su ancho mínimo legible y si no se apilan, y
      ningún ancho produce desplazamiento horizontal (`FR-030`, AC2). El cálculo de
      research D-07 dice que a 360 px se apilan y desde unos 364 px comparten renglón; **es una
      cuenta, no una medición**: si la prueba la contradice, se corrige el cálculo en research y no
      la prueba
- [X] T072 [P] [US6] RED: en el mismo archivo, el formulario a 360 px y sin errores a la vista mide
      **menos alto** que el número medido en T012 (`FR-032`, `SC-010`)
- [X] T073 [P] [US6] RED: en `frontend/tests/FormularioMovimiento.test.tsx`, un error en un campo que
      comparte renglón se muestra pegado a ese campo y no desarma el renglón del otro (AC4)

### Implementación

- [X] T074 [US6] Agregar `l-par` en `frontend/src/estilos/disposicion.css`: dos controles en un
      renglón que se apilan si no entran con su mínimo. Dos usos: `minmax(0, 1fr) 6rem` para monto y
      moneda, y mínimo de 10rem por columna para categoría y fecha (research D-07)
- [X] T075 [US6] Mover la moneda **en el DOM** para que quede después del monto en
      `frontend/src/movimientos/CamposDelMovimiento.tsx`, y agrupar los campos con `l-par`. Se mueve
      en el DOM y no con `order` de CSS para que el orden de tabulación y el de lectura coincidan con
      el visual por construcción (`FR-031`, research D-07)
- [X] T076 [US6] Pintar el `<fieldset>` del tipo como control segmentado reusando `c-segmentado` de
      T028, en `frontend/src/estilos/componentes.css`. **Siguen siendo radios**: el teclado y lo que
      se anuncia no cambian (`FR-034`)
- [X] T077 [US6] Actualizar el orden de tabulación esperado en
      `frontend/tests/TecladoFormulario.test.tsx`: tipo, monto, moneda, categoría, fecha, nota,
      guardar (`FR-029`, `FR-031`, AC3). Es uno de los cambios que `SC-006` autoriza
- [X] T078 [US6] Comprobar que `frontend/src/movimientos/VentanaDeEdicion.tsx` hereda la misma
      agrupación por usar `CamposDelMovimiento`, y agregar la prueba que lo afirma en
      `frontend/tests/VentanaDeEdicion.test.tsx` (`FR-033`, AC5)
- [X] T079 [US6] VERIFY de US6: la puerta completa. Verificar explícitamente que
      `frontend/tests/ValidacionFormulario.test.tsx` sigue en verde sin tocarse: qué campos hay, qué
      aceptan y qué se valida **no cambia** (`FR-034`)

**Checkpoint**: el formulario que más se usa entra en una pantalla de teléfono.

---

## Phase 9: User Story 7 — Un resumen del mes que se lee de un vistazo (P2)

**Goal**: ingresado, gastado y balance por moneda en un renglón; una moneda sin movimientos en una
línea; el desglose sólo en el dashboard.

**Independent Test**: abrir movimientos con una moneda con movimientos y otra sin ninguno, comprobar
la forma de cada una y la ausencia del desglose; después abrir el dashboard y comprobar que el
desglose sigue ahí.

### Tests (primero, en rojo)

- [X] T080 [P] [US7] RED: en `frontend/tests/ResumenDelPeriodo.test.tsx`, con `conDesglose={false}`
      el resumen **no** incluye el desglose por categoría, y sin la prop lo incluye (`FR-035`,
      `FR-036`, AC3 y AC4)
- [X] T081 [P] [US7] RED: en `frontend/tests/PantallaMovimientos.test.tsx`, la pantalla de
      movimientos ya no muestra el desglose (hoy ninguna prueba afirma sobre él —revisado en
      research D-11—, así que esto es un **agregado**, no un cambio) (`FR-035`)
- [X] T082 [P] [US7] RED: `frontend/tests/ResumenCompacto.navegador.test.tsx` — las tres cifras de
      una moneda entran en un único renglón **en cada ancho de referencia**, con montos de siete
      cifras enteras y sin desplazamiento horizontal. El peor caso no es 360 px: a 768 px la barra
      lateral se lleva 14rem y el contenido queda más angosto que a 767, donde la barra está abajo y
      no ocupa ancho. Ese salto hay que medirlo (`FR-037`, AC1 y AC6)
- [X] T083 [P] [US7] RED: en el mismo archivo, el resumen con dos monedas —una sin movimientos—
      ocupa **menos de la mitad** del alto medido en T012 (`SC-011`)
- [X] T084 [P] [US7] RED: en `frontend/tests/ResumenDelPeriodo.test.tsx`, una moneda con
      `totalIngresado` y `totalGastado` en cero se muestra en una sola línea que dice que no tuvo
      movimientos en el período, y una moneda con movimientos cuyo balance da cero se muestra
      completa (`FR-038`, AC2)

### Implementación

- [X] T085 [US7] Agregar la prop `conDesglose?: boolean` (por defecto `true`) a
      `frontend/src/resumen/ResumenDelPeriodo.tsx` según el contrato, y pasarla en `false` desde
      `frontend/src/movimientos/PantallaMovimientos.tsx`. El dashboard no cambia su llamada
- [X] T086 [US7] Encabezado en un renglón —"Resumen del mes" y el período al lado, en texto
      secundario— en `frontend/src/resumen/ResumenDelPeriodo.tsx`, conservando la aclaración de que
      el resumen y el listado no miran lo mismo (`FR-039`)
- [X] T087 [US7] Renglón de tres cifras y línea de "sin movimientos" en
      `frontend/src/resumen/TotalesDeUnaMoneda.tsx`: sigue siendo un `<dl>`, con `tabular-nums`, y
      el balance negativo conserva el signo —la distinción sin color— con el color de error **sobre**
      el signo, nunca en lugar de él (`FR-037`, `FR-038`, AC5, research D-08)
- [X] T088 [US7] Bajar las cifras a `--texto-chico` por debajo de 48rem en
      `frontend/src/estilos/componentes.css` **si T082 lo pide**: research D-08 lo calculó, no lo
      midió, y la prueba es la que decide
- [X] T089 [US7] Actualizar el caso de `frontend/tests/ResumenDelPeriodo.test.tsx:57` ("una moneda
      sin movimientos aparece con sus totales en cero"). Cambia una decisión de la feature 006 (su
      `FR-009`): la razón se conserva —la moneda sigue apareciendo— y cambia la forma, por decisión
      del usuario. Dejarlo escrito en el comentario de la prueba. La prueba vecina (`:78`) queda y
      cobra más peso: es la que separa los dos casos
- [X] T090 [US7] **Subir el PRD a la versión 7**, porque esta feature cambia una regla de producto
      y el PRD se queda describiendo la anterior. Reescribir `PRD:AC-31` **en su lugar**, no
      numerarlo al final: hoy dice que sin ningún movimiento en el período *"el total ingresado, el
      total gastado y el balance **se muestran en cero para cada moneda**"*, y con `FR-038` la
      pantalla pasa a mostrar una línea que dice que la moneda no tuvo movimientos. La corrección
      tiene que separar las dos mitades que hoy el AC confunde: **la respuesta del servidor sigue
      devolviendo ceros** (`FR-040`) y lo que cambia es **lo que la pantalla muestra**. Agregar el
      bloque de versión arriba de `PRD.md` con el motivo, siguiendo la forma de la versión 6, y
      declarar que las specs de las features 006 y 010 lo citan con su texto viejo y **no se tocan**:
      son el registro de lo que se construyó entonces
- [X] T091 [US7] Dejar escrito en `specs/014-identidad-visual/spec.md`, al lado de la sección *"Una
      regla anterior que esta feature NO rompe"*, **la que sí rompe**: `PRD:AC-31`, con el motivo y
      el número de versión del PRD que lo corrige. Esa sección existe porque un borrador anterior
      afirmó romper una regla y era falso; el caso simétrico —romper una y no decirlo— merece el
      mismo lugar
- [X] T092 [US7] Verificar que los tests de backend que citan `006:AC-31`
      (`ResumenDelPeriodoTests.cs`, `MonedaComoDatoTests.cs`) **siguen en verde sin tocarse**: miran
      la respuesta del servidor, que no cambia. **Es justo por eso que este conflicto se esconde**:
      la suite se queda entera en verde mientras el PRD describe una pantalla que ya no existe
- [X] T093 [US7] VERIFY de US7: la puerta completa. **El backend no se toca**: sigue mandando el
      desglose en las dos respuestas, así que `ResumenDelPeriodoTests.cs:T031` de la feature 010
      —que compara dos respuestas del servidor y no dos pantallas— queda intacta (`FR-040`)

**Checkpoint**: la pantalla de movimientos arranca con lo que se usa todos los días a la vista.

---

## Phase 10: User Story 9 — Usar la app en modo oscuro (P2)

**Goal**: la app sigue la preferencia del dispositivo, en todas las pantallas, desde la primera
pintura.

**Independent Test**: abrir la app con la preferencia del dispositivo en oscuro y comprobar que la
paleta sea la oscura y que cada par de contraste cumpla su umbral en ese modo.

### Tests (primero, en rojo)

- [X] T094 [P] [US9] RED: `frontend/tests/ModoOscuro.navegador.test.tsx` — con la preferencia de
      esquema de color en `dark`, el color de fondo calculado del `<body>` es el del modo oscuro, y
      con `light` el del claro, en la pantalla de acceso (AC1 y AC2, `FR-017`)
- [X] T095 [P] [US9] RED: en el mismo archivo, lo mismo para movimientos, categorías y dashboard
      (AC4, `FR-017`)
- [X] T096 [P] [US9] RED: en el mismo archivo, al cambiar la preferencia con la pantalla montada y
      un email escrito, los colores cambian y el valor del campo **sigue ahí** (`FR-018`, AC3). Es
      la prueba de que el cambio lo hace el navegador y no React: con JavaScript eligiendo el tema,
      el componente se remontaría
- [X] T097 [P] [US9] RED: `frontend/tests/PrimeraPintura.test.ts` — ningún archivo de
      `frontend/src/` elige el tema desde JavaScript (`matchMedia` para pintar, clase `.oscuro`
      puesta a mano): la media query del CSS es la única fuente, y es lo que garantiza que no haya
      destello claro (`FR-019`, `SC-008`, research D-03)

### Implementación

- [X] T098 [P] [US9] RED: en `frontend/tests/ModoOscuro.navegador.test.tsx`, con la preferencia en
      oscuro el `color-scheme` calculado del `<html>` incluye `dark`, y **ninguna regla de
      `frontend/src/estilos/` lo pisa** con un `color-scheme` propio ni le fija un fondo claro a un
      `select`, un `input[type=date]` o una barra de desplazamiento (`FR-020`). Es el caso borde que
      la spec nombra —el selector de fecha blanco en medio de una pantalla oscura— y el único
      requisito del modo oscuro que no se cumple solo con declarar la paleta
- [X] T099 [US9] Completar en `frontend/src/estilos/componentes.css` y `disposicion.css` lo que haya
      quedado con un color fuera de token —la causa típica de que una pantalla se vea clara en modo
      oscuro—, hasta que T094 y T095 queden en verde
- [X] T100 [US9] VERIFY de US9: la puerta completa. `Paleta.test.ts` mide los dos modos desde T013,
      así que `SC-008` queda cubierto por una prueba y no por una revisión a ojo. Saldar **D11-05**

**Checkpoint**: la app entera funciona en los dos modos.

---

## Phase 11: User Story 8 — Íconos en lugar de palabras donde no hay ambigüedad (P3)

**Goal**: lápiz y tacho en cada fila de categorías y de movimientos; la palabra donde el ícono
dudaría.

**Independent Test**: abrir categorías y movimientos y comprobar que cada fila tenga los dos íconos,
que cada uno se anuncie con su acción y con qué fila es, y que las eliminaciones sigan pidiendo
confirmación con palabras.

### Tests (primero, en rojo)

- [X] T101 [P] [US8] RED: `frontend/tests/BotonIcono.test.tsx` — `aria-label` es el nombre completo,
      el ícono y el rótulo corto son `aria-hidden`, y el rótulo se muestra en `:hover` y en
      `:focus-visible` (`FR-045`, `FR-042`, AC3). No se usa `title`: no aparece al llegar con el
      teclado, que `FR-042` exige (research D-06)
- [X] T102 [P] [US8] RED: en `frontend/tests/PantallaCategorias.test.tsx`, cada fila tiene lápiz y
      tacho, el nombre accesible sigue siendo "Renombrar X" y "Dar de baja X" —el mismo de hoy, por
      eso estas pruebas **no deberían cambiar** (`SC-006`)—, y la baja sigue pidiendo confirmación
      con su advertencia (`FR-041`, `FR-044`, AC1, AC2, AC4)
- [X] T103 [P] [US8] RED: en `frontend/tests/ListadoMovimientos.test.tsx`, cada movimiento tiene
      lápiz y tacho, y el nombre accesible de **editar** dice de qué movimiento se trata, como ya lo
      hace el de eliminar (`FR-047`, AC6)
- [X] T104 [P] [US8] RED: `frontend/tests/FilasConIconos.navegador.test.tsx` — **en cada ancho de
      referencia** el nombre de la categoría y sus dos íconos entran en un solo renglón, un nombre
      largo (50 caracteres, el máximo que acepta el campo) se corta con puntos suspensivos antes de
      empujar los íconos a otra línea, y cada ícono mide al menos 44 × 44 px (`FR-043`, AC5)
- [X] T105 [P] [US8] RED: `frontend/tests/PalabrasEnLosBotones.test.tsx` — los botones de la tabla de
      `FR-046` que llevan palabra la conservan: envíos de formulario, confirmaciones irreversibles,
      "Cancelar" y "Cerrar sesión" (`FR-045`, AC7, AC8)

### Implementación

- [X] T106 [US8] Agregar `IconoEditar` (lápiz) e `IconoEliminar` (tacho) en
      `frontend/src/ui/iconos.tsx`. Es el tacho y no una cruz: en la misma fila, al renombrar,
      aparece "Cancelar", y una cruz se lee como cerrar o cancelar, no como eliminar
- [X] T107 [US8] Crear `frontend/src/ui/BotonIcono.tsx` con la interfaz del contrato (`nombre`,
      `accion`, `icono`, `onClick`)
- [X] T108 [US8] Agregar el aspecto de `BotonIcono` y su rótulo en `:hover` / `:focus-visible` en
      `frontend/src/estilos/componentes.css`, con área tocable de `--alto-tactil` en alto **y** en
      ancho
- [X] T109 [P] [US8] Reemplazar los dos botones con texto por `BotonIcono` en
      `frontend/src/categorias/PantallaCategorias.tsx`, conservando el nombre accesible actual
      (`FR-042`)
- [X] T110 [P] [US8] Reemplazar los dos botones con texto por `BotonIcono` en
      `frontend/src/movimientos/ListadoMovimientos.tsx` y hacer que el de editar diga de qué
      movimiento se trata (`FR-047`)
- [X] T111 [US8] Actualizar `frontend/tests/VentanaDeEdicion.test.tsx`, que busca `'Editar'`: el
      nombre pasa a decir qué movimiento edita (`FR-047`, uno de los cambios que `SC-006` autoriza).
      Verificar que las **aserciones** de `frontend/tests/EliminarMovimiento.test.tsx` no cambian:
      el tacho conserva el nombre accesible del botón de hoy (`FR-042`). De ese archivo ya se
      borraron props en US5, que es un cambio de otra clase (`SC-006` grupo b)
- [X] T112 [US8] VERIFY de US8: la puerta completa

**Checkpoint**: todas las historias funcionan de forma independiente.

---

## Phase 12: Polish y puerta de cierre

- [X] T113 [P] Comprobar los casos borde de la spec que todavía no tienen prueba y agregar la que
      falte en `frontend/tests/CasosBorde.navegador.test.tsx`: mensaje de error largo que no
      ensancha el bloque, email largo que se desplaza dentro del campo, texto del navegador al
      200 %, botón "Creando…" que no cambia de ancho, y pantalla apaisada
- [X] T114 [P] Comprobar que `frontend/tests/ClasesConRegla.test.ts` y
      `frontend/tests/AnchoDeLasPantallas.test.ts` siguen en verde con las clases nuevas
      (`l-marco`, `l-par`, `l-centrado`, `c-tarjeta`, `c-segmentado`, la barra, `BotonIcono`): toda
      clase que el código nombra tiene su regla, y ningún ancho fijo supera los 360 px
- [X] T115 [P] Limpiar las reglas CSS que quedaron **huérfanas** al desaparecer la cabecera de
      movimientos y los botones "Volver": `l-cabecera` y lo que haya quedado sin ningún `className`
      que lo nombre. `ClasesConRegla.test.ts` mira una sola dirección —clase usada sin regla— así que
      una regla sin uso pasa invisible y queda como CSS muerto que la próxima feature va a leer como
      si significara algo
- [X] T116 [P] Actualizar `specs/014-identidad-visual/research.md` con lo que las mediciones hayan
      corregido de los cálculos de D-07 y D-08 (el ancho mínimo de 10rem, las siete cifras a 360 px)
      y con las alturas de línea base de T012. Lo que se midió se marca como medido
- [X] T117 [P] Anotar en `specs/014-identidad-visual/spec.md` el estado de las deudas: **D11-05**
      saldada (T100), **D11-01** saldada (T037, T065), y de **D12-01** qué parte queda abierta —si
      la lectura del listado a 360 px con una nota larga es cómoda, que es D14-01
- [X] T118 Barrido responsive de cierre: `frontend/tests/Responsive.navegador.test.tsx` recorre
      **las cuatro pantallas × los seis anchos × los dos modos** y afirma en cada combinación las
      tres invariantes transversales — sin desplazamiento horizontal de la página, ningún control
      tocable por debajo de 44 × 44 px, y ningún control fuera de la ventana ni tapado por la barra.
      Es la red que atrapa la combinación que ninguna historia miró: cada una verificó lo suyo, y lo
      responsive se rompe justo en los cruces. Las pantallas se enumeran recorriendo el enum `Vista`
      más el acceso, no una lista escrita a mano
- [ ] T119 Recorrido manual del paso 2 de `specs/014-identidad-visual/quickstart.md`, en claro y en
      oscuro, los diez puntos de la tabla, y **arrastrando el borde de la ventana de 360 a 1440 px
      en cada pantalla**: lo que una prueba no puede juzgar es si el cambio de forma se ve bien,
      sólo que ocurre. Agregar ese arrastre como paso del quickstart
- [ ] T120 Cronometrar a mano el alta completa desde un teléfono de 360 px: menos de un minuto, sin
      zoom y sin desplazarse de costado (`SC-002`). **Es el único criterio de éxito que ninguna
      prueba cubre, y no es un olvido**: mide el tiempo de una persona, y `NFR-005` dice que lo que
      mide tiempo no puede sostener la puerta del CI. Se reporta en el PR con el número real
- [ ] T121 Paso 3 del quickstart en un teléfono real (zona de gestos, zoom al tocar un campo). Es
      **opcional**: si no se corre, se dice en el PR, no se da por hecho
- [ ] T122 Puerta de cierre de la feature (`AGENTS.md` + constitución), con la salida a la vista:
      frontend `lint` + `format` + `tsc --noEmit` + `test` + `build`; y el backend completo aunque
      no se haya tocado — `dotnet format --verify-no-changes`, `dotnet build -warnaserror`,
      `dotnet test`, cobertura y las **ocho** barreras (`verificar-contrato.sh`,
      `verificar-autorizacion.sh`, `verificar-desglose.sh`, `verificar-linter.sh`,
      `verificar-monedas.sh`, `verificar-aislamiento.sh`, `verificar-nota.sh`,
      `verificar-ambito-de-categoria.sh`)
- [ ] T123 **Ojo con `verificar-monedas.sh`**: desde la feature 009 exige `frontend/src/` limpio y
      sin recompilar, y esta feature toca `frontend/src/` en casi todas las tareas. Correrla con el
      árbol commiteado, no con cambios sin guardar, o va a dar rojo por el motivo equivocado

---

## Dependencies & Execution Order

### Dependencias entre fases

- **Setup (fase 1)**: no depende de nada. **Bloquea todo**: sin el navegador que mide, ninguna
  historia visual se puede escribir primero como prueba que falla. T006 (`tests/anchos.ts`) es
  prerrequisito de toda prueba que barra anchos, que son casi todas las que miden
- **Foundational (fase 2)**: depende del setup. **Bloquea todas las historias.** T012 además tiene
  que correrse **antes** de modificar el formulario y el resumen, o la línea base se pierde
- **US1 → US2**: US2 mide la pantalla que US1 construye. No son independientes y no se pretende que
  lo sean: son las dos caras del MVP
- **US5** es independiente de US1 y US2 (toca el marco, no el acceso) y puede ir en paralelo
- **US4** depende de US5: comprueba las pantallas **dentro** del marco nuevo
- **US6, US7, US8** dependen de US5, porque viven dentro del marco. Entre ellas son independientes
- **US9** depende de la fase 2 (la paleta oscura) y conviene cerrarla **después** de US6, US7 y US8,
  para que T095 recorra las pantallas ya terminadas
- **US3** depende de la fase 2 y de US1

### Dentro de cada historia

Tests primero y en rojo real → íconos y componentes → estilos → pantallas → pruebas existentes que
cambian → VERIFY. La tarea VERIFY cierra el grupo y no se marca hasta ver la salida en verde.

### Oportunidades de paralelismo

- Fase 1: T004, T006, T009 y T010 son archivos distintos
- Fase 2: T018 (index.html) va en paralelo con lo de `base.css`
- Todos los bloques `RED:` de una historia son archivos de prueba distintos y van en paralelo
- T051 y T052 (quitar "Volver") son dos pantallas distintas
- T109 y T110 (los íconos en las filas) son dos pantallas distintas
- Fase 12: T113–T117 son archivos distintos

---

## Implementation Strategy

### MVP (US1 + US2)

1. Fase 1 — el navegador que mide
2. Fase 2 — tokens, paleta de los dos modos, línea base medida
3. Fase 3 y 4 — la pantalla de acceso terminada y medida
4. **PARAR Y VALIDAR**: abrirla en claro y en oscuro, en escritorio y a 360 px

Es exactamente el pedido que originó la feature: *"se ve totalmente crudo, sin colores ni nada"*.

### Entrega incremental

MVP → US5 (la barra) → US4 (el resto hereda) → US3 (los estados) → US6 (el formulario) →
US7 (el resumen) → US9 (modo oscuro, cerrando sobre todo lo anterior) → US8 (los íconos) → polish.

Cada escalón deja la app usable: ninguna historia deja a otra a medio terminar.

---

## Notes

- `[P]` = archivos distintos, sin dependencias
- Un test que afirma un número de píxeles exacto dependiente del texto es un test que va a parpadear
  entre WSL y el CI. Se afirman invariantes
- Research D-07 y D-08 traen **cuentas, no mediciones**, y lo dicen. Si la prueba en navegador las
  contradice, se corrige el research, nunca la prueba
- Commitear después de cada tarea o grupo lógico. No se commitea con la puerta en rojo

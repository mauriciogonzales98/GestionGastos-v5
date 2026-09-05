---

description: "Task list for 010-dashboard-con-graficos"
---

# Tasks: Dashboard con gráficos

**Input**: Design documents from `/specs/010-dashboard-con-graficos/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md)

**Tests**: obligatorios. No es una opción de esta feature: el Principio I de
`.specify/memory/constitution.md` prohíbe escribir código de producción sin un test que ya haya
fallado.

**Organization**: por historia de usuario, en orden de prioridad.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[TEST]**: escribe una verificación · **[ROJO]**: la corre y exige que falle · **[VERIFY]**: puerta
- **[Story]**: US1, US2, US3, US4

---

## Lo que conviene leer antes de empezar

**1 · Esta feature no tiene backend, y eso cambia el ritmo.** Cero endpoints, cero DTOs, cero campos
del contrato, cero migraciones. Sólo **dos** tareas tocan `backend/`: una compara dos respuestas del
resumen entre sí (T031) y otra agrega una cita en un comentario (T042). Todo lo demás es
`frontend/`, así que la puerta por tarea es `lint` + `tsc --noEmit` + `test`, que corre en segundos.
El costo se concentra al cierre, cuando se corren las seis barreras.

**2 · El primer rojo hay que fabricarlo, y es fácil.** A diferencia de la 009, acá no hay una barrera
que se ponga en rojo sola: el contrato no cambia. El rojo de US1 es la ausencia del elemento —la
pantalla principal no pide el resumen y no lo pinta—, así que el test falla por *no encontrar lo que
busca*, no por compilación. Comprobar que ése es el motivo es parte de la tarea `[ROJO]`.

**3 · El resumen NO se iza a `App.tsx`** ([D-06](./research.md)). Es el atajo que cualquiera tomaría
por analogía con los catálogos de la feature 007, y acá produce exactamente el bug que `FR-012`
prohíbe: el rango del dashboard movería los números de la pantalla principal. La razón va escrita en
el código, no sólo acá — es un comentario que le ahorra el error a quien venga después.

**4 · La barra no es un dibujo del número: es el número con un ancho puesto**
([D-03](./research.md)). No hay un gráfico y al lado una tabla. Por eso **ningún test afirma sobre
píxeles ni sobre el ancho de nada** salvo el único que verifica la proporción (T020): todos los demás
afirman sobre el texto, que es donde el dato vive.

**5 · Ningún test escribe un número fijo sobre el tamaño del catálogo de monedas.** Es la regla D-10
de la feature 009 y esta feature la hereda con más motivo que ninguna: **el resumen devuelve una
entrada por cada moneda del catálogo**, así que "el resumen trae dos monedas" es la aserción más
natural del mundo y se rompe el día que `verificar-monedas.sh` corre la suite con una de más. Ya se
rompió una vez.

**6 · Tres cicatrices de la 009 se heredan, no se vuelven a pisar** ([D-09](./research.md)): la
respuesta vieja que pisa a la vigente (`22e3e96`), el cartel de error que sobrevive a una carga que
salió bien (`10a2e6d`), y el catch silencioso (`b0bc50e`). Las tres tienen la misma forma acá, con
una ventana más ancha: un rango de un año sobre 10000 movimientos tarda más que un acotado del
listado.

**7 · El esquema no cambia y no hay migración.** Si en algún momento parece que hace falta una, algo
se entendió mal: ver [data-model.md](./data-model.md).

---

## Phase 1: Setup

**Purpose**: saber de qué verde se parte, para que cualquier rojo posterior sea atribuible.

- [X] T001 Correr la puerta completa de las dos pilas sobre la rama recién sacada —`pnpm --dir frontend lint`, `pnpm --dir frontend exec tsc --noEmit`, `pnpm --dir frontend test`, `dotnet format backend/GestionGastos.slnx --verify-no-changes`, `dotnet build backend/GestionGastos.slnx -warnaserror`, `dotnet test backend/`— y anotar el conteo de tests de cada una. Es la línea de base: sin ella, un rojo del primer día se confunde con uno heredado

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: el `Resumen` de prueba que todas las historias necesitan, escrito una sola vez.

**⚠️ CRÍTICO**: las cuatro historias montan componentes que reciben un `Resumen`. Sin un fixture
compartido, cada archivo de test arma el suyo, y el día que el contrato cambie hay que arreglar
cuatro. Es el mismo criterio con el que ya existen `monedas.fixture.ts` y `categorias.fixture.ts`.

- [X] T002 Crear `frontend/tests/resumen.fixture.ts` con un `Resumen` de ejemplo: dos monedas —una con ingresos y gastos en varias categorías, otra **sin movimientos y en cero**—, más un helper para construir variantes. Dejar escrita en el archivo la regla del punto 5: los tests derivan del fixture (`fixture.monedas.length`), **nunca escriben cuántas monedas hay**. Reutilizar las monedas de `monedas.fixture.ts` en vez de inventar códigos nuevos
- [X] T003 [VERIFY] Puerta del frontend (`lint` + `tsc --noEmit` + `test`), con su salida. Un fixture que no compila bloquea las cuatro historias

---

## Phase 3: US1 — Cómo vengo este mes, apenas entro (P1) 🎯 MVP

**Goal**: el resumen del mes en curso aparece en la pantalla principal, arriba del formulario y del
listado. Salda la deuda **D9-06**.

**Independent Test**: registrar ingresos y gastos en dos monedas, recargar la pantalla principal, y
ver arriba los totales, el balance y el desglose de cada moneda — sin navegar a ninguna otra
pantalla.

### El cliente

- [X] T004 [TEST] [P] [US1] En `frontend/tests/cliente.test.ts`: `obtenerResumen()` sin argumentos pide `GET /api/resumen` **sin query string**, y con un rango manda `desde` y `hasta`. Un `401` lanza `ErrorDeSesion`, como el resto del cliente. Que la llamada sin argumentos no mande parámetros vacíos importa: el mes por omisión lo decide el servidor ante la **ausencia** de los dos ([contracts/api.md](./contracts/api.md))

### Los componentes que pintan un `Resumen`

- [X] T005 [TEST] [P] [US1] Crear `frontend/tests/ResumenDelPeriodo.test.tsx`: dado el fixture, por cada moneda se ven su código, lo ingresado, lo gastado y el balance (FR-002). Un balance **negativo se muestra como un número negativo**, no como un error ni como cero: un mes en rojo es exactamente la información que alguien necesita ver. Y la moneda sin movimientos aparece igual, en cero (FR-009)
- [X] T006 [TEST] [P] [US1] Crear `frontend/tests/GastosPorCategoria.test.tsx`: una fila por categoría, con su **nombre y su total** legibles como texto (FR-008); el orden es el que llegó y no se reordena en la pantalla (FR-016); y un `gastosPorCategoria` vacío muestra que no hay datos para graficar **sin ningún mensaje de error** (FR-009). **Sin barra todavía**: la barra es US2, y separarlas es lo que permite ver que el dato se lee sin ella
- [X] T007 [TEST] [P] [US1] En `frontend/tests/GastosPorCategoria.test.tsx`: los totales que se muestran son **los que llegaron**, carácter por carácter. Ninguna suma, ningún porcentaje, ningún total general calculado en la pantalla (FR-014). Es el requisito más fácil de romper sin que nadie lo note, porque romperlo da un número que igual parece correcto

### El montaje en la pantalla principal

- [X] T008 [TEST] [P] [US1] En `frontend/tests/PantallaMovimientos.test.tsx`: el resumen aparece **antes** que el formulario de registro y que el listado, afirmado sobre el orden real del DOM y no sobre la mera presencia (FR-011). Y **no hay ningún control de período en esta pantalla**: su resumen es siempre el del mes en curso (FR-011b)
- [X] T009 [TEST] [P] [US1] En `frontend/tests/PantallaMovimientos.test.tsx`: registrar un movimiento **vuelve a pedir el resumen**, y los totales incorporan lo recién registrado sin recargar la página. Lo mismo al editar uno. Es el escenario 2 de la historia, y es lo que hace que el número de arriba no mienta después de la primera interacción
- [X] T010 [TEST] [P] [US1] En `frontend/tests/PantallaMovimientos.test.tsx`, los tres estados que **no se pueden confundir** (FR-010, [D-10](./research.md)): mientras carga se dice que carga; si el servidor falla se dice que no se pudo cargar y **no se muestran ceros**; y el cartel de fallo **desaparece cuando una carga posterior sale bien**. Ese último es la cicatriz `10a2e6d` de la feature 009 — un cartel que sobrevive a una carga buena miente
- [X] T011 [TEST] [P] [US1] En `frontend/tests/PantallaMovimientos.test.tsx`: un `401` al pedir el resumen llama a `onSesionVencida` y vuelve al acceso, igual que cualquier otra pantalla protegida (FR-017). Un fallo de sesión no es un fallo de carga y no se muestra como tal
- [X] T012 [ROJO] [US1] Correr `pnpm --dir frontend test` y exigir **ROJO** en T004 a T011, verificando que el motivo es la ausencia de lo que se busca y **no** un error de compilación o un import roto. Mostrar la salida: es la comprobación del punto 2 de arriba

### La implementación

- [X] T013 [US1] `obtenerResumen(desde?, hasta?)` en `frontend/src/api/cliente.ts`, con la misma forma que `obtenerMovimientos`: los parámetros sólo se agregan si están. Verde de T004
- [X] T014 [P] [US1] `frontend/src/resumen/TotalesDeUnaMoneda.tsx` y `frontend/src/resumen/ResumenDelPeriodo.tsx`. Verde de T005
- [X] T015 [P] [US1] `frontend/src/resumen/GastosPorCategoria.tsx`, **sólo la tabla**: una fila por categoría con nombre y total. Verde de T006 y T007
- [X] T016 [US1] Montarlo en `frontend/src/movimientos/PantallaMovimientos.tsx`, arriba de todo, con su carga, su recarga tras registrar y editar, y sus tres estados separados. **El estado del resumen vive acá y no en `App.tsx`**, y el comentario que lo explica va en el código: izarlo haría que el rango del dashboard moviera estos números, que es lo que `FR-012` prohíbe ([D-06](./research.md)). Verde de T008 a T011
- [X] T017 [VERIFY] [US1] Puerta del frontend completa, con su salida

**Checkpoint**: la deuda D9-06 está saldada. El número que el servidor calcula bien desde FEAT-001c
por fin se ve, y se mueve cuando uno registra.

---

## Phase 4: US2 — Ver en qué se me va la plata (P2)

**Goal**: el dashboard nace como pantalla propia y el desglose pasa a estar representado
gráficamente.

**Independent Test**: navegar al dashboard y ver, por cada moneda, una fila por categoría con su
nombre, su total y una barra proporcional; volver a la pantalla principal.

> **Esta historia es más barata de lo que parece, y conviene saber por qué.** Los componentes que
> pintan un `Resumen` ya existen desde US1, así que el dashboard es *dónde* se muestran, no *cómo*.
> Y la barra son unas pocas líneas: es un ancho en porcentaje sobre una fila que ya existe
> ([D-03](./research.md)). Lo caro acá es el test de contraste, que estrena una forma que el proyecto
> no tenía.

- [X] T018 [TEST] [P] [US2] En `frontend/tests/App.test.tsx`: hay forma de ir de la pantalla principal al dashboard y de volver; `VISTA_INICIAL` sigue siendo `'movimientos'`; y **cerrar sesión desde el dashboard vuelve a movimientos**, no deja la vista puesta para la próxima cuenta. Es la misma regla que la feature 007 escribió para la pantalla de categorías, y la razón es la misma: que la próxima cuenta no entre donde salió la anterior
- [X] T019 [TEST] [P] [US2] Crear `frontend/tests/PantallaDashboard.test.tsx`: el dashboard pide el resumen y lo pinta reutilizando los componentes de `resumen/`, y **titula el período con el `desde` y el `hasta` que devolvió el servidor** — no con un mes calculado en el navegador. Es para lo que esos dos campos viajan desde la feature 006, y ésta es la primera pantalla que los usa
- [X] T020 [TEST] [P] [US2] En `frontend/tests/GastosPorCategoria.test.tsx`: cada fila lleva una barra cuyo ancho es proporcional al mayor total de esa moneda —la mayor al 100 %, una de la mitad al 50 %—, la barra es **decorativa** (`aria-hidden`) y no aporta ningún texto que la fila no tenga ya (FR-001, FR-008). **Éste es el único test de la feature que mira un ancho**, porque la proporción es lo único que puede fallar ahí
- [X] T021 [TEST] [P] [US2] En `frontend/tests/GastosPorCategoria.test.tsx`, `PRD:AC-13` y `NFR-003` del lado de la forma: **todas las barras comparten el mismo relleno**. Ninguna categoría se distingue por su color porque ninguna se codifica por color ([D-04](./research.md)); lo que las distingue es su nombre, al lado de su barra. Un test que exija colores distintos estaría verificando lo contrario de la decisión
- [X] T022 [TEST] [P] [US2] Crear `frontend/tests/Contraste.test.ts`: una función que calcula la relación de contraste WCAG, probada contra pares de valor conocido — **incluido uno que tiene que dar por debajo del umbral** ([D-12](./research.md)). Sin ese caso no sabemos que el verificador sabe distinguir, y es el Principio V aplicado a algo que no es un script. Recién después, los pares que el dashboard declara: 4,5:1 en texto normal, 3:1 en la barra y en los componentes
- [X] T023 [ROJO] [US2] Correr `pnpm --dir frontend test` y exigir **ROJO** en T018 a T022, verificando el motivo de cada uno. Mostrar la salida
- [X] T024 [US2] `Vista` suma `'dashboard'` en `frontend/src/App.tsx`, con el botón para ir desde la pantalla principal y el de volver, igual que la de categorías ([D-07](./research.md)). Sin router: no son rutas que enrutar sino un estado con tres valores. Verde de T018
- [X] T025 [US2] `frontend/src/dashboard/PantallaDashboard.tsx`, que pide el resumen **sin período** por ahora —el rango es US3— y lo pinta con los componentes de `resumen/`. Su estado es suyo y no se comparte con la pantalla principal (D-06). Verde de T019
- [X] T026 [US2] La barra en `frontend/src/resumen/GastosPorCategoria.tsx` y su estilo en `frontend/src/estilos/componentes.css`, con la convención `c-` del archivo: el componente no define su propia posición. Verde de T020, T021 y T022
- [X] T027 [VERIFY] [US2] Puerta del frontend completa, con su salida

**Checkpoint**: hay dashboard, y el desglose está representado gráficamente sin que haya entrado una
sola dependencia nueva.

---

## Phase 5: US3 — Mirar el período que yo elija (P3)

**Goal**: el rango de fechas del dashboard, con el resumen de la pantalla principal quieto.

**Independent Test**: registrar movimientos en dos meses, elegir el rango del mes anterior en el
dashboard, comprobar que los totales son los de ese mes, volver a la principal y comprobar que sus
números no se movieron.

- [X] T028 [TEST] [P] [US3] En `frontend/tests/PantallaDashboard.test.tsx`: elegir un rango vuelve a pedir el resumen **con ese `desde` y ese `hasta`**, y dos campos vacíos lo piden sin parámetros — que es *sin período pedido*, o sea el mes que el servidor elija ([D-08](./research.md))
- [X] T029 [TEST] [P] [US3] En `frontend/tests/PantallaDashboard.test.tsx`, `FR-005`: ante un `400` con la clave `rango`, el mensaje **del servidor** se muestra junto al control del período, y **los totales que estaban a la vista siguen ahí**. No se reemplazan por un vacío: un vacío se lee como *"no hay nada"* y escondería que la pregunta estaba mal formada. La pantalla **no** valida el rango por su cuenta — `PeriodoPedido` es el único intérprete, y un segundo intérprete es lo que ese comentario existe para evitar
- [X] T030 [TEST] [P] [US3] En `frontend/tests/PantallaDashboard.test.tsx`, la carrera ([D-09](./research.md)): dos cambios de rango seguidos con las respuestas llegando **al revés**, y la del rango viejo **no pisa** a la del vigente. Es la cicatriz `22e3e96` de la feature 009, con una ventana más ancha: un rango de un año sobre 10000 movimientos tarda más que un acotado del listado
- [X] T031 [TEST] [P] [US3] En `backend/GestionGastos.Api.Tests/Integracion/ResumenDelPeriodoTests.cs`, `FR-013` y `PRD:AC-09`: `GET /api/resumen` sin parámetros y `GET /api/resumen?desde=<1º del mes>&hasta=<último>` devuelven **el mismo desglose para las mismas categorías**. Es la igualdad entre la pantalla principal y el dashboard acotado al mes en curso, verificada donde puede verificarse de verdad: contra los dos números, no contra dos pantallas
- [X] T032 [TEST] [P] [US3] En `frontend/tests/App.test.tsx`, `FR-012` y `PRD:AC-08`: navegar al dashboard, cambiar el rango, volver a la pantalla principal, y comprobar que su resumen muestra **exactamente los mismos números** que antes de ir. Es el único requisito de esta feature cuya violación sería invisible en la pantalla donde se produce, y por eso el test navega en vez de mirar un componente aislado
- [X] T033 [ROJO] [US3] Correr las dos suites y exigir **ROJO** en T028 a T032, verificando el motivo. Mostrar las dos salidas. **Ojo con T031**: si pasa en verde al primer intento, no está mal — significa que la igualdad ya se cumple, que es lo esperable, y lo que el test agrega es que no se pueda romper sin avisar. Anotarlo
- [X] T034 [US3] `frontend/src/dashboard/ControlesDelPeriodo.tsx`: los dos campos de fecha y el lugar del mensaje de error, con la clave `rango`. Verde de T029
- [X] T035 [US3] El estado del período en `PantallaDashboard.tsx`, con la guarda que descarta la respuesta de un período que ya no es el vigente. Verde de T028, T030 y T032
- [X] T036 [VERIFY] [US3] Puerta de las **dos** pilas, con su salida: esta historia es la única que agrega un test de backend

**Checkpoint**: el dashboard es lo que lo distingue del resumen — el lugar donde uno elige qué mirar
— y la pantalla principal sigue clavada al mes en curso.

---

## Phase 6: US4 — Mirar una sola moneda (P4)

**Goal**: el filtro de moneda del dashboard. Salda la deuda **D9-02**.

**Independent Test**: con movimientos en dos monedas, elegir una y ver sólo esa; volver a "todas" y
ver las dos con los mismos números.

- [X] T037 [TEST] [P] [US4] En `frontend/tests/PantallaDashboard.test.tsx`: el selector ofrece **las monedas del catálogo más la opción de no acotar**, y sale del catálogo que baja por props —el que `App.tsx` ya pide una vez por sesión—, **no de una lista escrita a mano**. Incluir el caso que importa: una moneda que ninguna constante del código conoce aparece igual (FR-007). Es el que se pone en rojo el día que alguien escriba `['ARS', 'USD']`, y el que `verificar-monedas.sh` ya vigila desde la 009
- [X] T038 [TEST] [P] [US4] En `frontend/tests/PantallaDashboard.test.tsx`, `FR-006` y [D-05](./research.md): elegir una moneda muestra **sólo** su bloque, con **los mismos números** que tenía cuando se veían todas, y **no dispara ninguna petición**. Ese último es la mitad que prueba que el filtro es de presentación: si pidiera de nuevo, sería otra decisión
- [X] T039 [TEST] [P] [US4] En `frontend/tests/App.test.tsx`, `FR-006b`: con una moneda elegida en el dashboard, volver a la pantalla principal y comprobar que su resumen sigue mostrando **todas** las monedas. El filtro es del dashboard y no se contagia — que es, una capa más arriba, la misma garantía que la 009 blindó en el servidor
- [X] T040 [ROJO] [US4] Correr `pnpm --dir frontend test` y exigir **ROJO** en T037, T038 y T039. Mostrar la salida
- [X] T041 [US4] El selector y el estado del filtro en `frontend/src/dashboard/PantallaDashboard.tsx`: un recorte sobre `resumen.monedas`, sin ninguna petición y sin ninguna suma. Verde de T037, T038 y T039
- [X] T042 [VERIFY] [US4] Puerta del frontend completa, con su salida

**Checkpoint**: las cuatro aberturas del PRD están construidas y las dos deudas que la 009 dejó
—D9-02 y D9-06— están saldadas.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T043 [P] Agregar la cita de `PRD:AC-11` y `PRD:AC-12` a la documentación de los casos de `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoResumenTests.cs` ([D-11](./research.md)). **No se agrega ninguna medición**: los dos escalones que esos AC piden —1000 en < 2 s, 10000 en < 4 s, p95 sobre 100 ejecuciones— son exactamente los dos `InlineData` que ya están escritos desde la feature 006. Lo que falta es que un test los **nombre**, que es lo que el Principio II exige
- [X] T044 Correr `dotnet test backend/ --filter "FullyQualifiedName~RendimientoResumen"` y **anotar los tres p95 en la tabla del [quickstart](./quickstart.md)**. Está excluida del CI por medir tiempo de pared, así que el número sale de acá o no sale. La referencia de la feature 006 es 6 ms con 1000 filas en una moneda y 9 ms en dos: tener los números permite decir si algo cambió, en vez de sólo que pasó
- [X] T045 [P] Revisar los archivos de test que esta feature tocó y confirmar que **ninguno escribe un número fijo sobre el tamaño del catálogo de monedas** (punto 5 de arriba). El resumen devuelve una entrada por moneda, así que es la aserción más natural del mundo y la que rompe la suite el día que `verificar-monedas.sh` corre con una de más
- [X] T046 Recorrer el [quickstart](./quickstart.md) entero —los trece pasos— y anotar cualquier línea que no haya salido como dice. En la 008 fue el quickstart, y no la suite, el que encontró el número escrito a mano
- [ ] T047 [VERIFY] Las **seis** barreras, con su salida: `verificar-contrato.sh` (~2,5 min; el contrato no cambia y se corre igual), `verificar-autorizacion.sh`, `verificar-desglose.sh` —que es la que protege `FR-015`—, `verificar-monedas.sh` (~1 min; **exige los dos árboles limpios**, así que commitear antes), `verificar-aislamiento.sh` (~7 min) y `verificar-linter.sh`
- [ ] T048 Cobertura del backend con `dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings`, con su salida
- [ ] T049 [P] Actualizar `plan-de-implementacion/README.md`: el ticket 14 (DISC-001-05) pasa a la tabla de implementados, con lo que esta feature construyó, las dos deudas que saldó y la corrección de la premisa de rendimiento del PRD
- [ ] T050 [P] Anotar en la *Deuda registrada* de [spec.md](./spec.md) lo que aparezca durante la implementación. D10-01 a D10-06 ya están; esto es para lo nuevo
- [ ] T051 [VERIFY] Puerta completa de las dos pilas más el build de producción del frontend (`pnpm --dir frontend build`), con su salida

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: sin dependencias
- **Foundational (T002–T003)**: depende de Setup. **Bloquea todo**: las cuatro historias montan componentes que reciben un `Resumen`
- **US1 (T004–T017)**: depende de Foundational
- **US2 (T018–T027)**: depende de **US1**. No es independiente y no se puede fingir que lo sea: el dashboard reutiliza los componentes de `resumen/` que US1 construye, y la barra de T026 se agrega al `GastosPorCategoria` de T015
- **US3 (T028–T036)**: depende de **US2** — el rango se elige en la pantalla que US2 crea
- **US4 (T037–T042)**: depende de **US2**. **No depende de US3**: el filtro de moneda recorta el resumen que llegó, sea cual sea el período con el que se pidió
- **Polish (T043–T051)**: depende de las cuatro historias

### Dentro de cada historia

- El `[TEST]` antes que su `[ROJO]`, y el `[ROJO]` antes de la implementación. Sin excepciones
- **T013 antes que T016**: la pantalla no puede pedir lo que el cliente no sabe pedir
- **T015 antes que T026**: la fila se construye primero y la barra se le agrega después. Ese orden es la decisión D-03 hecha visible en el trabajo — primero el dato legible, después su representación
- **T024 antes que T025**: no hay dónde montar el dashboard hasta que la vista exista
- **T034 y T035 después de T029 y T030**: el control y la guarda de la carrera se escriben contra sus tests, no antes
- **T043 y T044 en ese orden**: primero se nombra el AC, después se corre y se anota el número
- **T047 después de commitear**: `verificar-monedas.sh` exige los dos árboles limpios, o no puede distinguir lo que ensució ella de lo que ya estaba sucio

### Parallel Opportunities

- **T005, T006 y T007** son tres archivos de test distintos (o tres casos independientes del mismo): se pueden escribir a la vez
- **T008 a T011** son cuatro comportamientos independientes de `PantallaMovimientos.test.tsx`: se escriben a la vez, se corren juntos
- **T014 y T015** son dos archivos de componente distintos
- **T018 a T022** tocan tres archivos de test distintos
- **T028 a T032** tocan tres archivos distintos, uno de ellos de backend
- **T043, T045, T049 y T050** tocan cuatro archivos distintos
- **US4 no depende de US3**: con dos personas, arrancan a la vez apenas cierre US2

---

## Parallel Example: US1

```bash
# T008 a T011, en paralelo — cuatro comportamientos independientes de la misma pantalla:
Task: "FR-011: el resumen aparece antes que el formulario y que el listado"
Task: "US1-esc.2: registrar un movimiento vuelve a pedir el resumen"
Task: "FR-010: cargando, sin datos y no se pudo cargar son tres estados distintos"
Task: "FR-017: un 401 al pedir el resumen vuelve al acceso"
```

---

## Implementation Strategy

### MVP: sólo US1

1. T001 → Setup
2. T002–T003 → Foundational (el fixture compartido)
3. T004–T017 → US1
4. **PARAR Y VALIDAR**: abrir la pantalla principal y ver los totales del mes arriba; registrar un
   gasto y ver el total moverse; bajar el backend y comprobar que dice que no se pudo cargar en vez
   de mostrar ceros

Con eso sólo, la deuda **D9-06** queda saldada y el número que el servidor calcula bien desde
FEAT-001c por fin se ve. Es la historia más chica de las cuatro y la que más valor entrega sola: las
otras tres construyen el lugar donde uno *elige qué* mirar, pero ésta es la que hace que haya algo
que mirar.

### Orden sugerido con una sola persona

US1 → US2 → US3 → US4 → Polish.

Es el orden de prioridad y también el de dependencia, que acá coinciden: cada historia usa lo que la
anterior construyó. La única flexibilidad real es que **US4 puede adelantarse a US3** si se quiere
cerrar antes la deuda D9-02 — el filtro de moneda no necesita el rango de fechas.

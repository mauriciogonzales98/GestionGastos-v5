---

description: "Task list for 009-elegir-y-filtrar-moneda"
---

# Tasks: Elegir y filtrar la moneda de un movimiento

**Input**: Design documents from `/specs/009-elegir-y-filtrar-moneda/`

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

**1 · El primer rojo llega solo, y no hay que fabricarlo.** `ContratoMovimientosTests` arma el
cuerpo del `POST` y del `PUT` con los nombres que declara `frontend/src/api/tipos.ts`, y su `switch`
**lanza** ante un campo que no sabe ejercitar. Declarar `monedaId` en el contrato pone esos tests en
rojo antes de que exista una línea de implementación. Es la barrera empujando en la misma dirección
que el Principio I, y es por eso que **la declaración del contrato es la primera tarea de cada
historia que lo toca**, no una tarea de setup.

**2 · Por eso el contrato se declara dentro de la historia que lo usa, y no antes.** La constitución
prohíbe commitear con la puerta en rojo. Si el contrato se declarara en la fase Foundational, esa
fase quedaría en rojo hasta que aterrizara el backend de US1 — o sea, no se podría cerrar ni
commitear. Declararlo dentro de la historia hace que cada fase abra y cierre su propio rojo. **La
unidad de commit es la fase, no la tarea.**

**3 · Dos tareas tocan producción para producir un rojo, y las dos restauran lo que tocaron**
(T027 y T043). Al cerrar la feature, `git status` no puede mostrar nada raro ahí adentro.

**4 · Ningún test escribe un número fijo sobre el tamaño del catálogo** ([D-10](./research.md)).
Ni "hay dos monedas", ni "la segunda es USD", ni un `monedaId = 2` escrito a mano. Todo sale del
catálogo o del helper que agrega una. `verificar-monedas.sh` corre la suite **con una moneda de más
puesta**, y esta feature abre cuatro lugares nuevos donde a alguien le va a resultar natural
escribir "tienen que ser dos". Ya se rompió una vez.

**5 · El esquema no cambia y no hay migración.** Si en algún momento hace falta escribir una, algo
se entendió mal: ver [data-model.md](./data-model.md).

---

## Phase 1: Setup

**Purpose**: saber de qué verde se parte, para que cualquier rojo posterior sea atribuible.

- [ ] T001 Correr la puerta completa de las **dos** pilas sobre la rama recién sacada —`pnpm --dir frontend lint`, `pnpm --dir frontend exec tsc --noEmit`, `pnpm --dir frontend test`, `dotnet format backend/GestionGastos.slnx --verify-no-changes`, `dotnet build backend/GestionGastos.slnx -warnaserror`, `dotnet test backend/`— y anotar el conteo de tests de cada una. Es la línea de base: sin ella, un rojo del primer día se confunde con uno heredado. Las dos pilas y no sólo el backend, porque ésta es la primera feature desde la 007 que toca las dos

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: el helper que agrega una moneda al catálogo y la borra, disponible para toda la suite.

**⚠️ CRÍTICO**: `moneda` es una tabla que `LimpiarCuentasAsync` **no** toca, a propósito ([D-05 de
la feature 008](../008-monedas-como-dato/research.md)). La primera moneda que un test cree y no
borre se le queda al siguiente. La 008 ya resolvió eso con `ConLaMonedaAsync`, pero lo dejó privado
dentro de `MonedaComoDatoTests`; esta feature necesita el mismo helper desde tres archivos más.

- [ ] T002 Extraer `ConLaMonedaAsync` de `backend/GestionGastos.Api.Tests/Integracion/MonedaComoDatoTests.cs` a un `Integracion/CatalogoDeMonedas.cs` compartido, **sin cambiar su comportamiento**: el `try`/`finally` que borra la moneda pase lo que pase, y el orden de limpieza —los movimientos antes que la moneda— que la 008 tuvo que arreglar sobre la marcha. Dejar escrita en el archivo la regla [D-10](./research.md): nada de números fijos sobre el tamaño del catálogo
- [ ] T003 Correr `dotnet test backend/ --filter "FullyQualifiedName~MonedaComoDato"` y exigir que los seis casos de la 008 sigan **verdes** sin haber sido modificados en lo que hacen. Es una extracción, no un cambio: si algo se pone rojo, se movió más de lo que había que mover
- [ ] T004 [VERIFY] Puerta del backend completa, con su salida

---

## Phase 3: US1 — Registrar en la moneda que elijo (P1) 🎯 MVP

**Goal**: la moneda deja de ser una decisión del servidor. Se elige en el formulario, sale del
catálogo, y si no se toca queda la predeterminada.

**Independent Test**: registrar un gasto eligiendo una moneda distinta de la predeterminada y
comprobar que quedó en esa moneda; registrar otro sin tocar el campo y comprobar que quedó en la
predeterminada.

### El contrato, y el rojo que trae puesto

- [ ] T005 [TEST] [US1] Declarar en `frontend/src/api/tipos.ts` la interfaz `Moneda` —`id`, `codigo`, `nombre`, `simbolo`, `esPredeterminada`— y el campo `monedaId?: number | null` en `NuevoMovimiento`, con el comentario que explica qué significa su ausencia ([D-01](./research.md), [D-02](./research.md)). **`decimales` NO viaja**: no lo consume nadie hasta el ticket 6, y un campo que nadie usa es un dato que salió a la red sin que nadie lo decidiera. Ver [contracts/api.md](./contracts/api.md)
- [ ] T006 [ROJO] [US1] Correr `dotnet test backend/ --filter "FullyQualifiedName~Contrato"` y exigir **ROJO**: `Los_Campos_De_NuevoMovimiento_Son_Los_Que_La_Api_Acepta_De_Verdad` lanza con su propio mensaje —"el contrato declara el campo `monedaId` y este test no sabe con qué valor ejercitarlo"—. Mostrar la salida: es el rojo que la barrera produce sola, y el que justifica el punto 1 de arriba
- [ ] T007 [US1] Enseñarle a ese test a ejercitar `monedaId`, con un identificador **tomado del catálogo** y no escrito a mano (D-10), y agregar la aserción de que el `monedaCodigo` devuelto es el de esa moneda. **Sigue ROJO**, y ahora por el motivo correcto: la API todavía ignora el campo y devuelve la predeterminada. Mostrar la salida — la diferencia entre los dos rojos es lo que prueba que el test mira el valor y no sólo el `201`
- [ ] T008 [TEST] [US1] Crear `backend/GestionGastos.Api.Tests/Contrato/ContratoMonedasTests.cs`: los campos de `Moneda` comparados **en las dos direcciones** contra el JSON de `GET /api/monedas`, con el mismo mecanismo que los otros tres archivos de `Contrato/`

### Los tests de comportamiento

- [ ] T009 [TEST] [P] [US1] AC-01 y FR-001 en un `Integracion/MonedaElegidaTests.cs` nuevo: con una moneda agregada por el helper de T002, un `POST /api/movimientos` con su `monedaId` deja el movimiento **en esa moneda**, y el resumen lo suma en su entrada y en la de ninguna otra
- [ ] T010 [TEST] [P] [US1] AC-02, FR-002 y `PRD:NFR-01`: un `POST` **sin** `monedaId` deja el movimiento en la predeterminada del catálogo. Comentar que este caso **pasa desde el primer intento y es a propósito**: es el comportamiento de hoy, y lo que verifica es que no se rompió — es la compatibilidad hacia atrás del contrato, y el único caso de la feature cuyo verde inicial es la respuesta correcta
- [ ] T011 [TEST] [P] [US1] AC-11 y FR-003 —**la deuda D8-01 de la feature 008 saldándose**—: un `POST` con un `monedaId` que no está en el catálogo devuelve `400` con la clave `monedaId` en `errors`, y `GET /api/movimientos` demuestra que **no se creó nada**. Citar D8-01 en el comentario: es la primera vez que hay una entrada de moneda que validar, que es exactamente la razón por la que esa deuda esperaba a este ticket
- [ ] T012 [TEST] [P] [US1] AC-03, AC-04, FR-004, FR-005 y FR-006 en `MonedaElegidaTests`: `GET /api/monedas` devuelve **una entrada por fila del catálogo** —comparado contra la tabla, no contra un número— con exactamente una `esPredeterminada`, y **una moneda agregada sólo como dato aparece en la respuesta**. Ese último es el que no puede pasar con una lista escrita en el código
- [ ] T013 [ROJO] [US1] Correr T008 a T012 y exigir **ROJO** en cada uno, verificando que el motivo es el esperado y no un error de compilación: T008 y T012 por `404` —el endpoint no existe—, T009 y T011 porque la API ignora el campo. Mostrar la salida

### La implementación del backend

- [ ] T014 [US1] `backend/GestionGastos.Api/Monedas/MonedaDto.cs` y `Monedas/MonedasEndpoints.cs` con `GET /api/monedas`, ordenado por identificador, **exigiendo sesión**; registrarlo con `app.MapMonedas()` en `Program.cs`, junto a los otros cinco. La lectura va directa contra `contexto.Monedas`, **sin canal**: la moneda no tiene dueño y un canal que no acota nada sugeriría que hay algo que aislar ([D-03](./research.md)). Verde de T008 y T012
- [ ] T015 [US1] `monedaId` en `NuevoMovimientoDto`; la búsqueda de la moneda en el alta; y la regla en `ValidacionDelMovimiento`, **en la función compartida por el alta y la edición** y con la clave de error `monedaId` ([D-04](./research.md)). Ausente o `null` sigue cayendo en `SingleAsync(m => m.EsPredeterminada)`, que no se toca. Verde de T007, T009, T010 y T011
- [ ] T016 [VERIFY] [US1] Puerta del backend completa **más `./backend/verificar-autorizacion.sh`**, con su salida. La barrera de autorización va acá y no al final: `GET /api/monedas` es un endpoint nuevo, y si naciera abierto es ahora cuando hay que enterarse

### El frontend

- [ ] T017 [TEST] [P] [US1] En `frontend/tests/FormularioMovimiento.test.tsx`: el selector ofrece **exactamente las monedas que bajan por props**, con la predeterminada ya elegida, y **una moneda que ninguna constante del código conoce aparece igual**. Ese último caso es `PRD:AC-04` del lado de la pantalla, y es el que se pone en rojo el día que alguien escriba `['ARS', 'USD']` en el frontend ([D-11](./research.md))
- [ ] T018 [TEST] [P] [US1] En `frontend/tests/PantallaMovimientos.test.tsx`: guardar **sin tocar el selector** manda la predeterminada y no agrega ninguna interacción respecto de antes (SC-002, `PRD:NFR-01`). Es el criterio que más fácil se rompe sin que nadie lo note, porque romperlo no produce ningún error
- [ ] T019 [TEST] [P] [US1] En `frontend/tests/CargaInicial.test.tsx`: cargar la pantalla principal pide el catálogo de monedas **una sola vez** (AC-12, FR-013). Mismo criterio y mismo archivo que el de categorías de la feature 007
- [ ] T020 [ROJO] [US1] Correr `pnpm --dir frontend test` y exigir **ROJO** en T017, T018 y T019. Mostrar la salida
- [ ] T021 [US1] `obtenerMonedas()` en `frontend/src/api/cliente.ts`; `App.tsx` pide el catálogo **una vez** y lo baja por props, junto al de categorías y con el mismo manejo de error ([D-06](./research.md)); el selector en `FormularioMovimiento.tsx`, alimentado por esas props; `monedaId` en el cuerpo que arma; y `monedaId` agregado a `CAMPOS_CON_LUGAR`, o el error del servidor cae en la región general en vez de al lado del selector (D-04). Verde de T017, T018 y T019
- [ ] T022 [VERIFY] [US1] Puerta de las **dos** pilas más `./backend/verificar-contrato.sh`, con su salida. El contrato cambió en dos lugares y ésta es la primera vez que se comprueba entero (~2,5 min)

**Checkpoint**: el ticket ya entrega lo suyo. Se puede registrar en cualquier moneda del catálogo,
una moneda agregada como dato aparece en el selector sin tocar código, y quien usa una sola no
cambió nada de lo que hacía.

---

## Phase 4: US2 — No dudar en qué moneda está cada monto (P2)

**Goal**: cada fila del listado dice el código de su moneda, sin depender de qué símbolo elija
`Intl`.

**Independent Test**: dos movimientos del mismo monto en dos monedas distintas; cada fila muestra un
código distinto.

- [ ] T023 [TEST] [P] [US2] En `frontend/tests/ListadoMovimientos.test.tsx`: AC-05 y FR-007 — un gasto de 100 en pesos y otro de 100 en dólares muestran **códigos distintos** en sus filas, además del monto formateado. Y un tercer caso con un código que ninguna constante conoce, que tiene que mostrarse igual: el código sale del dato del movimiento, no de una tabla de equivalencias
- [ ] T024 [ROJO] [US2] Correr y exigir **ROJO**. Mostrar la salida. El rojo importa acá más que en otras tareas: el listado **ya distingue** las dos monedas por el símbolo que produce `Intl`, así que un test mal escrito —uno que busque "US$" en vez del código— pasaría en verde sin que exista la columna
- [ ] T025 [US2] La columna del código en `frontend/src/movimientos/ListadoMovimientos.tsx`, con su `<th scope="col">`. Verde
- [ ] T026 [VERIFY] [US2] Puerta del frontend completa, con su salida

---

## Phase 5: US3 — Ver sólo los movimientos de una moneda (P3)

**Goal**: el listado se puede acotar a una moneda, y eso se combina con los acotados que ya existen.

**Independent Test**: movimientos en dos monedas; acotado a una no muestra ninguno de la otra; sin
acotar los muestra todos.

- [ ] T027 [TEST] [P] [US3] En `backend/GestionGastos.Api.Tests/Integracion/FiltrosDelListadoTests.cs`, cuatro casos: AC-06 (`?monedaId=` acota), AC-07 (sin el parámetro vienen todas), AC-08 y FR-009 (los **tres** acotados a la vez devuelven la intersección), y FR-015 (una moneda inexistente devuelve `[]` **sin error**, mismo criterio que la categoría inexistente). Van en el archivo que ya existe y no en uno nuevo: es el tercer acotado de la misma consulta, no una consulta distinta
- [ ] T028 [TEST] [US3] En `Integracion/ResumenDelPeriodoTests.cs`: con movimientos en dos monedas, el resumen sigue devolviendo **las dos** entradas. Es el guardarraíl de [D-05](./research.md): el resumen **no** hereda el acotado del listado, y eso hay que dejarlo verificado antes de escribir el parámetro que podría hacérselo heredar
- [ ] T029 [ROJO] [US3] Correr T027 y T028 y exigir **ROJO** en los de T027 (el parámetro no existe, así que el acotado no acota) y **VERDE** en T028 (el resumen todavía no puede haberse roto). Mostrar la salida
- [ ] T030 [US3] El `monedaId` opcional en `MovimientosConsulta.Filtrado`, con la condición escrita **dentro de `DeLaCuenta`** junto a la de categoría, y el parámetro en `GET /api/movimientos`. **`Agrupado` pasa `monedaId: null` explícito y comentado** ([D-05](./research.md)). Verde de T027
- [ ] T031 [ROJO] [US3] El desarme de D-05, que es la parte de esta historia que más fácil se saltea: hacer temporalmente que `Agrupado` reciba y aplique un `monedaId`, y exigir que **T028 se ponga ROJO**. Restaurar el `null` y exigir el verde. Sin esto, el `null` explícito es un comentario que nadie comprobó — y el daño que evita es el mismo que `verificar-desglose.sh` vigila para `categoria.activa`: los totales de un período ya cerrado cambiando sin que nadie toque un movimiento. Mostrar las dos salidas
- [ ] T032 [TEST] [P] [US3] En `frontend/tests/PantallaMovimientos.test.tsx`: el control de acotado ofrece **las monedas del catálogo más la opción de no acotar** (FR-010), sale de la **misma** lectura que alimenta el selector del formulario (AC-12), y elegir una vuelve a pedir el listado con ese acotado. **Sólo el control de moneda**: los de categoría y fecha no existen y no se construyen acá (D9-01)
- [ ] T033 [ROJO] [US3] Correr y exigir **ROJO**. Mostrar la salida
- [ ] T034 [US3] El parámetro de acotado en `obtenerMovimientos()` de `frontend/src/api/cliente.ts` y el control en `frontend/src/movimientos/PantallaMovimientos.tsx`, alimentado por las props que ya bajan de la raíz. Verde
- [ ] T035 [VERIFY] [US3] Puerta de las dos pilas **más `./backend/verificar-desglose.sh`**, con su salida. Esa barrera va acá porque ésta es la historia que toca la consulta que el resumen comparte

---

## Phase 6: US4 — Corregir la moneda sin borrar el movimiento (P4)

**Goal**: la moneda de un movimiento propio se corrige en una ventana emergente, y los totales lo
reflejan.

**Independent Test**: un movimiento en pesos; se le cambia la moneda a dólares; su monto deja de
sumar en los totales en pesos y pasa a sumar en los de dólares, y su monto, su categoría y su fecha
quedan intactos.

- [ ] T036 [TEST] [US4] Declarar `monedaId?: number | null` en `MovimientoEditado` en `frontend/src/api/tipos.ts`, con el comentario que explica que **acá ausente significa "la que ya tenía"**, y por qué eso no contradice que `fecha` sea obligatoria: la regla común es que ausente nunca produce un cambio que nadie pidió ([D-02](./research.md))
- [ ] T037 [ROJO] [US4] Correr los tests del contrato y exigir **ROJO** en `Los_Campos_De_MovimientoEditado_Son_Los_Que_La_Api_Acepta_De_Verdad`, por la misma vía que en T006. Enseñarle el caso `monedaId` —con el valor tomado del catálogo— y exigir que **siga rojo** porque el `PUT` ignora el campo. Mostrar las dos salidas
- [ ] T038 [TEST] [P] [US4] AC-10 y FR-012 en `MonedaElegidaTests`: un `PUT` que cambia **sólo** la moneda deja el monto, la categoría y la fecha sin alterar
- [ ] T039 [TEST] [P] [US4] FR-011 y D-02 en `Integracion/EdicionDeMovimientoTests.cs`: un `PUT` **sin** `monedaId` conserva la moneda que el movimiento ya tenía —y no la predeterminada, que es el error natural de copiar el alta—; uno con un `monedaId` fuera del catálogo devuelve `400` con la clave `monedaId` y **el movimiento queda como estaba**; y uno sobre un movimiento de otra cuenta sigue devolviendo `404`, sin distinguir "no existe" de "no es tuyo"
- [ ] T040 [TEST] [US4] AC-09 y FR-012b: cambiada la moneda de un movimiento, `GET /api/resumen` muestra que su monto **dejó de sumar en la moneda anterior** y **pasó a sumar en la nueva**. **Las dos direcciones en el mismo test**: un caso que sólo mire el destino pasa en verde con una implementación que sume en las dos monedas a la vez, que es el defecto más probable de todos los de esta historia
- [ ] T041 [ROJO] [US4] Correr T038, T039 y T040 y exigir **ROJO**, verificando el motivo de cada uno. Mostrar la salida
- [ ] T042 [US4] `monedaId` en `MovimientoEditadoDto`, la búsqueda de la moneda en el `PUT` y su paso por la **misma** `ValidacionDelMovimiento` que el alta (D-04). Ausente conserva `movimiento.MonedaId`. Verde de T037 a T040
- [ ] T043 [VERIFY] [US4] Puerta del backend completa más `./backend/verificar-contrato.sh`, con su salida
- [ ] T044 [TEST] [US4] En un `frontend/tests/VentanaDeEdicion.test.tsx` nuevo: la ventana se abre con el monto, la categoría, la fecha y la moneda del movimiento **ya cargados**; `Escape` la cierra; guardar llama a la edición y la fila del listado queda actualizada. El entorno de tests es happy-dom y **soporta `<dialog>.showModal()`** — se verificó antes de elegirlo ([D-07](./research.md))
- [ ] T045 [ROJO] [US4] Correr y exigir **ROJO**. Mostrar la salida
- [ ] T046 [US4] Extraer los campos y las reglas de validación de `FormularioMovimiento.tsx` a un `CamposDelMovimiento.tsx` compartido, parametrizado por los valores iniciales, la etiqueta del botón y si la fecha es obligatoria ([D-08](./research.md)). **Los tests del alta tienen que seguir verdes sin ser modificados**: es una extracción, y si hay que tocarlos es que cambió el comportamiento
- [ ] T047 [US4] `VentanaDeEdicion.tsx` con `<dialog>` nativo y `showModal()`, `editarMovimiento()` en `cliente.ts`, y el control que la abre desde cada fila del listado. Verde de T044
- [ ] T048 [VERIFY] [US4] Puerta de las dos pilas, con su salida

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T049 [TEST] Agregar a `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoAltaTests.cs` un caso que mida el p95 del guardado **mandando la moneda explícita**, sobre las mismas 100 ejecuciones y con el mismo criterio de < 1 s (SC-008, `PRD:NFR-03`). **El caso existente se deja intacto**: es la referencia que permite atribuir un rojo al `SELECT` de la moneda y no a la máquina ([D-09](./research.md)). Correr los dos y **anotar los dos números**
- [ ] T050 Extender `backend/verificar-monedas.sh` para que su comprobación de árbol limpio cubra también **`frontend/src/`**, no sólo `backend/GestionGastos.Api/` ([D-11](./research.md)). Hasta hoy la promesa de "sumar una moneda cuesta 0 líneas" sólo estaba protegida de un lado, y desde esta feature el frontend también puede romperla
- [ ] T051 [ROJO] Ver fallar esa extensión por su propia vía: dejar sucio un archivo de `frontend/src/` —alcanza un comentario— y exigir que el script **lo detecte y salga en rojo**. Restaurar y exigir el verde. Es el Principio V: una barrera que creció y no se vio fallar en su parte nueva no protege esa parte. Mostrar las dos salidas
- [ ] T052 [P] Actualizar la fila de la *Barrera de monedas* en la tabla de *Stack* de `AGENTS.md`: ahora exige limpio el árbol de las dos pilas, y por qué. `ci.yml` **no** cambia — no hay barrera nueva, la sexta creció
- [ ] T053 Recorrer el [quickstart](./quickstart.md) entero —los ocho pasos más la medición— y anotar cualquier línea que no haya salido como dice. En la 008 fue el quickstart, y no la suite, el que encontró el número escrito a mano
- [ ] T054 [VERIFY] Las **seis** barreras, con su salida: `verificar-contrato.sh`, `verificar-autorizacion.sh`, `verificar-desglose.sh`, `verificar-monedas.sh`, `verificar-aislamiento.sh` (~7 min) y `verificar-linter.sh`
- [ ] T055 Cobertura del backend con `dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings`, con su salida
- [ ] T056 [P] Actualizar `plan-de-implementacion/README.md`: el ticket 13 (4b) pasa a la tabla de implementados, con lo que esta feature construyó y lo que dejó anotado como deuda
- [ ] T057 [P] Anotar en la *Deuda registrada* de [spec.md](./spec.md) lo que aparezca durante la implementación. D9-01 a D9-06 ya están; esto es para lo nuevo
- [ ] T058 [VERIFY] Puerta completa de las dos pilas más el build de producción del frontend (`pnpm --dir frontend build`), con su salida

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: sin dependencias
- **Foundational (T002–T004)**: depende de Setup. **Bloquea todo**: sin el helper compartido, cada archivo de tests nuevo escribe su propia limpieza y la primera que se olvide envenena la suite
- **US1 (T005–T022)**: depende de Foundational
- **US2 (T023–T026)**: depende de Foundational. **No depende de US1** — la columna del código se alimenta de `monedaCodigo`, que viaja desde FEAT-001a
- **US3 (T027–T035)**: depende de **US1**, no sólo de Foundational: el control de acotado se alimenta del catálogo que T021 pide en la raíz
- **US4 (T036–T048)**: depende de **US1** — reusa la validación de T015 y los campos del formulario de T021
- **Polish (T049–T058)**: depende de las cuatro historias

### Dentro de cada historia

- El `[TEST]` antes que su `[ROJO]`, y el `[ROJO]` antes de la implementación. Sin excepciones
- **T007 después de T006**: hay que ver el rojo que la barrera produce sola antes de enseñarle el campo, o se pierde la señal de que la barrera funcionaba
- **T014 antes que T021**: el frontend no puede pedir un catálogo que no existe
- **T028 antes que T030, y T031 después de T030**: el guardarraíl del resumen se escribe antes del parámetro que podría romperlo, y se desarma después de que el parámetro exista
- **T046 antes que T047**: la ventana monta los campos compartidos, así que los campos se extraen primero
- **T050 antes que T051, y los dos antes de T052**: no se documenta una barrera que no se vio fallar

### Parallel Opportunities

- T009, T010, T011 y T012 son cuatro casos independientes: se pueden escribir a la vez, no correr a la vez
- T017, T018 y T019 tocan tres archivos de test distintos
- T038 y T039 son dos archivos distintos, `MonedaElegidaTests` y `EdicionDeMovimientoTests`
- T052, T056 y T057 tocan tres archivos distintos
- **US2 es independiente de US1**: con equipo, arranca apenas cierre la fase Foundational

---

## Parallel Example: US1

```bash
# T009 a T012, en paralelo — cuatro casos independientes del mismo comportamiento:
Task: "AC-01/FR-001: el alta con monedaId deja el movimiento en esa moneda"
Task: "AC-02/FR-002: el alta sin monedaId cae en la predeterminada"
Task: "AC-11/FR-003: un monedaId fuera del catálogo se rechaza y no crea nada"
Task: "AC-03/AC-04/FR-004: GET /api/monedas devuelve el catálogo, y una moneda agregada aparece"
```

---

## Implementation Strategy

### MVP: sólo US1

1. T001 → Setup
2. T002–T004 → Foundational (**crítico**: el helper y su limpieza)
3. T005–T022 → US1
4. **PARAR Y VALIDAR**: registrar en dos monedas desde la pantalla; agregar una moneda al catálogo
   con SQL puro y verla aparecer en el selector sin tocar código

Con eso sólo, el ticket cumple su promesa central —`PRD:FR-01`, `FR-02` y `FR-03`— y salda la deuda
**D8-01** que la feature 008 dejó esperando este momento. US2, US3 y US4 son las otras tres
aberturas del PRD y suman encima, pero ninguna de ellas es lo que hace que el ticket exista.

### Orden sugerido con una sola persona

US1 → US2 (barata y aislada, cuatro tareas) → US3 → US4 → Polish.

US4 va última no por prioridad de producto sino por tamaño: es la única historia que estrena una
interfaz entera, y hacerla antes deja las tres restantes esperando detrás de la más larga.

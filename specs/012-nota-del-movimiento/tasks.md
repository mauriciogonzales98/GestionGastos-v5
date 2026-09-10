---

description: "Task list for 012-nota-del-movimiento"
---

# Tasks: Nota descriptiva del movimiento

**Input**: Design documents from `/specs/012-nota-del-movimiento/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md)

**Tests**: obligatorios. No es una opción de esta feature: el Principio I de
`.specify/memory/constitution.md` prohíbe escribir código de producción sin un test que ya haya
fallado.

**Organization**: por historia de usuario, en orden de prioridad.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[TEST]**: escribe una verificación · **[ROJO]**: la corre y exige que falle · **[VERIFY]**: puerta
- **[Story]**: US1, US2, US3

---

## Lo que conviene leer antes de empezar

**1 · El rojo de esta feature no hay que fabricarlo en ninguna parte.** No existe la columna, ni la
propiedad del dominio, ni el campo en ninguno de los tres DTO, ni el control, ni la columna del
listado. Es la primera feature en cinco que arranca de cero en las dos pilas, así que ningún primer
test puede pasar por accidente.

**2 · El rojo más barato está en el contrato, y viene explicándose solo.** Agregar `nota` a
`frontend/src/api/tipos.ts` pone en rojo los tests de `Contrato/` **antes** de que exista el campo en
el DTO. El caso del alta arma el cuerpo de la petición a partir de los nombres **del contrato** con un
`switch` por campo, y ese `switch` **lanza una excepción con instrucciones** cuando encuentra un campo
que no sabe ejercitar: *"un campo del contrato sin ejercitar es un campo sin barrera"*. Es el mismo
rojo que la 011 usó para `decimales`.

**3 · Hay UN rojo que se puede fingir sin darse cuenta, y es el de `FR-011`.** Tiene que producirse
contra una fila guardada **sin valor**, no contra una fila sin nota cualquiera: si la fila de prueba se
guardó con la cadena vacía, no hay nada que normalizar y el test pasa en verde desde el principio — y
entonces la barrera de `FR-011` existiría sin haber verificado nunca lo suyo. **El test escribe las dos
representaciones explícitamente.** Es lo que la reevaluación del Constitution Check dejó marcado.

**4 · Dos tests van a nacer en verde, y eso hay que decirlo en la salida en vez de disimularlo.** El de
`NFR-002`/`AC-07` —los totales no se mueven— y el de `AC-13` —la semilla sobrevive a la migración—
pasan desde el primer momento, porque la nota no entra en la consulta que agrupa y porque `ARS` y `USD`
ya son tres letras. Son tests de regresión, no de construcción: valen por el día que alguien toque la
consulta del resumen, no por hoy.

**5 · Una migración, dos cambios, y por eso los dos reds del esquema van juntos**
([D-10](./research.md)). La migración lleva la columna de la nota **y** la restricción de tres letras
sobre `moneda.codigo`, que es la deuda D11-02 esperando desde la feature 009 a un ticket que abriera
una migración. Como es un solo archivo, los tests que la manejan tienen que estar escritos antes de
generarla: por eso el rojo de `FR-010` está en Phase 2 y no en la fase de US3. **US3 queda casi entera
en Phase 2 a propósito**, y lo que le queda es la única verificación que no se puede hacer antes.

**6 · `FR-010` podía romper `verificar-monedas.sh` y no lo hace. Ya está verificado.** Esa barrera
siembra con **`XTS`** —que ISO 4217 reserva para pruebas— y los tests usan `XCA`, `XCE`, `XCT`, `XED`,
`XEL`, `XMV`, `XPF`, `XSC` y `EUR`. Los doce códigos que el proyecto escribe son tres letras. Era el
único acoplamiento real de US3 y está cerrado antes de empezar; T034 lo comprueba corriendo la barrera.

**7 · La barrera nueva es la primera desde la 007, y su afirmación es más fina que la del desglose**
([D-09](./research.md)). `verificar-desglose.sh` busca una palabra que no debe aparecer en ningún lado;
ésta tiene que exigir que la nota **aparezca en la proyección** de la consulta del listado y **nunca en
su filtro**. Una verificación más fina es exactamente la que hay que **ver fallar** antes de creerle.

**8 · La tabla de [D-12](./research.md) es el presupuesto, y es cerrada.** Siete tests existentes se
pueden tocar, cada uno con el requisito que lo justifica. **Un test fuera de esa tabla que haya que
retocar es la señal de que la nota se comió algo que no le corresponde.** Y el caso inverso está
escrito también: si un test de `Resumenes/`, de aislamiento o de categorías se pone en rojo, **el que
está mal es el código de esta feature** — un rojo ahí es información, no un test para arreglar.

**9 · La tentación de esta feature es agregar de más, no de menos.** La barra de filtros existe desde
la 011 y sumarle un campo para buscar por nota costaría muy poco. Es exactamente lo que `FR-007`
prohíbe y lo que la barrera de Phase 6 existe para impedir.

---

## Phase 1: Setup

**Purpose**: saber de qué verde se parte, para que cualquier rojo posterior sea atribuible.

- [X] T001 Correr la puerta completa de las dos pilas sobre la rama recién sacada —`pnpm --dir frontend lint`, `pnpm --dir frontend format`, `pnpm --dir frontend exec tsc --noEmit`, `pnpm --dir frontend test`, `dotnet format backend/GestionGastos.slnx --verify-no-changes`, `dotnet build backend/GestionGastos.slnx -warnaserror`, `dotnet test backend/`— y anotar el conteo de tests de cada una en la salida. Es la línea de base: sin ella, un rojo del primer día se confunde con uno heredado. Anotar además **las tres cosas que la verificación contra el código encontró y que son el punto de partida**: que `nota` no aparece en `backend/GestionGastos.Api/` ni en `frontend/src/`, que `backend/GestionGastos.Api/Migrations/` tiene 5 migraciones y la última es de la feature 007, y que `backend/GestionGastos.Api.Tests/Rendimiento/` tiene tres tests y **ninguno mide el listado**

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: el esquema. Sin la columna no hay nada que guardar, así que **ninguna historia puede
empezar** hasta que esta fase esté completa.

**⚠️ Los dos reds del esquema van juntos porque la migración es una sola** ([D-10](./research.md)). El
de `FR-010` pertenece conceptualmente a US3 y está acá por esa razón, no por prioridad.

- [X] T002 [TEST] Agregar `nota` a las tres formas del movimiento en `frontend/src/api/tipos.ts` —opcional en `NuevoMovimiento`, **obligatoria** en `MovimientoEditado`, siempre presente y no nula en `Movimiento`— con el comentario que explica la asimetría por el mismo motivo que `fecha` ya la tiene ([contracts/api.md](./contracts/api.md), [D-06](./research.md)). Es la mitad del contrato que va primero a propósito: pone en rojo `backend/GestionGastos.Api.Tests/Contrato/ContratoMovimientosTests.cs` antes de que exista el campo del otro lado
- [X] T003 [TEST] En `backend/GestionGastos.Api.Tests/Integracion/`: el esquema **rechaza** un código de moneda que no sean tres letras —`1X2`, `ab1`, `A1`— escrito con SQL crudo contra la base migrada (`FR-010`, `AC-13` del ticket). Hoy entra sin protesta: ése es el rojo
- [X] T004 [TEST] En el mismo archivo: el esquema **acepta** `EUR` y **la semilla existente sobrevive a la migración** (`SC-010`). **Este test nace en verde** —`ARS` y `USD` ya son tres letras— y hay que decirlo en la salida: es un test de regresión que protege el día que alguien agregue una moneda al catálogo, no una verificación de algo que se construye hoy
- [X] T005 [ROJO] Correr `dotnet test backend/` y exigir **ROJO** en T002 (tres casos de `Contrato/`, con el mensaje del `switch` a la vista) y en T003, verificando en cada caso que el motivo es la ausencia real y no un error de compilación. T004 tiene que dar **verde** ya. Mostrar la salida
- [X] T006 La migración única en `backend/GestionGastos.Api/Migrations/`: `movimiento.nota` como `varchar(120)` anulable sin valor por omisión y **sin índice**, más la restricción de tres letras sobre `moneda.codigo` ([data-model.md](./data-model.md)). Con ella, la propiedad `Nota` en `backend/GestionGastos.Api/Dominio/Movimiento.cs` y el mapeo de las dos cosas en `backend/GestionGastos.Api/Persistencia/GestionGastosDbContext.cs`. Dejar escrito en el código **por qué no hay índice** —un índice sobre la nota sólo sirve para buscar por ella, que es lo que `FR-007` prohíbe— y **por qué la columna mide exactamente 120** y no más. Verde de T003
- [X] T007 [VERIFY] Puerta del backend (`dotnet format --verify-no-changes` + `dotnet build -warnaserror` + `dotnet test backend/`), con su salida. T002 sigue en rojo y eso es correcto: el contrato se cierra en US1

---

## Phase 3: User Story 1 — Anotar en qué gasté (P1) 🎯 MVP

**Goal**: se puede escribir una nota al registrar un movimiento y leerla en el listado. Y se puede
seguir registrando sin tocarla.

**Independent Test**: registrar dos movimientos, uno con nota y uno sin, y mirar el listado: aparece el
texto del primero y el segundo se ve sin relleno y sin error.

- [X] T008 [TEST] [P] [US1] En `backend/GestionGastos.Api.Tests/Movimientos/`: el alta guarda la nota y el listado la devuelve (`FR-002`, `FR-006`, `PRD:AC-01`)
- [X] T009 [TEST] [P] [US1] En el mismo lugar: el alta **sin** el campo, con `null`, con la cadena vacía y con **sólo espacios** deja el movimiento sin nota, sin ningún error (`FR-005`, `PRD:AC-02`, [D-03](./research.md))
- [X] T010 [TEST] [P] [US1] En el mismo lugar: una nota de **120 caracteres se acepta** y una de **121 se rechaza** con la clave `nota` y sin crear nada (`FR-003`, `PRD:AC-03`, `PRD:AC-04`). Y los dos casos que fijan **cómo** se cuenta: **120 emoji se aceptan** —el límite se mide en caracteres Unicode y no en unidades UTF-16 ([D-02](./research.md), `FR-013`)— y **120 caracteres con un espacio a cada lado se aceptan**, porque se recorta antes de medir ([D-03](./research.md))
- [X] T011 [TEST] [P] [US1] En el mismo lugar: **`FR-011`** — las **cuatro** rutas que devuelven un movimiento (el alta, el listado, la consulta individual y la edición) devuelven la cadena vacía para una fila guardada **sin valor**. El test **escribe las dos representaciones de "sin nota" explícitamente** —una fila sin valor y una con la cadena vacía— y exige que las cuatro rutas no las distingan (`SC-011`). Es el rojo que se puede fingir sin darse cuenta: con una fila cualquiera sin nota, este test pasa en verde desde el principio y no verifica nada (punto 3)
- [X] T012 [TEST] [P] [US1] En el mismo lugar: el mensaje de error del largo **no repite la nota**. Es la única entrada de texto libre de la aplicación, y devolver el valor lo haría viajar de vuelta y aparecer donde termine el mensaje
- [X] T013 [TEST] [P] [US1] En `frontend/tests/cliente.test.ts`: `crearMovimiento` manda `nota` (fila 4 de [D-12](./research.md))
- [X] T014 [TEST] [P] [US1] Crear `frontend/tests/NotaDelMovimiento.test.tsx`: el campo existe con nombre accesible, **se puede guardar sin haberlo tocado** (`FR-001`, `PRD:AC-09`), y el error del largo aparece **al lado del campo** vía `CampoConError` y no como un cartel suelto (`FR-008`)
- [X] T015 [TEST] [P] [US1] En `frontend/tests/NotaDelMovimiento.test.tsx`: **`AC-08`** — una nota que contiene `<b>hola</b>` y `-- DROP` se muestra en el listado con **exactamente esos caracteres**, sin interpretar nada como marcado (`NFR-001`). El escape por omisión ya lo hace; este test existe para que **desactivarlo rompa en rojo** ([D-08](./research.md))
- [X] T016 [TEST] [P] [US1] En `frontend/tests/ListadoMovimientos.test.tsx`: la columna nueva muestra la nota junto a su movimiento, y un movimiento sin nota se ve **sin texto de relleno** (`FR-006`, fila 3 de [D-12](./research.md))
- [X] T017 [ROJO] [US1] Correr `dotnet test backend/` y `pnpm --dir frontend test` y exigir **ROJO** en T008 a T016, verificando el motivo de cada uno. Mostrar la salida
- [X] T018 [US1] `Nota` en los tres DTO de `backend/GestionGastos.Api/Movimientos/MovimientoDtos.cs`, **con la normalización de lectura en `MovimientoDto`** ([D-04](./research.md)): la ausencia de valor sale como la cadena vacía, y los cuatro lugares de `MovimientosEndpoints.cs` que lo construyen la heredan sin escribirla. Dejar escrito en el código que la normalización está **ahí y no en los cuatro lugares** porque es lo que hace que el quinto lugar herede la regla, y que EF materializa la proyección llamando al constructor en memoria — que es por qué funciona también en las dos rutas que son `IQueryable`. Con eso, el alta pasa la nota al dominio. Verde de T008, T009, T011 y **del contrato (T002)**
- [X] T019 [US1] La regla del largo en `backend/GestionGastos.Api/Movimientos/ValidacionDelMovimiento.cs`, con la clave de error `nota`, **contando caracteres Unicode** y **recortando antes de medir** ([D-02](./research.md), [D-03](./research.md)). Es el sexto campo que pasa por esa validación y no necesita ninguna estructura nueva: la misma regla sirve al alta y a la edición, que es lo que ese archivo ya existe para garantizar. Verde de T010 y T012
- [X] T020 [US1] `crearMovimiento` manda la nota en `frontend/src/api/cliente.ts`. Verde de T013
- [X] T021 [US1] El campo en `frontend/src/movimientos/CamposDelMovimiento.tsx`: un control de **varias líneas**, **último** del formulario —después de la fecha—, envuelto por `CampoConError` como los otros cinco ([D-07](./research.md)). Agregar `nota` a `CAMPOS_CON_LUGAR`, sin lo cual el error del servidor llega y cae en la región general en vez de al lado del campo ([D-05](./research.md)). Y **corregir el comentario que afirma que el envío con Enter sale de cualquier campo**: dentro de un control de varias líneas Enter inserta un salto, así que deja de ser cierto. Verde de T014
- [X] T022 [US1] La columna en `frontend/src/movimientos/ListadoMovimientos.tsx`, entre la moneda y las acciones, con su regla de ancho en `frontend/src/estilos/componentes.css` ([D-08](./research.md)). El texto completo va en el DOM; el ancho lo absorbe el envoltorio desplazable que la 011 ya dejó puesto. Verde de T015 y T016
- [X] T023 [US1] Extender `frontend/tests/TecladoFormulario.test.tsx` con el control nuevo en el orden de tabulación (fila 2 de [D-12](./research.md)). **Se puso en rojo por diseño**: ese test enumera el orden control por control y su propio comentario dice que haberse puesto en rojo al agregar un botón es la señal de que sirve. `PRD:AC-55` no cambia de exigencia — el formulario se sigue recorriendo entero con Tab y enviándose con Enter sobre el botón
- [X] T024 [VERIFY] [US1] Puerta de **las dos pilas** completa, con su salida. La US1 toca esquema, contrato y pantalla, así que no hay acá el atajo de puerta sólo-frontend que tuvo la 011

---

## Phase 4: User Story 2 — Corregir o borrar lo que anoté (P2)

**Goal**: la nota de un movimiento propio se puede cambiar y se puede vaciar, y el listado muestra lo
nuevo sin rastro de lo anterior.

**Independent Test**: sobre un movimiento que ya tiene nota, editarla y después vaciarla, verificando el
listado después de cada guardado.

- [X] T025 [TEST] [P] [US2] En `backend/GestionGastos.Api.Tests/Movimientos/`: la edición cambia la nota (`PRD:AC-05`) y la **vacía** tanto con la cadena vacía como con `null` (`FR-004`, `PRD:AC-06`)
- [X] T026 [TEST] [P] [US2] En el mismo lugar: la edición **exige** la nota —omitirla se rechaza, igual que omitir la fecha ([D-06](./research.md))— y una nota de 121 caracteres en la edición **no altera nada del movimiento**: ni la nota, ni el monto, ni la categoría, ni la fecha (`FR-003`, `PRD:AC-03`)
- [X] T027 [TEST] [P] [US2] En `backend/GestionGastos.Api.Tests/Movimientos/`: agregar, cambiar y borrar la nota de un movimiento deja el total ingresado, el total gastado, el balance y el desglose por categoría **con los mismos valores** (`NFR-002`, `PRD:AC-07`). **Va en `Movimientos/` y no en `Resumenes/`** aunque afirme algo sobre el resumen: los tests del resumen están fuera de la tabla de [D-12](./research.md) y tocarlos sería gastar presupuesto para agregar un caso que es sobre la nota. **Nace en verde**: la nota no entra en la consulta que agrupa. Decirlo en la salida — vale por el día que alguien toque esa consulta, no por hoy
- [X] T028 [TEST] [P] [US2] En `frontend/tests/VentanaDeEdicion.test.tsx`: la ventana **trae la nota que el movimiento ya tenía** —no vacía— y la manda de vuelta al guardar; vaciarla manda el valor vacío (fila 5 de [D-12](./research.md))
- [X] T029 [TEST] [P] [US2] En `frontend/tests/NotaDelMovimiento.test.tsx`: una nota escrita en **varias líneas** se guarda con sus saltos, se lee en el listado en **una sola línea visual** sin que el salto agregue ni quite nada, y al reabrir la ventana de edición vuelve **con sus saltos intactos** (`FR-012`)
- [X] T030 [ROJO] [US2] Correr las dos suites y exigir **ROJO** en T025, T026, T028 y T029, con el motivo verificado. T027 da verde y eso también hay que decirlo. Mostrar la salida
- [X] T031 [US2] La edición aplica la nota en `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs`, con `Nota` **obligatoria** en `MovimientoEditadoDto`. Verde de T025 y T026
- [X] T032 [US2] `editarMovimiento` manda la nota en `frontend/src/api/cliente.ts`, y `frontend/src/movimientos/VentanaDeEdicion.tsx` la pasa como valor inicial al formulario compartido. El campo no se escribe de nuevo: `CamposDelMovimiento` es uno para las dos pantallas, que es la razón por la que existe. Verde de T028 y T029
- [X] T033 [VERIFY] [US2] Puerta de las dos pilas completa, con su salida

---

## Phase 5: User Story 3 — El catálogo no acepta un código que no es un código (P3)

**Goal**: `moneda.codigo` exige tres letras en el esquema. Es la deuda **D11-02**, saldada.

**Independent Test**: un `INSERT` directo de un código inválido contra el esquema migrado es rechazado
por la base.

**Nota de alcance**: esta historia quedó **casi entera en Phase 2**, porque comparte la migración con la
columna de la nota ([D-10](./research.md)). Lo que queda acá es la única verificación que no se puede
hacer antes de que exista la restricción.

- [X] T034 [US3] Correr `./backend/verificar-monedas.sh` y exigir **verde**: la restricción nueva no cambia el resultado de la barrera que siembra el catálogo con SQL puro. Es el único acoplamiento real de esta historia con el resto del proyecto, y está previsto —siembra con `XTS`, tres letras—, pero **previsto no es verificado**: la barrera agrega una moneda a la base de verdad y es la única forma de comprobar que la restricción no la rechaza. Exige los **dos árboles limpios** (`git status --short` vacío), así que commitear antes
- [X] T035 [VERIFY] [US3] Puerta del backend, con su salida

---

## Phase 6: La barrera de `FR-007` — la nota no clasifica

**Purpose**: `FR-007` es la decisión de producto central del ticket. Sin verificación es una intención
escrita en un documento.

**Por qué acá y no dentro de una historia**: no sirve a ninguna de las tres. Verifica una propiedad del
conjunto ya construido, así que no tiene sentido antes ([D-13](./research.md)).

- [X] T036 [TEST] Crear `backend/GestionGastos.Api.Tests/Integracion/BarreraDeLaNotaTests.cs`: inspeccionar el SQL de la consulta del listado y exigir que la nota **aparezca en la proyección** y **nunca en el filtro** (`FR-007`, `SC-007`, [D-09](./research.md)). Va junto a `BarreraDeAislamientoTests` y `BarreraDelDesgloseTests`, que son las dos que ya inspeccionan SQL — **ninguna carpeta nueva**. La afirmación es más fina que la del desglose, que busca una palabra que no debe aparecer en ningún lado: acá la palabra **tiene** que aparecer, en un lugar y no en el otro
- [X] T037 Crear `backend/verificar-nota.sh` con bit de ejecución `100755`, con la forma de las seis que ya existen: le agrega a la consulta del listado el acotado por nota, exige el **rojo**, restaura y exige el **verde**. Es el Principio V — una barrera que nunca se vio fallar no es una barrera— y acá pesa el doble, porque una afirmación de ausencia hecha inspeccionando texto informa verde tanto cuando es cierta como cuando la inspección dejó de mirar
- [X] T038 Correr `./backend/verificar-nota.sh` y mostrar la salida completa: el rojo con el acotado puesto y el verde después de restaurar. Si el rojo no aparece, la barrera no sirve y el que está mal es el test de T036
- [X] T039 [VERIFY] Puerta del backend, con su salida

---

## Phase 7: El rendimiento del listado (`NFR-003`, `PRD:AC-10`)

**Purpose**: `AC-10` pide una medición que **nunca existió**. Hay tres tests de rendimiento y ninguno
mide el listado.

- [X] T040 [TEST] Crear `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoListadoTests.cs`: 1000 movimientos **con nota**, 100 ejecuciones, p95 por debajo de 2000 ms (`NFR-003`, `PRD:AC-10`, [D-11](./research.md)). Las notas se siembran **no vacías y de largo realista**: sembrarlas sin nota mediría la consulta de antes de esta feature y daría verde sin ejercitar la columna nueva — el mismo error que la 009 evitó al exigir dos monedas en el sembrado del resumen. Las fechas salen de `SembradoDeRendimiento`, que las ancla al año de la fecha que recibe (la lección de FIX-004). **Mide la respuesta de la API, no el navegador**, y el nombre y la documentación del test lo dicen: medir la pantalla exigiría la dependencia que `NFR-005` prohíbe
- [X] T041 Correr `dotnet test backend/` (en local corren todos; en CI éste queda fuera por `--filter "FullyQualifiedName!~Rendimiento"`) y mostrar **el número real** del p95. **Este test nace en verde**: el resumen agrupa las mismas 1000 filas en 6 ms, así que el listado con una columna de texto más debería quedar en el mismo orden de magnitud. Si diera cerca del techo, el que está mal es el diseño y no el techo

---

## Phase 8: Polish & cierre

- [ ] T042 Commitear todo. `verificar-monedas.sh` **exige los dos árboles limpios** o no puede distinguir lo que ensució ella de lo que ya estaba sucio: `git status --short` tiene que estar vacío antes del paso siguiente
- [ ] T043 Correr **las siete barreras** con su salida (~13 min): `verificar-nota.sh` (la nueva), `verificar-contrato.sh` (~2,5 min, el campo viaja en las tres formas del movimiento), `verificar-monedas.sh` (~1 min, `FR-010`), `verificar-aislamiento.sh` (~7 min, la nota es **el primer campo de texto libre que el aislamiento tiene que tapar**), `verificar-autorizacion.sh`, `verificar-desglose.sh` y `verificar-linter.sh`
- [ ] T044 [P] Cobertura del backend: `dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings`, con su salida
- [ ] T045 [P] Build de producción del frontend: `pnpm --dir frontend build`
- [ ] T046 [P] **Comprobar que no entró ninguna dependencia** (`NFR-005`, `SC-009`): `git diff main -- frontend/package.json frontend/pnpm-lock.yaml 'backend/**/*.csproj'` tiene que estar vacío. Contar caracteres Unicode se hizo con la biblioteca estándar de las dos plataformas ([D-02](./research.md)); si acá aparece algo, se cableó una dependencia para eso
- [ ] T047 **Comprobar la tabla de [D-12](./research.md) contra lo que realmente se tocó**: `git diff --stat main -- frontend/tests backend/GestionGastos.Api.Tests`. Cada test modificado tiene que estar en la tabla; si hay uno que no está, decir cuál y qué requisito lo justifica — o revertirlo. Y **al revés**: si un test de `Resumenes/`, de aislamiento, de categorías o del dashboard aparece modificado, el que está mal es el código de esta feature
- [ ] T048 Correr los pasos a mano del [quickstart](./quickstart.md). **Los pasos 1 a 5 no los cubre ningún test**: los 360 px con la séptima columna medidos de verdad, el camino rápido de carga con un solo Tab más, el campo con lector de pantalla, la fila de un movimiento de antes de la migración y los saltos de línea de ida y vuelta. Si no hay navegador en el entorno, anotarlo como **D12-01**, que sigue abierta desde la feature 010
- [ ] T049 Actualizar `plan-de-implementacion/README.md`: mover el ticket 2 (DISC-001-02) a la tabla de implementado, con qué lo demuestra en el código y qué deudas saldó. Y lo que hace distinto a este cierre: **la tabla de pendientes queda vacía y el plan DISC-001 se termina**
- [ ] T050 Cerrar la tabla de *Deuda registrada* de [spec.md](./spec.md): confirmar cuáles de D12-01 a D12-08 quedaron efectivamente abiertas, y anotar en la tabla de la feature 011 que **D11-02 y D11-07 están saldadas**. Con el plan terminado, anotar además a quién le quedan las que sigan abiertas: ya no hay "el próximo ticket"

---

## Dependencies

```text
Phase 1 (T001)
   └─> Phase 2 (T002 … T007)          ← BLOQUEANTE: sin la columna no hay nada que guardar
          ├─> Phase 3 · US1 (T008 … T024)     🎯 MVP
          │      └─> Phase 4 · US2 (T025 … T033)   ← necesita que la nota exista para corregirla
          ├─> Phase 5 · US3 (T034, T035)      ← independiente de US1 y US2
          └─> Phase 6 · barrera FR-007 (T036 … T039)  ← necesita la consulta ya construida (US1)
                 └─> Phase 7 · rendimiento (T040, T041)
                        └─> Phase 8 · cierre (T042 … T050)
```

**Las dependencias reales, no las de comodidad**:

- **US2 depende de US1** de verdad: sin poder escribir una nota no hay nada que corregir. Es la única
  dependencia entre historias.
- **US3 no depende de nada** y su implementación ya está en Phase 2. Se puede verificar en cualquier
  momento después de T006.
- **Phase 6 depende de US1** porque inspecciona la consulta del listado con la nota ya en su proyección.
- **Phase 7 depende de US1** por lo mismo: mide el listado con la columna puesta.
- **T042 antes de T043**, sin excepción: `verificar-monedas.sh` no funciona con el árbol sucio.

## Parallel execution

**Dentro de Phase 2**: T003 y T004 son el mismo archivo, así que no van en paralelo. T002 sí es
independiente de los dos.

**Dentro de US1** — nueve tests en paralelo, cinco del backend y cuatro del frontend:

```text
T008, T009, T010, T011, T012   (backend/GestionGastos.Api.Tests/Movimientos/)
T013                            (frontend/tests/cliente.test.ts)
T014, T015                      (frontend/tests/NotaDelMovimiento.test.tsx — mismo archivo, secuencial entre sí)
T016                            (frontend/tests/ListadoMovimientos.test.tsx)
```

Las tareas de implementación T018 a T022 tocan archivos distintos pero **no van en paralelo**: T018
—la normalización en el DTO— es lo que hace verde a T011, y el resto se apoya en que el campo ya viaje.

**Dentro de US2** — cinco tests en paralelo:

```text
T025, T026, T027   (backend/GestionGastos.Api.Tests/Movimientos/)
T028               (frontend/tests/VentanaDeEdicion.test.tsx)
T029               (frontend/tests/NotaDelMovimiento.test.tsx)
```

**En Phase 8**: T044, T045 y T046 son independientes entre sí y de T043. T047 a T050 son secuenciales
porque cada uno informa al siguiente.

## Implementation strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1).** Con eso el ticket entrega su valor: se puede anotar en qué
se gastó y leerlo en el listado. Son 24 tareas de las 50.

**Incremento 2 = US2** (T025 a T033). Nueve tareas más y la nota se vuelve utilizable de verdad: un
texto libre que no se puede corregir se abandona.

**Incremento 3 = US3** (T034, T035). Dos tareas, y su implementación ya está hecha. **Es lo que se
recorta si hay que recortar**, y por eso está aislada: sacarla es sacar la restricción de la migración,
sin tocar nada más.

**Lo que no se recorta**: Phase 6. Es la verificación de `FR-007`, que es la decisión de producto
central del ticket — la que impide que la nota se convierta en la segunda taxonomía informal que
`PRD:RF-33` evita. Sin ella el requisito está escrito y nada lo sostiene.

**Con el cierre de esta feature el plan DISC-001 queda sin tickets pendientes.** T049 y T050 son ese
cierre, y conviene no tratarlos como trámite: son el único lugar donde queda anotado a quién le tocan
las deudas que sigan abiertas cuando ya no hay un próximo ticket al que apuntarlas.

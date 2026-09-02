---

description: "Task list template for feature implementation"
---

# Tasks: Resumen del mes con desglose por categoría

**Input**: Design documents from `specs/006-resumen-del-mes/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/resumen.md)

**Tests**: obligatorios. TDD es el Principio I de `.specify/memory/constitution.md`, no una opción de
este comando. Cada tarea `[TEST]` se escribe y **se ve fallar** antes de la tarea de código que la
pone en verde, y esa salida se muestra.

**Organization**: por historia, en el orden del plan. La Fase 2 bloquea a las tres.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo (archivos distintos, sin dependencias)
- **[TEST]**: escribe un test que tiene que fallar antes de que exista su implementación
- **[ROJO]**: su producto es una salida en rojo, mostrada. Si sale verde, la tarea falló
- **[VERIFY]**: la puerta de la constitución, con la salida a la vista

## Path Conventions

Backend en `backend/`, frontend en `frontend/`. La única excepción declarada del proyecto —los tests
de contrato leen `frontend/src/api/tipos.ts`— está en `docs/adr/ADR-001`.

---

## ⚠️ Por qué la Fase 2 va antes que todo

Dos motivos, y ninguno es de comodidad.

**El primero: la barrera de aislamiento no cubre lo que esta feature va a escribir.** Vigila los
métodos del canal que devuelven `IQueryable<Movimiento>`; el resumen devuelve sumas, así que una
agregación sin `usuario_id` pasaría en verde y la barrera ni la enumeraría
([D-01](./research.md#d-01--la-barrera-de-aislamiento-no-cubre-lo-que-esta-feature-va-a-escribir)).
Si esto se arregla **después** de escribir el resumen, el arreglo se hace mirando un código que ya
está bien, que es la peor forma de arreglar una barrera: no se ve fallar.

**El segundo: el período tiene que tener un solo intérprete antes de que haya dos endpoints que lo
usen.** Unificarlo después es un refactor sobre código nuevo y sin estrenar
([D-03](./research.md#d-03--el-período-se-valida-igual-que-en-el-listado-y-en-un-solo-lugar)).

---

## Phase 1: Setup

**Purpose**: saber que el punto de partida está en verde, para que cualquier rojo posterior sea de
esta feature y no de algo heredado.

- [X] T001 Correr la puerta del backend sobre la rama recién sacada de `main` —`dotnet format
      backend/GestionGastos.slnx --verify-no-changes`, `dotnet build backend/GestionGastos.slnx
      -warnaserror` y `dotnet test backend/`— y mostrar su salida. Tiene que estar en verde antes de
      tocar nada

---

## Phase 2: Foundational — la barrera y el intérprete del período (bloquea a US1, US2 y US3)

**Purpose**: dejar en pie lo que va a vigilar y lo que van a compartir las tres historias.

**⚠️ CRITICAL**: ninguna tarea de historia puede empezar hasta que esta fase esté completa.

### La barrera (D-01)

- [X] T002 [TEST] Agregar temporalmente a `backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs`
      una agregación **deliberadamente sin acotar por cuenta** —un `GroupBy` sobre
      `contexto.Movimientos` sin `usuario_id`, que devuelva algo que no sea
      `IQueryable<Movimiento>`— y correr `dotnet test backend/ --filter
      "FullyQualifiedName~BarreraDeAislamiento"`. **Tiene que quedar en VERDE.** Ese verde es el
      agujero, mostrado y no argumentado: la barrera aprueba una consulta que devuelve los
      movimientos de todas las cuentas. Mostrar la salida
- [X] T003 Generalizar en `backend/GestionGastos.Api.Tests/Integracion/BarreraDeAislamientoTests.cs`
      el descubrimiento de `Todas_Las_Consultas_Del_Canal_Acotan_Por_Cuenta`: que enumere los
      métodos que devuelven **cualquier** `IQueryable<>`, no sólo `IQueryable<Movimiento>`, y que el
      cast al invocarlos sea al `IQueryable` no genérico. Actualizar el comentario de la clase
      explicando qué condición caducó y por qué
- [X] T004 [ROJO] Volver a correr la barrera con la agregación sin acotar todavía puesta. **Ahora
      tiene que quedar en ROJO**, nombrando el método. Mostrar la salida. Recién ahí **quitar** la
      agregación temporal y comprobar el verde
- [X] T005 [TEST] Agregar a `backend/verificar-aislamiento.sh` un **quinto desarme**: una agregación
      del canal que deja de acotar por cuenta. Actualizar la cabecera del script, que hoy dice
      "las cuatro formas", y su lista de motivos con la vía nueva —*una lectura que no devuelve
      movimientos escapa al descubrimiento de la barrera*
- [X] T006 [ROJO] Correr `./backend/verificar-aislamiento.sh` entero y mostrar su salida. Los cinco
      desarmes tienen que dar rojo cada uno, y el verde final tiene que volver. Si un desarme pasa
      en verde, la barrera no cubre esa vía y la tarea no está hecha
- [X] T007 [VERIFY] Puerta del backend completa: `dotnet format --verify-no-changes` + `dotnet build
      -warnaserror` + `dotnet test backend/`, con su salida

### El intérprete del período (D-03)

- [X] T008 [TEST] Crear `backend/GestionGastos.Api.Tests/Unitarios/PeriodoPedidoTests.cs` con las
      cuatro reglas de la tabla de [data-model.md](./data-model.md): sin parámetros → el mes del
      reloj inyectado; los dos en orden → ese rango; `desde > hasta` → rechazo; un solo extremo →
      rechazo. Falla porque el tipo no existe
- [X] T009 Crear `backend/GestionGastos.Api/Dominio/PeriodoPedido.cs` con la mínima
      implementación que ponga T008 en verde. Reusa `RangoDeFechas` y `RangoDelMes`: **no
      reimplementa** el invariante `Desde <= Hasta`, que ya vive en el tipo
- [X] T010 Reemplazar en `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs` las tres
      validaciones del listado por una llamada a `PeriodoPedido`. **`FiltrosDelListadoTests` es la
      red y no se toca**: si un solo test de la feature 005 cambia de resultado, el refactor cambió
      comportamiento y hay que volver atrás, no ajustar el test
- [X] T011 [VERIFY] Puerta del backend completa, con su salida

**Checkpoint**: la barrera sabe fallar por la vía nueva y hay un solo intérprete del período. Recién
acá puede empezar el resumen.

---

## Phase 3: User Story 1 - Cómo vengo este mes (Priority: P1) 🎯 MVP

**Goal**: `GET /api/resumen` devuelve, por moneda, lo ingresado, lo gastado y el balance del mes en
curso.

**Independent Test**: cargar gastos e ingresos de una cuenta dentro y fuera del mes en curso y pedir
el resumen sin parámetros; los totales salen de los de adentro y de ninguno de los de afuera.

### Tests de la historia 1

> Se escriben primero y tienen que fallar. Acá el rojo es espontáneo: el endpoint no existe, así que
> el primero falla con `404` antes de que haya una línea de producción.

- [X] T012 [TEST] [US1] Crear `backend/GestionGastos.Api.Tests/Integracion/ResumenDelPeriodoTests.cs`
      con el andamio —`FactoriaConReloj` con fecha fija ([D-08](./research.md#d-08--los-tests-fijan-el-reloj-sin-excepción)),
      `CuentaDePrueba`— y el test de **AC-30**: con ingresos y gastos del mes, `totalIngresado` y
      `totalGastado` son las sumas por tipo. Comparados contra un valor escrito a mano, no contra
      una suma recalculada en el test
- [X] T013 [TEST] [P] [US1] Agregar el test de **AC-31** (INV-06): una cuenta sin ningún movimiento
      recibe **una entrada por cada moneda del catálogo** —no una lista vacía—, con totales y
      balance en `0` y `gastosPorCategoria` en `[]`
- [X] T014 [TEST] [P] [US1] Agregar el test de **FR-002**: movimientos del mes anterior no suman en
      el resumen sin parámetros. El mes anterior se calcula con aritmética de meses desde el reloj
      fijo, **nunca restando 30 días** ([D-08](./research.md#d-08--los-tests-fijan-el-reloj-sin-excepción))
- [X] T015 [TEST] [P] [US1] Agregar el test de **INV-01**: `balance == totalIngresado -
      totalGastado`, incluido un período donde da **negativo**, que es un resultado válido
- [X] T016 [TEST] [P] [US1] Agregar el test de **AC-15 y AC-16**: un alta recién hecha ya suma en el
      total de su tipo en el pedido siguiente
- [X] T017 [TEST] [US1] Agregar el test de **AC-02** (INV-04, la deuda de la feature 004): con dos
      cuentas cargadas, el resumen de una da **exactamente** el valor esperado calculado a mano y
      ningún monto de la otra suma. La cuenta ajena carga un monto mucho mayor, para que la
      contaminación no pueda confundirse con un redondeo
- [X] T018 [TEST] [P] [US1] Agregar el test de **RF-03**: sin sesión, `401`. Un resumen no es un
      agregado inocuo — es la foto financiera de una cuenta
- [X] T019 [TEST] [US1] Crear `backend/GestionGastos.Api.Tests/Contrato/ContratoResumenTests.cs`:
      los campos de `Resumen`, `ResumenPorMoneda` y `TotalPorCategoria` comparados contra el JSON
      real **en las dos direcciones**, cada nivel contra su nodo
      ([D-07](./research.md#d-07--el-contrato-se-declara-con-interfaces-con-nombre-sin-objetos-anidados))

### Implementación de la historia 1

- [X] T020 [US1] Crear `backend/GestionGastos.Api/Movimientos/MontoAgrupado.cs` con la fila que
      devuelve la agregación. Documentar que `Total` **no** es un `decimal(11,2)`: el techo de un
      movimiento no es el techo de una suma ([D-11](./research.md#d-11--el-techo-del-monto-agregado-no-es-el-del-movimiento))
- [X] T021 [US1] En `backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs`: extraer el
      acotado a un `DeLaCuenta` **privado** —el `WHERE` con `usuario_id`, escrito una sola vez— y
      dejar `Filtrado` como `DeLaCuenta + OrderBy`. Agregar `Agrupado` como `DeLaCuenta + GroupBy(
      moneda, tipo, categoría) + Sum`, **sin** el `OrderBy`, que el `GROUP BY` descartaría
      ([D-04](./research.md#d-04--una-sola-consulta-agregada-y-la-composición-en-memoria)). La
      barrera generalizada en T003 lo inspecciona automáticamente
- [X] T022 [US1] Crear `backend/GestionGastos.Api/Resumen/ResumenDtos.cs` con `Resumen`,
      `ResumenPorMoneda` y `TotalPorCategoria`, según [contracts/resumen.md](./contracts/resumen.md)
- [X] T023 [US1] Crear `backend/GestionGastos.Api/Resumen/CalculoDelResumen.cs`: vuelca las filas de
      `Agrupado` sobre **el catálogo de monedas**, que es lo que garantiza una entrada por moneda
      aunque no haya movimientos ([D-05](./research.md#d-05--las-monedas-salen-del-catálogo-no-de-los-movimientos))
- [X] T024 [US1] Crear `backend/GestionGastos.Api/Resumen/ResumenEndpoints.cs` con `GET
      /api/resumen` y registrarlo con `app.MapResumen()` en `backend/GestionGastos.Api/Program.cs`.
      Devuelve `desde` y `hasta` **siempre**, también cuando el cliente no los mandó
      ([D-06](./research.md#d-06--la-respuesta-devuelve-el-período-que-se-usó))
- [X] T025 [P] [US1] Agregar `Resumen`, `ResumenPorMoneda` y `TotalPorCategoria` a
      `frontend/src/api/tipos.ts`, como tres interfaces con nombre y **sin objetos anidados
      inline**, cada una con el comentario que explique lo que no se ve en los campos
- [X] T026 [VERIFY] [US1] Puerta del backend completa, más `pnpm --dir frontend exec tsc --noEmit`,
      con su salida

**Checkpoint**: el resumen del mes funciona y está aislado. Es el MVP y se puede demostrar solo.

---

## Phase 4: User Story 2 - En qué se me va la plata (Priority: P2)

**Goal**: el resumen trae el desglose de gastos por categoría, y ese desglose suma exactamente el
total gastado.

**Independent Test**: cargar gastos en varias categorías de un mismo período y comprobar que cada
total es la suma de los suyos y que la suma de todos reconstruye `totalGastado`.

> **Esta historia casi no agrega código, y eso es a propósito.** `Agrupado` ya agrupa por categoría
> desde T021, porque [D-04](./research.md#d-04--una-sola-consulta-agregada-y-la-composición-en-memoria)
> hace que el total y el desglose salgan de las mismas filas. Lo que esta fase agrega son **los
> tests que fijan esa propiedad** y el único pedazo de lógica propio: que el desglose deje afuera a
> los ingresos. Decir que la historia "ya estaba" sería confundir que el código exista con que la
> propiedad esté verificada.

### Tests de la historia 2

- [X] T027 [TEST] [US2] Crear
      `backend/GestionGastos.Api.Tests/Integracion/DesglosePorCategoriaTests.cs` con el test de
      **AC-27**: gastos en tres categorías distintas, y el total de cada una igual a la suma de sus
      montos
- [X] T028 [TEST] [P] [US2] Agregar el test de **INV-02**: la suma de `gastosPorCategoria[].total`
      es igual a `totalGastado`, por moneda. Es la igualdad que D-04 vuelve estructural; el test
      existe para que se note el día que alguien la separe en dos consultas
- [X] T029 [TEST] [P] [US2] Agregar el test de **INV-07** (FR-008): con ingresos cargados, **ninguna
      categoría de ingreso aparece** en el desglose, y esos montos sí suman en `totalIngresado` y en
      `balance`
- [X] T030 [TEST] [P] [US2] Agregar el test de **FR-009**: una categoría de gasto sin movimientos en
      el período **no aparece** en el desglose, en lugar de aparecer en cero
- [X] T031 [TEST] [P] [US2] Agregar los tests de **AC-20** y **AC-21**: un gasto que cambia de
      categoría deja de sumar en la anterior y suma en la nueva; un gasto eliminado no suma en
      ningún total. Los dos verifican que el resumen se deriva y no se guarda

### Implementación de la historia 2

- [X] T032 [US2] En `backend/GestionGastos.Api/Resumen/CalculoDelResumen.cs`, componer
      `gastosPorCategoria` sólo con las filas de tipo gasto. Los ingresos siguen sumando en
      `totalIngresado` y en `balance`
- [X] T033 [VERIFY] [US2] Puerta del backend completa, con su salida

**Checkpoint**: US1 y US2 funcionan, cada una verificable por su cuenta.

---

## Phase 5: User Story 3 - El mismo resumen, para el período que yo elija (Priority: P3)

**Goal**: `desde` y `hasta` acotan el resumen, con las mismas reglas del listado.

**Independent Test**: pedir el mismo conjunto con un rango explícito y comparar contra el listado
filtrado con ese mismo rango.

### Tests de la historia 3

- [X] T034 [TEST] [US3] Agregar a `ResumenDelPeriodoTests.cs` el test de **AC-29**: movimientos
      dentro y fuera de un rango, y sólo los de adentro suman — **incluidos los fechados exactamente
      en los dos extremos**, que es donde un `>` en lugar de un `>=` se esconde
- [X] T035 [TEST] [P] [US3] Agregar el test del **rango de un solo día**: `desde == hasta` es válido
      y contiene los movimientos de ese día
- [X] T036 [TEST] [P] [US3] Agregar los tests de **FR-004**: rango invertido → `400`, y medio rango
      → `400`. Con la clave `rango` en `errors`, la misma que el listado, porque sale del mismo
      intérprete
- [X] T037 [TEST] [P] [US3] Agregar el test de **INV-03**: para un mismo rango, `totalGastado` es
      igual a la suma de los gastos del listado filtrado con ese rango. Es el test que impide que
      las dos vistas se separen
- [X] T038 [TEST] [P] [US3] Agregar el test de **FR-014** con rango explícito: un rango sin
      movimientos devuelve la forma completa en cero, no una lista vacía

### Implementación de la historia 3

- [X] T039 [US3] Aceptar `desde` y `hasta` en `backend/GestionGastos.Api/Resumen/ResumenEndpoints.cs`
      y resolverlos con `PeriodoPedido`. **No se escribe ninguna validación nueva**: si aparece una,
      D-03 se rompió y hay dos intérpretes otra vez
- [X] T040 [VERIFY] [US3] Puerta del backend completa, con su salida

**Checkpoint**: las tres historias funcionan de forma independiente.

---

## Phase 6: Polish & cierre

- [X] T041 [P] Actualizar la tabla de *Deuda registrada* de
      [`specs/004-aislamiento-cuentas/spec.md`](../004-aislamiento-cuentas/spec.md): AC-02 pasa a
      **Saldado**, con el nombre del test de T017. La tabla queda **sin ninguna fila pendiente**
      (SC-005)
- [X] T042 [P] Crear `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoResumenTests.cs` con el
      sembrado que ya existe, midiendo RNF-01 —< 2 s con 1000 movimientos, < 4 s con 10000— sobre
      `GET /api/resumen`. Si da rojo, se reabre el índice de
      [D-10](./research.md#d-10--sin-migración-y-el-índice-se-deja-como-está) **con el número en la
      mano**, no antes
- [X] T043 [P] Corregir los comentarios que esta feature volvió falsos: el de
      `backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs`, que habla de lo que pasaba
      "hasta el ticket 01c", y el de `backend/GestionGastos.Api/Dominio/Moneda.cs`, que dice que el
      catálogo "existe pero no se expone" — desde esta feature se expone, agrupando
- [X] T044 [P] Actualizar `plan-de-implementacion/README.md`: FEAT-001b y FEAT-001c pasan de la
      tabla de *Pendiente* a la de *Ya implementado*, con lo que las demuestra en el código.
      FEAT-001b ya está en `main` desde el PR #20 y la tabla todavía no lo refleja
- [X] T045 Comprobar —no asumir— que `./backend/verificar-autorizacion.sh` cubre `/api/resumen`:
      descubre los endpoints por `EndpointDataSource` en runtime, así que debería entrar solo. Con
      un `AllowAnonymous` puesto a propósito tiene que fallar **nombrando la ruta**
- [X] T046 Correr la cobertura con `dotnet test backend/GestionGastos.slnx --settings
      backend/cobertura.runsettings` y mostrarla. `MovimientosConsulta`, `CalculoDelResumen`,
      `ResumenEndpoints` y `PeriodoPedido` son código de decisión: se espera 100 % de línea y de
      rama, y lo que falte se explica
- [X] T047 [P] Recorrer [quickstart.md](./quickstart.md) de punta a punta, incluidos el paso 3 —el
      total contaminado— y el paso 6 —el período vacío—, y corregir lo que no salga como dice
- [X] T048 [VERIFY] Puerta de cierre de feature, entera y con su salida a la vista: `dotnet format
      --verify-no-changes`, `dotnet build -warnaserror`, `dotnet test backend/`, la cobertura, y las
      **cuatro** barreras — `verificar-contrato.sh`, `verificar-autorizacion.sh`,
      `verificar-linter.sh` y `verificar-aislamiento.sh`. Más `pnpm --dir frontend lint`, `exec tsc
      --noEmit`, `test` y el build de producción

---

## Dependencies & Execution Order

### Entre fases

- **Fase 1 (Setup)**: sin dependencias.
- **Fase 2 (Foundational)**: depende de la 1 y **bloquea a las tres historias**. Sus dos mitades
  —barrera (T002-T007) e intérprete del período (T008-T011)— son independientes entre sí y podrían
  ir en paralelo; el orden escrito pone la barrera primero porque es la que protege a todo lo demás.
- **Fases 3-5 (historias)**: dependen de la 2 completa. En orden de prioridad.
- **Fase 6 (cierre)**: depende de las tres historias.

### Dentro de cada historia

- Los `[TEST]` van **antes** que su implementación y se ven fallar.
- Dentro de la implementación: la fila agregada (T020) → la consulta (T021) → los DTO (T022) → el
  cálculo (T023) → el endpoint (T024). Cada uno usa al anterior.
- El `[VERIFY]` cierra la historia. Sin su verde, la historia no está hecha.

### Dependencias particulares de esta feature

- **T003 depende de T002**: generalizar la barrera sin haber visto primero el agujero en verde es
  arreglar algo que nadie vio roto.
- **T004 depende de T003**: el rojo sólo prueba algo si el desarme sigue puesto.
- **T010 depende de T009**, y toda la Fase 3 depende de T011: el resumen usa `PeriodoPedido` desde
  el primer día, no lo adopta después.
- **T032 depende de T023**: es una condición dentro del cálculo que ya existe.
- **T039 no agrega validación**: depende de T009 y de nada más.
- **T041 depende de T017**: la deuda se marca saldada nombrando el test que la salda, no antes.

### Oportunidades de paralelismo

- **Fase 2**: las dos mitades (T002-T007 y T008-T011), si hay dos personas.
- **US1**: T013 a T018 en paralelo una vez que T012 dejó el archivo con su andamio. T025 (el
  frontend) en paralelo con toda la implementación del backend.
- **US2**: T028 a T031 en paralelo después de T027.
- **US3**: T035 a T038 en paralelo después de T034.
- **Fase 6**: T041, T042, T043, T044 y T047 son archivos distintos.

---

## Implementation Strategy

### MVP (Foundational + US1)

1. Fase 1 y Fase 2 completas — la barrera sabe fallar por la vía nueva.
2. Fase 3 — el resumen del mes, aislado y verificado con dos cuentas.
3. **PARAR Y VALIDAR**: los pasos 1, 2, 3 y 6 del quickstart ya se pueden recorrer.

Con eso la aplicación responde *cómo vengo este mes*, que es media promesa del producto.

### Entrega incremental

1. Fase 2 → el piso.
2. US1 → *cómo vengo este mes*. Demostrable.
3. US2 → *en qué se me va la plata*. Demostrable.
4. US3 → el período elegido. Demostrable.
5. Fase 6 → la deuda de 004 saldada, el rendimiento medido y las cuatro barreras en verde.

---

## Notes

- `[P]` = archivos distintos, sin dependencias entre sí.
- **Ningún test se ajusta para que pase.** Si un test de la feature 005 cambia de resultado en T010,
  el refactor cambió comportamiento: se vuelve atrás.
- **Las 48 tareas quedaron hechas el 2026-09-01.** Lo que la feature NO dejó hecho —y quién lo
  cubre— está en la tabla de *Deuda registrada* de [spec.md](./spec.md), junto con las tres
  desviaciones de proceso que hubo. Lo que quede pendiente se lee ahí, no acá.
- **No se commitea con la puerta en rojo**, y no se abre PR.
- Si aparece una migración, algo se salió del alcance
  ([D-10](./research.md#d-10--sin-migración-y-el-índice-se-deja-como-está)).

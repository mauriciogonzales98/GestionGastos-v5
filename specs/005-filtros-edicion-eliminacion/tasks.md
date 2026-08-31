---

description: "Task list — Filtros del listado, edición y eliminación"
---

# Tasks: Filtros del listado, edición y eliminación

**Input**: Design documents from `/specs/005-filtros-edicion-eliminacion/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/movimientos.md), [quickstart.md](./quickstart.md)

**Tests**: **obligatorios**. No es una opción de esta feature: los Principios I y II de
`.specify/memory/constitution.md` exigen que cada AC tenga un test que lo nombre y que ese test
falle antes de que exista el código. Acá el rojo es **espontáneo** —los endpoints no existen, así
que el primer test de cada uno falla con `404` sin ayuda—, a diferencia de la feature 004, que tuvo
que fabricarlo desarmando cosas.

**Organization**: agrupadas por historia, en el orden de prioridad de la spec: US1 editar, US2
eliminar, US3 filtrar. La Fase 2 va antes que las tres y **bloquea a todas**.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede correr en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1, US2, US3)
- **[TEST]**: tarea de test. Va **antes** que el código que la pone en verde
- **[ROJO]**: tarea de rojo deliberado sobre una barrera. Rompe a propósito lo que protege, muestra
  el rojo y restaura
- **[VERIFY]**: puerta del grupo. Se corren los comandos de `AGENTS.md` con su salida a la vista

## Path Conventions

Web app: `backend/` y `frontend/`, como fija `AGENTS.md`. De `frontend/` esta feature **sólo toca
`src/api/tipos.ts`**: la pantalla que use los endpoints es trabajo aparte ([plan.md](./plan.md)).

---

## ⚠️ Por qué la Fase 2 va antes que todo

La barrera de aislamiento **no cubre lo que esta feature va a hacer**. Su excepción declarada
excluye `MovimientosEndpoints.cs` entero, no la operación, y eso era seguro mientras el único
acceso escrito a mano ahí fuera un INSERT. La edición trae leer-modificar-guardar, y ese "encontrar
primero" es justo la lectura que puede nacer sin acotar.

Está comprobado, no supuesto: un `MapGet` que devuelve `contexto.Movimientos.ToListAsync()` —los
movimientos de **todas** las cuentas— metido en ese archivo compila y deja la barrera en 4/4 verde
([D-01](./research.md)).

**Estrechar la barrera después de escribir los endpoints sería escribirla sabiendo qué tiene que
dejar pasar.** Por eso va primero, y por eso su rojo se muestra antes de que el estrechamiento
exista.

---

## Phase 1: Setup

**Purpose**: arrancar desde una base conocida. No hay nada que instalar: esta feature no agrega
dependencias, no toca el esquema y no genera migración.

- [X] T001 Correr la puerta del backend sobre la rama recién sacada de `main` —`dotnet format
  --verify-no-changes`, `dotnet build backend/GestionGastos.slnx -warnaserror`, `dotnet test
  backend/`— y dejar su salida a la vista. Es el verde de partida: sin él, un rojo de esta feature
  no se distingue de uno heredado

---

## Phase 2: Foundational — la barrera antes que la superficie (bloquea a US1, US2 y US3)

**Purpose**: dejar la barrera de aislamiento en condiciones de vigilar el código que todavía no se
escribió.

**⚠️ CRÍTICO**: ninguna tarea de US1, US2 o US3 empieza hasta que T005 esté en verde. Si los
endpoints se escriben antes, la barrera se estrecha mirándolos.

- [X] T002 [TEST] Agregar a `backend/verificar-aislamiento.sh` un **cuarto desarme**: colar en
  `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs` un endpoint que lea
  `contexto.Movimientos` sin acotar por cuenta. Correr el script y **mostrar que ese paso da
  VERDE** — es el agujero de [D-01](./research.md) a la vista. Un desarme que hoy no puede dar rojo
  es exactamente lo que justifica la fase
- [X] T003 Estrechar la excepción declarada en
  `backend/GestionGastos.Api.Tests/Integracion/BarreraDeAislamientoTests.cs`: `MovimientosEndpoints.cs`
  deja de estar exento por archivo y pasa a estarlo **sólo para escrituras** (`Add`, `Update`,
  `Remove`). Cualquier otro uso de `contexto.Movimientos` ahí adentro es infracción. En los demás
  archivos no cambia nada. Actualizar el comentario de `EscrituraDeclarada` para que diga qué
  permite y qué no
- [X] T004 [ROJO] Correr `./backend/verificar-aislamiento.sh` entero y mostrar su salida. El
  desarme nuevo **tiene que dar rojo ahora**, y los tres que ya estaban tienen que seguir dándolo.
  Si el cuarto sigue en verde, T003 no está haciendo nada y no se sigue
- [X] T005 [VERIFY] Puerta del backend completa

**Checkpoint**: la barrera vigila también adentro del archivo que va a leer-modificar-guardar, y se
le vio el rojo por esa vía. Recién ahora se puede agregar superficie.

---

## Phase 3: User Story 1 - Corregir lo que cargué mal (Priority: P1) 🎯 MVP

**Goal**: consultar y modificar un movimiento propio, sin que nada de eso alcance a un movimiento
ajeno.

**Independent Test**: registrar un movimiento, modificarlo, y comprobar que el listado devuelve los
valores nuevos y no los viejos. Con dos cuentas, comprobar que lo ajeno responde como inexistente.

### Tests de la historia 1

- [X] T006 [TEST] [US1] Crear
  `backend/GestionGastos.Api.Tests/Integracion/EdicionDeMovimientoTests.cs` con su andamio —
  colección de base de datos y reloj fijo con `FactoriaConReloj`— y el test de **AC-03**: el dueño
  consulta su movimiento por identificador y recibe la **misma forma** que devuelven el alta y el
  listado. Falla con `404` antes de existir el endpoint: ése es el rojo
- [X] T007 [TEST] [P] [US1] Agregar el test de **AC-01**: modificar el monto de 1.500 a 15.000, y
  comprobar que **el listado** lo devuelve en 15.000. Comprobar sobre el listado y no sólo sobre la
  respuesta del `PUT`: la respuesta puede estar bien y la fila mal
- [X] T008 [TEST] [P] [US1] Agregar el test de **AC-02**: cambiar categoría y fecha a la vez, y
  comprobar que el listado muestra la categoría nueva y que el movimiento aparece o no según si su
  fecha nueva cae en el período consultado
- [X] T009 [TEST] [US1] Agregar el test de **AC-04** (INV-01, deuda de 004 / AC-07 del PRD):
  mandar `"usuarioId": <id de la otra cuenta>` en el cuerpo del `PUT` y comprobar que el movimiento
  **sigue siendo de quien lo editó**. Mandar el campo aunque el contrato no lo tenga: hoy se
  descarta al deserializar, y el test tiene que seguir valiendo el día que el DTO gane un campo
- [X] T010 [TEST] [US1] Agregar los tests de **AC-05** y **AC-06** (deuda de 004: AC-03 y AC-04)
  con un helper que compare **dos respuestas entre sí** —la de un identificador ajeno y la de uno
  inexistente— por código, cuerpo y `Content-Type`. **No** afirmar `404` en cada una por separado:
  eso pasa en verde aunque los cuerpos delaten la existencia ([D-03](./research.md)). El
  identificador inexistente se consigue registrando y borrando, que se parece más al caso real que
  un número arbitrario. Comprobar además que el movimiento ajeno queda **sin cambios**
- [X] T011 [TEST] [P] [US1] Agregar el test de **AC-07** (INV-06): un `PUT` con monto inválido
  responde `400` con los errores por campo, con la misma forma que ya usa el alta, y el movimiento
  queda **sin cambios**. Agregar también el caso de orden: un movimiento **ajeno** con cuerpo
  inválido responde `404` y no `400`, porque un `400` confirmaría que se llegó a mirar el cuerpo
- [X] T012 [TEST] [P] [US1] Agregar en
  `backend/GestionGastos.Api.Tests/Contrato/ContratoMovimientosTests.cs` el caso de
  `MovimientoEditado`: los campos que la API acepta de verdad coinciden con los que declara
  `frontend/src/api/tipos.ts`, en las dos direcciones, como ya hacen los otros tipos

### Implementación de la historia 1

- [X] T013 [US1] Renombrar `backend/GestionGastos.Api/Movimientos/ValidacionDelAlta.cs` a
  `ValidacionDelMovimiento.cs` y su clase con él ([D-05](./research.md)). Deja de ser del alta en
  cuanto la usa la edición, y un nombre que miente cuesta caro después. Sin cambios de
  comportamiento
- [X] T014 [US1] Agregar `MovimientoEditadoDto` en
  `backend/GestionGastos.Api/Movimientos/MovimientoDtos.cs`, con **`Fecha` obligatoria**. El
  comentario tiene que decir por qué difiere de `NuevoMovimientoDto`: ausente significaría "hoy" y
  una edición sin fecha movería el movimiento en silencio
- [X] T015 [US1] Agregar `PropioPorId(contexto, usuarioId, id)` a
  `backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs`, devolviendo
  `IQueryable<Movimiento>` y **acotando por cuenta en la consulta**, no en memoria (INV-02). Entra
  solo al radar de `Todas_Las_Consultas_Del_Canal_Acotan_Por_Cuenta`, que la descubre por reflexión
  ([D-02](./research.md)): es la apuesta de 004 cobrándose
- [X] T016 [US1] Implementar `GET /api/movimientos/{id}` en
  `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs`, usando el canal. `404` con el
  mismo cuerpo para inexistente, ajeno y ya eliminado ([D-06](./research.md))
- [X] T017 [US1] Implementar `PUT /api/movimientos/{id}` en el mismo archivo: buscar por el canal
  **primero**, validar después; la categoría se busca con el mismo criterio que el alta —predefinida
  o propia, y activa— y el tipo se deriva de ella (INV-03, INV-04)
- [X] T018 [US1] [P] Agregar `MovimientoEditado` a `frontend/src/api/tipos.ts` con su comentario.
  Va junto con T014: el contrato cambia en un solo movimiento o la verificación se pone en rojo
- [X] T019 [US1] Devolver el encabezado `Location` en `POST /api/movimientos`, ahora que la ruta
  existe. El comentario que hoy explica su ausencia —"Cuando FEAT-001b agregue la lectura
  individual, vuelve con su ruta de verdad"— se reemplaza por el encabezado que prometía
- [X] T020 [VERIFY] [US1] Puerta del backend completa, más `pnpm --dir frontend exec tsc --noEmit`
  por el cambio en `tipos.ts`

**Checkpoint**: se puede corregir un dato mal cargado, y lo ajeno responde como inexistente. Es el
MVP: entregado solo, la app deja de acumular errores que nadie puede reparar.

---

## Phase 4: User Story 2 - Borrar lo que no va (Priority: P2)

**Goal**: eliminar un movimiento propio, sin alcanzar los ajenos.

**Independent Test**: registrar dos movimientos, eliminar uno, y comprobar que el listado devuelve
sólo el otro.

### Tests de la historia 2

- [X] T021 [TEST] [US2] Crear
  `backend/GestionGastos.Api.Tests/Integracion/EliminacionDeMovimientoTests.cs` con el test de
  **AC-08**: el dueño elimina su movimiento, recibe `204`, y el listado deja de devolverlo
- [X] T022 [TEST] [US2] Agregar el test de **AC-09** (deuda de 004: AC-05) reusando el helper de
  comparación de T010: el `DELETE` sobre un identificador ajeno responde igual que sobre uno
  inexistente, **y el movimiento de la otra cuenta sigue apareciendo en el listado de su dueño**.
  Esa segunda mitad es la que distingue "respondió bien" de "no tocó nada"
- [X] T023 [TEST] [P] [US2] Agregar el test de **AC-10**: eliminar dos veces el mismo movimiento
  devuelve `204` y después `404`, sin ningún error inesperado

### Implementación de la historia 2

- [X] T024 [US2] Implementar `DELETE /api/movimientos/{id}` en
  `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs`, buscando por el canal y borrando
  la fila ([D-09](./research.md)). Sin baja lógica: si aparece una columna de estado, algo se salió
  del alcance
- [X] T025 [VERIFY] [US2] Puerta del backend completa

**Checkpoint**: el ciclo de vida del movimiento está completo — se crea, se corrige y se borra— y
las tres operaciones por identificador respetan el aislamiento.

---

## Phase 5: User Story 3 - Encontrar lo que busco (Priority: P3)

**Goal**: acotar el listado por categoría y por rango de fechas, sin cambiar lo que ve quien no pide
nada.

**Independent Test**: cargar movimientos en dos categorías y dos meses, y comprobar que cada
combinación de filtros devuelve exactamente el subconjunto que corresponde.

### Tests de la historia 3

- [ ] T026 [TEST] [US3] Crear `backend/GestionGastos.Api.Tests/Integracion/FiltrosDelListadoTests.cs`
  con el reloj clavado y el test de **AC-13** y **SC-006**: sin filtros, el listado devuelve
  exactamente lo mismo que devolvía antes de esta feature — el mes en curso **del servidor**. Es la
  prueba de regresión de que agregar filtros no cambió el comportamiento por omisión
- [ ] T027 [TEST] [US3] Agregar el test de **AC-14**: un rango de un solo día que contiene un
  movimiento lo devuelve. **Que el resultado no sea vacío es lo que prueba que los extremos se
  incluyen**; un rango amplio pasaría en verde con los extremos excluidos
- [ ] T028 [TEST] [P] [US3] Agregar los tests de **AC-11** y **AC-12**: con filtro de categoría
  sólo esa; sin filtro, todas
- [ ] T029 [TEST] [P] [US3] Agregar el test de **AC-15**: categoría y rango a la vez devuelven los
  que cumplen **las dos** condiciones. Sembrar un movimiento que cumpla sólo una de las dos: sin
  él, un `or` implementado por error pasa en verde
- [ ] T030 [TEST] [P] [US3] Agregar el test de **AC-16**: un rango sin movimientos devuelve `[]` y
  **no** un `404`
- [ ] T031 [TEST] [P] [US3] Agregar el test de **AC-17**: filtrar por una categoría inexistente o de
  otra cuenta devuelve `[]` y no un `400`. Rechazarla confirmaría cuáles existen
- [ ] T032 [TEST] [US3] Agregar los tests de **FR-015**: rango invertido (`desde > hasta`) responde
  `400`, y medio rango —sólo `desde` o sólo `hasta`— también. La lista vacía está prohibida acá:
  se lee como "no hay nada" y esconde el error

### Implementación de la historia 3

- [ ] T033 [US3] Crear `backend/GestionGastos.Api/Dominio/RangoDeFechas.cs` generalizando
  `RangoDelMes` ([D-04](./research.md)), con el invariante `Desde <= Hasta` en el tipo (INV-05) y
  la construcción del mes como fábrica. **Registrar el tipo nuevo en `ArgumentosDePrueba` de
  `BarreraDeAislamientoTests`**: si no, la barrera lanza a propósito diciendo que esa consulta queda
  sin vigilar. No es un obstáculo, es la barrera trabajando
- [ ] T034 [US3] Extender la consulta del listado en
  `backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs` para aceptar el rango y la
  categoría opcional, conservando el acotado por cuenta y el orden explícito `fecha DESC, id DESC`
- [ ] T035 [US3] Aceptar `desde`, `hasta` y `categoriaId` como parámetros de consulta en
  `GET /api/movimientos` en `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs`, con el
  mes en curso del servidor como valor por omisión (FR-013) y la validación de FR-015
- [ ] T036 [VERIFY] [US3] Puerta del backend completa

**Checkpoint**: las tres historias entregadas. La superficie de movimientos es de 5 endpoints y las
cinco respetan el aislamiento.

---

## Phase 6: Polish & cierre

- [ ] T037 [P] Corregir el comentario de `backend/GestionGastos.Api/Dominio/Movimiento.cs`, que dice
  *"Se crea y no cambia. La edición y la baja llegan en FEAT-001b"*. Esta feature lo vuelve falso, y
  un comentario que miente es peor que ninguno
- [ ] T038 [P] Actualizar la tabla de *Deuda registrada* de
  `specs/004-aislamiento-cuentas/spec.md`: **AC-02**, **AC-03**, **AC-04** y **AC-07** quedan
  cubiertos por los tests de US1 y US2, con el test que los cubre nombrado. **AC-05** también.
  Comprobar que lo que quede en esa tabla siga siendo cierto — es el mismo error que el README del
  plan tuvo durante cuatro tickets
- [ ] T039 [P] Agregar `verificar-aislamiento.sh` a la lista de barreras corridas y confirmar que
  `./backend/verificar-autorizacion.sh` cubre los **tres endpoints nuevos** sin tocar nada: exige
  sesión en todo endpoint y los descubre solo. Si no los cubriera, es un hallazgo
- [ ] T040 [P] Correr la cobertura con `dotnet test backend/GestionGastos.slnx --settings
  backend/cobertura.runsettings` y revisar que los tres endpoints nuevos y `PropioPorId` queden
  medidos. Confirmar que el filtro `[*]*d__*` sigue dejando el reporte en 54 clases más las nuevas,
  sin máquinas de estado
- [ ] T041 [P] Recorrer [quickstart.md](./quickstart.md) de punta a punta, incluidos el paso 5 —el
  `diff` de los dos cuerpos tiene que salir **vacío**— y el paso 6 con sus siete casos de filtros
- [ ] T042 [VERIFY] Puerta de cierre de feature, entera y con su salida a la vista: `dotnet format
  --verify-no-changes`, build con `-warnaserror`, `dotnet test` completo, cobertura, y las
  **cuatro** barreras. Más la puerta del frontend entera —lint, format, typecheck, tests, build—,
  que acá sí importa: `tipos.ts` cambió

---

## Dependencies & Execution Order

### Entre fases

- **Setup (T001)**: sin dependencias
- **Foundational (T002–T005)**: **bloquea a las tres historias**. Es la única fase que no se puede
  reordenar: estrechar la barrera después de escribir los endpoints sería estrecharla mirándolos
- **US1 (T006–T020)**: depende de Foundational. Es el MVP
- **US2 (T021–T025)**: depende de Foundational y, en la práctica, de US1: reusa el helper de
  comparación de respuestas de T010
- **US3 (T026–T036)**: depende de Foundational. **No** depende de US1 ni de US2 — toca la consulta
  del listado, no las rutas por identificador
- **Polish (T037–T042)**: depende de las tres

### Dentro de cada historia

- Los `[TEST]` van **antes** que la implementación, y su rojo se muestra. Acá el rojo es espontáneo:
  un test contra un endpoint que no existe falla con `404` sin fabricar nada
- `[VERIFY]` cierra la historia; no se pasa a la siguiente con la puerta en rojo
- En US1, T013 (el renombre) antes que T017: el `PUT` usa la validación ya renombrada, para no
  escribir dos veces el mismo `using`

### Oportunidades de paralelismo

- **T007, T008 y T011 con T006**: mismo archivo pero bloques independientes; se pueden escribir en
  cualquier orden. T012 y T018 tocan archivos distintos y son paralelas de verdad
- **T028 a T031** son cuatro tests independientes sobre el mismo archivo nuevo
- **US3 completa puede ir en paralelo con US1 y US2** si hay dos personas: no comparten archivos
  salvo `MovimientosConsulta.cs` y `MovimientosEndpoints.cs`, que sí hay que coordinar
- **T037 a T041** son revisiones sobre cosas distintas: van en paralelo de verdad

---

## Implementation Strategy

### MVP (Foundational + US1)

1. T001 → T005: la barrera estrechada, con su rojo visto
2. T006 → T020: consultar y modificar, con lo ajeno indistinguible de lo inexistente
3. **PARAR Y VALIDAR**: dos cuentas, editar el propio, intentar el ajeno, comparar los dos cuerpos
4. En este punto la app deja de acumular datos que nadie puede reparar, y cuatro de los cinco AC de
   la deuda de 004 quedan cubiertos

### Entrega incremental

1. MVP → se puede corregir
2. \+ US2 → también borrar; el ciclo de vida está completo
3. \+ US3 → los filtros
4. \+ Polish → la deuda de 004 saldada, la cobertura y el quickstart recorrido

---

## Notes

- **El rojo acá es gratis y hay que aprovecharlo.** La feature 004 tuvo que fabricar sus rojos
  desarmando código porque verificaba lo que ya funcionaba. Esta escribe cosas que no existen: si
  un test pasa en verde la primera vez que se corre, está mal escrito
- **Ningún test depende del día en que corre.** El listado sin filtros recorta al mes en curso del
  servidor, y los tests de rango se escriben naturalmente con la fecha de hoy. Reloj clavado con
  `FactoriaConReloj` en **todos**, también en los de filtros
- **No hay migración.** Si aparece una, algo se salió del alcance
- **La moneda no se edita.** Está en *Deuda registrada* apuntando a 4a/4b
- **Los filtros no entran al contrato** ([D-08](./research.md)): viajan como parámetros de consulta
  y la verificación compara JSON. Es un límite conocido y declarado, no un olvido
- Commit por tarea o por grupo lógico, nunca con la puerta en rojo

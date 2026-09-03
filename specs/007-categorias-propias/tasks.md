---

description: "Task list template for feature implementation"
---

# Tasks: Categorías propias del usuario

**Input**: Design documents from `specs/007-categorias-propias/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/categorias.md), [quickstart.md](./quickstart.md)

**Tests**: obligatorios. TDD es el Principio I de `.specify/memory/constitution.md`, no una opción de
este comando. Cada tarea `[TEST]` se escribe y **se ve fallar** antes de la tarea de código que la
pone en verde, y esa salida se muestra.

**Organization**: por historia, en el orden del plan. La Fase 2 bloquea a las cuatro.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo (archivos distintos, sin dependencias)
- **[TEST]**: escribe un test que tiene que fallar antes de que exista su implementación
- **[ROJO]**: su producto es una salida en rojo, mostrada. Si sale verde, la tarea falló
- **[VERIFY]**: la puerta de la constitución, con la salida a la vista

## Path Conventions

Backend en `backend/`, frontend en `frontend/`. La única excepción declarada del proyecto —los tests
de contrato leen `frontend/src/api/tipos.ts`— está en `docs/adr/ADR-001` y no cambia acá.

---

## ⚠️ Por qué la Fase 2 va antes que todo

**Las dos barreras de esta feature vigilan código que todavía no existe, y ése es el único momento
en que se las puede ver fallar de verdad.**

La del canal de categorías: hoy no hay nada que aislar —las diez categorías son de todos— así que la
barrera de aislamiento ni mira esa tabla. Escribirla *después* del canal es escribirla mirando
código que ya está bien, que es la peor forma de escribir una barrera
([D-03](./research.md#d-03--un-canal-único-de-lectura-de-categorías-con-su-barrera)).

La del desglose es peor, porque el agujero **ya está abierto**: hoy todas las categorías tienen
`activa = true`, así que agregarle `WHERE activa` a la consulta del resumen **no rompe ni un test**.
La suite entera queda en verde con el filtro puesto. Ese verde es la deuda D6-04 de la feature 006,
y hay que verlo antes de taparlo
([D-05](./research.md#d-05--el-desglose-del-resumen-no-puede-empezar-a-filtrar-por-activa)).

Y la migración va primera de todo: sin la columna `discriminador`, FR-009 ni se puede testear —el
alta choca contra el índice— así que ninguna historia se puede cerrar
([D-01](./research.md#d-01--la-unicidad-tiene-que-dejar-de-chocar-contra-una-fila-dada-de-baja)).

---

## Phase 1: Setup

**Purpose**: saber que el punto de partida está en verde, para que cualquier rojo posterior sea de
esta feature y no de algo heredado.

- [X] T001 Correr la puerta del backend y la del frontend sobre la rama recién sacada de `main`
      —`dotnet format backend/GestionGastos.slnx --verify-no-changes`, `dotnet build
      backend/GestionGastos.slnx -warnaserror`, `dotnet test backend/`, `pnpm --dir frontend lint`,
      `pnpm --dir frontend exec tsc --noEmit` y `pnpm --dir frontend test`— y mostrar su salida.
      Tiene que estar todo en verde antes de tocar nada

---

## Phase 2: Foundational — la migración y las dos barreras (bloquea a US1, US2, US3 y US4)

**⚠️ CRITICAL**: ninguna tarea de historia puede empezar hasta que esta fase esté completa.

### La migración: que la unicidad conviva con la baja lógica (D-01)

- [X] T002 [TEST] Crear `backend/GestionGastos.Api.Tests/Integracion/UnicidadDeCategoriasTests.cs`
      con los tres casos de la tabla de [data-model.md](./data-model.md#el-índice), escritos contra
      el `DbContext` y no contra endpoints —que todavía no existen—: dos activas de la misma cuenta
      con el mismo nombre y tipo **chocan**; una activa y una dada de baja **no**; dos dadas de baja
      homónimas **tampoco**. Los dos últimos fallan hoy, con un choque del índice
- [X] T003 Agregar `Discriminador` a `backend/GestionGastos.Api/Dominio/Categoria.cs` y rehacer el
      índice en `backend/GestionGastos.Api/Persistencia/GestionGastosDbContext.cs` a `UNIQUE
      (usuario_id, nombre, tipo, discriminador)`, conservando el nombre
      `ux_categoria_ambito_nombre_tipo`
- [X] T004 Generar la migración con `dotnet ef migrations add` y aplicarla. **Si la migración toca
      alguna de las diez filas sembradas, algo se salió del alcance** (D-10). T002 tiene que quedar
      en verde
- [X] T005 [TEST] Agregar a `UnicidadDeCategoriasTests` el caso de SC-005: después de migrar, las
      diez predefinidas siguen con el mismo `id`, el mismo nombre y el mismo tipo, y todas con
      `discriminador = 0`
- [X] T006 [VERIFY] Puerta del backend completa, con su salida

### La barrera del canal de categorías (D-03)

- [X] T007 Crear `backend/GestionGastos.Api/Categorias/CategoriasConsulta.cs` **moviendo** ahí la
      consulta que hoy vive dentro de `CategoriasEndpoints.cs`, sin cambiarle el comportamiento. El
      acotado por ámbito va en un método privado, igual que `DeLaCuenta` en `MovimientosConsulta`
- [X] T008 [TEST] Agregar temporalmente a `CategoriasConsulta.cs` una consulta **deliberadamente sin
      acotar** —`contexto.Categorias` sin `usuario_id`— y correr `dotnet test backend/ --filter
      "FullyQualifiedName~BarreraDeAislamiento"`. **Tiene que quedar en VERDE.** Ese verde es el
      agujero: la barrera no mira la tabla de categorías. Mostrar la salida
- [X] T009 Extender `backend/GestionGastos.Api.Tests/Integracion/BarreraDeAislamientoTests.cs` para
      que vigile **también** `CategoriasConsulta`: los métodos públicos estáticos, la misma
      inspección del SQL, y el mismo grito si alguno no devuelve un `IQueryable`. El predicado del
      ámbito **no** se comparte con el de movimientos —una categoría puede ser de nadie— así que lo
      que se comparte es la vigilancia, no el acotado
- [X] T010 [ROJO] Volver a correr la barrera con la consulta sin acotar todavía puesta. **Ahora
      tiene que quedar en ROJO**, nombrando el método. Mostrar la salida. Recién ahí **quitar** la
      consulta temporal y comprobar el verde
- [X] T011 [TEST] Agregar a `backend/verificar-aislamiento.sh` un **séptimo desarme**: una consulta
      del canal de categorías que deja de acotar por ámbito. Actualizar la cabecera del script, que
      hoy dice "las seis formas", y su lista de motivos
- [X] T012 [ROJO] Correr `./backend/verificar-aislamiento.sh` entero y mostrar su salida. Los siete
      desarmes tienen que dar rojo cada uno y el verde final tiene que volver
- [X] T013 [VERIFY] Puerta del backend completa, con su salida

### La barrera del desglose: la deuda D6-04 (D-05)

- [X] T014 [TEST] Agregarle temporalmente `&& m.Categoria!.Activa` a
      `MovimientosConsulta.Agrupado` y correr `dotnet test backend/` **entero**. **Tiene que quedar
      en VERDE.** Ése es el agujero, y es el que la feature 006 dejó anotado: hoy todas las
      categorías están activas, así que el filtro no cambia ningún número y ningún test lo nota.
      Mostrar la salida
- [X] T015 Crear `backend/GestionGastos.Api.Tests/Integracion/BarreraDelDesgloseTests.cs`: inspecciona
      con `ToQueryString()` el SQL que genera `MovimientosConsulta.Agrupado` y **exige que no nombre
      `activa`**. El mensaje de error tiene que explicar el daño —los totales históricos cambian
      solos— y citar D6-04, no sólo decir que falló
- [X] T016 [ROJO] Correr la barrera con el filtro todavía puesto. **ROJO.** Mostrar la salida.
      Recién ahí **quitar** el filtro y comprobar el verde
- [X] T017 [TEST] Crear `backend/verificar-desglose.sh` —la quinta barrera del proyecto— que le
      agregue el filtro por `activa` a la consulta del resumen, exija el rojo, restaure y exija el
      verde. Bit de ejecución en `100755`, como las otras cuatro
- [X] T018 [ROJO] Correr `./backend/verificar-desglose.sh` entero y mostrar su salida
- [X] T019 [P] Agregar la barrera nueva a la tabla de *Stack* de `AGENTS.md`, con su comando y su
      motivo, junto a las otras cuatro

### La higiene de la suite (Principio IV)

- [X] T020 Ampliar `LimpiarCuentasAsync` en `backend/GestionGastos.Api.Tests/Integracion/` para que
      se lleve **también las categorías propias**. Hoy no las limpia porque no existen; desde esta
      feature, un test que crea una categoría se la deja puesta al siguiente
- [X] T021 [VERIFY] Puerta del backend completa, con su salida

**Checkpoint**: la unicidad convive con la baja, la barrera de aislamiento cubre las categorías y el
desglose tiene quien lo defienda. Recién acá pueden empezar las historias.

---

## Phase 3: User Story 1 - Nombrar mis gastos con mis propias palabras (Priority: P1) 🎯 MVP

**Goal**: crear una categoría propia y que aparezca en el selector, junto a las predefinidas y sin
que la vea nadie más.

**Independent Test**: crear una categoría de gasto desde una cuenta, comprobar que aparece en su
catálogo y no en el de otra, y registrar un movimiento con ella.

- [X] T022 [TEST] [US1] Crear `backend/GestionGastos.Api.Tests/Contrato/ContratoCategoriasTests.cs`
      comparando `CategoriaDto` contra `Categoria` de `frontend/src/api/tipos.ts` en las dos
      direcciones. Falla: falta `esPropia` de los dos lados
- [X] T023 [US1] Agregar `EsPropia` a `backend/GestionGastos.Api/Categorias/CategoriaDto.cs` y
      `esPropia` a `frontend/src/api/tipos.ts`, **en el mismo movimiento** (FR-020). No se exponen
      `activa` ni `usuarioId`, con el motivo escrito ([D-07](./research.md#d-07--el-contrato-gana-un-campo-espropia))
- [X] T024 [TEST] [US1] Crear `backend/GestionGastos.Api.Tests/Integracion/CategoriasPropiasTests.cs`
      con AC-02: una cuenta recién registrada ve las predefinidas de tipo gasto y ninguna de tipo
      ingreso en su selector de gasto, todas con `esPropia: false`. Y el **orden** que fija el
      contrato —por tipo, y dentro de cada tipo por identificador—, que hoy no lo verifica nadie
- [X] T025 [US1] Escribir en `CategoriasConsulta.cs` la consulta del ámbito de una cuenta
      —`(usuario_id IS NULL OR usuario_id = @yo) AND activa`— y hacer que el `GET /api/categorias`
      la use
- [X] T026 [TEST] [US1] AC-01 en `CategoriasPropiasTests`: crear una categoría propia de gasto la
      hace aparecer en el catálogo de esa cuenta, con `esPropia: true`, y **no** en el de otra
- [X] T027 [TEST] [P] [US1] AC-07 y FR-007: se rechaza el nombre repetido contra una **propia** y
      contra una **predefinida**, y la comparación ignora mayúsculas, acentos y espacios al borde
      (`"  supermercado  "` choca contra `"Supermercado"`)
- [X] T028 [TEST] [P] [US1] AC-10: nombre vacío, en blanco y de 51 caracteres se rechazan con la
      clave `nombre`; el de 50 se acepta
- [X] T029 [TEST] [P] [US1] AC-08: dos cuentas distintas crean cada una la misma categoría y las dos
      se aceptan, y cada una ve sólo la suya
- [X] T030 [TEST] [P] [US1] Sin sesión, `POST /api/categorias` responde `401` (RF-03)
- [X] T031 [TEST] [P] [US1] Mismo nombre, **otro tipo** se acepta: la unicidad es por `(nombre, tipo)`
- [X] T032 [US1] Crear `backend/GestionGastos.Api/Categorias/ValidacionDeLaCategoria.cs` con el
      recorte del nombre, el largo y la unicidad contra el ámbito. Misma forma que
      `ValidacionDelMovimiento`: devuelve el diccionario de errores que espera `ValidationProblem`
- [X] T033 [US1] Implementar `POST /api/categorias` en `CategoriasEndpoints.cs`: `201` con
      `Location`, la fila nace con `Activa = true` y `Discriminador = 0`
- [X] T034 [TEST] [US1] Agregar a `ContratoCategoriasTests` la petición del alta (`NuevaCategoria`) y
      su respuesta, en las dos direcciones
- [X] T035 [P] [US1] Declarar `NuevaCategoria` en `frontend/src/api/tipos.ts` y `crearCategoria` en
      `frontend/src/api/cliente.ts`
- [X] T036 [VERIFY] [US1] Puerta del backend y del frontend completas, con su salida

**Checkpoint**: US1 entregable. Los pasos 1, 2, 3 y 8 del [quickstart](./quickstart.md) ya se
recorren.

---

## Phase 4: User Story 2 - Corregir un nombre sin perder la historia (Priority: P2)

**Goal**: renombrar una categoría propia y que el nombre nuevo aparezca donde ya se usaba.

**Independent Test**: renombrar una categoría con movimientos y comprobar el nombre nuevo en el
listado y en el desglose del resumen.

- [X] T037 [TEST] [US2] AC-04 en `CategoriasPropiasTests`: renombrar una categoría propia con
      movimientos cambia el nombre en el listado **y** en el desglose del resumen, sin tocar ningún
      movimiento. Es la prueba de que el movimiento guarda el identificador y no el nombre
- [X] T038 [TEST] [P] [US2] Clarificación 1: el renombre valida la misma unicidad que el alta
      —contra propias y contra predefinidas—, y **no choca consigo misma**: renombrar "Gimnasio" a
      "Gimnasio" no es un error
- [X] T039 [TEST] [P] [US2] AC-03: `PUT` sobre una categoría **predefinida** responde `403` y la deja
      con el mismo nombre y el mismo tipo. **No** es `404`: la persona la está viendo
      ([D-06](./research.md#d-06--qué-responde-cada-rechazo))
- [X] T040 [TEST] [P] [US2] AC-11 y FR-013: `PUT` sobre una categoría propia de **otra** cuenta
      responde `404` **con el mismo cuerpo** que un identificador inexistente, y la deja sin cambios
- [X] T041 [TEST] [P] [US2] AC-10 en el renombre: nombre vacío o de más de 50 se rechaza y la
      categoría queda como estaba
- [X] T042 [US2] Implementar `PUT /api/categorias/{id}` reusando `ValidacionDeLaCategoria` entera.
      **El tipo no viaja en la petición**: cambiarlo movería de tipo a los movimientos que la usan
- [X] T043 [TEST] [US2] Agregar el `PUT` a `ContratoCategoriasTests`, y declarar `CategoriaEditada`
      en `tipos.ts` y `renombrarCategoria` en `cliente.ts`
- [X] T044 [VERIFY] [US2] Puerta del backend y del frontend completas, con su salida

**Checkpoint**: US2 entregable. Pasos 4 y 5 del quickstart.

---

## Phase 5: User Story 3 - Dejar de usar una categoría sin borrar el pasado (Priority: P3)

**Goal**: dar de baja una categoría sin que la historia se mueva ni un peso.

**Independent Test**: dar de baja una categoría con movimientos y comprobar dos cosas opuestas a la
vez: desapareció del selector, y ningún número del resumen cambió.

- [X] T045 [TEST] [US3] AC-05 en `CategoriasPropiasTests`: después de la baja, la categoría **no**
      está en `GET /api/categorias` y **sí** sigue nombrando sus movimientos en el listado
- [X] T046 [TEST] [US3] **AC-06 y FR-011, el test que sostiene la feature**: se guarda el resumen
      entero antes de la baja, se da de baja una categoría con movimientos, y el resumen después
      tiene que ser **idéntico** —totales, balance y el monto de esa categoría en el desglose—. Se
      compara el documento completo, no campo por campo: un campo que nadie mira es por donde se
      escapa la diferencia
- [X] T047 [TEST] [P] [US3] AC-09: crear una categoría con el mismo nombre y tipo que una dada de
      baja se **acepta**, la nueva tiene otro `id`, y el movimiento viejo sigue apuntando al viejo.
      Es la razón de existir de `discriminador`
- [X] T048 [TEST] [P] [US3] La baja es idempotente: dos `DELETE` seguidos devuelven `204` los dos
- [X] T049 [TEST] [P] [US3] AC-03 y AC-11 en el `DELETE`: `403` sobre una predefinida, `404` sobre
      una de otra cuenta, con el mismo cuerpo que un id inexistente
- [X] T050 [US3] Implementar `DELETE /api/categorias/{id}`: apaga `Activa` y escribe
      `Discriminador = Id` **en el mismo `UPDATE`**. `204` sin cuerpo
- [X] T051 [TEST] [US3] FR-023 en `ValidacionMovimientoTests`: editar un movimiento **sin cambiarle
      la categoría** se acepta aunque esa categoría esté dada de baja, y moverlo a **otra** dada de
      baja se rechaza. **El test existente `Rechaza_Una_Categoria_Dada_De_Baja` no se toca**: cubre
      el alta, y el alta no cambia (FR-022)
- [X] T052 [US3] Agregar la condición a la edición en
      `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs`: válida si está activa **o**
      si es la que ese movimiento ya tenía. Escribirlo como "la edición no filtra por activa" sería
      más corto y estaría mal ([D-04](./research.md#d-04--fr-021-ya-está-implementado-y-lo-que-hay-que-hacer-es-no-romperlo))
- [X] T053 [TEST] [US3] Agregar el `DELETE` a `ContratoCategoriasTests` y `darDeBajaCategoria` a
      `cliente.ts`
- [X] T054 [US3] Crear `backend/GestionGastos.Api.Tests/Integracion/AislamientoDeCategoriasTests.cs`
      con el caso de acceso cruzado de los **cuatro** endpoints, que es lo que FR-014 y NFR-01 piden
      al 100 %. Los casos sueltos ya están en T029, T040 y T049; acá se reúnen para que la cobertura
      se pueda afirmar mirando un archivo
- [X] T055 [TEST] [US3] FR-021 y SC-009 en `AislamientoDeCategoriasTests`: una cuenta **no** puede
      registrar ni editar un movimiento apuntando a una categoría **propia de otra cuenta**, y el
      rechazo no dice si esa categoría existe. Esto ya funciona desde FEAT-001b y `ValidacionMovimientoTests`
      lo defiende, pero **ese test arma la categoría ajena a mano porque no existían las propias**:
      esta feature es la que las vuelve reales, así que es el ticket que introduce el riesgo y tiene
      que ser el que lo mire. Se escribe con una categoría creada por la otra cuenta a través del
      endpoint, no insertada a mano
- [X] T056 [VERIFY] [US3] Puerta del backend completa, con su salida

**Checkpoint**: el backend entero está. Pasos 6 y 7 del quickstart, que son los que más importan.

---

## Phase 6: User Story 4 - Un solo catálogo en pantalla (Priority: P4)

**Goal**: que las tres historias anteriores se puedan usar desde la aplicación.

**Independent Test**: cargar la pantalla y contar las peticiones al catálogo; crear una categoría en
la pantalla de gestión, volver, y verla en el selector sin recargar.

- [X] T057 [TEST] [US4] AC-12 en `frontend/src/App.test.tsx`: al cargar la pantalla principal,
      `/api/categorias` se pide **exactamente una vez**
- [X] T058 [US4] Subir el catálogo de `PantallaMovimientos` a `frontend/src/App.tsx` y bajarlo por
      props, junto con las funciones que lo modifican
      ([D-08](./research.md#d-08--el-catálogo-sube-a-apptsx)). `FormularioMovimiento` no cambia de
      forma: sigue recibiendo `categorias` por props y no ve de dónde vienen
- [X] T059 [TEST] [US4] `frontend/src/categorias/PantallaCategorias.test.tsx`: lista las propias y
      las predefinidas, y **no ofrece renombrar ni dar de baja** las que tienen `esPropia: false`
      (AC-03 en la pantalla)
- [X] T060 [US4] Crear `frontend/src/categorias/PantallaCategorias.tsx` con el alta, el renombre y la
      baja. Estados de carga y de error como en `PantallaMovimientos`, y **nunca un catch silencioso**
- [X] T061 [TEST] [US4] AC-13 y FR-019: crear y renombrar desde la pantalla de gestión se refleja en
      el selector del formulario al volver, **sin recargar** y **sin una segunda petición** del
      catálogo
- [X] T062 [US4] Agregar la vista de categorías al estado de `App.tsx`, con el mismo mecanismo que ya
      alterna login ↔ movimientos. **Sin dependencias nuevas** (FR-018, y la regla de `AGENTS.md`)
- [X] T063 [TEST] [US4] El edge case de la spec: si la categoría elegida en el formulario de alta se
      da de baja, el selector la saca de la selección en vez de dejar que la persona choque contra
      un error que no puede entender (FR-022)
- [X] T064 [US4] Ajustar `FormularioMovimiento` para T063
- [X] T065 [VERIFY] [US4] Puerta del frontend completa —`lint`, `tsc --noEmit`, `test`— y build de
      producción, con su salida

---

## Phase 7: Cierre

- [X] T066 Recorrer el [quickstart](./quickstart.md) entero, los ocho pasos, y anotar cualquier línea
      que no haga lo que el documento dice
- [X] T067 [VERIFY] Las **cinco** barreras: `verificar-contrato.sh`, `verificar-autorizacion.sh`,
      `verificar-aislamiento.sh`, `verificar-linter.sh` y `verificar-desglose.sh`. Con su salida
- [X] T068 Cobertura del backend con `backend/cobertura.runsettings`, con su salida
- [X] T069 [P] Actualizar `plan-de-implementacion/README.md`: el ticket 3 pasa a la tabla de
      implementados, con lo que lo demuestra en el código
- [X] T070 [P] Cerrar la deuda **D6-04** en la tabla de *Deuda registrada* de
      `specs/006-resumen-del-mes/spec.md`, nombrando el test y la barrera que la saldan
- [X] T071 [P] Anotar en la *Deuda registrada* de [spec.md](./spec.md) lo que esta feature no dejó
      hecho, y las desviaciones de proceso que haya habido, si las hubo
- [X] T072 [VERIFY] Puerta completa de los dos stacks, con su salida. **Es también lo único que
      puede afirmar SC-008** —que el comportamiento no cambió para una cuenta que no usa categorías
      propias—: no hay un test que lo mida, lo mide que los 190 anteriores sigan en verde

---

## Dependencies

### Entre fases

- **Fase 2 bloquea a todas.** Sin la migración, FR-009 no se puede testear; sin las barreras, se
  escriben mirando código que ya está bien.
- **US1 → US2 → US3**: no se puede renombrar lo que no se puede crear, ni dar de baja lo que no se
  puede renombrar. Y US3 necesita a US1 para tener qué dar de baja.
- **US4 depende de las tres**, porque la pantalla las usa. Pero **T057 y T058 se pueden hacer en
  paralelo con el backend**: subir el catálogo a `App.tsx` no depende de que existan los endpoints
  nuevos.

### Particulares de esta feature

- **T009 depende de T008**: extender la barrera sin haber visto el agujero en verde es arreglar algo
  que nadie vio roto.
- **T015 depende de T014**, por lo mismo, y el de T014 es el agujero más silencioso de los dos: la
  suite entera queda en verde con el filtro puesto.
- **T004 depende de T002**: la migración se escribe con el test de unicidad ya en rojo.
- **T042 depende de T032**: el renombre reusa la validación del alta entera; si hay que escribirla
  dos veces, la primera quedó mal factorizada.
- **T052 depende de T050**: hasta que no se pueda dar de baja, no hay forma de armarle el escenario
  a FR-023.
- **T070 depende de T046**: la deuda de la feature 006 se marca saldada nombrando el test que la
  salda, no antes.

### Oportunidades de paralelismo

- **Fase 2**: las dos barreras (T007-T013 y T014-T019) son archivos distintos, si hay dos personas.
  La migración no: bloquea a las dos.
- **US1**: T027 a T031 en paralelo una vez que T026 dejó el archivo con su andamio.
- **US2**: T038 a T041 en paralelo después de T037.
- **US3**: T047, T048 y T049 en paralelo después de T046.
- **Fase 7**: T069, T070 y T071 son archivos distintos.

---

## Implementation Strategy

### MVP (Fase 2 + US1)

1. Fases 1 y 2 completas — la migración puesta y las dos barreras sabiendo fallar.
2. Fase 3 — crear una categoría propia y verla en el catálogo, aislada de las otras cuentas.
3. **PARAR Y VALIDAR**: los pasos 1, 2, 3 y 8 del quickstart ya se recorren.

Con eso el catálogo dejó de ser fijo, que es la mitad de la promesa del ticket.

### Entrega incremental

1. Fase 2 → el piso.
2. US1 → crear. Demostrable.
3. US2 → renombrar sin perder la historia. Demostrable.
4. US3 → dar de baja sin mover un número. Demostrable, y es la que más cuidado necesita.
5. US4 → la pantalla, que es lo que vuelve usable a las tres.
6. Fase 7 → las cinco barreras, la cobertura y la deuda D6-04 saldada.

---

## Notes

- `[P]` = archivos distintos, sin dependencias entre sí.
- **Ningún test se ajusta para que pase.** En particular T051: si
  `Rechaza_Una_Categoria_Dada_De_Baja` se pone en rojo, es que el cambio de la edición se filtró al
  alta, y eso es un error del código, no del test.
- **Los tests que crean categorías las limpian antes y después** (T020, Principio IV). La base la
  comparte toda la suite y una corrida interrumpida deja la fila puesta.
- **No se commitea con la puerta en rojo**, y no se abre PR.
- Si aparece una segunda migración, algo se salió del alcance
  ([D-10](./research.md#d-10--la-migración-y-lo-que-tiene-que-sobrevivir)).

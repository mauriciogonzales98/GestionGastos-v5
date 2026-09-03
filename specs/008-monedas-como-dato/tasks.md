---

description: "Task list for 008-monedas-como-dato"
---

# Tasks: Monedas administrables como dato

**Input**: Design documents from `/specs/008-monedas-como-dato/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/monedas.md](./contracts/monedas.md)

**Tests**: obligatorios. No es una opción de esta feature: el Principio I de
`.specify/memory/constitution.md` prohíbe escribir código sin un test que ya haya fallado. Y acá el
punto es más fuerte todavía — **esta feature no entrega nada más que verificaciones**, así que un
test que no se vio fallar no deja nada.

**Organization**: por historia de usuario, en orden de prioridad.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[TEST]**: escribe una verificación · **[ROJO]**: la corre y exige que falle · **[VERIFY]**: puerta
- **[Story]**: US1, US2, US3

---

## Lo que hace rara a esta lista, y conviene leer antes de empezar

**Casi todo lo que estas tareas verifican ya funciona.** El catálogo existe, el resumen ya separa
por moneda, y con una sola moneda la salida no va a cambiar en ningún momento de esta feature. Eso
rompe el rojo-verde de siempre: un test escrito contra código que ya cumple **pasa en el primer
intento**, y un test que nunca falló no prueba que observa lo que dice observar.

La salida no es saltearse el rojo: es **producirlo quitando la propiedad**, que es la forma que el
proyecto ya usa en sus cinco barreras. Cada `[ROJO]` de esta lista dice exactamente qué se rompe a
propósito, y todas esas roturas se restauran en la misma tarea.

**Ningún archivo de `backend/GestionGastos.Api/` queda modificado al final** ([D-04](./research.md)).
Dos tareas lo tocan de forma temporal para producir un rojo, y las dos lo restauran. Si al cerrar la
feature `git status` muestra algo ahí adentro, algo salió mal.

---

## Phase 1: Setup

**Purpose**: saber de qué verde se parte, para que cualquier rojo posterior sea atribuible.

- [ ] T001 Correr la puerta del backend completa sobre la rama recién sacada —`dotnet format --verify-no-changes`, `dotnet build -warnaserror`, `dotnet test backend/`— y anotar el conteo de tests. Es la línea de base: sin ella, un rojo del primer día se confunde con uno heredado

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: el archivo de tests y, sobre todo, **la limpieza de la moneda que esos tests crean**.

**⚠️ CRÍTICO**: ninguna historia puede empezar antes de esto. `moneda` es una tabla que
`LimpiarCuentasAsync` **no** toca ([D-05](./research.md)), así que la primera moneda que un test cree
y no borre se le queda al siguiente. Se resuelve antes de crear ninguna.

- [ ] T002 [TEST] Crear `backend/GestionGastos.Api.Tests/Integracion/MonedaComoDatoTests.cs` con un primer caso que afirme que el catálogo tiene **exactamente dos** monedas, `ARS` y `USD`. Es el canario: cualquier moneda que sobreviva a un test lo pone en rojo
- [ ] T003 [ROJO] Agregar temporalmente a ese archivo un segundo caso que inserte una moneda **y no la borre**, y correr los dos con `dotnet test --filter "FullyQualifiedName~MonedaComoDato"`. **ROJO en T002**, y el rojo es el daño de D-05 ocurriendo de verdad. Mostrar la salida
- [ ] T004 Escribir en `MonedaComoDatoTests` el helper que agrega una moneda y la borra al terminar, con `try`/`finally` para que la limpieza corra también cuando el caso falla. Pasar el caso de T003 a usarlo. **Verde.** Documentar por qué la limpieza NO va en `LimpiarCuentasAsync`: ahí borraría las dos monedas sembradas para toda la suite, el mismo error que ese método ya evita en categorías filtrando por `usuario_id != null`
- [ ] T005 [VERIFY] Puerta del backend completa, con su salida

**Checkpoint**: se pueden crear monedas en los tests sin envenenar la suite.

---

## Phase 3: User Story 1 — Sumar una moneda sin tocar la aplicación (Priority: P1) 🎯 MVP

**Goal**: convertir "se puede agregar una moneda sin tocar código" de creencia en hecho ejecutado.

**Independent Test**: se agrega una moneda al catálogo sólo como dato, aparece en el resumen, se
puede registrar un movimiento con ella, y ni el ensamblado ni el árbol de fuentes cambiaron.

### Tests

- [ ] T006 [TEST] [P] [US1] AC-03 y FR-003 en `MonedaComoDatoTests`: el catálogo migrado tiene `ARS` y `USD`, y **exactamente una** marcada como predeterminada. El test cuenta las predeterminadas, no comprueba cuál es: que sea `ARS` es semilla, que haya una sola es la invariante (FR-004)
- [ ] T007 [TEST] [US1] AC-01 y FR-002 en `MonedaComoDatoTests`: agregada una moneda al catálogo con el helper de T004, `GET /api/resumen` devuelve **una entrada más**, con sus tres totales y su desglose en cero, y al final de la lista
- [ ] T008 [ROJO] [US1] El rojo de T007, producido quitando la propiedad: reemplazar temporalmente en `backend/GestionGastos.Api/Resumenes/CalculoDelResumen.cs` la lectura del catálogo por una lista fija de dos monedas, correr T007 y exigir **ROJO**. Restaurar el archivo y exigir el verde. Es lo que prueba que el test mira el catálogo y no una casualidad. Mostrar las dos salidas
- [ ] T009 [TEST] [US1] AC-02 y FR-001 en `MonedaComoDatoTests`: mover la predeterminada a la moneda nueva **con dos sentencias, apagar y después prender** ([D-02](./research.md)), registrar un gasto por `POST /api/movimientos`, y comprobar que suma en los totales de esa moneda y en los de ninguna otra. Comentar por qué van dos sentencias: una sola puede violar `ux_moneda_unica_predeterminada` según el orden en que el motor toque las filas
- [ ] T010 [ROJO] [US1] Correr T009 **sin** mover la predeterminada. **ROJO**: el movimiento cae en `ARS`. Es lo que distingue "la moneda nueva se usa" de "se registró algo en alguna moneda". Mostrar la salida

### La barrera

- [ ] T011 [US1] Crear `backend/verificar-monedas.sh` —la **sexta** barrera del proyecto—: compila una vez, calcula el hash del ensamblado y del árbol de fuentes de `backend/GestionGastos.Api/`, agrega la moneda con SQL puro por el cliente `mysql`, corre `dotnet test --no-build --filter "FullyQualifiedName~MonedaComoDato"`, y exige que los dos hashes sigan iguales. Restaura el catálogo con un `trap`, como las otras cuatro que escriben. Bit de ejecución en `100755`
- [ ] T012 [ROJO] [US1] Ver fallar al script por su propia vía: forzar una recompilación entre los dos hashes y exigir que **lo detecte y salga en rojo**. Es el Principio V aplicado a sí mismo — un script que nunca falló no verifica nada. Restaurar y exigir el verde. Mostrar las dos salidas
- [ ] T013 [ROJO] [US1] El segundo desarme: tocar una línea de un archivo de `backend/GestionGastos.Api/` y exigir que el hash del árbol de fuentes lo detecte. Es la mitad "0 líneas de código modificadas", que el hash del ensamblado solo no cubre —un comentario no cambia el binario—. Restaurar y exigir el verde
- [ ] T014 [P] [US1] Agregar la barrera a la tabla de *Stack* de `AGENTS.md`, con su comando, qué comprueba y cuánto tarda, en la forma de las otras cinco
- [ ] T015 [P] [US1] Agregar el paso `Barrera de monedas` a `.github/workflows/ci.yml`, junto a las otras barreras y antes de `verificar-linter.sh` ([D-06](./research.md))
- [ ] T016 [VERIFY] [US1] Puerta del backend completa **más `./backend/verificar-monedas.sh` entero**, con su salida

**Checkpoint**: RF-032 deja de ser una intención. US1 sola ya entrega el valor del ticket.

---

## Phase 4: User Story 2 — Los totales siguen separados con volumen (Priority: P2)

**Goal**: ejercitar la separación por moneda en la condición que la puede romper.

**Independent Test**: 1000 movimientos repartidos en dos monedas, p95 bajo 2 s, y ningún total con
un centavo de la otra moneda.

- [ ] T017 [TEST] [P] [US2] AC-05 y FR-006 en `MonedaComoDatoTests`: una cuenta con gastos en dos monedas dentro de la **misma** categoría y período; el desglose la muestra una vez por moneda, con el total de esa moneda, y ninguno incluye montos de la otra
- [ ] T018 [TEST] [P] [US2] AC-06 y FR-005 en `MonedaComoDatoTests`: ingresos y gastos en las dos monedas; el balance de cada una es sus ingresos menos sus gastos, sin cruzar nada
- [ ] T019 [ROJO] [US2] El rojo de T017 y T018, producido quitando la propiedad: sacar temporalmente `MonedaId` del `GROUP BY` de `MovimientosConsulta.Agrupado` y exigir **ROJO** en los dos. Restaurar y exigir el verde. Es la comprobación de que los dos tests miran la separación y no dos totales que casualmente dan bien. Mostrar las salidas
- [ ] T020 [TEST] [US2] AC-04 y FR-011: agregar a `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoResumenTests.cs` un caso que siembre 1000 movimientos repartidos en **dos** monedas y exija p95 < 2 s. **El caso existente de una sola moneda se deja intacto**: es la referencia que permite atribuir un rojo al costo de la segunda moneda y no al volumen ([D-03](./research.md))
- [ ] T021 [ROJO] [US2] Correr el caso nuevo con el sembrado forzado a una sola moneda y comprobar que el guardarraíl de filas —el que ya exige que el sembrado haya caído en el mes medido— se pueda extender a exigir **dos** monedas sembradas. **ROJO** con una sola. Sin esto, un sembrado que dejó de repartir mide lo mismo que el caso viejo y pasa en verde sin medir nada nuevo
- [ ] T022 [VERIFY] [US2] Puerta del backend completa, más `dotnet test --filter "FullyQualifiedName~RendimientoResumen"` con los dos casos y sus números a la vista

**Checkpoint**: la separación por moneda está medida, no supuesta.

---

## Phase 5: User Story 3 — Lo que ya funcionaba sigue funcionando (Priority: P3)

**Goal**: la red de seguridad. Con una sola moneda, nada cambió.

**Independent Test**: la salida del resumen con una moneda es la de antes de esta feature.

- [ ] T023 [TEST] [P] [US3] AC-08 y FR-009 en `MonedaComoDatoTests`: una moneda del catálogo **sin** movimientos en el período aparece igual, con totales, balance y desglose en cero, y sin ningún error. Citar en el comentario que esto es el AC-31 de la feature 006 conservado a propósito, y que **contradice el AC-07/AC-08 del PRD de la 4a** por la decisión registrada en D8-04
- [ ] T024 [TEST] [P] [US3] AC-09 en `MonedaComoDatoTests`: una cuenta sin ningún movimiento en el período devuelve una entrada en cero por **cada** moneda del catálogo, y ningún error
- [ ] T025 [US3] AC-07 y FR-008: comprobar que los tests del resumen de la feature 006 siguen en verde sin haber sido modificados, y **no duplicarlos**. Anotar en `MonedaComoDatoTests` cuáles son los que cubren esta regresión, para que quien lea sepa dónde está verificada
- [ ] T026 [VERIFY] [US3] Puerta del backend completa, con su salida

**Checkpoint**: las tres historias cerradas.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T027 Recorrer el [quickstart](./quickstart.md) entero, los siete pasos más la medición, y anotar cualquier línea que no haya salido como dice. Un quickstart que nadie ejecutó es documentación que envejece sin avisar
- [ ] T028 [VERIFY] Las **seis** barreras: `verificar-contrato.sh`, `verificar-autorizacion.sh`, `verificar-desglose.sh`, `verificar-monedas.sh`, `verificar-aislamiento.sh` y `verificar-linter.sh`, con su salida. `verificar-contrato.sh` en verde es, además, la prueba de que el contrato del resumen no cambió, que es lo que FR-009 pide
- [ ] T029 Cobertura del backend con `backend/cobertura.runsettings`, con su salida
- [ ] T030 [P] Confirmar que `git status backend/GestionGastos.Api/` está **vacío**. Es el criterio de D-04 hecho comprobación: el trabajo de esta feature es aditivo, y las dos tareas que tocaron producción para producir un rojo restauraron lo que tocaron
- [ ] T031 [P] Actualizar `plan-de-implementacion/README.md`: el ticket 12 (4a) pasa a la tabla de implementados, con la nota de que la mayor parte ya venía de FEAT-001a y FEAT-001c y que esta feature la verificó
- [ ] T032 [P] Anotar en la *Deuda registrada* de [spec.md](./spec.md) lo que esta feature no dejó hecho, si aparece algo nuevo durante la implementación. D8-01 a D8-06 ya están; esto es para lo que se descubra
- [ ] T033 [VERIFY] Puerta completa de los dos stacks, con su salida. El frontend no se tocó, y correrlo igual es lo que lo demuestra

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: sin dependencias
- **Foundational (T002–T005)**: depende de Setup. **Bloquea todo**: sin la limpieza de T004, cualquier test que cree una moneda envenena la suite
- **US1 (T006–T016)**: depende de Foundational
- **US2 (T017–T022)**: depende de Foundational. **No depende de US1**
- **US3 (T023–T026)**: depende de Foundational. **No depende de US1 ni de US2**
- **Polish (T027–T033)**: depende de las tres historias

### Dentro de cada historia

- El `[TEST]` antes que su `[ROJO]`, y el `[ROJO]` antes de darlo por bueno
- T011 (el script) después de T007 y T009: el script corre esos tests, así que tienen que existir
- T012 y T013 después de T011, y los dos antes de T014 y T015: no se documenta ni se pone en CI una barrera que no se vio fallar
- T020 después de T017 y T018: primero que la separación sea correcta, después que sea rápida

### Parallel Opportunities

- T014 y T015 son dos archivos distintos, `AGENTS.md` y `ci.yml`
- T017 y T018 son dos casos independientes del mismo archivo: se pueden escribir a la vez, no correr a la vez
- T023 y T024, lo mismo
- T030, T031 y T032 tocan tres archivos distintos
- **Las tres historias son independientes entre sí**: con equipo, US1, US2 y US3 arrancan juntas apenas cierre la fase Foundational

---

## Parallel Example: US1

```bash
# T014 y T015, en paralelo — archivos distintos, sin dependencia entre ellos:
Task: "Agregar la barrera a la tabla de Stack de AGENTS.md"
Task: "Agregar el paso Barrera de monedas a .github/workflows/ci.yml"
```

---

## Implementation Strategy

### MVP: sólo US1

1. T001 → Setup
2. T002–T005 → Foundational (**crítico**: la limpieza)
3. T006–T016 → US1
4. **PARAR Y VALIDAR**: `./backend/verificar-monedas.sh` en verde, y visto fallar por sus dos vías

Con eso sólo, el ticket ya entrega lo suyo: RF-032 verificado de punta a punta. US2 mide y US3 cuida
las espaldas, pero la promesa del ticket está cumplida al cerrar US1.

### Entrega incremental

1. Setup + Foundational → se pueden crear monedas sin romper la suite
2. **US1 → la promesa verificada (MVP)**
3. US2 → medida bajo volumen
4. US3 → regresión cubierta
5. Polish → quickstart, seis barreras, cobertura y el diff limpio de producción

---

## Notes

- `[P]` = archivos distintos, sin dependencias
- **Cada `[ROJO]` restaura lo que rompió, en la misma tarea.** Ninguno queda pendiente para después
- Commit por tarea o por grupo lógico; **nunca con la puerta en rojo**
- **El criterio de cierre menos habitual de esta feature**: `git status backend/GestionGastos.Api/` vacío. Es lo que separa "verificamos la propiedad" de "la conseguimos cambiando el código"

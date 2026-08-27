---

description: "Task list — Aislamiento entre cuentas verificado"
---

# Tasks: Aislamiento entre cuentas verificado

**Input**: Design documents from `/specs/004-aislamiento-cuentas/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: **obligatorios, y son el entregable**. Esta feature no agrega conducta: lo único que
produce son tests y una barrera. El Principio II exige que cada AC tenga un test que lo nombre.

**Organization**: agrupadas por historia. US1 verifica la lectura, US2 la escritura, US3 pone la
barrera que protege a las dos. Ese orden importa: la barrera de US3 vigila exactamente la condición
que US1 y US2 dejan verificada, así que antes de ellas no tendría qué proteger.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede correr en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1, US2, US3)
- **[TEST]**: tarea de test
- **[ROJO]**: tarea de **desarme deliberado**. Rompe a propósito lo que el test protege, muestra el
  rojo y restaura. Es lo que reemplaza al rojo espontáneo en esta feature
- **[VERIFY]**: puerta del grupo. Se corren los comandos de `AGENTS.md` con su salida a la vista

## Path Conventions

Web app: `backend/` y `frontend/`, como fija `AGENTS.md`. **Esta feature no toca `frontend/`**; su
puerta se corre igual antes de cerrar, para comprobar justamente eso.

---

## ⚠️ Lo que hace rara a esta feature, y cómo se trabaja

Casi todos los tests de acá **nacen en verde**: verifican comportamiento que ya existe. Un test de
aislamiento roto se ve exactamente igual que uno que funciona, así que "pasó" no significa nada por
sí solo.

Por eso cada grupo de tests lleva su tarea `[ROJO]` inmediatamente después: se rompe a propósito lo
que el test dice proteger, se **muestra la salida en rojo**, y se restaura. Una tarea `[TEST]` cuya
`[ROJO]` no pudo mostrar el rojo no está terminada — significa que el test no verifica lo que dice.

Es la forma que toma el Principio I cuando lo que se construye es verificación de algo heredado.

---

## Phase 1: Setup

**Purpose**: arrancar desde una base conocida. No hay nada que instalar: esta feature no agrega
dependencias, no toca el esquema y no genera migración.

- [X] T001 Correr la puerta del backend sobre `main` reciente —`dotnet format --verify-no-changes`,
  `dotnet build -warnaserror`, `dotnet test backend/`— y dejar su salida a la vista. Es el verde de
  partida: sin él, un rojo de esta feature no se distingue de un rojo heredado

---

## Phase 2: Foundational (bloquea a US1 y US2)

**Purpose**: dos cuentas de verdad, con datos que se parecen. Es la infraestructura de la que
dependen todos los escenarios cruzados.

**⚠️ CRÍTICO**: acá se decide si esta feature verifica algo o no. Las tres formas de que un test de
aislamiento pase en verde sin probar nada —las dos cuentas terminan siendo la misma, la otra cuenta
no tiene movimientos, los datos no se parecen— se cierran en esta fase o no se cierran nunca
([D-06](./research.md)).

- [X] T002 Crear `backend/GestionGastos.Api.Tests/Integracion/AislamientoEntreCuentasTests.cs` con
  su andamio: la colección de base de datos, el reloj fijo con `FactoriaConReloj`
  ([D-07](./research.md)), y un helper `DosCuentasConMovimientosAsync` que cree **dos** cuentas con
  `CuentaDePrueba.CrearYEntrarAsync` y siembre en cada una movimientos **de la misma fecha y la
  misma categoría, con montos distintos**. Que los datos se parezcan es el punto: si difieren en
  fecha o categoría, el aislamiento lo puede estar haciendo la casualidad y el test no lo distingue
- [X] T003 Agregar en ese helper la comprobación de que las dos cuentas son **realmente dos**:
  afirmar que sus `Id` diferen y que las dos tienen al menos un movimiento propio. Sin esto, un
  fixture que reuse una cuenta deja toda la suite de aislamiento pasando en verde sin verificar nada
- [X] T004 [VERIFY] Puerta del backend: `dotnet format --verify-no-changes`, `dotnet build
  -warnaserror`, `dotnet test backend/`

**Checkpoint**: hay dos cuentas distintas, cada una con movimientos propios indistinguibles de los
de la otra salvo por el dueño. Los escenarios cruzados pueden empezar.

---

## Phase 3: User Story 1 - Nadie ve el dinero de otro (Priority: P1) 🎯 MVP

**Goal**: el listado de cada cuenta devuelve exactamente sus movimientos y ninguno de la otra.

**Independent Test**: crear dos cuentas, registrar movimientos con cada una, y comprobar que el
listado de cada una devuelve los propios y ninguno ajenos.

### Tests de la historia 1

- [X] T005 [TEST] [US1] Agregar en `AislamientoEntreCuentasTests.cs` el test de **AC-01**: con dos
  cuentas que tienen movimientos propios en el mes en curso, el listado de cada una devuelve
  únicamente los suyos. Comprobar por **identificador**, no por cantidad: dos listados de largo 1
  son iguales de largo aunque el movimiento sea el equivocado
- [X] T006 [TEST] [P] [US1] Agregar el test del listado vacío: una cuenta **sin** movimientos
  propios, con la otra teniendo varios en el mes, recibe un arreglo vacío. Es el caso que distingue
  "acota por cuenta" de "devuelve lo que haya"
- [X] T007 [ROJO] [US1] Quitar `m.UsuarioId == usuarioId` de
  `backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs`, correr T005 y T006, y **mostrar
  el rojo**. Restaurar y comprobar el verde. Si alguno de los dos pasa en verde con el acotado
  quitado, ese test no verifica el aislamiento y hay que arreglarlo antes de seguir
- [X] T008 [VERIFY] [US1] Puerta del backend completa

**Checkpoint**: el aislamiento de la lectura está verificado con dos cuentas reales, y se le vio el
rojo. Es el MVP: entregado solo, ya convierte la propiedad heredada en una comprobada.

---

## Phase 4: User Story 2 - Lo que registro queda a mi nombre (Priority: P2)

**Goal**: el dueño de un movimiento lo decide la sesión, nunca el cuerpo de la petición.

**Independent Test**: registrar un movimiento desde una cuenta indicando a otra como propietaria en
el cuerpo, y comprobar que aparece en el listado de quien lo registró y no en el de la otra.

### Tests de la historia 2

- [ ] T009 [TEST] [US2] Agregar en `AislamientoEntreCuentasTests.cs` el test de **AC-06**: la cuenta
  A registra un movimiento con `"usuarioId": <id de B>` en el cuerpo; el movimiento aparece en el
  listado de **A** y el listado de B no cambia. Mandar el campo aunque el contrato del alta no lo
  tenga: hoy se descarta al deserializar, y el test tiene que seguir valiendo el día que
  `NuevoMovimientoDto` gane un campo ([INV-03](./data-model.md))
- [ ] T010 [TEST] [US2] Agregar el test de **AC-08**: leer el listado de B **antes y después** de
  que A registre un movimiento, y comprobar que es idéntico campo por campo. Se comprueba sobre la
  **otra** cuenta, no sobre la que operó: es lo que distingue "mi listado está bien" de "el suyo no
  cambió"
- [ ] T011 [ROJO] [US2] Reemplazar `UsuarioId = usuarioActual.Id` por el id de otra cuenta en
  `backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs`, correr T009 y T010, y **mostrar
  el rojo**. Restaurar y comprobar el verde
- [ ] T012 [VERIFY] [US2] Puerta del backend completa

**Checkpoint**: las dos mitades del aislamiento —leer y escribir— están verificadas. Se cubrió el
100 % de la superficie que existe: 2 de 2 endpoints (**AC-09**, reescalado).

---

## Phase 5: User Story 3 - Desarmar el aislamiento hace ruido (Priority: P3)

**Goal**: las tres formas de desarmar el aislamiento —la consulta que deja de acotar, la lectura
que nace fuera del canal, y el alta que asigna un propietario ajeno— ponen la suite en rojo.

**Independent Test**: desarmar el aislamiento de cada una de las tres formas y comprobar que la
suite se pone en rojo; restaurar y comprobar que vuelve al verde.

> **Por qué esta historia existe, si US1 y US2 ya detectan el desarme.** Por dos motivos distintos.
> Lo que US1 y US2 **no** detectan es una consulta *nueva* que nadie acote: no saben que existe, y
> por eso la barrera vigila el canal y no sólo la condición ([D-04](./research.md)). Y lo que sí
> detectan —T007 y T011 lo demuestran— lo demuestran **una sola vez**, el día que se escriben. El
> script lo vuelve a comprobar en cada corrida, que es lo que atrapa al test debilitado sin querer.

### Tests de la historia 3

- [ ] T013 [TEST] [US3] Crear `backend/GestionGastos.Api.Tests/Integracion/BarreraDeAislamientoTests.cs`
  con el test del SQL: `MovimientosConsulta.DelMes(...).ToQueryString()` tiene que nombrar
  `usuario_id` en su `WHERE`. Mismo patrón y mismo motivo que
  `La_Consulta_Pide_El_Orden_Explicitamente_Y_No_Lo_Hereda_Del_Indice`, que ya existe en
  `ListadoMovimientosTests.cs`: mirar el resultado no alcanza para saber qué pidió la consulta
- [ ] T014 [TEST] [US3] Agregar en ese archivo el test del **canal único**: ningún archivo de
  `backend/GestionGastos.Api/` fuera de `Movimientos/MovimientosConsulta.cs` puede **leer**
  `contexto.Movimientos`. La escritura de `MovimientosEndpoints.cs` es la excepción declarada, y
  tiene que estar nombrada en el test, no descubierta. Hoy la regla ya se cumple sin estar escrita:
  este test la convierte en regla ([D-04](./research.md))
- [ ] T015 [US3] Consolidar el canal si T014 encontró algún uso fuera de `MovimientosConsulta`. Si no
  encontró ninguno —que es lo esperado—, esta tarea se cierra dejándolo dicho en el comentario de
  `MovimientosConsulta`: que sea el único canal de lectura pasó de coincidencia a regla vigilada
- [ ] T016 [US3] Escribir `backend/verificar-aislamiento.sh` siguiendo la forma de
  `verificar-autorizacion.sh`: cinco pasos —verde de partida, rojo con el acotado quitado de la
  consulta, rojo con una lectura fuera del canal, rojo con el alta asignando un propietario ajeno,
  verde restaurado—, con `set -euo pipefail`, `trap` que restaure
  siempre, y el chequeo de `ConnectionStrings__Default`. La salida exacta que tiene que producir
  está en [quickstart.md](./quickstart.md). Darle bit de ejecución (`chmod +x`) y **verificarlo con
  `git ls-files -s`**: es la cicatriz de FIX-002, un script sin bit de ejecución que el CI no podía
  correr
- [ ] T017 [ROJO] [US3] Correr `./backend/verificar-aislamiento.sh` entero y mostrar su salida. Los
  pasos 2, 3 y 4 **tienen que dar rojo**: si alguno pasa en verde, la barrera no está mirando lo que
  cree mirar, y eso es un rojo aunque la suite esté en verde
- [ ] T018 [VERIFY] [US3] Puerta del backend completa

**Checkpoint**: el aislamiento está verificado y protegido. Desarmarlo hace ruido, y la barrera
demostró que sabe hacerlo.

---

## Phase 6: Polish & cierre

- [ ] T019 Agregar `verificar-aislamiento.sh` a la tabla de *Stack* de `AGENTS.md`, junto a las
  otras tres barreras, con la línea que explica qué comprueba y por qué. Una barrera que no está en
  la tabla no la corre nadie
- [ ] T020 Agregar el paso "Barrera de aislamiento" a `.github/workflows/ci.yml`, después de los tres
  que ya están. Va con las otras porque recompila con archivos modificados, así que tiene que ir
  después del paso de Tests para no invalidar su `--no-build`
- [ ] T021 [P] Correr la cobertura con `dotnet test backend/GestionGastos.slnx --settings
  backend/cobertura.runsettings` y revisar que `Movimientos/MovimientosConsulta.cs` y el camino de
  alta de `MovimientosEndpoints.cs` queden medidos
- [ ] T022 [P] Revisar que ningún AC quede sin test que lo nombre: **AC-01**, **AC-06**, **AC-08**,
  **AC-09** y **AC-10** (reformulado como FR-004). Los cinco restantes —**AC-02**, **AC-03**,
  **AC-04**, **AC-05**, **AC-07**— **no se marcan como cubiertos**: su endpoint no existe y quedan
  en la tabla de *Deuda registrada* de la spec. Confirmar que esa tabla sigue diciendo la verdad
- [ ] T023 [P] Recorrer [quickstart.md](./quickstart.md) de punta a punta, incluido el paso 3 con
  `curl` y dos frascos de cookies. Es lo que comprueba que los escenarios se parecen a la realidad
  y no sólo a sí mismos
- [ ] T024 [VERIFY] Puerta de cierre de feature, entera y con su salida a la vista: `dotnet format
  --verify-no-changes`, build con `-warnaserror`, `dotnet test` completo, cobertura, y las
  **cuatro** barreras (`verificar-contrato.sh`, `verificar-autorizacion.sh`, `verificar-linter.sh` y
  la nueva `verificar-aislamiento.sh`). Más la puerta del frontend entera —lint, format, typecheck,
  tests, build— aunque esta feature no lo toque. La del contrato importa especialmente acá: el
  contrato **no tenía que cambiar**, y ése es el paso que lo comprueba

---

## Dependencies & Execution Order

### Entre fases

- **Setup (T001)**: sin dependencias
- **Foundational (T002–T004)**: bloquea a US1 y US2. Sin dos cuentas distintas con datos que se
  parecen, todo escenario cruzado pasa en verde sin verificar nada
- **US1 (T005–T008)**: depende de Foundational. Es el MVP
- **US2 (T009–T012)**: depende de Foundational, y de US1 en la práctica — para comprobar dónde cayó
  lo que se escribió hay que poder leerlo aislado
- **US3 (T013–T018)**: depende de US1 y US2. Su barrera protege la condición que esas dos dejan
  verificada; antes no tendría qué proteger
- **Polish (T019–T024)**: depende de las tres

### Dentro de cada historia

- La tarea `[ROJO]` va **inmediatamente después** de los `[TEST]` que verifica, y antes del
  `[VERIFY]`. Es la que decide si esos tests sirven
- `[VERIFY]` cierra la historia; no se pasa a la siguiente con la puerta en rojo
- En US3, el test (T013, T014) antes que el script (T016): el script corre los tests, así que sin
  ellos no tiene qué poner en rojo

### Oportunidades de paralelismo

Pocas y honestas: casi todo vive en dos archivos de test, y dos tareas sobre el mismo archivo no van
en paralelo.

- T006 con T005: mismo archivo, pero bloques independientes — se pueden escribir en cualquier orden
- T021, T022 y T023 son revisiones sobre cosas distintas: van en paralelo de verdad
- T019 y T020 tocan archivos distintos (`AGENTS.md` y `ci.yml`), pero son la misma decisión: van
  juntas o queda una barrera a medio enchufar

---

## Implementation Strategy

### MVP (sólo US1)

1. T001 → T004: dos cuentas distintas con datos que se parecen
2. T005 → T008: el listado de cada cuenta devuelve lo suyo, y se le vio el rojo
3. **PARAR Y VALIDAR**: dos cuentas, movimientos en la misma fecha y categoría, cada listado con lo
   suyo. Quitar el acotado y ver caer los tests
4. Ya en este punto la mitad de lectura del aislamiento dejó de ser una propiedad sin comprobar

### Entrega incremental

1. MVP → la lectura está verificada
2. \+ US2 → también la escritura; 2 de 2 endpoints cubiertos
3. \+ US3 → desarmarlo hace ruido, y la barrera demostró que sabe hacerlo
4. \+ Polish → la barrera enchufada en `AGENTS.md` y en el CI, cobertura, y la deuda registrada al
   día

---

## Notes

- **Un test que pasa no dice nada acá.** Todo lo que se verifica ya funcionaba antes de la feature.
  Lo que dice algo es la tarea `[ROJO]`: si no pudo mostrar el rojo, el test no sirve
- **Ningún test duerme y ninguno depende del día de hoy.** El listado recorta al mes en curso del
  servidor, así que el reloj va clavado con `FactoriaConReloj` ([D-07](./research.md))
- **No hay migración.** Si aparece una, algo se salió del alcance
- **El contrato no cambia.** Si algún escenario obliga a cambiar una respuesta, esta feature dejó de
  ser de verificación y hay que volver a la spec
- Commit por tarea o por grupo lógico, nunca con la puerta en rojo
- `frontend/` no se toca en ninguna tarea salvo para correrle la puerta en T024

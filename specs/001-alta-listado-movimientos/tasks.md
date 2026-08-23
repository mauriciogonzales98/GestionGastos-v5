---

description: "Task list — Alta de movimientos y listado simple"
---

# Tasks: Alta de movimientos y listado simple

**Input**: Design documents from `/specs/001-alta-listado-movimientos/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: **obligatorios y primero**. No es una opción de esta feature: el Principio I de
`.specify/memory/constitution.md` prohíbe escribir código de producción sin un test que ya haya
fallado, y el Principio II exige que cada AC tenga un test que lo nombre por su identificador. Toda
tarea `[TEST]` termina con un **rojo real mostrado en la salida** antes de que empiece la tarea de
código que le sigue.

**Organization**: agrupadas por historia de usuario, para que cada una se implemente y se verifique
sola.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede correr en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1, US2, US3)
- **[TEST]**: tarea de test — va **antes** que su tarea de código y tiene que quedar en rojo
- **[VERIFY]**: puerta del grupo. Se corren los comandos de `AGENTS.md` con su salida a la vista

## Path Conventions

Web app: `backend/` y `frontend/`, como fija `AGENTS.md` y detalla *Project Structure* en
[plan.md](./plan.md).

---

## Phase 1: Setup (andamiaje del repositorio)

**Purpose**: crear la solución, los proyectos y las herramientas. Nada de esto existe todavía.

**Por qué es tan grande**: `.github/workflows/ci.yml` ya invoca `Directory.Build.props`,
`.editorconfig`, `verificar-contrato.sh` y `verificar-linter.sh`. El CI falla en el primer push
mientras no estén. Registrado en *Complexity Tracking* de [plan.md](./plan.md).

- [ ] T001 Crear la solución y los dos proyectos en `backend/GestionGastos.sln`: `GestionGastos.Api` (web, .NET 10) y `GestionGastos.Api.Tests` (xUnit), con la referencia de tests a Api
- [ ] T002 Agregar los paquetes del backend en `backend/GestionGastos.Api/GestionGastos.Api.csproj`: `Microsoft.EntityFrameworkCore` 9.0.18 y `Pomelo.EntityFrameworkCore.MySql` 9.0.0
- [ ] T003 Crear `backend/Directory.Build.props` encendiendo los analizadores de Roslyn (`EnableNETAnalyzers`, `AnalysisLevel`, `EnforceCodeStyleInBuild`) para todos los proyectos de la solución
- [ ] T004 Crear `backend/.editorconfig` con las reglas activas y, para cada regla apagada, un comentario con el motivo. Excluir `Migrations/` del análisis: es código generado
- [ ] T005 [P] Crear `backend/cobertura.runsettings` incluyendo en la medición el código de `Contrato/`, que vive en el proyecto de tests y coverlet no instrumenta por defecto
- [ ] T006 [P] Inicializar el frontend en `frontend/` con Vite + React 19 + TypeScript y pnpm, generando `frontend/pnpm-lock.yaml`
- [ ] T007 [P] Configurar ESLint y Prettier en `frontend/eslint.config.js` y `frontend/.prettierrc`, con la regla que prohíbe `any` sin comentario justificativo (`AGENTS.md`, *Code conventions*)
- [ ] T008 Agregar las dependencias de desarrollo justificadas en [research.md D-10](./research.md) a `frontend/package.json`: `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/user-event`. Ninguna dependencia de producción nueva
- [ ] T009 Configurar Vitest con entorno `jsdom` en `frontend/vite.config.ts` y definir en `frontend/package.json` los scripts `dev`, `build`, `lint`, `format`, `test` que `.github/workflows/ci.yml` invoca. **`format` tiene que ser `prettier --check .`**, que verifica sin escribir: el CI lo corre como paso de verificación y `AGENTS.md` lo declara el espejo de `dotnet format --verify-no-changes`. Si saliera como `--write`, ese paso jamás podría ponerse en rojo y encima modificaría archivos en el runner. Para formatear de verdad va un script aparte, `format:fix`, con `prettier --write .`
- [ ] T010 Escribir el primer test del frontend en `frontend/tests/humo.test.ts` —una aserción trivial sobre el entorno— y correr `pnpm --dir frontend test` para ver la suite en verde. **Sin `--passWithNoTests`**: Vitest falla cuando no encuentra tests, y esa señal se apaga con la bandera en vez de arreglarse. Con TDD siempre hay un test antes que su código, así que la bandera nunca hace falta
- [ ] T011 Actualizar `.gitignore` en la raíz para `bin/`, `obj/`, `node_modules/`, `dist/` y los archivos de user-secrets
- [ ] T012 [VERIFY] Puerta del andamiaje, con los mismos comandos que corre el CI: `dotnet build backend/GestionGastos.sln -warnaserror`, `dotnet format backend/GestionGastos.sln --verify-no-changes`, `pnpm --dir frontend lint`, `pnpm --dir frontend format`, `pnpm --dir frontend exec tsc --noEmit`, `pnpm --dir frontend test`. Verde es cero fallos **y cero warnings**

**Checkpoint**: la solución compila vacía y el frontend arranca. Las barreras todavía no existen.

---

## Phase 2: Foundational (prerequisitos bloqueantes)

**Purpose**: esquema, dominio, el catálogo de categorías y **las dos barreras de calidad**. Ninguna
historia puede empezar antes de que esto cierre.

**⚠️ CRÍTICO**: las barreras van acá y no al final. El plan `DISC-001` documenta lo que pasa si no:
FEAT-001c se escribió, se verificó y se mergeó sin linter, porque el linter no molestaba.

### Dominio y persistencia

- [ ] T013 [TEST] Escribir en `backend/GestionGastos.Api.Tests/Unitarios/RangoDelMesTests.cs` los tests de `RangoDelMes.De(DateOnly)`: primer y último día del mes, con fechas fijas para un mes de 31, uno de 30, febrero común y febrero bisiesto. **Nombrar AC-25**. Correr y mostrar el rojo
- [ ] T014 Implementar el tipo puro `RangoDelMes` con `De(DateOnly hoy)` en `backend/GestionGastos.Api/Dominio/RangoDelMes.cs`. Parametrizado por fecha, nunca lee `DateTime.Now` ([research.md D-03](./research.md))
- [ ] T015 [P] Crear las entidades de `backend/GestionGastos.Api/Dominio/`: `Movimiento.cs`, `Categoria.cs`, `Moneda.cs`, `Usuario.cs` y el enum `TipoMovimiento`, con los campos de [data-model.md](./data-model.md)
- [ ] T016 Crear `backend/GestionGastos.Api/Persistencia/GestionGastosDbContext.cs` con las configuraciones de [data-model.md](./data-model.md): `decimal(11,2)` y `CHECK (monto > 0)` en `monto`, `date` en `fecha`, `categoria.usuario_id` nullable, `UNIQUE (usuario_id, nombre, tipo)` en categoría, y el índice `(usuario_id, fecha DESC, id DESC)` en movimiento
- [ ] T017 Crear `backend/GestionGastos.Api/Persistencia/IUsuarioActual.cs` y su única implementación, que devuelve el id de la fila semilla ([research.md D-05](./research.md))
- [ ] T018 Generar la migración inicial en `backend/GestionGastos.Api/Migrations/` con la semilla: las 10 categorías de FR-006, las monedas `ARS` (predeterminada) y `USD`, y el usuario semilla
- [ ] T019 Crear el fixture de tests en `backend/GestionGastos.Api.Tests/Integracion/BaseDeDatosFixture.cs`, que crea y migra `gestiongastos_test` por su cuenta y **lanza a propósito** si `ConnectionStrings__Default` no está definida, en vez de adivinar contra qué base escribe

### El catálogo de categorías (FR-006) — lo que le da a las barreras algo real que proteger

- [ ] T020 [TEST] Escribir en `backend/GestionGastos.Api.Tests/Integracion/CategoriasEndpointTests.cs` el test de `GET /api/categorias`: devuelve exactamente 10 categorías, 7 de tipo `gasto` y 3 de tipo `ingreso`, con `tipo` como cadena. **Nombrar AC-10**. Mostrar el rojo
- [ ] T021 Implementar `GET /api/categorias` en `backend/GestionGastos.Api/Categorias/`, con el DTO y el mapeo de `tipo` a cadena según [contracts/api-http.md](./contracts/api-http.md)
- [ ] T022 Crear `frontend/src/api/tipos.ts` con la interfaz `Categoria` y el tipo `TipoMovimiento`. **Este archivo es la fuente de verdad del contrato** ([research.md D-09](./research.md))

### Las barreras (Principio V)

- [ ] T023 [TEST] Escribir en `backend/GestionGastos.Api.Tests/Contrato/ContratoCategoriasTests.cs` la verificación que **lee** `frontend/src/api/tipos.ts` y compara sus campos contra el JSON que emite `GET /api/categorias`, en las dos direcciones. Es la excepción de estructura declarada en `AGENTS.md`: lectura en una sola dirección
- [ ] T024 Crear `backend/verificar-contrato.sh`: desalinea el contrato a propósito, comprueba que el test de T023 se pone en **rojo**, restaura y comprueba que vuelve a verde. No alcanza con que los tests pasen — la barrera tiene que probar que sabe fallar
- [ ] T025 Crear `backend/verificar-linter.sh`: introduce una violación deliberada en código escrito a mano y comprueba que rompe el build; la introduce dentro de `Migrations/` y comprueba que **no** lo rompe. Verifica las dos direcciones
- [ ] T026 Dar bit de ejecución a los dos scripts (`chmod +x backend/verificar-*.sh`) y confirmar que queda registrado en git con `git update-index --chmod=+x`. Sin esto el CI falla aunque el script sea correcto — es exactamente el fallo que el plan `DISC-001` registra como FIX-002
- [ ] T027 [VERIFY] Puerta completa por primera vez: build con `-warnaserror`, `dotnet format --verify-no-changes`, `dotnet test backend/`, `./backend/verificar-contrato.sh` (~90 s) y `./backend/verificar-linter.sh`, en ese orden. Las dos barreras van al final porque recompilan e invalidarían el `--no-build`

**Checkpoint**: el CI pasa de punta a punta. Las dos barreras existen y se probó que saben fallar.

---

## Phase 3: User Story 1 — Registrar un gasto y verlo en el listado (P1) 🎯 MVP

**Goal**: la persona carga un gasto y lo ve en el listado del mes, en una sola pantalla.

**Independent Test**: completar el formulario de un gasto y verificar que queda persistido y visible
en el listado, sin necesitar ninguna otra funcionalidad.

### Backend — alta

- [ ] T028 [TEST] [US1] Escribir en `backend/GestionGastos.Api.Tests/Integracion/AltaMovimientoTests.cs` el test de `POST /api/movimientos` con gasto válido: responde `201`, devuelve el movimiento completo y queda una fila con el `usuario_id` del usuario semilla. **Nombrar AC-15**. Mostrar el rojo
- [ ] T029 [TEST] [US1] Agregar en el mismo archivo el test de la fecha por defecto: petición sin `fecha` queda registrada con el día que se le inyecta como "hoy", no con `DateTime.Now`. **Nombrar AC-17**. Mostrar el rojo
- [ ] T030 [US1] Implementar `POST /api/movimientos` en `backend/GestionGastos.Api/Movimientos/`, con su DTO de petición y de respuesta según [contracts/api-http.md](./contracts/api-http.md). La moneda sale de la predeterminada del catálogo (FR-009) y el propietario de `IUsuarioActual`, asignado a mano en el `INSERT` (FR-010)

### Backend — listado

- [ ] T031 [TEST] [US1] Escribir en `backend/GestionGastos.Api.Tests/Integracion/ListadoMovimientosTests.cs` el test de `GET /api/movimientos`: devuelve los del mes actual y **no** los de fuera, verificando los cuatro bordes con fechas fijas (último día del mes anterior, primero del actual, último del actual, primero del siguiente). **Nombrar AC-25**. Mostrar el rojo
- [ ] T032 [TEST] [US1] Agregar el test de orden en doble capa: uno sobre el resultado (`fecha DESC`, desempate por `id DESC`) y otro que falla si el `OrderBy` desaparece de la consulta. El índice devuelve el orden correcto aunque la consulta no lo pida ([research.md D-04](./research.md)). Mostrar el rojo
- [ ] T033 [TEST] [US1] Agregar el test de listado vacío: sin movimientos en el mes devuelve `200` con arreglo vacío, no `404`. **Nombrar FR-012**. Mostrar el rojo
- [ ] T034 [US1] Implementar `GET /api/movimientos` en `backend/GestionGastos.Api/Movimientos/`, filtrando por `RangoDelMes.De(hoy)` con extremos incluidos y ordenando explícitamente por `fecha DESC, id DESC`
- [ ] T035 [US1] Extender `frontend/src/api/tipos.ts` con `Movimiento`, `NuevoMovimiento` y el tipo de error `ProblemDetails`, y agregar en `backend/GestionGastos.Api.Tests/Contrato/` la verificación de esos tres tipos contra el JSON real

### Frontend

- [ ] T036 [TEST] [P] [US1] Escribir en `frontend/tests/CampoConError.test.tsx` los tests del componente de campo: renderiza `label` asociado por `for`/`id`, y con error presente pone `aria-invalid="true"`, `aria-describedby="{campo}-error"` y un contenedor con `role="alert"`. Mostrar el rojo
- [ ] T037 [US1] Implementar el componente único de campo en `frontend/src/ui/CampoConError.tsx`, según el punto 2 del *Contrato de marcado de la UI* en [plan.md](./plan.md). Ninguna pantalla arma la tripleta `label` + control + error a mano
- [ ] T038 [TEST] [US1] Escribir en `frontend/tests/FormularioMovimiento.test.tsx` el test del formulario: arranca en `gasto` con la fecha de hoy, el selector ofrece las 7 categorías de gasto y ninguna de ingreso. **Nombrar AC-10**. Mostrar el rojo
- [ ] T039 [US1] Implementar `frontend/src/movimientos/FormularioMovimiento.tsx` como `<form>` real con `<button type="submit">`, usando `CampoConError` para cada campo y un `<select>` nativo para categoría
- [ ] T040 [TEST] [P] [US1] Escribir en `frontend/tests/ListadoMovimientos.test.tsx` los tests del listado: tabla con `<th scope="col">`, tipo mostrado **como texto** y no sólo por color, y el mensaje de vacío cuando no hay filas. Mostrar el rojo
- [ ] T041 [US1] Implementar `frontend/src/movimientos/ListadoMovimientos.tsx` con las cuatro columnas de [contracts/ui-pantalla.md](./contracts/ui-pantalla.md)
- [ ] T042 [US1] Implementar el cliente HTTP en `frontend/src/api/cliente.ts`, tipado contra `tipos.ts`, con errores tipados y sin ningún catch silencioso (`AGENTS.md`, *Architecture conventions*)
- [ ] T043 [TEST] [US1] Escribir en `frontend/tests/PantallaMovimientos.test.tsx` el test del ciclo completo: guardar un gasto lo inserta **en su posición** en el listado, vacía el formulario y devuelve el foco al primer campo. **Nombrar AC-15 y FR-014**. Mostrar el rojo
- [ ] T044 [TEST] [US1] Agregar el test de AC-55 con `user-event`: recorrer el formulario entero sólo con `Tab`, completarlo y enviarlo con `Enter`, sin usar el mouse. **Nombrar AC-55**. Mostrar el rojo
- [ ] T045 [US1] Implementar `frontend/src/movimientos/PantallaMovimientos.tsx` uniendo formulario y listado en una sola pantalla (FR-013) y aplicando el ciclo post-guardado de FR-014
- [ ] T046 [US1] Crear `frontend/src/estilos/` con la regla `l-*` / `c-*` del punto 3 del *Contrato de marcado*, y el foco visible con `:focus-visible` sin anular `outline`. Sin colores ni espaciados: eso es del ticket 6
- [ ] T047 [TEST] [US1] Agregar el test del movimiento guardado fuera del mes actual: se registra pero no aparece en el listado, y la confirmación lo dice en vez de sugerir que se perdió. Mostrar el rojo
- [ ] T048 [US1] Implementar ese aviso en `PantallaMovimientos.tsx`
- [ ] T049 [VERIFY] [US1] Puerta completa de frontend y backend

**Checkpoint**: **el MVP funciona.** Se puede cargar un gasto y verlo. Entregable por sí solo.

---

## Phase 4: User Story 2 — Registrar un ingreso y verlo en el listado (P2)

**Goal**: la misma pantalla registra ingresos, distinguibles de los gastos en el listado.

**Independent Test**: completar el formulario de un ingreso y verificar que queda persistido y
visible en el listado, marcado como ingreso y no como gasto.

- [ ] T050 [TEST] [US2] Agregar en `backend/GestionGastos.Api.Tests/Integracion/AltaMovimientoTests.cs` el test de alta de ingreso: `201` y la fila queda con `tipo = ingreso`. **Nombrar AC-16**. Mostrar el rojo
- [ ] T051 [TEST] [US2] Agregar en `backend/GestionGastos.Api.Tests/Integracion/ListadoMovimientosTests.cs` el test de listado mixto: con gastos e ingresos del mes, devuelve los dos y cada uno con su tipo. **Nombrar AC-22**. Mostrar el rojo
- [ ] T052 [US2] Extender el alta y el listado en `backend/GestionGastos.Api/Movimientos/` para aceptar y devolver `tipo = ingreso` (FR-002)
- [ ] T053 [TEST] [US2] Agregar en `frontend/tests/FormularioMovimiento.test.tsx` el test del cambio de tipo: al pasar a `ingreso` el selector ofrece exactamente las 3 categorías de ingreso, ninguna de gasto, **y la selección anterior se limpia**. **Nombrar AC-10**. Mostrar el rojo
- [ ] T054 [US2] Implementar en `FormularioMovimiento.tsx` el repoblado del selector al cambiar el tipo, limpiando la selección previa para que la combinación imposible no sea alcanzable
- [ ] T055 [TEST] [US2] Agregar en `frontend/tests/ListadoMovimientos.test.tsx` el test de que gasto e ingreso se distinguen por texto, no sólo por color. Mostrar el rojo
- [ ] T056 [VERIFY] [US2] Puerta completa de frontend y backend

**Checkpoint**: US1 y US2 funcionan juntas. El dominio está completo en su camino feliz.

---

## Phase 5: User Story 3 — El formulario rechaza lo que no puede registrarse (P3)

**Goal**: ningún movimiento inválido llega a la base, y la persona siempre sabe por qué.

**Independent Test**: intentar guardar cada variante inválida y verificar que se muestra el motivo y
que la cantidad de movimientos registrados no cambia.

- [ ] T057 [TEST] [US3] Escribir en `backend/GestionGastos.Api.Tests/Integracion/ValidacionMovimientoTests.cs` los tests de monto: vacío, `0`, negativo, `10.999` y `1000000000.00` devuelven `400` con `ProblemDetails` cuyo `errors` tiene la clave `monto`, y **la cantidad de filas no cambia**. Incluir los bordes que sí pasan: `0.01` y `999999999.99`. **Nombrar AC-18**. Mostrar el rojo
- [ ] T058 [US3] Implementar la validación de monto en `backend/GestionGastos.Api/Movimientos/`, cubriendo FR-004 y FR-004b. El techo es una validación declarada, **no** un error genérico del almacenamiento
- [ ] T059 [TEST] [US3] Agregar los tests de categoría: sin categoría, con categoría inexistente y con categoría de tipo distinto al del movimiento devuelven `400` con la clave `categoriaId` en `errors`. **Nombrar AC-40 y FR-011**. Mostrar el rojo
- [ ] T060 [US3] Implementar las validaciones de categoría en `backend/GestionGastos.Api/Movimientos/` (FR-005 y FR-011), aplicándolas en la capa de aplicación y no sólo en el formulario
- [ ] T061 [TEST] [US3] Agregar el test de `tipo` ausente o distinto de `gasto`/`ingreso`: `400` con la clave `tipo`. Mostrar el rojo
- [ ] T062 [US3] Implementar esa validación y verificar que el formato de error es el mismo `ProblemDetails` para las cuatro familias ([research.md D-07](./research.md))
- [ ] T063 [TEST] [US3] Escribir en `frontend/tests/ValidacionFormulario.test.tsx` los tests del cliente: cada mensaje aparece junto a su campo, con `aria-invalid` y `aria-describedby` puestos, y el formulario **conserva lo cargado**. **Nombrar AC-18 y AC-40**. Mostrar el rojo
- [ ] T064 [TEST] [US3] Agregar el test de que un error devuelto por el servidor se enruta al **mismo lugar** que uno del cliente, mapeando la clave de `errors` al campo. Mostrar el rojo
- [ ] T065 [US3] Implementar en `FormularioMovimiento.tsx` la validación de cliente y el mapeo de `errors` del servidor a `CampoConError`. Nada de `alert()`, notificaciones flotantes ni un bloque de errores agrupado arriba
- [ ] T066 [TEST] [US3] Agregar el test del error sin campo: un fallo al persistir se muestra en la región de error del formulario, con `role="alert"`, conservando lo cargado. Mostrar el rojo
- [ ] T067 [US3] Implementar la región de error del formulario y el estado *Enviando* que deshabilita el botón hasta la respuesta, evitando el doble envío
- [ ] T068 [VERIFY] [US3] Puerta completa de frontend y backend

**Checkpoint**: las tres historias completas. Nada inválido entra a la base.

---

## Phase 6: Polish y cierre de la feature

**Purpose**: lo que se mide una sola vez, al final, sobre la feature entera.

- [ ] T069 [TEST] Escribir en `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoAltaTests.cs` la medición de AC-34: p95 del guardado < 1 s sobre 100 ejecuciones. **Nombrar AC-34**
- [ ] T070 Agregar el guardarraíl del sembrado de rendimiento: una función pura parametrizada por fecha, anclada al año en curso, y una confirmación explícita de que el mes sembrado tiene filas. Sin ese guardarraíl el test pasa en verde midiendo una consulta vacía — es la lección que el plan `DISC-001` deja escrita en FIX-004
- [ ] T071 Correr la cobertura con `dotnet test backend/GestionGastos.sln --settings backend/cobertura.runsettings` y revisar que el código de `Contrato/` quede medido
- [ ] T072 [P] Revisar que ningún AC de SC-005 quede sin test que lo nombre: AC-10, AC-15, AC-16, AC-17, AC-18, AC-22, AC-25, AC-34, AC-40, AC-55. Un AC sin test cubierto no cuenta como implementado (Principio II)
- [ ] T073 [P] Revisar que no haya ningún `any` sin comentario justificativo ni ningún catch silencioso en `frontend/src/` y `backend/GestionGastos.Api/`
- [ ] T074 Escribir el ADR de la excepción de estructura —los tests de `Contrato/` leyendo `frontend/src/api/tipos.ts`— que `AGENTS.md` referencia pero que todavía no existe en el repositorio
- [ ] T075 [VERIFY] Puerta de cierre de feature, entera y con su salida a la vista: lint, **format**, typecheck y tests de frontend más build de producción; `dotnet format --verify-no-changes`, build con `-warnaserror`, `dotnet test` completo (incluida la suite `Rendimiento`, que sólo corre en local), cobertura, `./backend/verificar-contrato.sh` y `./backend/verificar-linter.sh`

---

## Dependencies

```text
Phase 1 (Setup)  ──→  Phase 2 (Foundational)  ──→  Phase 3 (US1 · P1 · MVP)
                                                        │
                                                        ├──→ Phase 4 (US2 · P2)
                                                        │
                                                        └──→ Phase 5 (US3 · P3)
                                                                  │
                                                                  ▼
                                                        Phase 6 (Polish)
```

- **Phase 2 bloquea todo.** Sin esquema, dominio y barreras no se puede empezar ninguna historia.
- **US1 es el MVP** y entrega valor sola.
- **US2 y US3 dependen de US1** pero no entre sí: una vez cerrada la fase 3, se pueden encarar en
  cualquier orden, o en paralelo si hay dos personas. US3 se pisa con US2 en
  `FormularioMovimiento.tsx`, así que conviene serializarlas salvo que haya dos.
- **US3 va última por diseño**: el camino feliz tiene que existir antes de poder desviarse de él.

## Parallel Opportunities

| Fase | Tareas en paralelo | Por qué |
|------|--------------------|---------|
| Setup | T005, T006, T007 | Archivos distintos, sin dependencias entre sí |
| Foundational | T015 con T014 | Entidades y `RangoDelMes` no se tocan |
| US1 | T036 con T040 | Componente de campo y listado son archivos distintos |
| Polish | T072, T073 | Dos revisiones independientes |

El resto es secuencial: dentro de cada historia, la tarea de código depende de que su test esté en
rojo primero.

## Implementation Strategy

**MVP** = Phase 1 + Phase 2 + Phase 3 (US1). Deja una aplicación que registra gastos y los muestra.

**Incrementos commiteables**, respetando el techo de ~300 líneas por commit acordado el 2026-08-20:

| Commit | Tareas | Nota |
|--------|--------|------|
| 1 | T001–T012 | Andamiaje. **Rompe el techo** por el motivo registrado en *Complexity Tracking* |
| 2 | T013–T027 | Dominio, esquema, catálogo y las dos barreras |
| 3 | T028–T035 | Backend de US1 |
| 4 | T036–T049 | Frontend de US1 — el MVP queda entregable |
| 5 | T050–T056 | US2 |
| 6 | T057–T068 | US3 |
| 7 | T069–T075 | Cierre |

Ninguna tarea se marca como completada —ni acá, ni en un commit, ni en un reporte— hasta que su
puerta esté en verde y la salida real de los comandos se haya mostrado (Principio III).

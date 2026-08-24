---

description: "Task list — Identidad y sesión"
---

# Tasks: Identidad y sesión

**Input**: Design documents from `/specs/002-identidad-sesion/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: **obligatorios y primero**. No es opcional en esta feature: el Principio I de
`.specify/memory/constitution.md` prohíbe escribir código de producción sin un test que ya haya
fallado, y el Principio II exige que cada AC tenga un test que lo nombre. Toda tarea `[TEST]`
termina con un **rojo real mostrado en la salida** antes de que empiece la tarea de código.

**Organization**: agrupadas por historia de usuario, para que cada una se implemente y se verifique
sola.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede correr en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1, US2, US3)
- **[TEST]**: tarea de test — va **antes** que su tarea de código y tiene que quedar en rojo
- **[VERIFY]**: puerta del grupo. Se corren los comandos de `AGENTS.md` con su salida a la vista

## Path Conventions

Web app: `backend/` y `frontend/`, como fija `AGENTS.md`.

---

## ⚠️ Antes de empezar: esta feature rompe cosas a propósito

Dos tareas de este ticket dejan el árbol en rojo hasta que la siguiente las acompaña, y conviene
saberlo para no confundir el rojo esperado con un error:

- **T025** enciende la autorización global. En ese momento **toda la suite de FEAT-001a se pone en
  rojo**, porque sus tests piden sin sesión. **T026** los acompaña.
- **T007** borra la fila semilla. Desde ahí, cualquier test que la dé por existente falla.

No es deuda ni descuido: es el corte que el PRD ordena. Las dos parejas van juntas y sin commit
intermedio con la puerta en rojo (Principio III).

---

## Phase 1: Setup

**Purpose**: lo que hace falta antes de tocar dominio.

- [X] T001 Agregar `BCrypt.Net-Next` 4.2.0 a `backend/GestionGastos.Api/GestionGastos.Api.csproj`. Es la única dependencia nueva de producción y su justificación está en [research.md D-02](./research.md); ninguna dependencia nueva en el frontend
- [X] T002 [P] Extender la lista blanca de `backend/GestionGastos.Api.Tests/Integracion/BaseDeDatosFixture.cs` para admitir `gestiongastos_migracion_test` **además** de `gestiongastos_test`. Se agrega un nombre, **no** se abre la restricción: el fixture migra y limpia tablas, y apuntarlo al esquema de desarrollo se lleva puestos los datos

---

## Phase 2: Foundational (prerequisitos bloqueantes)

**Purpose**: el esquema, el hash y la migración que borra la semilla. Ninguna historia puede empezar
antes de que esto cierre.

- [X] T003 [TEST] Escribir en `backend/GestionGastos.Api.Tests/Unitarios/HasherDeContrasenasTests.cs` los tests del hash: el valor almacenado no es la contraseña y tiene formato bcrypt (`$2`); verificar acepta la contraseña correcta y rechaza la incorrecta; **dos hashes de la misma contraseña son distintos**. **Nombrar AC-10 y AC-11**. Correr y mostrar el rojo
- [X] T004 Implementar `backend/GestionGastos.Api/Cuentas/HasherDeContrasenas.cs` sobre `BCrypt.Net-Next`, con la interfaz mínima que los tests usan. La sal la genera y la guarda la librería dentro del hash: AC-11 sale por construcción y no por disciplina ([D-02](./research.md))
- [X] T005 Agregar `ContrasenaHash` a `backend/GestionGastos.Api/Dominio/Usuario.cs` y su configuración en `backend/GestionGastos.Api/Persistencia/GestionGastosDbContext.cs`: `varchar(72)`, `NOT NULL`, y la colación **insensible a mayúsculas** en `email` según [data-model.md](./data-model.md). Sin esa colación, `Ana@x.com` y `ana@x.com` serían dos cuentas y FR-002 quedaría incumplido
- [ ] T006 [TEST] Escribir en `backend/GestionGastos.Api.Tests/Migraciones/MigracionDeCuentasTests.cs` el test de AC-09, con su **propia base** `gestiongastos_migracion_test` ([D-07](./research.md)): migrar hasta `Inicial`, sembrar la fila semilla y movimientos suyos, aplicar la migración de este ticket, y verificar que no queda ninguno de los dos. **Nombrar AC-09**. Mostrar el rojo
- [X] T007 Generar la migración en `backend/GestionGastos.Api/Migrations/` en el orden que impone la clave foránea: `ALTER usuario ADD contrasena_hash`, después `DELETE` de los movimientos de la semilla, y **recién ahí** el `DELETE` del usuario. Al revés falla ([data-model.md](./data-model.md)). `Down` **no** restituye los datos y lo declara: un `Down` que miente es peor que uno que avisa
- [X] T008 Registrar en `backend/GestionGastos.Api/Program.cs` la autenticación por cookie —`HttpOnly`, `SameSite=Strict`, `Secure` fuera de desarrollo— tomando el reloj del `TimeProvider` ya inyectado ([D-01](./research.md), [D-03](./research.md)). **Todavía NO se activa la autorización global**: eso es T025, y activarla antes dejaría la aplicación sin forma de iniciar sesión
- [ ] T009 [VERIFY] Puerta del backend: `dotnet format --verify-no-changes`, `dotnet build -warnaserror` y `dotnet test`. Verde es cero fallos **y cero warnings**

**Checkpoint**: el esquema tiene cuentas, la semilla ya no existe y el hash funciona. Todavía no hay
forma de crear una cuenta ni de entrar.

---

## Phase 3: User Story 1 — Crear una cuenta y entrar con ella (P1) 🎯 MVP

**Goal**: una persona crea su cuenta con email y contraseña y puede entrar con esas mismas
credenciales.

**Independent Test**: crear una cuenta con un email no registrado y después iniciar sesión con ese
email y esa contraseña.

### Tests

- [X] T010 [TEST] [US1] Escribir en `backend/GestionGastos.Api.Tests/Integracion/AltaDeCuentaTests.cs` el test de `POST /api/cuentas` con email nuevo: responde `201`, queda una fila en `usuario`, y esas credenciales permiten iniciar sesión. **Nombrar AC-01**. Mostrar el rojo
- [X] T011 [TEST] [US1] Agregar el test del email ya registrado: la respuesta es **idéntica** a la del alta exitosa —mismo código y mismo cuerpo—, sigue habiendo **una sola** cuenta con ese email, y su `contrasena_hash` **no cambió**. **Nombrar AC-02 y NFR-03**. Mostrar el rojo
- [X] T012 [TEST] [P] [US1] Agregar los tests de validación del alta: email ausente, vacío o con formato inválido, y contraseña ausente o de menos de 12 caracteres, devuelven `400` con la clave del campo en `errors`. Acá **sí** se dice qué está mal: no revela qué cuentas existen. Incluir el borde que pasa: exactamente 12. Mostrar el rojo
- [X] T013 [TEST] [P] [US1] Agregar el test de que el email se trata **sin distinguir mayúsculas**: dado de alta `Ana@x.com`, un alta posterior con `ana@x.com` no crea una segunda cuenta. Mostrar el rojo

### Implementación

- [X] T014 [US1] Implementar `POST /api/cuentas` en `backend/GestionGastos.Api/Cuentas/`, con su DTO de petición y de respuesta según [contracts/api-http.md](./contracts/api-http.md). La respuesta es la misma exista o no la cuenta; lo que cambia es que no se crea nada
- [X] T015 [TEST] [US1] Escribir en `backend/GestionGastos.Api.Tests/Integracion/InicioDeSesionTests.cs` el test del camino feliz de `POST /api/sesion`: credenciales correctas devuelven `200` y **la cookie de sesión**, y a partir de ahí `GET /api/sesion` reconoce a esa cuenta. Es lo que cierra AC-01 del lado del servidor. **Nombrar AC-03**. Mostrar el rojo
- [X] T016 [US1] Implementar `POST /api/sesion` en `backend/GestionGastos.Api/Sesion/`, emitiendo la cookie con el identificador de la cuenta como claim
- [ ] T017 [US1] Extender `frontend/src/api/tipos.ts` con `Credenciales`, `NuevaCuenta` y `SesionActual`, y agregar en `backend/GestionGastos.Api.Tests/Contrato/` la verificación de esos tipos contra el JSON real, en las dos direcciones. Ese archivo sigue siendo la fuente de verdad del contrato ([ADR-001](../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md))
- [ ] T018 [VERIFY] [US1] Puerta del backend completa, más `./backend/verificar-contrato.sh`, que tiene que seguir probando que sabe ponerse en rojo con los tipos nuevos adentro

**Checkpoint**: se puede crear una cuenta y obtener una sesión. Nada la exige todavía.

---

## Phase 4: User Story 2 — Entrar, salir, y que sin sesión no se pueda nada (P2)

**Goal**: la sesión se vuelve una frontera: ninguna pantalla ni endpoint responde sin ella.

**Independent Test**: iniciar sesión, cerrarla, y comprobar que a partir de ahí cualquier pantalla u
operación exige autenticarse de nuevo.

### Backend — rechazo indistinguible

- [ ] T019 [TEST] [US2] Agregar en `InicioDeSesionTests.cs` los tests del rechazo: email inexistente y contraseña incorrecta devuelven el **mismo** `401` con el **mismo** cuerpo, y ninguna sesión queda iniciada. **Nombrar AC-04 y NFR-03**. Mostrar el rojo
- [ ] T020 [TEST] [US2] Agregar el test de que el login **ejecuta un hash aunque el email no exista** ([D-04](./research.md)). Se verifica la **conducta**, no el tiempo: un test que midiera milisegundos sería intermitente y el Principio IV lo prohíbe. Mostrar el rojo
- [ ] T021 [US2] Implementar el rechazo indistinguible en `backend/GestionGastos.Api/Sesion/`: misma respuesta para las dos causas, y verificación contra un hash descartable cuando la cuenta no existe

### Backend — la sesión como recurso

- [ ] T022 [TEST] [P] [US2] Escribir el test de `GET /api/sesion`: con sesión devuelve `200` con el email; sin sesión, `401`. Mostrar el rojo
- [ ] T023 [TEST] [P] [US2] Escribir el test de `DELETE /api/sesion`: devuelve `204`, la cookie deja de valer, y **cerrar una sesión que ya no existe también devuelve `204`** — no es un error. **Nombrar AC-06**. Mostrar el rojo
- [ ] T024 [US2] Implementar `GET` y `DELETE /api/sesion` en `backend/GestionGastos.Api/Sesion/`

### Backend — la frontera

- [ ] T025 [TEST] [US2] Escribir en `backend/GestionGastos.Api.Tests/Autorizacion/SinSesionTests.cs` el test de que los tres endpoints de FEAT-001a responden `401` sin sesión **y no ejecutan su efecto**: tras un `POST /api/movimientos` rechazado, la cantidad de filas no cambia. Que no se ejecute es la mitad del criterio — un `POST` que rechaza pero igual inserta cumple el código y falla el requisito. **Nombrar AC-05**. Mostrar el rojo
- [ ] T026 [US2] Activar la autorización **global** en `Program.cs` con las **dos** excepciones explícitas (`POST /api/cuentas`, `POST /api/sesion`). Global con excepciones y no endpoint por endpoint: así un endpoint nuevo nace protegido en vez de nacer abierto ([contracts/api-http.md](./contracts/api-http.md))
- [X] T027 [US2] Reemplazar `UsuarioSemilla` por `backend/GestionGastos.Api/Persistencia/UsuarioDeLaSesion.cs`, que lee el identificador del `ClaimsPrincipal` y **lanza** si no hay sesión ([D-05](./research.md)). Eliminar `UsuarioSemilla.cs`. **`IUsuarioActual` no cambia**, así que `MovimientosEndpoints` no se toca: esa es la costura que FEAT-001a dejó preparada
- [X] T028 [US2] Acompañar T026/T027 actualizando la suite de FEAT-001a para que autentique antes de pedir. **Es el rojo esperado de este ticket**: sin esto, todos sus tests fallan con `401`. Va en el mismo commit que T026 y T027 — no se commitea con la puerta en rojo (Principio III)

### Backend — la barrera nueva (Principio V)

- [ ] T029 [TEST] [US2] Escribir en `backend/GestionGastos.Api.Tests/Autorizacion/BarreraDeAutorizacionTests.cs` la barrera: **descubrir los endpoints del `EndpointDataSource` en tiempo de ejecución** y exigir que todos respondan `401` sin credenciales, salvo las dos excepciones declaradas. **No enumerarlos a mano**: una lista escrita al lado pasa en verde justo el día que alguien agrega un endpoint desprotegido, que es el único día que importa
- [ ] T030 [US2] Crear `backend/verificar-autorizacion.sh`, que agrega un endpoint desprotegido a propósito, comprueba que la barrera de T029 se pone en **rojo**, lo quita y comprueba que vuelve a verde. El Principio V exige que toda barrera nueva pruebe que sabe fallar. Darle bit de ejecución y registrarlo con `git update-index --chmod=+x` — sobre `/mnt/c`, `core.fileMode` está en `false` y git no lo detecta solo (FIX-002). Agregar el paso a `.github/workflows/ci.yml`

### Backend — expiración

- [ ] T031 [TEST] [US2] Escribir el test de AC-12 **adelantando el `TimeProvider`** más de 24 h y comprobando que la petición siguiente devuelve `401`. Incluir el caso complementario: con actividad dentro de la ventana, la sesión **sigue** valiendo — sin él, una expiración rota "hacia el otro lado" pasaría en verde. **Nombrar AC-12**. Mostrar el rojo
- [ ] T032 [US2] Configurar `SlidingExpiration` con `ExpireTimeSpan` de 24 h en `Program.cs` ([D-03](./research.md))

### Frontend

- [ ] T033 [TEST] [P] [US2] Escribir en `frontend/tests/FormularioAcceso.test.tsx` los tests del formulario de acceso: usa `CampoConError`, el campo de contraseña es `type="password"`, lleva los `autocomplete` que corresponden, y se envía entero con teclado. Mostrar el rojo
- [ ] T034 [US2] Implementar `frontend/src/acceso/FormularioAcceso.tsx` con el conmutador *Iniciar sesión / Crear cuenta*, según [contracts/ui-pantalla.md](./contracts/ui-pantalla.md). El error de credenciales va a la **región del formulario** y no al lado de un campo: señalar uno de los dos diría cuál estaba bien
- [ ] T035 [TEST] [US2] Escribir en `frontend/tests/App.test.tsx` los tests de la raíz: mientras averigua muestra un indicador —**no** la pantalla de acceso, que haría parpadear el login en cada recarga—; sin sesión muestra acceso; con sesión muestra movimientos, que es la mitad de AC-03 que vive en la pantalla. **Nombrar AC-03**. Mostrar el rojo
- [ ] T036 [TEST] [US2] Agregar el test de que **cualquier `401`** devuelve a la pantalla de acceso con un aviso de sesión vencida, y que lo que se estaba haciendo **no desaparece en silencio**. Mostrar el rojo
- [ ] T037 [US2] Implementar `frontend/src/App.tsx` y ajustar `frontend/src/api/cliente.ts` para mandar la cookie (`credentials: 'include'`) y tratar el `401` como señal de sesión vencida ([D-09](./research.md))
- [ ] T038 [US2] Agregar el cierre de sesión a `frontend/src/movimientos/PantallaMovimientos.tsx`: un `<button>`, no un enlace — cambia estado del servidor, y los enlaces son para navegar
- [ ] T039 [VERIFY] [US2] Puerta completa de frontend y backend, más las tres barreras: contrato, linter y autorización

**Checkpoint**: sin sesión no se puede nada. La aplicación tiene frontera.

---

## Phase 5: User Story 3 — Los movimientos son de quien los cargó (P3)

**Goal**: lo que cada cuenta registra queda a su nombre, y su listado se calcula sólo sobre lo suyo.

**Independent Test**: con sesión iniciada, registrar un movimiento y verificar su propietario y que
el listado sólo trae los de esa cuenta.

- [ ] T040 [TEST] [US3] Agregar en `backend/GestionGastos.Api.Tests/Integracion/AltaMovimientoTests.cs` el test de que el propietario del movimiento es el usuario de la sesión y **no** un valor fijo. **Nombrar AC-07**. Mostrar el rojo
- [ ] T041 [TEST] [US3] Agregar en `backend/GestionGastos.Api.Tests/Integracion/ListadoMovimientosTests.cs` el test con **dos cuentas**: cada una ve únicamente sus movimientos. **Nombrar AC-08**. Mostrar el rojo
- [ ] T042 [US3] Ajustar lo que haga falta para que los dos pasen. Si `IUsuarioActual` quedó bien resuelto en T027 no debería hacer falta código nuevo: **decirlo así en el reporte en vez de fabricar un cambio**, y dejar los tests, que son lo que faltaba
- [ ] T043 [VERIFY] [US3] Puerta completa de frontend y backend

**Checkpoint**: las tres historias funcionan. Falta el aislamiento verificado a fondo, que es del ticket `01c`.

---

## Phase 6: Polish y cierre de la feature

- [ ] T044 Correr la cobertura con `dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings` y revisar que el código nuevo de `Cuentas/`, `Sesion/` y `Autorizacion/` quede medido
- [ ] T045 [P] Revisar que ningún AC de SC-007 quede sin test que lo nombre: AC-01..AC-12. Un AC sin test cubierto no cuenta como implementado (Principio II)
- [ ] T046 [P] Revisar que no haya ningún `any` sin comentario justificativo ni catch silencioso en `frontend/src/` y `backend/GestionGastos.Api/`, y que **ninguna contraseña ni hash aparezca en un log, un mensaje de error o una respuesta**
- [ ] T047 Actualizar `AGENTS.md` con la barrera nueva (`verificar-autorizacion.sh`) y con la segunda base de tests, para que la tabla de *Stack* siga siendo el único lugar donde vive esa información
- [ ] T048 [VERIFY] Puerta de cierre de feature, entera y con su salida a la vista: lint, format, typecheck, tests y build de producción en frontend; `dotnet format --verify-no-changes`, build con `-warnaserror`, `dotnet test` completo, cobertura, y las **tres** barreras

---

## Dependencies & Execution Order

### Phase Dependencies

```text
Phase 1 (Setup) ──→ Phase 2 (Foundational) ──→ Phase 3 (US1 · P1 · MVP)
                                                     │
                                                     ▼
                                               Phase 4 (US2 · P2)
                                                     │
                                                     ▼
                                               Phase 5 (US3 · P3)
                                                     │
                                                     ▼
                                               Phase 6 (Polish)
```

- **Phase 2 bloquea todo.** Sin la columna, el hash y la migración no hay cuentas.
- **US1 es el MVP** y entrega valor sola: existen cuentas y se puede entrar.
- **US2 depende de US1**, no al revés: no se puede exigir sesión sin una forma de obtenerla. Ésta es
  una dependencia real, no de conveniencia — es la razón por la que las historias **no** son
  paralelizables en esta feature, a diferencia de FEAT-001a.
- **US3 depende de US2**: necesita sesiones reales para que el propietario signifique algo.

### Parejas que van juntas, sin commit intermedio

| Tareas | Por qué |
|--------|---------|
| T026 + T027 + T028 | Activar la autorización global rompe toda la suite de FEAT-001a hasta que sus tests autentican |
| T029 + T030 | Una barrera sin su verificación de que sabe fallar no es una barrera (Principio V) |

## Parallel Opportunities

| Fase | Tareas en paralelo | Por qué |
|------|--------------------|---------|
| Setup | T002 con T001 | Archivos distintos, sin dependencias entre sí |
| US1 | T012 con T013 | Los dos son tests del mismo endpoint, en archivos separados |
| US2 | T022 con T023 | `GET` y `DELETE` de sesión no se pisan |
| US2 | T033 con el bloque de backend | Frontend y backend no comparten archivos |
| Polish | T045 con T046 | Revisiones independientes |

## Implementation Strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1).** Con eso ya existen cuentas reales y se puede entrar,
que es el cambio que este ticket viene a producir. La frontera (US2) es lo que lo vuelve útil de
verdad, y conviene no dejarla para otro día: entre US1 y US2 la aplicación tiene cuentas **y**
sigue abierta, que es el peor de los dos mundos.

Por eso, aunque US1 sea entregable, **no se despliega sola**. El PRD lo dice para el conjunto: los
tres tickets `01a`, `01b` y `01c` tienen que estar en `main` antes de exponer la aplicación a
usuarios reales.

---

description: "Task list — Límite de intentos fallidos de inicio de sesión"
---

# Tasks: Límite de intentos fallidos de inicio de sesión

**Input**: Design documents from `/specs/003-limite-intentos/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: **obligatorios y primero**. El Principio I de `.specify/memory/constitution.md` prohíbe
escribir código de producción sin un test que ya haya fallado, y el Principio II exige que cada AC
tenga un test que lo nombre. Toda tarea `[TEST]` termina con un **rojo real mostrado en la salida**
antes de que empiece la tarea de código.

**Organization**: agrupadas por historia de usuario. El orden de las tres historias no es sólo de
prioridad: **US1 hace que el bloqueo exista, US2 que se levante, US3 que no delate nada**. US3 va
última a propósito, porque su primer test tiene que encontrar el atajo que US1 va a dejar puesto —
el `if` que responde temprano— y ponerlo en rojo. Implementar US3 antes sería escribir el código
"bien" sin haber visto nunca el rojo que lo justifica.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede correr en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1, US2, US3)
- **[TEST]**: tarea de test — va **antes** que su tarea de código y tiene que quedar en rojo
- **[VERIFY]**: puerta del grupo. Se corren los comandos de `AGENTS.md` con su salida a la vista

## Path Conventions

Web app: `backend/` y `frontend/`, como fija `AGENTS.md`. **Esta feature no toca `frontend/`**
([D-09](./research.md)); su puerta se corre igual antes de cerrar.

---

## Phase 1: Setup

**Purpose**: arrancar desde una base conocida. No hay nada que instalar: esta feature no agrega
dependencias.

- [X] T001 Correr la puerta del backend sobre `main` reciente —`dotnet format --verify-no-changes`,
  `dotnet build -warnaserror`, `dotnet test backend/`— y dejar su salida a la vista. Es el verde de
  partida: sin él, el primer rojo de la feature no se distingue de un rojo heredado

---

## Phase 2: Foundational (bloquea a las tres historias)

**Purpose**: la tabla donde vive el contador. Ninguna historia puede empezar sin ella.

**⚠️ CRÍTICO**: hasta que la migración esté aplicada, todo test de las historias falla por "table
doesn't exist", que es un rojo que no verifica nada.

- [X] T002 [TEST] Escribir en `backend/GestionGastos.Api.Tests/Integracion/IntentoDeAccesoEsquemaTests.cs`
  el test del esquema: que `intento_de_acceso` acepta una fila, y que insertar el **mismo email con
  otra combinación de mayúsculas** choca contra la clave primaria en vez de crear una segunda fila.
  Esto último es lo que verifica la colación insensible a mayúsculas; sin este test, la colación
  binaria por defecto pasa desapercibida y el límite se esquiva escribiendo `Ana@` en vez de `ana@`
  (riesgo 2 del plan). Mostrar el rojo
- [X] T003 Crear `backend/GestionGastos.Api/Dominio/IntentoDeAcceso.cs` con las tres propiedades de
  [data-model.md](./data-model.md), y mapearla en
  `backend/GestionGastos.Api/Persistencia/GestionGastosDbContext.cs`: tabla `intento_de_acceso`,
  clave primaria `email` con colación `utf8mb4_0900_ai_ci`, `fallos_consecutivos` `tinyint unsigned`,
  `ultimo_fallo` `datetime(6)`, e índice `ix_intento_de_acceso_ultimo_fallo`
- [X] T004 Generar la migración con `dotnet ef migrations add LimiteDeIntentosFallidos` y **leerla
  antes de aplicarla**: tiene que crear una sola tabla y no tocar ninguna existente. Comprobar que
  el `Down` la elimina
- [X] T005 [VERIFY] Puerta del backend: `dotnet format --verify-no-changes`, `dotnet build
  -warnaserror`, `dotnet test backend/`

**Checkpoint**: la tabla existe y la colación está verificada. Las historias pueden empezar.

---

## Phase 3: User Story 1 - Probar contraseñas deja de ser gratis (Priority: P1) 🎯 MVP

**Goal**: tras 5 fallos consecutivos sobre un email, todo intento nuevo sobre ese email se rechaza
durante 15 minutos, incluido el que traiga la contraseña correcta.

**Independent Test**: fallar cinco veces contra un email y comprobar que el sexto intento se
rechaza, incluso presentando la contraseña correcta.

### Tests de la historia 1

- [X] T006 [TEST] [US1] Escribir en `backend/GestionGastos.Api.Tests/Unitarios/LimiteDeIntentosTests.cs`
  los tests de la regla **sin base de datos**: con 4 fallos no bloquea; con 5 y `ahora - ultimo_fallo
  < 15 min` bloquea; con 5 y la ventana vencida no bloquea; el borde exacto de los 15 minutos cae
  del lado de "ya no bloquea". Mostrar el rojo
- [X] T007 [US1] Implementar `backend/GestionGastos.Api/Sesion/LimiteDeIntentos.cs` con las tres
  constantes de [data-model.md](./data-model.md) —5 fallos, 15 min, 24 h— y la decisión de bloqueo
  como **función pura** sobre `(fallos, ultimoFallo, ahora)`. Que sea pura es lo que permite T006 sin
  levantar nada
- [X] T008 [TEST] [US1] Agregar en `backend/GestionGastos.Api.Tests/Integracion/LimiteDeIntentosTests.cs`
  los tests del bloqueo contra la API: cinco fallos y el sexto rechazado (**AC-01**); con la
  contraseña **correcta** dentro de la ventana también se rechaza y **no queda sesión iniciada**
  (**AC-02**); adelantando el reloj a los 14 min sigue rechazando (**AC-03**); con 4 fallos, el
  quinto intento correcto entra (**AC-04**). Usar `FactoriaConReloj`, nunca esperar. Mostrar el rojo
- [X] T009 [TEST] [P] [US1] Agregar en el mismo archivo los tests del alcance del bloqueo: el email
  A bloqueado no impide que B entre con su contraseña correcta (**AC-07**); un email **no
  registrado** también acumula y se bloquea al sexto (**AC-09**, la mitad que es de conteo — la
  indistinguibilidad de su respuesta es de US3); un `HttpClient` distinto, sin las cookies del
  primero, recibe el mismo rechazo sobre el email bloqueado (**AC-10**). Mostrar el rojo
- [X] T010 [TEST] [US1] Escribir en `backend/GestionGastos.Api.Tests/Integracion/BloqueoSobreviveAlReinicioTests.cs`
  el test de **AC-11**: bloquear un email, **descartar la aplicación y levantar otra** sobre la misma
  base, y comprobar que sigue rechazada. La segunda factoría se construye con el reloj en el mismo
  instante que la primera ([D-07](./research.md)); si arranca en el instante real, el test puede dar
  verde o rojo por el salto del reloj y no por lo que se está probando. Mostrar el rojo

### Implementación de la historia 1

- [X] T011 [US1] Enganchar la comprobación en `backend/GestionGastos.Api/Sesion/SesionEndpoints.cs`
  y registrar el servicio en `backend/GestionGastos.Api/Program.cs`. El incremento del contador va
  como **UPSERT atómico** (`INSERT ... ON DUPLICATE KEY UPDATE`) con
  `ExecuteSqlInterpolatedAsync`, nunca leer-modificar-guardar: cinco peticiones en paralelo leyendo
  0 y guardando 1 dejan el email a un fallo del límite después de cinco fallos
  ([D-05](./research.md)). El mismo `UPDATE` resuelve el reinicio por ventana vencida con un `IF` en
  SQL, sin leer antes. Un intento rechazado por el bloqueo **no toca la fila**: es lo que hace que la
  ventana sea fija y no deslizante
- [X] T012 [TEST] [US1] Escribir en `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoLimiteTests.cs`
  el test de **AC-12**: 100 ejecuciones del login con fila de contador y sin ella, descartando las de
  calentamiento, comparando **percentil 95** contra percentil 95 con tolerancia de 50 ms. Va en
  `Rendimiento/` para que el filtro del CI lo excluya ([D-08](./research.md)); en local corre
- [X] T013 [VERIFY] [US1] Puerta del backend completa, con los tests de rendimiento incluidos —en
  local corren todos

**Checkpoint**: el bloqueo existe, cuenta por email, alcanza sólo a ese email y sobrevive a un
reinicio. La feature ya cierra el riesgo aceptado de `01a`.

---

## Phase 4: User Story 2 - El bloqueo se levanta solo (Priority: P2)

**Goal**: la ventana vence sin que nadie intervenga, y un intento exitoso deja el contador en cero.

**Independent Test**: bloquear un email, adelantar el reloj más de 15 minutos, y comprobar que la
contraseña correcta vuelve a iniciar sesión.

### Tests de la historia 2

- [X] T014 [TEST] [US2] Agregar en `backend/GestionGastos.Api.Tests/Integracion/LimiteDeIntentosTests.cs`
  el test de **AC-05**: con fallos previos que no bloquean, un intento exitoso deja el contador en
  cero —la fila desaparece— y hacen falta **5 fallos nuevos** para bloquear. Verificar las dos
  mitades: que la fila no está, y que 4 fallos posteriores todavía no bloquean. Sin la segunda
  mitad, un reinicio parcial pasa en verde. Mostrar el rojo
- [X] T015 [TEST] [US2] Agregar el test de **AC-06**: con el email bloqueado, adelantar el reloj 15
  minutos y comprobar que la contraseña correcta **inicia sesión**, sin que nadie haya intervenido.
  Agregar el caso complementario: un fallo posterior al vencimiento deja el contador en **1** y no en
  6, así que hacen falta cuatro más para volver a bloquear ([D-03](./research.md)). Mostrar el rojo
- [X] T016 [TEST] [P] [US2] Agregar en el mismo archivo el test de la purga por inactividad: la fila
  de un email con `ultimo_fallo` de más de 24 h **desaparece** cuando se registra el fallo de otro
  email, y ese email vuelve a foja cero. Es la decisión de purga del plan
  ([D-03](./research.md)); sin test, la tabla crece para siempre y nadie se entera. Mostrar el rojo

### Implementación de la historia 2

- [X] T017 [US2] Ajustar `backend/GestionGastos.Api/Sesion/LimiteDeIntentos.cs` y
  `SesionEndpoints.cs`: borrar la fila en el inicio de sesión exitoso, y borrar en el camino del
  fallo las filas con `ultimo_fallo` de más de 24 h con un `ExecuteDeleteAsync` por índice. La purga
  va en el camino que **ya escribe**, no en el de lectura: el de lectura corre en todos los logins,
  incluidos los exitosos, y ahí se gasta el presupuesto de NFR-02
- [X] T018 [VERIFY] [US2] Puerta del backend completa

**Checkpoint**: nadie queda bloqueado para siempre, y la tabla no crece sin límite.

---

## Phase 5: User Story 3 - El bloqueo no delata qué emails existen (Priority: P3)

**Goal**: el rechazo por bloqueo es indistinguible del rechazo por credenciales incorrectas, en
mensaje, en código **y en tiempo**.

**Independent Test**: comparar las respuestas —y sus tiempos— de un email bloqueado, un email
inexistente y una contraseña incorrecta.

> **Este es el orden que importa.** T019 tiene que encontrar en rojo el atajo que T011 dejó puesto:
> si el bloqueo responde antes de verificar ningún hash, vuelve en ~2 ms contra ~100 ms, y ese
> cronómetro dice qué emails acumularon cinco fallos. Ver el rojo **es** la tarea; escribir el código
> "bien" desde T011 sin haberlo visto deja la protección sin prueba de que sabe fallar.

### Tests de la historia 3

- [X] T019 [TEST] [US3] Agregar en `backend/GestionGastos.Api.Tests/Integracion/LimiteDeIntentosTests.cs`
  el **test hermano determinista** de [D-04](./research.md): el camino del email bloqueado **ejecuta
  una verificación de hash**, exista o no la cuenta. Verifica la conducta que produce el tiempo, no
  milisegundos, así que corre en el CI y no es intermitente (Principio IV). Mostrar el rojo
- [X] T020 [TEST] [P] [US3] Agregar el test de **AC-08** y **AC-09**: la respuesta al rechazo por
  límite y la respuesta al rechazo por credenciales incorrectas tienen el **mismo código y el mismo
  cuerpo**, y lo mismo vale para un email bloqueado **no registrado** contra uno registrado y
  bloqueado. Comparar la respuesta entera, no sólo el status: un campo de más en el cuerpo es la
  misma filtración. Mostrar el rojo
- [X] T021 [TEST] [US3] Agregar en `backend/GestionGastos.Api.Tests/Rendimiento/RendimientoLimiteTests.cs`
  el test de **AC-13**: 100 ejecuciones del rechazo por bloqueo contra 100 del rechazo por
  credenciales incorrectas, percentil 95 contra percentil 95, tolerancia de 50 ms. Fuera del CI, como
  T012. Mostrar el rojo

### Implementación de la historia 3

- [X] T022 [US3] Ajustar `backend/GestionGastos.Api/Sesion/SesionEndpoints.cs` para que el camino
  del email bloqueado **igual verifique un hash** —el de la cuenta si existe, el `HashDescartable`
  si no— y recién entonces responda el `401`, con el mismo `Results.Problem` que el rechazo por
  credenciales. Dejar el comentario que explica que ese trabajo desperdiciado **es el requisito**:
  sin él, el primer refactor que vea código muerto lo borra y AC-13 se rompe sin poner ningún test
  funcional en rojo (riesgo 1 del plan)
- [X] T023 [VERIFY] [US3] Puerta del backend completa, con los tests de rendimiento

**Checkpoint**: las tres causas del `401` son la misma respuesta y tardan lo mismo.

---

## Phase 6: Polish & cierre

- [X] T024 Correr la cobertura con `dotnet test backend/GestionGastos.slnx --settings
  backend/cobertura.runsettings` y revisar que el código nuevo de `Sesion/LimiteDeIntentos.cs` y el
  camino nuevo de `SesionEndpoints.cs` queden medidos
- [X] T025 [P] Revisar que ningún AC quede sin test que lo nombre: **AC-01..AC-13**. Un AC sin test
  cubierto no cuenta como implementado (Principio II)
- [X] T026 [P] Revisar que no haya `any` sin justificar ni catch silencioso en el código nuevo, y que
  **ningún email, hash ni estado del contador aparezca en un log, un mensaje de error o una
  respuesta**. El estado del contador es tan revelador como el hash: publicarlo es publicar qué
  emails existen
- [X] T027 [P] Actualizar `plan-de-implementacion/README.md`: `1a` pasa a la tabla de implementados
  —quedó pendiente cuando se mergeó `002`— y `1b` sale de la tabla de pendientes
- [X] T028 [VERIFY] Puerta de cierre de feature, entera y con su salida a la vista: `dotnet format
  --verify-no-changes`, build con `-warnaserror`, `dotnet test` completo, cobertura, y las **tres**
  barreras (`verificar-contrato.sh`, `verificar-autorizacion.sh`, `verificar-linter.sh`). Más la
  puerta del frontend —lint, format, typecheck, tests, build— aunque esta feature no lo toque: se
  corre para comprobar justamente eso

---

## Dependencies & Execution Order

### Entre fases

- **Setup (T001)**: sin dependencias
- **Foundational (T002–T005)**: bloquea a las tres historias. Sin la tabla, todo test de historia
  falla por "table doesn't exist", que es un rojo que no verifica nada
- **US1 (T006–T013)**: depende de Foundational. Es el MVP
- **US2 (T014–T018)**: depende de US1 — no se puede verificar que un bloqueo se levanta si no hay
  bloqueo
- **US3 (T019–T023)**: depende de US1, y **tiene que ir después** para que T019 encuentre el atajo
  en rojo
- **Polish (T024–T028)**: depende de las tres

### Dentro de cada historia

- El `[TEST]` va antes que su tarea de código, siempre, con el rojo mostrado
- La función pura (T007) antes del endpoint (T011)
- `[VERIFY]` cierra la historia; no se pasa a la siguiente con la puerta en rojo

### Oportunidades de paralelismo

Pocas y honestas: casi todo el código de esta feature vive en dos archivos, y dos tareas sobre el
mismo archivo no van en paralelo.

- T009 con T008: archivos de test distintos no, **el mismo archivo** — pero son bloques
  independientes, así que se pueden escribir en cualquier orden. Marcado `[P]` por eso
- T016 con T014/T015: mismo caso
- T020 con T019: mismo caso
- T025, T026 y T027 son revisiones sobre cosas distintas: van en paralelo de verdad

---

## Implementation Strategy

### MVP (sólo US1)

1. T001 → T005: la tabla existe y su colación está verificada
2. T006 → T013: el bloqueo funciona, cuenta por email y sobrevive a un reinicio
3. **PARAR Y VALIDAR**: cinco fallos, el sexto rechazado con la contraseña correcta, reinicio y sigue
   rechazado
4. Ya en este punto el riesgo aceptado de `01a` está cerrado

### Entrega incremental

1. MVP → nadie puede probar contraseñas sin fin
2. + US2 → nadie queda bloqueado para siempre, y la tabla no crece sin límite
3. + US3 → el bloqueo deja de ser un oráculo de qué emails existen
4. + Polish → cobertura, las tres barreras y el README del plan al día

---

## Notes

- **Ningún test duerme.** La ventana se verifica con `FactoriaConReloj` / `RelojFijo`, que `002` ya
  dejó puestos. Un `Task.Delay(15 min)` en esta suite es un error, no una alternativa lenta
- Los dos tests de tiempo (T012, T021) **no corren en el CI** y sí en local: un rojo ahí se mira dos
  veces antes de creerle, pero no se ignora
- Commit por tarea o por grupo lógico, nunca con la puerta en rojo
- `frontend/` no se toca en ninguna tarea salvo para correrle la puerta en T028

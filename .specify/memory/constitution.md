# GestionGastos Constitution

Este documento fija los principios no negociables del proyecto. Lo leen `/speckit-plan`
(sección *Constitution Check*), `/speckit-tasks` y `/speckit-implement`. Si una spec, un plan o una
tarea contradicen algo de acá, gana la constitución: hay que corregir el artefacto, no la regla.

El contexto del proyecto (stack, comandos exactos, convenciones, glosario) vive en `AGENTS.md` y no
se duplica acá.

## Core Principles

### I. Test-First (NO NEGOCIABLE)

TDD obligatorio. Para cada tarea de implementación el orden es:

1. Se escribe el test que expresa el criterio de aceptación (AC) del PRD/spec.
2. Se corre y **tiene que fallar** por la razón esperada (rojo real, no un error de compilación
   accidental). Ese rojo se muestra en la salida antes de seguir.
3. Recién ahí se escribe el código mínimo que lo pone en verde.
4. Se refactoriza con los tests en verde.

Prohibido escribir código de producción sin un test que ya haya fallado primero. Prohibido ajustar
un test para que pase cuando el que está mal es el código.

### II. Cada AC tiene su test, y el test lo nombra

Todo criterio de aceptación del PRD se traduce en al menos un test automatizado, y el test cita el
identificador (`AC-12`, `RNF-03`) en su nombre o en un comentario. Un AC sin test cubierto no se
considera implementado, aunque la feature "funcione" a mano.

### III. La etapa de testing es una fase con puerta, no un paso opcional

El ciclo de cada tarea es `RED → GREEN → REFACTOR → VERIFY`. **VERIFY** es una fase propia y su
puerta es la suite completa del stack afectado (ver *Quality Gates*). Una tarea no se marca como
completada — ni en `tasks.md`, ni en un commit, ni en un reporte al usuario — hasta que su puerta
esté en verde y la salida real de los comandos se haya mostrado.

Si un test queda en rojo y no se puede arreglar, se reporta explícitamente con la salida del
comando. Nunca se declara "listo" con la suite en rojo, y nunca se saltea la puerta por apuro.

### IV. Tests deterministas y aislados

Nada de tests que dependan del orden de ejecución, de la fecha de hoy, de una red externa o de datos
que dejó otro test. Los tests de backend corren contra `gestiongastos_test`, nunca contra la base de
desarrollo. Un test intermitente se arregla o se borra: no se tolera un rojo "que a veces pasa",
porque entrena a ignorar la suite.

### V. Las barreras de calidad se verifican a sí mismas

Las verificaciones que protegen el proyecto (contrato frontend↔backend, linter del backend) tienen
que probar que se ponen en **rojo** cuando lo que protegen se rompe. Para eso existen
`backend/verificar-contrato.sh` y `backend/verificar-linter.sh`. Una barrera que nunca se vio fallar
no es una barrera. Lo mismo aplica a cualquier barrera nueva que se agregue.

## Quality Gates

La puerta de VERIFY. Se corren los comandos del stack tocado, con su salida a la vista. Los comandos
exactos y su porqué están en la tabla de `AGENTS.md`; acá está qué exige la puerta y cuándo.

| Cuándo | Frontend | Backend |
|--------|----------|---------|
| Por tarea (VERIFY) | `lint` + `tsc --noEmit` + `test` | `dotnet format --verify-no-changes` + `dotnet build -warnaserror` + `dotnet test` |
| Antes de cerrar una feature | lo anterior + build de producción | lo anterior + cobertura + `verificar-contrato.sh` + `verificar-linter.sh` |

Reglas de la puerta:

- **Verde significa cero fallos y cero warnings.** El backend compila con `-warnaserror`: un hallazgo
  de los analizadores rompe el build y eso cuenta como puerta en rojo.
- **Se corre lo que toca la tarea**, pero antes de cerrar la feature se corre todo, incluidas las dos
  barreras (`verificar-contrato.sh`, `verificar-linter.sh`), aunque tarden.
- **No se commitea con la puerta en rojo.** Tampoco se abre PR.
- **No se apaga una regla del linter ni se desactiva un test para pasar la puerta.** Si una regla
  hay que apagarla, se apaga en `backend/.editorconfig` con el motivo escrito, y la decisión se
  justifica en la spec.
- Un test saltado (`Skip`, `it.skip`) es deuda visible: lleva comentario con el motivo y un ticket.

## Development Workflow

- El flujo es el de Spec Kit: `/speckit-specify → /speckit-clarify → /speckit-plan → /speckit-tasks →
  /speckit-implement`, con `/speckit-analyze` y `/speckit-checklist` cuando la feature lo amerite.
- `/speckit-plan` completa el *Constitution Check* contra este archivo **antes** de escribir el
  diseño, y otra vez después. Una violación se resuelve o se documenta en *Complexity Tracking* con
  su justificación; no se ignora.
- `/speckit-tasks` genera, para cada tarea de implementación, su tarea de test **antes** que la de
  código, y una tarea de VERIFY explícita al cierre de cada grupo.
- `/speckit-implement` no avanza a la tarea siguiente con la puerta de la anterior en rojo.
- CI (`.github/workflows/ci.yml`) corre la misma puerta en cada push y PR. Es la red de seguridad,
  no el reemplazo: la puerta se corre en local primero.

## Governance

Esta constitución está por encima de cualquier otra práctica del repo. Una enmienda requiere: el
cambio escrito acá, el motivo, y el bump de versión (semver: MAJOR si se saca o se invierte un
principio, MINOR si se agrega uno o una sección, PATCH si es redacción). Los pedidos de "esta vez
saltate los tests" se rechazan; lo que sí se puede es acotar el alcance de la tarea.

**Version**: 1.0.0 | **Ratified**: 2026-08-23 | **Last Amended**: 2026-08-23

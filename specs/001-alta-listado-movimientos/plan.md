# Implementation Plan: Alta de movimientos y listado simple

**Branch**: `001-alta-listado-movimientos` | **Date**: 2026-08-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-alta-listado-movimientos/spec.md`

## Summary

Registrar gastos e ingresos con un formulario y verlos en un listado del mes actual, todo en una
sola pantalla. Es el ticket FEAT-001a del plan `DISC-001`, reconstruido desde cero sobre un
repositorio que todavía no tiene código.

El enfoque técnico: una API .NET sobre MySQL con el propietario, la moneda y la categoría ya
modelados como datos —para que la autenticación (ticket 1a), las categorías propias (3) y las
monedas (4a/4b) entren después sin migrar nada—, y un frontend React de una sola pantalla. La
validación vive en dos capas y emite un único formato de error por campo. Como el andamiaje del
repositorio no existe y `.github/workflows/ci.yml` ya lo invoca, esta feature crea también la
solución, el linter y las dos barreras de calidad.

Decisiones y sus alternativas descartadas: [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.301) en backend; TypeScript en frontend

**Primary Dependencies**: ASP.NET Core, Entity Framework Core 9.0.18 + Pomelo.MySQL 9.0.0, React 19
+ Vite. Nuevas en esta feature, sólo de desarrollo y justificadas en
[research.md D-10](./research.md): `@testing-library/react`, `@testing-library/user-event`, `jsdom`

**Storage**: MySQL 8.4.10 local, puerto 3306, schema `gestiongastos`; tests contra
`gestiongastos_test`

**Testing**: xUnit en backend (contra MySQL real, no proveedor en memoria), Vitest en frontend

**Target Platform**: aplicación web; API en Linux/Windows, frontend en navegador de escritorio

**Project Type**: web application (backend + frontend separados)

**Performance Goals**: confirmación del guardado < 1 s p95 sobre 100 ejecuciones (SC-001 / AC-34 /
RNF-02)

**Constraints**: sin dependencias de producción nuevas; techo de ~300 líneas agregadas por commit
(ver *Complexity Tracking* para la excepción del primer incremento); la cadena de conexión va en
user-secrets, nunca en `appsettings`

**Scale/Scope**: una cuenta, una pantalla, 3 endpoints, 4 entidades. El horizonte de rendimiento
del producto es 10.000 movimientos por cuenta (RNF-01), que esta feature no mide todavía pero cuyo
índice ya deja puesto

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Cómo lo cumple este plan | Estado |
|-----------|--------------------------|--------|
| **I. Test-First (no negociable)** | `/speckit-tasks` genera la tarea de test antes que la de código para cada unidad. El rojo real se muestra antes de escribir producción. | ✅ Pasa |
| **II. Cada AC tiene su test, y el test lo nombra** | Los 10 AC de SC-005 están mapeados a un test nombrado en la tabla de *Trazabilidad* de abajo. | ✅ Pasa |
| **III. VERIFY es una fase con puerta** | La puerta es la tabla de *Quality Gates* de la constitución, con los comandos de `AGENTS.md`. Cada incremento cierra con VERIFY. | ✅ Pasa |
| **IV. Tests deterministas y aislados** | `RangoDelMes.De(hoy)` es puro y parametrizado por fecha ([D-03](./research.md)); el sembrado usa fechas relativas, no un año fijo; los tests van contra `gestiongastos_test`. | ✅ Pasa |
| **V. Las barreras se verifican a sí mismas** | Esta feature **crea** `verificar-linter.sh` y `verificar-contrato.sh`, y ambas prueban que se ponen en rojo cuando lo que protegen se rompe. | ⚠️ Pasa, con costo — ver *Complexity Tracking* |

**Post-diseño (Phase 1)**: re-evaluado sobre `data-model.md`, `contracts/` y `quickstart.md`. Sin
gates nuevos en rojo. El único desvío sigue siendo el tamaño del primer incremento, ya registrado.

### Trazabilidad AC → test

Cada AC de SC-005 cita su identificador en el nombre del test, como exige el Principio II.

| AC | Qué verifica | Dónde vive el test |
|----|--------------|--------------------|
| AC-10 | El selector ofrece sólo las categorías del tipo que se carga | Backend (contrato del catálogo) + frontend (render del selector) |
| AC-15 | El gasto guardado aparece en el listado | Backend (integración) + frontend (ciclo completo) |
| AC-16 | El ingreso guardado aparece en el listado | Backend (integración) + frontend |
| AC-17 | Sin tocar la fecha, se registra con la de hoy | Backend (parametrizado por fecha) + frontend |
| AC-18 | Monto vacío, ≤ 0, > 2 decimales o > 999.999.999,99 se rechaza con motivo | Backend (validación) + frontend (mensaje junto al campo) |
| AC-22 | El listado trae gastos e ingresos del mes actual | Backend (integración) |
| AC-25 | El recorte por defecto es el mes actual, extremos incluidos | Backend (bordes de mes, fechas fijas) |
| AC-34 | Guardado < 1 s p95 sobre 100 ejecuciones | Backend, suite `Rendimiento` (excluida en CI) |
| AC-40 | Sin categoría se rechaza con motivo | Backend + frontend |
| AC-55 | El formulario se completa y envía sólo con teclado | Frontend (`user-event`) |

## Contrato de marcado de la UI

**Alcance deliberadamente chico.** Acá se decide **qué contrato cumple el marcado**, no cómo se ve.
Colores, espaciados, tipografía y disposición final son del ticket 6 (*Maquetación y
accesibilidad*), y el plan `DISC-001` es explícito en que una pasada de maquetación sobre pantallas
que todavía no existen se rehace.

Lo que sí se decide ahora es lo que sale caro retrofitear y lo que, si no tiene regla, cada feature
resuelve a su manera — que es la cicatriz que `plan-DISC-001.md` deja anotada: *"Ninguna de las
tres features de FEAT-001 definió su maquetación: el CSS resuelve lo semántico pero las clases de
disposición no tienen regla"*.

### 1. Accesibilidad del formulario (FR-015 / AC-55)

- El formulario es un `<form>` real y se envía con `<button type="submit">`. El envío con Enter
  desde cualquier campo funciona porque el navegador ya lo hace: no se reimplementa con handlers de
  tecla.
- Cada control tiene un `<label for="...">` apuntando a su `id`. Nada de `placeholder` como
  etiqueta ni de etiquetas puestas sólo con `aria-label`, salvo que no haya texto visible posible.
- **El orden del DOM es el orden de tabulación.** No se usa `tabindex` positivo. Si el orden visual
  necesitara diferir del orden del documento, se reordena el documento, no el `tabindex`.
- El foco visible se resuelve con `:focus-visible` y **nunca** se anula `outline` sin reemplazarlo
  por un indicador equivalente. Un `outline: none` sin sustituto es un fallo de esta regla, no una
  cuestión de gusto.
- Tras un guardado exitoso, el foco vuelve por código al primer campo del formulario (FR-014). Es
  lo que permite encadenar cargas sin tocar el mouse, y es lo que AC-55 verifica de punta a punta.
- El selector de categoría es un `<select>` nativo con `<optgroup>` si hiciera falta agrupar. Un
  combo propio tendría que reimplementar teclado, foco y anuncio, y `AGENTS.md` prohíbe la
  dependencia que lo evitaría.

### 2. Dónde vive el error de validación (FR-004, FR-004b, FR-005, FR-011)

Un solo patrón para las cuatro validaciones, y para las dos capas que las producen:

- El mensaje va en un elemento **inmediatamente después del control**, con `id="{campo}-error"`.
- El control lleva `aria-describedby="{campo}-error"` y `aria-invalid="true"` mientras el error
  esté presente, y ninguno de los dos cuando no lo esté.
- El contenedor del mensaje lleva `role="alert"` para que el error se anuncie al aparecer, sin
  mover el foco.
- **Un único componente de campo** encapsula esa estructura. Ninguna pantalla arma la tripleta
  `label` + control + error a mano: así el ticket 6 cambia la presentación en un solo lugar.
- Los errores que devuelve el servidor llegan en el diccionario `errors` del `ProblemDetails`
  ([D-07](./research.md)), indexado por nombre de campo, y se enrutan **al mismo lugar** que los
  del cliente. El origen del error no cambia dónde se muestra.
- Nada de `alert()`, ni notificaciones flotantes, ni un bloque de errores agrupado arriba del
  formulario. Un error que no está al lado de su campo obliga a buscarlo.
- Si un error no corresponde a ningún campo (por ejemplo, un fallo al persistir), va a una región
  de error del formulario, también con `role="alert"`, y el formulario **conserva lo cargado**
  (regla de *Edge Cases* en la spec).

### 3. Regla de nombres para las clases de disposición

El hueco que el plan denuncia. La regla, entera:

- **`l-*` es disposición, `c-*` es componente.** `l-` (layout) sólo aparece en contenedores que
  posicionan a sus hijos; `c-` en el elemento que *es* la cosa. Un mismo elemento puede llevar las
  dos, y en ese caso la de layout va primero: `class="l-pila c-formulario"`.
- **Un elemento con `c-` no define su propia posición ni su propio margen externo.** Dónde va lo
  decide su contenedor `l-`. Es lo que permite reubicar un componente en el ticket 6 sin editarlo.
- **Nada de clases utilitarias sueltas** (`mt-2`, `flex`, `text-right`). Si hace falta una
  disposición nueva, nace como `l-` con nombre propio y queda disponible para el resto.
- Los nombres van en español, como el resto del código y el glosario de `AGENTS.md`:
  `c-formulario-movimiento`, `c-listado-movimientos`, `l-pila`, `l-fila`.
- Los elementos semánticos (`form`, `table`, `label`, `button`) se estilan por su selector cuando
  la regla vale para todos; la clase se agrega sólo cuando hace falta distinguir un caso.

Todo lo que no esté en estas tres reglas es decisión abierta del ticket 6.

## Project Structure

### Documentation (this feature)

```text
specs/001-alta-listado-movimientos/
├── plan.md              # Este archivo
├── research.md          # Phase 0: 12 decisiones con sus alternativas descartadas
├── data-model.md        # Phase 1: entidades, restricciones e índices
├── quickstart.md        # Phase 1: cómo levantar y validar la feature
├── contracts/
│   ├── api-http.md      # Los 3 endpoints y el formato de error
│   └── ui-pantalla.md   # Estructura de la pantalla y sus estados
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec (16/16)
└── tasks.md             # Phase 2 (/speckit-tasks — NO lo crea /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.sln
├── Directory.Build.props          # Enciende los analizadores de Roslyn
├── .editorconfig                  # Qué reglas se aplican y cuáles se apagan, con su motivo
├── verificar-linter.sh            # Barrera: prueba que el linter sabe romper el build
├── verificar-contrato.sh          # Barrera: prueba que el contrato sabe ponerse en rojo
├── cobertura.runsettings
├── GestionGastos.Api/
│   ├── Movimientos/               # Endpoint de alta y de listado, validación, DTOs
│   ├── Categorias/                # Endpoint del catálogo
│   ├── Dominio/                   # Movimiento, Categoria, Moneda, Usuario, RangoDelMes
│   ├── Persistencia/              # DbContext, configuraciones, IUsuarioActual
│   └── Migrations/                # Excluidas del linter a propósito
└── GestionGastos.Api.Tests/
    ├── Contrato/                  # Lee frontend/src/api/tipos.ts (excepción declarada en AGENTS.md)
    ├── Integracion/               # Contra MySQL real
    ├── Unitarios/                 # RangoDelMes y validación
    └── Rendimiento/               # AC-34 (excluida en CI)

frontend/
├── src/
│   ├── api/
│   │   └── tipos.ts               # Fuente de verdad del contrato HTTP
│   ├── movimientos/               # Formulario, listado, pantalla
│   ├── ui/                        # El componente de campo con su error
│   └── estilos/
└── tests/
```

**Structure Decision**: backend y frontend separados en sus carpetas, como fija `AGENTS.md`. La
única excepción es la declarada allí: los tests de `backend/GestionGastos.Api.Tests/Contrato/`
**leen** `frontend/src/api/tipos.ts`. Es lectura en una sola dirección; el frontend no lee nada del
backend.

## Incrementos de entrega

El techo de ~300 líneas por commit obliga a partir el trabajo. El orden respeta el Test-First y deja
cada incremento con su puerta en verde.

Se organizan **por historia de usuario**, no por capa, para que cada incremento sea entregable y
verificable solo. El desglose en tareas está en [tasks.md](./tasks.md).

| # | Incremento | Qué entra | Puerta |
|---|-----------|-----------|--------|
| **1** | Andamiaje | Solución, proyectos, linter, frontend base | Build, formato, lint, typecheck |
| **2** | Dominio y barreras | Esquema, entidades, `RangoDelMes`, catálogo de categorías (FR-006), `verificar-linter.sh` y `verificar-contrato.sh` | Puerta completa, incluidas las dos barreras |
| **3** | US1 backend | FR-001, FR-003, FR-007, FR-008, FR-009, FR-010, FR-012 | Puerta completa |
| **4** | US1 frontend | FR-013, FR-014, FR-015 — **acá el MVP queda entregable** | Puerta completa + AC-55 |
| **5** | US2 | FR-002, y la mitad de ingresos de AC-10 y AC-16 | Puerta completa |
| **6** | US3 | FR-004, FR-004b, FR-005, FR-011 | Puerta completa |
| **7** | Cierre | AC-34, cobertura, el ADR de la excepción de estructura | Puerta completa + cobertura + las dos barreras |

**Sólo el 1 rompe el techo** de ~300 líneas, por el motivo registrado abajo. Del 2 en adelante
entran holgados.

**US3 va después de US1 y US2 por diseño**: el camino feliz tiene que existir antes de poder
desviarse de él, y el `CHECK (monto > 0)` del esquema ya está puesto desde el incremento 2, así que
el hueco intermedio es más chico de lo que parece.

## Complexity Tracking

| Violación | Por qué hace falta | Alternativa más simple, y por qué se rechazó |
|-----------|--------------------|---------------------------------------------|
| El incremento A excede el techo de ~300 líneas por commit | `.github/workflows/ci.yml` ya invoca `Directory.Build.props`, `.editorconfig`, `verificar-contrato.sh` y `verificar-linter.sh`. Ninguno existe: el CI falla en el primer push mientras no estén. El plan `DISC-001` los da por hechos (D-1 a D-4, FEAT-003) porque venían de la versión anterior, pero ni el código ni `prds/implementados/` están en este repositorio. | **Apagar los pasos del CI hasta que las piezas existan** — contradice el Principio V y repite el error que el plan documenta con FEAT-001c, que se escribió y mergeó sin linter justamente porque el linter no molestaba. **Sacar el andamiaje a una feature aparte** — es más limpio en el papel, pero deja una feature entera sin nada que verificar: las barreras necesitan un endpoint y un tipo reales contra los cuales fallar, y sin eso `verificar-contrato.sh` no tiene contrato que romper. Por eso A incluye el catálogo de categorías: es el trozo más chico de producto que le da a las dos barreras algo real que proteger. |

# Phase 0 — Research: Alta de movimientos y listado simple

Decisiones técnicas previas al diseño. El stack ya está fijado en `AGENTS.md` y no se investiga
acá; lo que se resuelve son las incógnitas que la spec dejó abiertas y las que nacen de arrancar
sobre un repositorio sin código.

---

## D-01 — Tipo y precisión del monto

**Decisión**: `decimal(11,2)` en MySQL, `decimal` en C#, con validación explícita del rango
`0.01 .. 999999999.99` en la capa de aplicación.

**Rationale**: FR-004b fija el techo en 999.999.999,99, que son exactamente 9 dígitos enteros más 2
decimales. `decimal(11,2)` es el tipo más chico que lo contiene sin margen sobrante, y es exacto:
no arrastra el error de representación que haría fallar las sumas por categoría del ticket 5. La
validación va además en la aplicación porque FR-004b exige un motivo visible, y el rechazo del
esquema llega como error genérico de almacenamiento.

**Alternativas consideradas**: `double`/`float` — descartado, la aritmética binaria no representa
`0.10` de forma exacta y los totales del dashboard quedarían con centavos fantasma. Entero de
centavos — exacto también, pero obliga a convertir en todos los bordes y el ticket 4a introduce
monedas con distinta cantidad de decimales, donde el factor deja de ser 100 fijo.

---

## D-02 — Tipo de la fecha del movimiento

**Decisión**: `DateOnly` en C#, columna `date` en MySQL. Sin hora, sin zona horaria.

**Rationale**: el dominio es el día en que ocurrió el movimiento, no un instante. Guardar
`datetime` obliga a decidir una zona horaria que el PRD nunca menciona y hace que un movimiento
cargado a las 23:00 pueda caer en otro mes al leerlo, rompiendo el recorte de FR-007 y el borde de
mes que la spec enumera en *Edge Cases*.

**Alternativas consideradas**: `datetime` en UTC — agrega una conversión en cada borde y un modo de
fallo (el movimiento que cambia de mes) a cambio de una precisión que nadie pide.

---

## D-03 — Cómo se calcula "el mes actual" (FR-007)

**Decisión**: un tipo puro `RangoDelMes.De(DateOnly hoy)` que devuelve el primer y el último día
del mes de `hoy`, extremos incluidos. La consulta filtra por ese rango. El "hoy" se inyecta, nunca
se lee de `DateTime.Now` dentro de la consulta.

**Rationale**: el Principio IV de la constitución prohíbe tests que dependan de la fecha de hoy.
Parametrizar la función por fecha es lo que la vuelve testeable: los bordes de mes —incluido
febrero y los meses de 30 y 31 días— se verifican pasando fechas fijas, sin esperar al 1 de marzo.
Es además la lección que `plan-DISC-001.md` deja escrita en FIX-004, donde un sembrado anclado a un
año fijo vencía el 2027-01-01.

**Alternativas consideradas**: calcular el rango en el frontend y mandarlo como parámetro —
descartado: FR-007 dice que el recorte es fijo y no se expone como control, y ponerlo en el cliente
lo convierte en algo que el cliente puede cambiar.

---

## D-04 — Orden del listado y su verificación (FR-008)

**Decisión**: ordenar por `fecha DESC, id DESC` de forma explícita en la consulta, con índice
`(usuario_id, fecha DESC, id DESC)`. El orden se verifica con **doble capa**: un test sobre el
resultado y otro que falla si el `OrderBy` desaparece de la consulta.

**Rationale**: `plan-DISC-001.md` lo advierte textualmente — el índice hace que MySQL devuelva el
orden correcto aunque la consulta no lo pida, así que un test que sólo mire el resultado pasa en
verde con el `OrderBy` borrado. `id DESC` es el desempate que la spec exige para dos movimientos
de la misma fecha: el último cargado va primero, que es lo que la persona espera ver al guardar.

**Alternativas consideradas**: desempatar por fecha de creación — obliga a una columna más que
nadie muestra; el `id` autoincremental ya expresa el mismo orden.

---

## D-05 — De dónde sale la cuenta propietaria (FR-010)

**Decisión**: una abstracción `IUsuarioActual` con una única implementación que devuelve el id de
una fila semilla fija. El `INSERT` toma el propietario de ahí, a mano, siempre.

**Rationale**: es la decisión que `plan-DISC-001.md` ya tomó para que el ticket 1a *reemplace* esa
abstracción en vez de migrar datos. El plan también advierte que el filtro global de lectura no
aplica al `INSERT`: si la escritura no asigna el propietario explícitamente, el aislamiento del
ticket 1c nace roto. Escribirlo así desde ahora convierte una convención en el único camino.

**Alternativas consideradas**: columna sin propietario, agregada después — obliga a una migración
con backfill sobre datos reales, exactamente lo que el plan quiso evitar.

---

## D-06 — Categorías y monedas como datos, no como enums

**Decisión**: dos tablas con semilla. `categoria` con `usuario_id` nullable (`NULL` = predefinida
del sistema) y `tipo`; `moneda` con `codigo`, `decimales` y `es_predeterminada`.

**Rationale**: RF-32 exige poder sumar una moneda al catálogo **sin modificar el código**, lo que
descarta un enum de plano. En categorías, el `usuario_id` nullable es lo que deja entrar el ticket
3 (categorías propias) sin migrar la tabla: una categoría propia es una fila con `usuario_id`
lleno. `decimales` por moneda es lo que el PRD anota en *Supuestos abiertos* y lo que el ticket 4a
va a necesitar.

**Alternativas consideradas**: enum en código para categorías —más simple hoy, pero el ticket 3
tendría que migrarlas a tabla y reescribir cada consulta que las toque.

---

## D-07 — Forma del error de validación

**Decisión**: `ProblemDetails` de validación (RFC 9457), que .NET produce de fábrica, con un
diccionario `errors` indexado por nombre de campo. Un único formato para FR-004, FR-004b, FR-005 y
FR-011.

**Rationale**: es lo que permite que el frontend ponga cada mensaje al lado de su campo en vez de
volcar un texto suelto, que es la mitad del contrato de marcado de este plan. No agrega ninguna
dependencia. Y al ser el mismo formato para las cuatro validaciones, el ticket 6 no tiene que
unificar tres patrones distintos, que es la cicatriz que `plan-DISC-001.md` denuncia.

**Alternativas consideradas**: un `{ mensaje: string }` propio — más chico, pero pierde la
asociación campo↔error y obliga a parsear texto en el cliente para saber qué campo marcar.

---

## D-08 — Validación en dos capas

**Decisión**: la regla se declara en la capa de aplicación (que produce el `ProblemDetails`) y se
respalda con restricciones en el esquema (`CHECK monto > 0`, clave foránea a `categoria`, `NOT
NULL`).

**Rationale**: FR-011 exige que la validación se aplique también cuando el dato llega por fuera del
formulario. El esquema es la única capa que no se puede saltear; la de aplicación es la única que
da un motivo entendible. Ninguna de las dos sola cumple el requerimiento entero.

**Alternativas consideradas**: validar sólo en el formulario — lo descarta FR-011 explícitamente.

---

## D-09 — Barrera del contrato frontend↔backend

**Decisión**: `frontend/src/api/tipos.ts` es la fuente de verdad del contrato. Tests en
`backend/GestionGastos.Api.Tests/Contrato/` leen ese archivo y lo comparan contra el JSON que la
API emite de verdad, en las dos direcciones. `backend/verificar-contrato.sh` comprueba que esa
comparación se pone en **rojo** cuando el contrato se desalinea.

**Rationale**: es la conclusión medida que `plan-DISC-001.md` deja en D-2 — los tipos del frontend
están escritos a mano y no derivan del backend, así que un rename coherente del backend deja en
verde el build, `tsc`, ESLint y toda la suite, y hace llegar `undefined` a la pantalla. `tsc`
verifica que el frontend sea coherente **consigo mismo**, no que coincida con el backend. El
Principio V de la constitución exige además que la barrera pruebe que sabe fallar.

**Alternativas consideradas**: generar los tipos del frontend desde OpenAPI — elimina la clase de
error entera, pero agrega un generador y un paso de build que `AGENTS.md` obliga a justificar, y
`AGENTS.md` ya declara la lectura de `tipos.ts` como la única excepción de estructura del proyecto,
con su ADR. Cambiar eso es una decisión de arquitectura que excede esta feature.

---

## D-10 — Dependencias nuevas del frontend (requieren justificación por `AGENTS.md`)

**Decisión**: agregar `@testing-library/react`, `@testing-library/user-event` y `jsdom` como
dependencias **de desarrollo** del frontend. Ninguna dependencia nueva de producción.

**Rationale**: AC-55 exige que el formulario se pueda recorrer, completar y enviar **íntegramente
con el teclado**, y FR-014 exige que tras guardar el foco vuelva al primer campo. `user-event` es
lo que simula tabulación y foco reales; sin él, AC-55 no tiene test automatizado y el Principio II
de la constitución queda incumplido. `jsdom` es el entorno DOM que Vitest necesita para eso.
`@testing-library/react` es la forma estándar de montar el componente sin acoplarse a su estructura
interna.

**Alternativas consideradas**: verificar AC-55 a mano en cada release — lo prohíbe el Principio II
(un AC sin test cubierto no se considera implementado). Playwright o similar — cubre el teclado de
verdad en un navegador, pero es una dependencia mucho más pesada y un runner nuevo, y `AGENTS.md`
fija Vitest como el runner del frontend.

---

## D-11 — Sin router ni librería de estado en el frontend

**Decisión**: una sola pantalla, estado en el componente. Sin React Router, sin Redux/Zustand, sin
cliente de datos.

**Rationale**: FR-013 fija que formulario y listado viven en la misma pantalla, sin navegación
intermedia. No hay una segunda ruta que enrutar ni estado compartido entre pantallas que
sincronizar. `AGENTS.md` prohíbe dependencias sin justificarlas, y acá no hay nada que justificar.

**Alternativas consideradas**: agregar el router "porque después va a hacer falta" — el ticket 5
agrega la pantalla de dashboard y ahí se justificará con un caso real, que es cuando corresponde.

---

## D-12 — Alcance del andamiaje inicial (sin precedente en el plan)

**Decisión**: esta feature crea también el andamiaje del repositorio —solución y proyectos .NET,
proyecto de frontend, configuración del linter y **las dos barreras**— porque nada de eso existe.

**Rationale**: `.github/workflows/ci.yml` ya está escrito y llama a `backend/Directory.Build.props`,
`backend/.editorconfig`, `backend/verificar-contrato.sh` y `backend/verificar-linter.sh`. Es decir,
el CI de este repositorio falla en el primer push mientras esas piezas no existan. El plan
`plan-DISC-001.md` las da por hechas (D-1 a D-4, FEAT-003) porque venían de la versión anterior,
pero `plan-de-implementacion/prds/implementados/` no está en el repositorio y el código tampoco.
El README del plan además lo anticipa: *"Empezando de cero, el orden 4→8 puede ir antes del 1"*.

**Consecuencia**: rompe el techo de ~300 líneas por commit para el primer incremento. Queda
registrado en *Complexity Tracking* de `plan.md`.

**Alternativas consideradas**: apagar los pasos del CI hasta que las piezas existan — contradice el
Principio V (una barrera que nunca se vio fallar no es una barrera) y repite exactamente el error
que el plan documenta con FEAT-001c, que se escribió y mergeó sin linter porque el linter no
molestaba.

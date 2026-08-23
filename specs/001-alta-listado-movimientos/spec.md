# Feature Specification: Alta de movimientos y listado simple

**Feature Branch**: `001-alta-listado-movimientos`

**Created**: 2026-08-23

**Status**: Draft

**Input**: Seguir `plan-de-implementacion/` (PRD-001 + plan DISC-001). Primera unidad
implementable sobre un repositorio sin código: el ticket que el plan llama **FEAT-001a —
Alta de movimientos y listado simple** (orden 1 del recorrido completo).

## Clarifications

### Session 2026-08-23

- Q: Cuando la persona abre el listado en esta feature, ¿debe ver todos sus movimientos históricos, o solamente los del mes actual? → A: Solamente los del mes actual, acotado de forma fija y sin controles visibles (opción B).
- Q: ¿Cuál es la lista exacta de categorías predefinidas con la que arranca la aplicación? → A: La que propone el PRD, tal cual — gastos: Comida, Transporte, Vivienda, Servicios, Salud, Ocio, Otros; ingresos: Sueldo, Ingreso extra, Otros (opción A).
- Q: ¿Hay un monto máximo que la aplicación deba rechazar al registrar un movimiento? → A: Sí, un techo explícito de 999.999.999,99 por movimiento, rechazado con motivo visible igual que un monto inválido (opción A).
- Q: Después de que la persona guarda un movimiento con éxito, ¿qué debe pasar con el formulario y con el listado? → A: Una sola pantalla — el formulario se limpia, el foco vuelve al primer campo y el listado se actualiza en el lugar con el movimiento recién creado (opción A).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar un gasto y verlo en el listado (Priority: P1)

La persona abre la aplicación, completa el formulario con el monto que gastó, elige una de las
categorías de gasto que la aplicación ya ofrece, deja o cambia la fecha, y guarda. El gasto
aparece inmediatamente en el listado de movimientos.

**Why this priority**: Es el núcleo del producto y el motivo por el que existe. Sin esto no hay
nada que listar, filtrar, editar ni totalizar: todas las features posteriores del plan (filtros,
resumen, categorías propias, monedas, dashboard) operan sobre movimientos que este flujo crea.

**Independent Test**: Se prueba entero completando el formulario de un gasto y verificando que el
movimiento queda persistido y visible en el listado, sin necesitar ninguna otra funcionalidad.

**Acceptance Scenarios**:

1. **Given** el formulario de registro abierto, **When** la persona completa monto, categoría de
   gasto y fecha, y guarda, **Then** el gasto queda registrado y aparece en el listado de la misma
   pantalla identificado como gasto, sin navegar a ningún lado, y el formulario queda vacío con el
   foco en su primer campo. *(AC-15 parcial — RF-10)*
2. **Given** el formulario de registro abierto sin haber tocado el campo fecha, **When** la persona
   completa monto y categoría y guarda, **Then** el movimiento queda registrado con la fecha del
   día actual. *(AC-17 — RF-12)*
3. **Given** una cuenta sin categorías propias, **When** la persona abre el formulario para cargar
   un gasto, **Then** el selector ofrece exactamente las siete categorías predefinidas de tipo
   gasto y ninguna de tipo ingreso. *(AC-10 — RF-06)*

---

### User Story 2 - Registrar un ingreso y verlo en el listado (Priority: P2)

La persona registra dinero que entró a su cuenta con el mismo formulario, eligiendo una categoría
de tipo ingreso. El ingreso aparece en el listado junto a los gastos, distinguible de ellos.

**Why this priority**: Sin ingresos la aplicación sólo responde *en qué se me va la plata*, no
*cómo vengo este mes*. Es la mitad del dominio, pero el flujo de gasto ya entrega valor por sí solo,
por eso va después.

**Independent Test**: Se prueba completando el formulario de un ingreso y verificando que queda
persistido y visible en el listado, marcado como ingreso y no como gasto.

**Acceptance Scenarios**:

1. **Given** el formulario de registro abierto, **When** la persona completa monto, categoría de
   ingreso y fecha, y guarda, **Then** el ingreso queda registrado y aparece en el listado
   identificado como ingreso. *(AC-16 parcial — RF-11)*
2. **Given** una cuenta sin categorías propias, **When** la persona abre el formulario para cargar
   un ingreso, **Then** el selector ofrece exactamente las tres categorías predefinidas de tipo
   ingreso y ninguna de tipo gasto. *(AC-10 aplicado a ingresos — RF-06)*
3. **Given** gastos e ingresos ya cargados con fecha dentro del mes actual, **When** la persona
   abre el listado, **Then** ve todos esos movimientos individuales, tanto gastos como ingresos.
   *(AC-22 parcial — RF-16)*

---

### User Story 3 - El formulario rechaza lo que no puede registrarse (Priority: P3)

Cuando la persona intenta guardar un movimiento con un monto que no es válido o sin haber elegido
categoría, la aplicación se lo impide, le dice por qué, y no registra nada.

**Why this priority**: Protege la integridad de todos los totales que vienen después: un monto en
cero, negativo o con tres decimales, o un movimiento sin categoría, envenena el resumen y el
dashboard. Va tercero porque el camino feliz tiene que existir antes de poder desviarse de él.

**Independent Test**: Se prueba intentando guardar cada variante inválida y verificando que la
aplicación muestra el motivo y que la cantidad de movimientos registrados no cambia.

**Acceptance Scenarios**:

1. **Given** un formulario con el monto vacío, en cero, negativo, con más de dos decimales o por
   encima de 999.999.999,99, **When** la persona intenta guardar, **Then** la aplicación rechaza el
   guardado, muestra el motivo y no se crea ningún movimiento. *(AC-18 — RF-13)*
2. **Given** un formulario sin categoría seleccionada, **When** la persona intenta guardar,
   **Then** la aplicación rechaza el guardado, muestra el motivo y no se crea ningún movimiento.
   *(AC-40 — RF-23)*
3. **Given** un formulario con una categoría de un tipo y el movimiento cargándose como el otro
   tipo, **When** la persona intenta guardar, **Then** la aplicación rechaza el guardado y no se
   crea ningún movimiento.

---

### Edge Cases

- **Monto en el límite**: `0.01` se acepta; `0` y cualquier negativo se rechazan; `10.999` se
  rechaza por tener más de dos decimales. El rechazo es por validación explícita, nunca por
  redondeo silencioso.
- **Monto en el techo**: `999999999.99` se acepta; `1000000000.00` se rechaza con su motivo, no con
  un error de almacenamiento.
- **Fecha futura o muy antigua**: se aceptan al registrar. El PRD no restringe el rango de fechas y
  prohibirlo impediría registrar un movimiento ya programado o cargar movimientos atrasados. Si la
  fecha cae fuera del mes actual, el movimiento queda registrado pero no se ve en el listado.
- **Listado vacío**: una cuenta sin ningún movimiento en el mes actual ve un listado vacío con un
  mensaje que lo indica, no un error ni una pantalla en blanco.
- **Movimiento guardado fuera del mes actual**: se registra igual, pero no aparece en el listado,
  porque su fecha cae fuera del recorte. La confirmación de guardado no debe sugerir que el
  movimiento se perdió.
- **Movimiento en el borde del mes**: los movimientos del primer y del último día del mes actual
  entran en el listado; los del último día del mes anterior y el primero del siguiente, no.
- **Dos movimientos con la misma fecha**: ambos aparecen como filas separadas; el orden entre ellos
  es estable y determinista entre recargas, no queda librado al azar.
- **Guardado que falla**: si el movimiento no se puede persistir, la persona ve el motivo y el
  formulario conserva lo que había cargado, para no obligarla a tipearlo de nuevo. El vaciado del
  formulario ocurre únicamente tras un guardado exitoso.
- **Guardar dos veces sin querer**: cada envío exitoso vacía el formulario, así que un segundo
  envío inmediato no puede repetir el movimiento anterior por inercia.
- **Categoría dada de baja**: fuera de alcance en esta feature — las categorías propias y su baja
  lógica llegan en el ticket 3 del plan.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** *(RF-10)*: El sistema MUST permitir registrar un gasto indicando monto, categoría y
  fecha mediante un formulario.
- **FR-002** *(RF-11)*: El sistema MUST permitir registrar un ingreso indicando monto, categoría y
  fecha mediante el mismo formulario.
- **FR-003** *(RF-12)*: El sistema MUST proponer la fecha actual como valor por defecto del campo
  fecha.
- **FR-004** *(RF-13)*: El sistema MUST rechazar el registro de un movimiento cuyo monto no sea un
  número mayor a cero con hasta dos decimales, mostrando el motivo del rechazo.
- **FR-004b**: El sistema MUST rechazar el registro de un movimiento cuyo monto supere
  999.999.999,99, mostrando el motivo del rechazo del mismo modo que con un monto inválido. Ese
  límite MUST ser una validación declarada, no un error genérico del almacenamiento.
- **FR-005** *(RF-23)*: El sistema MUST rechazar el registro de un movimiento que no tenga
  categoría asignada, mostrando el motivo del rechazo.
- **FR-006** *(RF-06)*: El sistema MUST ofrecer un catálogo de categorías predefinidas, no
  modificables por la persona usuaria, diferenciadas por tipo (gasto o ingreso), y MUST ofrecer en
  el formulario únicamente las del tipo que se está cargando. El catálogo inicial es exactamente
  éste, y ninguna otra: **gasto** — Comida, Transporte, Vivienda, Servicios, Salud, Ocio, Otros;
  **ingreso** — Sueldo, Ingreso extra, Otros. Son las mismas para toda cuenta.
- **FR-007** *(RF-16, RF-18 parcial)*: El sistema MUST listar los movimientos individuales —gastos
  e ingresos— de la cuenta cuya fecha cae dentro del mes actual, extremos incluidos, mostrando de
  cada uno su monto, su categoría, su fecha y si es gasto o ingreso. El recorte al mes actual es
  fijo y no se expone como control: es el valor por defecto que AC-25 exigirá cuando FEAT-001b
  agregue el selector de rango encima.
- **FR-008**: El sistema MUST mostrar el listado en un orden estable y determinista, con los
  movimientos más recientes primero.
- **FR-009** *(RF-24, RF-31 — anticipo parcial)*: El sistema MUST persistir la moneda como un dato
  de cada movimiento y no como una constante del código, registrando todo movimiento de esta
  feature en pesos. No expone selector ni filtro de moneda: eso llega en los tickets 4a y 4b del
  plan.
- **FR-010** *(RF-04 — anticipo parcial)*: El sistema MUST registrar cada movimiento como
  perteneciente a una cuenta, obtenida de una única fuente de identidad, de modo que la
  autenticación posterior la reemplace sin migrar datos.
- **FR-011**: El sistema MUST rechazar todo movimiento cuya categoría no corresponda al tipo del
  movimiento, y MUST aplicar esa validación también cuando el dato llega por fuera del
  formulario.
- **FR-012**: El sistema MUST mostrar un listado vacío con un mensaje explícito, sin error, cuando
  la cuenta no tiene movimientos en el mes actual.
- **FR-013** *(RF-22 — anticipo parcial)*: El sistema MUST presentar el formulario de registro y el
  listado de movimientos en una misma pantalla, sin navegación intermedia entre registrar y ver lo
  registrado.
- **FR-014**: Tras un guardado exitoso, el sistema MUST vaciar el formulario, devolver el foco a su
  primer campo y actualizar el listado en el lugar para incluir el movimiento recién creado si su
  fecha cae dentro del mes actual.
- **FR-015** *(AC-55 / RNF-06 parcial)*: El formulario de registro MUST poder recorrerse,
  completarse y enviarse íntegramente con el teclado, con foco visible y etiqueta asociada en cada
  control recorrido.

### Key Entities

- **Movimiento**: un gasto o un ingreso registrado. Atributos: tipo (gasto o ingreso), monto
  (mayor a cero y hasta 999.999.999,99, con hasta dos decimales), moneda, categoría, fecha y cuenta
  a la que pertenece.
- **Categoría**: etiqueta de clasificación con nombre y tipo (gasto o ingreso). En esta feature son
  sólo las diez predefinidas del sistema enumeradas en FR-006, iguales para todas las cuentas y no
  modificables.
- **Moneda**: unidad en la que está expresado el monto de un movimiento, con la cantidad de
  decimales admitidos como dato propio. En esta feature existe únicamente pesos.
- **Cuenta**: la persona propietaria de sus movimientos. En esta feature hay una sola y no se crea
  ni se elige; la reemplaza la autenticación del ticket 1a.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** *(AC-34 / RNF-02)*: El registro de un movimiento válido se confirma en menos de 1
  segundo en el percentil 95 sobre 100 ejecuciones.
- **SC-002**: Una persona registra su primer gasto —desde abrir la aplicación hasta verlo en el
  listado— en menos de 30 segundos, sin ayuda ni documentación.
- **SC-003**: El 100 % de los intentos de guardar con monto inválido o sin categoría son rechazados
  con un motivo visible, y ninguno deja un movimiento registrado.
- **SC-004**: El 100 % de los movimientos registrados aparecen en el listado con el mismo monto,
  categoría, fecha y tipo con los que se cargaron.
- **SC-005**: Cada criterio de aceptación citado en esta spec (AC-10, AC-15, AC-16, AC-17, AC-18,
  AC-22, AC-25, AC-34, AC-40, AC-55) tiene al menos un test automatizado que lo nombra por su identificador.

## Assumptions

- **El alcance es el ticket FEAT-001a del plan, reconstruido desde cero.** El repositorio no tiene
  `backend/` ni `frontend/`, y `plan-de-implementacion/prds/implementados/` —donde el README ubica
  los 8 tickets ya mergeados en la versión anterior— no está en este repositorio. Se toma el corte
  que el README describe ("Alta de movimientos y listado simple") y se derivan los requerimientos
  del `PRD.md` maestro, para que los 9 PRDs pendientes sigan encajando sin cambios sobre el punto
  de partida que asumen.
- **Fuera de alcance en esta feature, por decisión del plan**: los controles de filtrado del
  listado —por categoría y por rango de fechas—, la edición y la eliminación de movimientos
  (FEAT-001b); el resumen del mes con desglose por categoría (FEAT-001c); la autenticación y el
  aislamiento entre cuentas (tickets 1a–1c); las categorías propias (ticket 3); las varias monedas
  (4a, 4b); el dashboard con gráficos (5); la maquetación y la accesibilidad completa (6); la nota
  descriptiva del movimiento (ticket 2). El recorte al mes actual sí está en esta feature, como
  comportamiento fijo y sin control visible (ver FR-007).
- **Hay una única cuenta preexistente** y la aplicación no pide autenticarse. El plan lo decidió
  así a propósito: el ticket 1a reemplaza esa fuente de identidad en vez de migrar datos, y sus
  datos de desarrollo se descartan en esa migración.
- **AC-55 (completar y enviar el formulario sólo con teclado)** se verifica ya en esta feature
  —recogido en FR-015—, aunque el resto de RNF-06 se mida sobre la disposición final en el ticket
  6: el formulario de registro nace en este ticket y es el que AC-55 nombra.
- **AC-15 y AC-16 se cumplen sólo en su parte de listado.** Su mitad de dashboard y resumen se
  verifica en FEAT-001c y en el ticket 5, cuando esas pantallas existan.
- **No se agregan dependencias nuevas** más allá de las que el stack de `AGENTS.md` ya fija.
- **Techo de ~300 líneas agregadas por commit**, acordado en el plan el 2026-08-20.

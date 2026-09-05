# Feature Specification: Elegir y filtrar la moneda de un movimiento

**Feature Branch**: `009-elegir-y-filtrar-moneda`

**Created**: 2026-09-04

**Status**: Draft

**Input**: Ticket DISC-001-04b — "Registrar y filtrar en varias monedas"
(`plan-de-implementacion/prds/pendientes/prd-DISC-001-04b.md`), segundo de los dos cortes de
multi-moneda. Salda las deudas D8-01, D8-02 y D8-03 de la feature 008.

---

## De dónde sale esta spec

El PRD 4b está escrito desde la vereda del usuario y da por construido todo lo que no nombra. La
feature 008 aprendió, a fuerza de encontrarse el trabajo ya hecho, que eso hay que verificarlo antes
de planificar. Así que esta spec empieza igual que aquella: **requisito por requisito contra el
código**, y recién después dice qué queda.

### Lo que el PRD pide y ya está construido

| Lo que pide | Dónde está | Desde |
|---|---|---|
| El catálogo de monedas, con exactamente una predeterminada | tabla `moneda`, `Dominio/Moneda.cs`, migración `UnicaMonedaPredeterminada` | FEAT-001a / 008 |
| Los totales separados por moneda, sin conversión ni suma cruzada | `Resumenes/CalculoDelResumen.cs`, `MovimientosConsulta.Agrupado` | FEAT-001c / 008 |
| Que la moneda de cada movimiento **viaje** al cliente | `MovimientoDto.MonedaCodigo`, `Movimiento.monedaCodigo` en `frontend/src/api/tipos.ts` | FEAT-001a |
| Que el monto se vea con el símbolo de **su** moneda en el listado | `ListadoMovimientos.tsx`, `Intl.NumberFormat('es-AR', { style: 'currency', currency: monedaCodigo })` | FEAT-001a |
| Filtrar el listado por categoría y por rango de fechas, combinándose con **y** | `GET /api/movimientos?desde&hasta&categoriaId`, `MovimientosConsulta.Filtrado` | FEAT-001b |
| Un solo catálogo pedido una vez y bajado por props a las dos pantallas | `App.tsx` con `categorias` en la raíz (D-08 de la feature 007) | 007 |

Las dos últimas filas importan más de lo que parece. **AC-08 del PRD —los tres filtros a la vez—
no estrena la combinación con `AND`: la estrena el tercer filtro sobre una combinación que ya
existe y ya está probada.** Y **AC-12 —el catálogo pedido a lo sumo una vez— no inventa un patrón:
el propio PRD dice que hereda el criterio de la 007, y esa herencia está disponible porque los dos
tickets salieron en ese orden.**

### Lo que falta de verdad

Cuatro cosas, y las cuatro son del mismo tipo: **hoy la moneda es una decisión del servidor y tiene
que pasar a ser una elección del usuario.**

1. **La moneda no viaja en ninguna petición.** Ni `NuevoMovimientoDto` ni `MovimientoEditadoDto` la
   llevan; el alta la resuelve con `contexto.Monedas.SingleAsync(m => m.EsPredeterminada)` y la
   edición ni la toca. Es `PRD:FR-01`, `PRD:FR-02` y `PRD:FR-07`.
2. **No hay nada que validar y por eso no hay validación.** `ValidacionDelMovimiento` valida tipo,
   monto y categoría. Es la deuda **D8-01** de la feature 008, diferida acá con esta razón exacta:
   un test de "rechazar una moneda fuera del catálogo" tenía que inventarse primero la vía de
   entrada que decía comprobar. Esta feature abre esa vía, así que ahora sí se puede.
3. **No existe `GET /api/monedas`.** El catálogo no está expuesto: el resumen devuelve monedas *con
   totales de un período*, que es otra cosa. Sin endpoint no hay selector ni filtro que se puedan
   llenar desde el catálogo, y `PRD:AC-04` —que una moneda agregada como dato aparezca en los dos—
   no tendría de dónde salir.
4. **El listado no se puede acotar por moneda.** `MovimientosConsulta.Filtrado` recibe `categoriaId`
   y nada más.

### La brecha que el PRD no podía prever

El PRD 4b escribe sus criterios en términos de pantalla: *"el usuario filtra el listado por
dólares"*, *"cambia a dólares la moneda de un movimiento propio y guarda"*. Da por sentado que la
barra de filtros y el flujo de edición existen. **No existen.**

FEAT-001b y FEAT-001c fueron features **de backend**: dejaron `GET`, `PUT` y `DELETE
/api/movimientos/{id}` y los filtros del listado, y el frontend recibió sólo la declaración del
contrato en `tipos.ts`. Está escrito así en `plan-de-implementacion/README.md`. Hoy
`frontend/src/api/cliente.ts` no tiene `editarMovimiento` ni `eliminarMovimiento`, y
`obtenerMovimientos()` no recibe ningún filtro: la pantalla pide el listado entero del mes en curso.

O sea que en la pantalla, *"filtrar por moneda"* y *"corregir la moneda"* no son un control más
sobre una interfaz existente: son **construir la barra de filtros entera y el flujo de edición
entero**, y recién entonces agregarles la moneda. Eso es la mitad de frontend de FEAT-001b que
nunca se hizo, no trabajo de este ticket — pero sin ello dos de los cuatro objetivos del PRD no se
pueden demostrar del lado del usuario.

**Esta spec lo resuelve con el corte de la sesión de aclaración**: el frontend construye **el
selector de moneda, el flujo de edición en una ventana emergente y el control para acotar por
moneda**, y no construye ni la barra de filtros de categoría y fecha ni la vista de totales. Lo que
queda afuera no se pierde: queda anotado en *Deuda registrada* con el ticket que lo cubre. El
criterio detrás del corte, que el usuario dejó dicho de una forma que conviene no olvidar: **si la
pantalla que muestra algo todavía no existe, es porque está en un ticket más adelante** — no es una
omisión que esta feature tenga que tapar.

---

## Lo que hace distinta a esta feature

**Es la primera vez que un dato del catálogo entra por una petición.** Hasta hoy todo lo que el
cliente elegía —el tipo, el monto, la categoría, la fecha— o era libre o se validaba contra una
tabla que el propio usuario podía ver entera. La moneda estrena una forma nueva: un identificador
que sólo vale si existe en una tabla del sistema, que el cliente no administra y a la que se le
pueden agregar filas sin desplegar nada.

Eso arrastra una consecuencia que la 008 dejó anotada como **D8-08**: los tests de monedas comparten
el catálogo con la barrera `verificar-monedas.sh`, que le agrega una fila mientras corren. La regla
es corta y esta feature la hereda entera: **ningún test puede escribir un número fijo sobre el
tamaño del catálogo.** Un `Assert.Equal(2, monedas.Count)` pasa hoy y se rompe el día que la barrera
corre. Ya pasó una vez.

Y hay una segunda: **el selector de moneda tiene que llenarse del catálogo, no de una constante.**
Si el frontend escribe `['ARS', 'USD']` en cualquier lado, `PRD:AC-04` queda muerto y la promesa de
producto que la 008 verificó con una barrera entera —sumar una moneda cuesta 0 líneas de código—
deja de valer del lado de la pantalla, que es el único lado que el usuario mira.

---

## Clarifications

### Session 2026-09-04

- **P: El PRD escribe sus criterios en términos de pantalla, pero la barra de filtros y el flujo de
  edición no existen en el frontend. ¿Cuál es el alcance de esta feature?**
  R: El **flujo de edición sí se construye, y como ventana emergente** sobre la pantalla del
  listado. La barra de filtros de categoría y fecha **no**: del acotado, la pantalla estrena
  únicamente el control de moneda; los otros dos siguen verificándose contra la API, como en las
  features 005 y 006.
- **P: Al cambiar la moneda o el monto de un movimiento hay que recalcular los totales por categoría
  y el total general. La pantalla que muestra esos totales no existe todavía. ¿Se construye acá?**
  R: **No.** El recálculo se verifica contra la API, que ya lo hace bien desde FEAT-001c. La vista
  de totales es del ticket 5, y el criterio es general: lo que todavía no tiene pantalla es porque
  está más adelante en el plan, no porque se haya olvidado. Queda como **D9-06**.
- **P: El listado ya distingue las monedas por el símbolo del formateo (`$` contra `US$`).
  ¿Alcanza para `PRD:FR-04`, o hace falta el código explícito?**
  R: **El código ISO explícito, en su propia columna.** El símbolo depende de `Intl` y del locale, y
  con el control de moneda en pantalla la distinción tiene que aguantar cualquier moneda agregada al
  catálogo como dato — que es exactamente lo que `PRD:AC-04` promete y lo que
  `verificar-monedas.sh` protege.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar en la moneda que elijo (Priority: P1) 🎯 MVP

Tengo gastos en pesos y algunos en dólares. Al cargar uno, elijo la moneda en el formulario. Si no
la toco, queda en la predeterminada del catálogo, que hoy es pesos — que es lo que quiero casi
siempre.

**Why this priority**: Es la puerta que el ticket abre. Sin esto, todo lo demás —ver la moneda,
filtrar por ella, corregirla— opera sobre datos que siempre están en pesos, y no hay nada que ver,
filtrar ni corregir. Es también el único requisito que hace viajar la moneda, y por lo tanto el
único que crea la entrada que hay que validar.

**Independent Test**: Se registra un gasto eligiendo una moneda distinta de la predeterminada y se
comprueba que quedó en esa moneda; se registra otro sin tocar el campo y se comprueba que quedó en
la predeterminada. Entrega valor por sí solo: el usuario ya puede separar sus gastos en dólares de
los que están en pesos, y el resumen —que desde la 008 ya los discrimina— empieza a mostrar dos
columnas con datos de verdad en vez de una con datos y otra en cero.

**Acceptance Scenarios**:

1. **Given** el catálogo con la predeterminada en pesos y al menos otra moneda, **When** registro un
   gasto eligiendo esa otra moneda, **Then** el movimiento queda registrado en esa moneda y así se
   devuelve.
2. **Given** el mismo catálogo, **When** registro un gasto completando sólo monto y categoría, sin
   tocar el campo de moneda, **Then** el movimiento queda en la moneda predeterminada del catálogo,
   sin haber hecho ninguna interacción adicional respecto de antes de esta feature.
3. **Given** una moneda agregada al catálogo únicamente como dato, **When** abro el formulario,
   **Then** aparece entre las opciones, sin que se haya modificado ninguna línea de código.
4. **Given** el catálogo, **When** el formulario ofrece las monedas, **Then** ofrece exactamente las
   del catálogo, y exactamente una figura como la propuesta por defecto.
5. **Given** una petición de alta que indica una moneda que no está en el catálogo, **When** se
   intenta guardar, **Then** se rechaza indicando qué campo está mal, y no se crea ningún
   movimiento.

---

### User Story 2 - No dudar en qué moneda está cada monto (Priority: P2)

Miro el listado y veo dos movimientos de 100. Uno es de 100 pesos y el otro de 100 dólares, y la
diferencia es enorme. Necesito verla sin abrir nada.

**Why this priority**: Es una de las tres mitigaciones que PRD-001 propone para el riesgo central de
multi-moneda —cargar en la moneda equivocada— y es la que actúa después del error, cuando ya está
cargado. Va segunda y no primera porque hoy ya está parcialmente cubierta: el listado formatea cada
monto con el símbolo de su moneda desde FEAT-001a.

**Lo que falta es que esa distinción no dependa del formateo.** El símbolo lo elige `Intl` a partir
del locale, y para dos monedas cualesquiera puede repetirse o resultar ambiguo — con lo cual una
moneda agregada al catálogo como dato podría quedar indistinguible de otra, que es justo lo que
`PRD:AC-04` promete que no pasa. Por eso el código ISO va explícito y en su propia columna.

**Independent Test**: Dos movimientos del mismo monto en dos monedas distintas; se comprueba que la
fila de cada uno indica el código de su moneda, y que los dos códigos son distintos.

**Acceptance Scenarios**:

1. **Given** un gasto de 100 en pesos y otro de 100 en dólares, **When** miro el listado, **Then**
   cada fila muestra el código de la moneda de su monto, y los dos son distintos entre sí.
2. **Given** un movimiento en una moneda agregada al catálogo únicamente como dato, **When** miro el
   listado, **Then** su fila muestra el código de esa moneda, sin que se haya modificado ninguna
   línea de código.

---

### User Story 3 - Ver sólo los movimientos de una moneda (Priority: P3)

Quiero revisar qué gasté en dólares este mes, sin que los pesos me estorben. Y quiero poder
combinarlo con lo que ya filtro: una categoría, un rango de fechas.

**Why this priority**: Es comodidad de lectura sobre datos que las historias anteriores ya
separaron; nada se pierde ni se corrompe si falta. Depende de US1: filtrar por moneda cuando todo
está en pesos no muestra nada distinto de no filtrar.

**El control en pantalla es sólo el de moneda.** Los acotados por categoría y por rango de fechas
existen en la API desde FEAT-001b y su interfaz nunca se construyó; ésta no la construye. El
escenario 3 —los tres a la vez— se verifica contra la API, que es donde vive la combinación.

**Independent Test**: Movimientos en dos monedas; se pide el listado acotado a una y se comprueba
que no aparece ninguno de la otra; se pide sin acotar y aparecen todos.

**Acceptance Scenarios**:

1. **Given** movimientos en pesos y en dólares, **When** acoto el listado a dólares, **Then** veo
   únicamente los movimientos en dólares.
2. **Given** los mismos movimientos, **When** consulto el listado sin acotar por moneda, **Then**
   veo los de todas las monedas.
3. **Given** movimientos que difieren en moneda, en categoría y en fecha, **When** acoto por las
   tres cosas a la vez, **Then** veo únicamente los que cumplen las tres condiciones.
4. **Given** una moneda agregada al catálogo únicamente como dato, **When** miro las opciones de
   acotado, **Then** aparece, junto con la opción de no acotar por moneda, y son exactamente las
   mismas monedas que ofrece el formulario.

---

### User Story 4 - Corregir la moneda sin borrar el movimiento (Priority: P4)

Cargué un gasto en pesos y en realidad era en dólares. Quiero corregirlo, no borrarlo y volver a
cargarlo con la fecha y la categoría de nuevo.

**Why this priority**: Es la tercera mitigación del riesgo de PRD-001 y la última en la cadena: sólo
hace falta cuando el error ya ocurrió y ya se vio. Va última también porque es la que más trae
consigo: es la única historia que estrena una interfaz entera.

**La corrección se hace en una ventana emergente** sobre la pantalla del listado, con el movimiento
ya cargado en sus campos. No es una pantalla aparte ni una fila que se vuelve editable: se abre
encima, se corrige y se cierra, y el listado queda donde estaba. Es la única forma de edición que
esta feature ofrece.

**Los totales sí se recalculan, y no se muestran todavía.** Cambiar la moneda o el monto de un
movimiento cambia los totales por moneda, el balance y el desglose por categoría del período, y eso
se verifica —las dos direcciones, origen y destino— contra la lectura del resumen. La pantalla que
los pinta es del ticket 5 (**D9-06**).

**Independent Test**: Un movimiento en pesos; se le cambia la moneda a dólares; se comprueba que su
monto dejó de sumar en los totales en pesos y pasó a sumar en los de dólares, y que su monto, su
categoría y su fecha quedaron intactos.

**Acceptance Scenarios**:

1. **Given** un movimiento propio de 100 registrado en pesos, y el resumen del período mostrándolo
   en los totales en pesos, **When** cambio su moneda a dólares y guardo, **Then** su monto deja de
   sumar en los totales en pesos y pasa a sumar en los de dólares.
2. **Given** ese mismo movimiento, **When** cambio únicamente su moneda, **Then** su monto, su
   categoría y su fecha quedan sin alterar.
3. **Given** una edición que indica una moneda que no está en el catálogo, **When** se intenta
   guardar, **Then** se rechaza indicando qué campo está mal, y el movimiento queda como estaba.
4. **Given** un movimiento de **otra** cuenta, **When** se intenta cambiarle la moneda, **Then**
   responde como si no existiera, sin distinguir "no existe" de "no es tuyo", y no se modifica nada.

---

### Edge Cases

- **Una petición sin moneda.** Tiene que seguir siendo válida y caer en la predeterminada: es
  `PRD:NFR-01` —cero interacciones adicionales— y también la compatibilidad hacia atrás del
  contrato. Un campo obligatorio acá rompe a todo cliente que hoy no lo manda.
- **Una moneda que existe pero no es la predeterminada, mandada explícitamente.** Vale: el catálogo
  no tiene monedas "elegibles" y "no elegibles", tiene una que se propone por defecto.
- **La moneda mandada con un valor vacío, nulo o de otro tipo.** Vacío y nulo son "no la mandé" y
  caen en la predeterminada; un valor que no identifica ninguna fila del catálogo se rechaza como en
  US1-5.
- **Cambiar la moneda de un movimiento a la que ya tenía.** No es un error: se guarda y no cambia
  nada.
- **Filtrar por una moneda que no existe en el catálogo.** Mismo criterio que ya rige para la
  categoría inexistente en el listado: no es un error, simplemente no deja pasar nada. Rechazarlo
  con un error confirmaría cuáles existen.
- **Una moneda del catálogo sin ningún movimiento.** El listado acotado a ella devuelve vacío, que
  no es un error. Y el resumen la sigue mostrando en cero, que es `006:AC-31` y no cambia acá.
- **El catálogo no se puede cargar.** El formulario tiene que decirlo en vez de ofrecer una lista
  vacía en silencio, igual que hace hoy el catálogo de categorías.
- **Se agrega una moneda al catálogo mientras la pantalla está abierta.** No aparece hasta recargar,
  y eso es aceptado: el catálogo se pide una vez por carga (`PRD:NFR-02`).

---

## Requirements *(mandatory)*

### Functional Requirements

Cada requisito cita su origen en el PRD del ticket (`PRD:FR-0x`) y, cuando corresponde, la deuda de
otra feature que salda.

- **FR-001**: El sistema DEBE permitir que el alta de un movimiento indique en qué moneda del
  catálogo se registra. Origen: `PRD:FR-01`. Salda **D8-02** de la feature 008.
- **FR-002**: El sistema DEBE registrar el movimiento en la moneda predeterminada del catálogo
  cuando el alta no indica ninguna, sin exigir ninguna interacción adicional. Origen: `PRD:FR-02`,
  `PRD:NFR-01`.
- **FR-003**: El sistema DEBE rechazar el alta cuyo campo de moneda no identifique una moneda del
  catálogo, indicando el campo y el motivo, y NO DEBE crear ningún movimiento. Origen: `PRD:FR-01`,
  `PRD:AC-11`. **Salda D8-01 de la feature 008**, que es la razón por la que esa deuda esperaba a
  este ticket.
- **FR-004**: El sistema DEBE exponer el catálogo de monedas como una lectura propia, que devuelva
  exactamente las monedas del catálogo e indique cuál es la predeterminada. Origen: `PRD:FR-03`,
  `PRD:FR-06`, `RF-31`, `RF-32`.
- **FR-005**: El sistema DEBE ofrecer para elegir exactamente las monedas del catálogo, tomadas de
  esa lectura y no de ninguna lista escrita en el código, de modo que una moneda agregada como dato
  aparezca sin modificar ninguna línea. Origen: `PRD:FR-03`, `PRD:AC-04`, `RF-32`.
- **FR-006**: El sistema DEBE proponer como valor por defecto del campo de moneda la que el catálogo
  marca como predeterminada, y exactamente una lo está. Origen: `PRD:FR-02`, `PRD:AC-03`.
- **FR-007**: El sistema DEBE mostrar, para cada movimiento del listado, el **código** de la moneda
  de su monto, además del formateo que ya lleva su símbolo. El código sale del dato del movimiento y
  no de ninguna tabla de equivalencias escrita en el código, de modo que una moneda agregada al
  catálogo como dato se muestre correctamente. Origen: `PRD:FR-04`, `PRD:AC-05`.
- **FR-008**: El sistema DEBE permitir acotar el listado de movimientos a una moneda **desde la
  pantalla**, tomando "todas las monedas" como comportamiento por omisión. Origen: `PRD:FR-05`.
  Salda **D8-02**.
- **FR-009**: El acotado por moneda DEBE combinarse con **y** con los acotados por categoría y por
  rango de fechas que ya existen: un movimiento sale si cumple todas las condiciones pedidas. Como
  esos dos no tienen control en pantalla, la combinación se verifica sobre la lectura del listado.
  Origen: `PRD:FR-05`, `PRD:AC-08`.
- **FR-010**: El sistema DEBE ofrecer para acotar exactamente las monedas del catálogo más la opción
  de no acotar, y DEBEN ser el mismo conjunto de monedas que ofrece el alta. Origen: `PRD:FR-06`,
  `PRD:AC-12`.
- **FR-011**: El sistema DEBE permitir modificar un movimiento propio ya registrado —su moneda
  incluida— con las mismas reglas de validación que el alta, desde una **ventana emergente** que se
  abre sobre el listado con los valores actuales ya cargados y que, al cerrarse, deja el listado
  donde estaba. Origen: `PRD:FR-07`, `RF-14`.
- **FR-012**: Al modificar únicamente la moneda de un movimiento, el sistema DEBE conservar su
  monto, su categoría y su fecha sin alterarlos. Origen: `PRD:FR-07`, `PRD:AC-10`.
- **FR-012b**: Modificada la moneda o el monto de un movimiento, los totales por moneda, el balance
  y el desglose por categoría del período DEBEN reflejar el cambio: el monto deja de sumar donde
  estaba y suma donde quedó. Se verifica sobre la lectura del resumen; **la pantalla que muestra
  esos totales no es parte de esta feature** (D9-06). Origen: `PRD:FR-07`, `PRD:AC-09`.
- **FR-013**: El sistema DEBE pedir el catálogo de monedas a lo sumo una vez por carga de la
  pantalla principal, y las opciones del alta y las del acotado DEBEN salir de esa misma lectura.
  Origen: `PRD:NFR-02`, `PRD:AC-12`. Hereda el criterio de la feature 007 (D-08).
- **FR-014**: Ni el alta ni la edición pueden aceptar un movimiento en la moneda de otra cuenta ni
  exponer datos de otra cuenta por esta vía: el aislamiento vigente no se debilita. Origen: `INV-01`
  del proyecto. *(El catálogo de monedas es del sistema y no tiene dueño, a diferencia de las
  categorías; lo que hay que preservar es el aislamiento de los movimientos que la edición toca.)*
- **FR-015**: El acotado por una moneda que no está en el catálogo NO DEBE ser un error: no deja
  pasar ningún movimiento. Es el mismo criterio que ya rige para la categoría inexistente.

### Key Entities

- **Moneda**: una fila del catálogo del sistema, con su código ISO 4217, su nombre, su símbolo, sus
  decimales y la marca de predeterminada. **Nadie la crea, edita ni borra desde la aplicación**: se
  administra como dato, que es lo que la feature 008 verificó con `verificar-monedas.sh`. Esta
  feature la **lee** desde dos lugares nuevos —el alta y el acotado del listado— y no le agrega
  ninguna escritura.
- **Movimiento**: ya tiene su moneda desde FEAT-001a, y ya la devuelve. Lo que cambia es **quién la
  decide**: hasta hoy el servidor, desde acá el usuario, con el servidor poniendo la predeterminada
  cuando no se elige.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Registrar un movimiento en una moneda distinta de la predeterminada no requiere ningún
  paso previo: se hace en la misma pantalla y en la misma operación que uno en la predeterminada.
- **SC-002**: Registrar un movimiento en la moneda predeterminada requiere **exactamente las mismas
  interacciones que antes de esta feature: cero adicionales** (`PRD:NFR-01`).
- **SC-003**: Agregar una moneda al catálogo **como dato solamente** la hace aparecer tanto entre
  las opciones de registro como entre las de acotado, con **0 líneas de código modificadas**
  (`PRD:AC-04`).
- **SC-004**: Un intento de registrar o de editar con una moneda que no está en el catálogo se
  rechaza el **100%** de las veces, y deja **0** movimientos creados o modificados.
- **SC-005**: Acotando el listado a una moneda, el **100%** de los movimientos mostrados están en
  esa moneda, y con los tres acotados a la vez el resultado es exactamente la intersección de los
  tres.
- **SC-006**: Cambiada la moneda de un movimiento, su monto aparece en los totales de la moneda
  nueva y **no** aparece en los de la anterior — las dos direcciones verificadas, no sólo el
  destino.
- **SC-006b**: Corregir la moneda de un movimiento ya cargado se hace **sin salir de la pantalla del
  listado y sin volver a escribir el monto, la categoría ni la fecha**: la ventana emergente los
  ofrece ya cargados.
- **SC-007**: Cargar la pantalla principal pide el catálogo de monedas **a lo sumo 1 vez**, y las
  opciones de registro y de acotado ofrecen **el mismo conjunto**.
- **SC-008**: Sobre **100** ejecuciones, el registro de un movimiento con la moneda elegida se
  confirma en **menos de 1 s** en el percentil 95 (`PRD:NFR-03`, `RNF-02`).

---

## Assumptions

- **La moneda viaja por identificador, igual que la categoría.** El contrato ya usa `categoriaId`
  para elegir y `categoriaNombre` para mostrar; la moneda sigue la misma forma —se elige por
  identificador y se muestra por código— en vez de estrenar una convención distinta en el mismo
  cuerpo. Si el plan encuentra una razón para mandar el código ISO, es una decisión de diseño y va
  registrada ahí.
- **El campo de moneda es opcional en el alta y también en la edición.** Ausente significa "la
  predeterminada" al registrar y "la que ya tenía" al editar. Es lo mismo que ya se decidió para
  `fecha`, con la misma razón: en una edición, ausente no puede significar un cambio silencioso.
- **La medición de `PRD:NFR-03` se hace con el mismo arnés que la de la 008**, con 100 ejecuciones y
  excluida del CI por el filtro `FullyQualifiedName!~Rendimiento` que ya existe. La 008 subió ese
  número de 30 a 100 y anotó por qué; acá se hereda.
- **No se toca el resumen.** Ya discrimina por moneda desde FEAT-001c y la 008 lo verificó. Lo que
  esta feature cambia es qué movimientos caen en cada moneda, no cómo se suman.
- **Filtrar el resumen por moneda (`RF-30`, deuda D8-03) queda cubierto por el acotado del
  listado únicamente en lo que respecta al listado.** El filtro por moneda del *dashboard* es el
  ticket 5 y sigue fuera.
- **Ningún test escribe un número fijo sobre el tamaño del catálogo** (regla D8-08 de la 008). Los
  tests que necesiten "otra moneda" la agregan con el helper que la 008 dejó, con su limpieza en
  `try`/`finally`.
- **El formato regional del monto por moneda —separadores, posición del símbolo, la columna
  `decimales`— sigue siendo del ticket 6** (D8-05). Acá sólo importa que las monedas se distingan
  entre sí.
- **No hay conversión, ni cotización, ni total consolidado.** El PRD del producto los excluye y
  `RF-29` lo prohíbe explícitamente.

---

## Deuda registrada

Lo que esta feature **no** va a dejar hecho, con el ticket que lo cubre. Se hereda la forma de la
tabla de las features 004, 006, 007 y 008.

| # | Qué queda | Por qué no acá | Quién lo cubre |
|---|---|---|---|
| D9-01 | **La barra de filtros de categoría y de rango de fechas, y la interfaz de eliminación de un movimiento** | Es la mitad de frontend de FEAT-001b, que salió como feature de backend. La edición sí entra acá porque `PRD:FR-07` la necesita; los filtros de categoría y fecha y el borrado no los pide ningún requisito de este ticket, y arrastrarlos lo duplicaría | Ticket 6 (Maquetación y accesibilidad) |
| D9-06 | **La vista de totales: totales por moneda, balance y desglose por categoría en pantalla** | El recálculo de esos totales se verifica acá (FR-012b) contra una lectura que existe y funciona desde FEAT-001c. Lo que falta es quién los pinta, y eso es el dashboard. El criterio, dicho por el usuario al acotar el alcance: si la pantalla no existe todavía, es porque está más adelante en el plan | Ticket 5 (Dashboard con gráficos) |
| D9-02 | **`RF-30`: filtrar el resumen y el dashboard por moneda** | El acotado de esta feature es el del **listado**. El del dashboard necesita el dashboard, que no existe | Ticket 5 (Dashboard con gráficos) |
| D9-03 | **Alta, edición y baja de monedas desde la interfaz** | Fuera de alcance explícito del PRD: el catálogo se administra como dato, y la 008 construyó una barrera entera para que siga siendo así | Nadie. Es una decisión de producto, no deuda |
| D9-04 | **Recordar la última moneda elegida** como nuevo valor por defecto | Fuera de alcance explícito del PRD: el valor por defecto es el del catálogo (`RF-25`), no un historial | Nadie |
| D9-07 | **happy-dom no simula que `Escape` cierre un `<dialog>`**, así que ningún test automatizado cubre esa mitad del cierre | Se comprobó ejecutándolo: tras un `Escape` el `<dialog>` sigue con `open === true` y no emite `close`. `close()` sí funciona. Un test que dijera "verifica Escape" verificaría un `keydown` que nadie escucha, y pasaría en verde el día que el cierre se rompiera de verdad. Lo que sí se afirma es que el camino del cierre está cableado | Nadie. Es un límite del entorno, documentado. El paso 8 del quickstart lo comprueba a mano |
| D9-08 | **Dos tests intermitentes observados durante la implementación**, ninguno de esta feature | `RendimientoLimiteTests...AC12` (de la feature 003) mide contra un techo de 50 ms y falla bajo carga: 50,4 ms y 118 ms en dos corridas de la suite completa, y verde 3/3 aislado. Es la clase de rojo por la que el CI excluye `Rendimiento/`. Y un fallo único no identificado en la suite del frontend, que no volvió a aparecer en cuatro corridas seguidas | El ticket que decida si el techo de 50 ms de AC-12 sigue siendo el criterio correcto, o si ese caso debe medir de otra forma |
| D9-09 | **`moneda.codigo` es `char(3)` y nada exige que sean tres LETRAS.** Falta un `CHECK` que lo diga | `Intl.NumberFormat` exige tres letras ASCII y lanza con cualquier otra cosa, así que `'BT1'` —dato válido para el esquema— tumbaba el listado entero. El listado ya se degrada en vez de caerse (hallazgo 2 de la revisión), pero eso protege la pantalla, no la integridad del dato: el catálogo sigue aceptando un código que ninguna capa sabe formatear. El `CHECK` necesita una migración, y esta feature no tiene ninguna | El próximo ticket que toque el esquema |
| D9-10 | **El indicador "Cargando movimientos…" no se vuelve a encender al cambiar el acotado**: mientras llega el listado nuevo se sigue viendo el viejo, sin señal de que hay algo en curso | Es el hallazgo 6 de la revisión y se decidió **no** arreglarlo. La forma directa —`setCargandoListado(true)` al principio del efecto— la prohíbe `react-hooks/set-state-in-effect`, y con razón: cuesta un render extra. Las alternativas (derivar el estado de qué acotado corresponde a la lista que se tiene) cuestan bastante más que el problema, que es cosmético: la lista vieja se ve un instante y la nueva la reemplaza. La carrera que sí importaba —que la respuesta vieja pisara a la nueva— quedó cerrada aparte | El ticket 6 (Maquetación), si alguna vez molesta |
| D9-05 | **El formato regional del monto según la moneda** y la columna `decimales`, que sigue sin usarse | Es maquetación. Es **D8-05** de la 008, que sigue apuntando al mismo lugar | Ticket 6 (Maquetación y accesibilidad) |

---

## Dependencies

- **Feature 008 (`DISC-001-04a`) mergeada en `main`** — lo está desde el 2026-09-04, commit
  `7c33ae4`. Aporta el catálogo verificado, la predeterminada única garantizada por la migración
  `UnicaMonedaPredeterminada`, los totales ya separados por moneda y la barrera
  `verificar-monedas.sh`.
- **FEAT-001a**: el formulario de registro y el listado sobre los que se agrega el campo.
- **FEAT-001b (feature 005)**: los acotados del listado, a los que se suma el de moneda, y la
  modificación de un movimiento propio, de la que depende FR-011.
- **FEAT-001c (feature 006)**: los totales del resumen, sobre los que FR-012 verifica el efecto del
  cambio de moneda.
- **Feature 007**: el patrón de "un solo catálogo, pedido una vez en la raíz y bajado por props"
  (D-08), del que FR-013 hereda la solución.
- **El filtro de tests de rendimiento del CI** (`FullyQualifiedName!~Rendimiento`), declarado en la
  tabla de *Stack* de `AGENTS.md`, que SC-008 necesita.

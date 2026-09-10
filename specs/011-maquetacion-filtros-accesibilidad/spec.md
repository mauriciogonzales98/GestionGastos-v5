# Feature Specification: Maquetación, filtros del listado y accesibilidad

**Feature Branch**: `011-maquetacion-filtros-accesibilidad`

**Created**: 2026-09-08

**Status**: Draft

**Input**: Ticket DISC-001-06 — "Maquetación y accesibilidad"
(`plan-de-implementacion/prds/pendientes/prd-DISC-001-06.md`), noveno y último de los PRD de
DISC-001. **Ampliado por decisión del usuario** para saldar además **D10-01** (la barra de filtros
de categoría y de rango de fechas del listado, y la interfaz de eliminación de un movimiento) y
**D10-02** (el formato regional del monto y la columna `decimales`), las dos deudas que la feature
010 dejó apuntando a este ticket.

---

## De dónde sale esta spec

Las features 008, 009 y 010 aprendieron, a fuerza de encontrarse el trabajo ya hecho, que el PRD
hay que verificarlo contra el código antes de planificar. Esta spec empieza igual: **verificado
contra el código el 2026-09-08**, y recién después dice qué queda.

El reparto es el inverso del de la 010. Allá el backend estaba entero y el frontend en cero; acá el
backend está entero **otra vez** —los tres acotados y el borrado existen desde FEAT-001b y nadie los
llama— y lo que falta es todo de pantalla. Con una sola excepción, chica y declarada: `decimales`
tiene que empezar a viajar, y eso toca el contrato.

### Lo que ya está construido

| Lo que hace falta | Dónde está | Desde |
|---|---|---|
| Acotar el listado por **categoría** | `GET /api/movimientos?categoriaId`, `MovimientosConsulta.Filtrado` | FEAT-001b |
| Acotar el listado por **rango de fechas**, extremos incluidos | `GET /api/movimientos?desde&hasta`, interpretado por `Dominio/PeriodoPedido.cs` | FEAT-001b |
| El **mes en curso por omisión** cuando no se manda período | `PeriodoPedido.Interpretar` con el `hoy` del servidor | FEAT-001c |
| Las tres reglas del período —los dos extremos juntos o ninguno, el rango invertido rechazado, el mes en curso por omisión— en **un solo lugar** | `Dominio/PeriodoPedido.cs` (D-03 de la 006) | FEAT-001c |
| **Eliminar** un movimiento propio | `DELETE /api/movimientos/{id}`, con el 404 uniforme que no distingue lo ajeno de lo inexistente | FEAT-001b |
| La columna `decimales` de cada moneda | `Dominio/Moneda.cs`, `byte Decimales = 2`, y la semilla de la migración `Inicial` | FEAT-001a |
| La cuenta de la relación de contraste de WCAG 2.1 | `frontend/src/ui/contraste.ts`, `relacionDeContraste`, con su test | 010 |
| El foco visible reforzado y nunca anulado sin reemplazo | `estilos/base.css`, `:focus-visible` | FEAT-001a |
| El texto sólo para lectores de pantalla | `estilos/base.css`, `.u-solo-lectores` | FEAT-001b |
| El recorrido por teclado del **formulario de registro** (`PRD:AC-55`) | `frontend/tests/TecladoFormulario.test.tsx` | FEAT-001a |
| La asociación de cada error con su campo | `frontend/src/ui/CampoConError.tsx`, con su test | FEAT-001a |

Dos de esas filas merecen leerse dos veces.

**El acotado del listado no hay que construirlo: hay que enchufarlo.** `AcotadoDelListado` en
`frontend/src/api/cliente.ts` lleva escrito, en su propio comentario, que el servidor acota por
categoría y por fecha desde FEAT-001b y que *"este tipo es donde va a crecer cuando se salde"* la
deuda D9-01. La barra de acotado de `PantallaMovimientos` dice lo mismo: *"acá es donde la barra va
a crecer"*. Los dos lugares están señalizados desde la 009.

**El borrado es el único endpoint de la API que nunca tuvo un cliente.** `DELETE
/api/movimientos/{id}` existe, está probado y aísla por cuenta; `cliente.ts` no tiene una función
que lo llame. `PRD:RF-15` y `PRD:AC-21` están sin cumplir **en la pantalla**, no en el servidor.

### Lo que falta de verdad

**Cinco clases de disposición referenciadas sin ninguna regla.** Es `PRD-06:AC-04`, y el conteo está
hecho: `c-campo`, `c-formulario-acceso`, `c-formulario-movimiento__error`, `c-resumen` y
`c-totales-moneda`. No es una impresión sobre el CSS: es la lista.

**El proyecto no declara ni un color.** `estilos/base.css` dice en su primer comentario que colores,
espaciados y tipografía son de este ticket. Los cuatro valores de `ui/contraste.ts` son lo único que
hay, y dos de ellos —`#000000` y `#ffffff`— están anotados como *"los del navegador, no una
elección"*, puestos ahí para que el test tuviera contra qué medir. Es la deuda **D10-07**.

**Ningún ancho objetivo, y nada que lo verifique.** La aplicación se sostiene por el flujo por
defecto del navegador. El listado es una tabla de seis columnas y el dashboard dibuja barras al
100 % del ancho disponible; ninguno de los dos se probó nunca angosto.

**El monto no sabe cuántos decimales tiene su moneda.** `formatearMonto` delega en `Intl` la escala
según el código ISO, y `decimales` no viaja: `tipos.ts` lo dice con todas las letras —*"hoy no lo
consume nadie... el formato regional del monto es el ticket 6"*—. Es **D8-05**, **D9-05** y
**D10-02**, la misma deuda anotada tres veces.

### La contradicción que hubo que resolver antes de escribir

El PRD del ticket 6 y las tablas de deuda de las features 009 y 010 no dicen lo mismo, y no es un
detalle de redacción.

El PRD acota el ticket a maquetación y accesibilidad, y lo blinda con `PRD-06:FR-06` y
`PRD-06:AC-07`: *"no cambia qué pantallas hay, qué hace cada una ni cómo se navega"*, y la suite
existente tiene que seguir pasando **sin que ningún test se haya modificado**. Las tablas de deuda,
en cambio, mandan a este ticket la barra de filtros, la interfaz de eliminación y el formato del
monto — que son comportamiento nuevo, y por lo tanto tests nuevos y algún test existente cambiado.

**Se resolvió a favor de las tablas de deuda**, por decisión del usuario registrada en
*Clarifications*. La consecuencia está asumida y acotada abajo, en `FR-020`: el blindaje de
`PRD-06:FR-06` sigue en pie para todo lo que esta spec **no** agrega explícitamente. Un test que
haya que retocar y que no esté justificado por `FR-011` a `FR-019` es la señal de que la maquetación
se comió el comportamiento, que es exactamente el riesgo que el PRD quería atrapar.

## Lo que hace distinta a esta feature

Es la primera que **no agrega ninguna capacidad al servidor**. Todo lo que pide ya está calculado,
acotado y probado del otro lado de la API; lo que falta es la mitad de la aplicación que se mira.
El único cambio de backend es un campo más en un DTO.

Y es la primera cuyo criterio de éxito se mide sobre **la aplicación entera** y no sobre lo que ella
misma construyó. `AC-04` cuenta clases en todas las pantallas; `AC-06` mide contraste en todas;
`AC-02` recorre el foco de todas. Una pantalla que quedó afuera no se ve como una funcionalidad
faltante: se ve como un criterio que pasó midiendo de menos.

## Clarifications

### Session 2026-09-08

- P: El PRD del ticket 6 se limita a maquetación y accesibilidad, pero D10-01 y D10-02 apuntan
  también a este ticket. ¿Qué entra? → **Todo**: maquetación y accesibilidad, más la barra de
  filtros de categoría y rango de fechas, la interfaz de eliminación y el formato regional del
  monto. `PRD-06:FR-06` y `PRD-06:AC-07` se reescriben acá como `FR-020`, acotados a "todo lo que
  esta spec no agrega".
- P: ¿Con qué se verifican el contraste, las etiquetas y los 360 px? → **Con tests propios, sin
  dependencias nuevas.** Se extiende `ui/contraste.ts`, que ya calcula la relación de WCAG y ya
  tiene test, y se escriben tests que recorren el foco y exigen etiqueta accesible en cada control.
  Motivo: `AGENTS.md` exige justificar en la spec toda librería nueva, y una auditoría automática
  cubriría muchas más reglas que las tres que `PRD:RNF-06` pide, produciendo rojos que nadie
  encargó. El costo aceptado está en `FR-009` y anotado como deuda **D11-01**: jsdom no maqueta, así
  que el desborde horizontal a 360 px se verifica por regla de estilo y no por medición.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - La aplicación se ve terminada (Priority: P1) 🎯 MVP

Quien entra ve una aplicación con colores, espacios y jerarquía propios, y no un formulario sin
estilos sobre una lista. Cada clase que el código nombra tiene una regla que la respalda, y los
colores salen de un solo lugar declarado.

**Why this priority**: Es el núcleo del ticket y lo que desbloquea al resto. La paleta es
precondición de la Historia 2 —no se puede medir contraste AA sobre colores que nadie eligió— y las
reglas de disposición son precondición de la Historia 5, porque una barra de filtros nueva necesita
un contenedor que sepa acomodarla.

**Independent Test**: Se compara la lista de clases `l-`, `c-` y `u-` referenciadas en el código con
las reglas de la hoja de estilos y se exige 0 sin regla; se abre cada pantalla y se verifica que no
haya desborde horizontal a 360 px de ancho.

**Acceptance Scenarios**:

1. **Given** el código referencia una clase de disposición, **When** se la busca en la hoja de
   estilos, **Then** existe una regla para ella.
2. **Given** cualquier pantalla de la aplicación, **When** se la muestra en una ventana de 360 px de
   ancho, **Then** todo su contenido es alcanzable y nada desborda horizontalmente.
3. **Given** el listado de movimientos con sus seis columnas, **When** se lo muestra a 360 px,
   **Then** la tabla se desplaza dentro de su propio contenedor y la página no.
4. **Given** la paleta declarada, **When** se agrega un color nuevo, **Then** queda en el mismo
   único lugar del que ya salen los cuatro que existen.

---

### User Story 2 - Se usa entera con el teclado y se deja leer (Priority: P2)

Quien navega con teclado o con lector de pantalla recorre **todas** las pantallas —acceso,
movimientos, categorías, dashboard y la ventana de edición— sabiendo siempre dónde está parado, qué
es cada control y por qué se rechazó lo que intentó guardar.

**Why this priority**: Es el piso verificable de `PRD:RNF-06` y la mitad del ticket. Va después de
la paleta porque el contraste se mide sobre colores elegidos, no sobre los del navegador.

**Independent Test**: Se recorren con el teclado los controles de cada pantalla y se exige foco
visible y etiqueta accesible en cada uno; se mide la relación de contraste de cada par de la paleta.

**Acceptance Scenarios**:

1. **Given** cualquier pantalla, **When** se la recorre con el teclado, **Then** cada control
   interactivo muestra foco visible y expone una etiqueta accesible.
2. **Given** cualquier pantalla, **When** se recorre con el teclado, **Then** el orden del foco
   corresponde al orden de lectura.
3. **Given** el formulario de registro, **When** se lo completa y se lo envía sólo con el teclado,
   **Then** el movimiento queda registrado.
4. **Given** el foco sobre el último control del formulario de registro, **When** se sigue
   avanzando, **Then** el foco sale del formulario y no queda atrapado.
5. **Given** un campo inválido, **When** se intenta guardar, **Then** el motivo se muestra asociado
   a ese campo y esa asociación es alcanzable por teclado y por lector.
6. **Given** la paleta declarada, **When** se mide cada par texto/fondo, **Then** da al menos 4,5:1
   en texto normal y al menos 3:1 en texto grande y en componentes de interfaz.
7. **Given** la ventana de edición abierta, **When** se recorre con el teclado, **Then** el foco
   queda dentro de la ventana mientras está abierta y vuelve a un lugar previsible al cerrarse.

---

### User Story 3 - Borrar un movimiento que cargué mal (Priority: P3)

Quien registró un movimiento equivocado lo elimina desde el listado, con una confirmación previa
para que un clic accidental no borre nada.

**Why this priority**: Es `PRD:RF-15` y `PRD:AC-21`, el único requisito funcional del PRD del
producto que no tiene ni una línea de pantalla. El endpoint existe desde FEAT-001b y nadie lo llama.
Es una historia chica y de valor inmediato: hoy la única forma de deshacer un alta equivocada es
editarla.

**Independent Test**: Se elimina un movimiento del listado y se verifica que desaparece de la lista
y que su monto deja de sumar en el resumen del mes.

**Acceptance Scenarios**:

1. **Given** un movimiento propio en el listado, **When** se lo elimina y se confirma, **Then** deja
   de aparecer en el listado y su monto deja de sumar en el resumen del mes.
2. **Given** el pedido de confirmación abierto, **When** se cancela, **Then** el movimiento sigue
   estando y no se llamó al servidor.
3. **Given** un movimiento que otra sesión ya eliminó, **When** se lo intenta eliminar, **Then** se
   muestra el motivo y el listado deja de mostrarlo.
4. **Given** el único movimiento del listado, **When** se lo elimina, **Then** el listado muestra su
   mensaje de vacío y no una tabla sin filas.
5. **Given** una sesión vencida, **When** se intenta eliminar, **Then** se vuelve al acceso con un
   aviso que dice qué pasó con ese movimiento.

---

### User Story 4 - Ver sólo lo que estoy buscando (Priority: P4)

Quien tiene meses de movimientos cargados acota el listado por categoría y por rango de fechas,
además de por moneda, y ve cuántos resultados quedaron.

**Why this priority**: Son `PRD:RF-17` y `PRD:RF-18` con sus cuatro criterios (`AC-23` a `AC-26`),
la mitad de frontend de FEAT-001b que salió como feature de backend. Va después del borrado porque
es más grande y porque el servidor ya la resuelve entera: es enchufar, no construir.

**Independent Test**: Se elige una categoría y un rango y se verifica que el listado muestra
únicamente lo que cae dentro de los dos.

**Acceptance Scenarios**:

1. **Given** movimientos de varias categorías, **When** se elige una, **Then** el listado muestra
   únicamente los de esa categoría.
2. **Given** el listado recién abierto, **When** no se eligió ninguna categoría, **Then** se ven los
   de todas.
3. **Given** el listado recién abierto, **When** no se eligió ningún rango, **Then** se ven
   únicamente los del mes actual, y el control muestra ese mes sin que nadie lo haya elegido y sin
   que la pantalla lo haya calculado.
4. **Given** un rango de fechas elegido, **When** se aplica, **Then** el listado muestra los
   movimientos cuya fecha cae dentro del rango, extremos incluidos.
5. **Given** los tres acotados elegidos a la vez, **When** se aplican, **Then** el listado muestra
   únicamente lo que cumple los tres.
6. **Given** un rango con la fecha final anterior a la inicial, **When** se aplica, **Then** se
   muestra el motivo del rechazo y el listado no cambia lo que estaba mostrando.
7. **Given** un rango con un solo extremo cargado, **When** se aplica, **Then** se muestra el motivo
   y no se pide un listado a medias.
8. **Given** cualquier combinación de acotados, **When** no queda ningún movimiento, **Then** se
   dice que no hay resultados para ese acotado y no se muestra ningún error.

---

### User Story 5 - El monto se lee en la escala de su moneda (Priority: P5)

Quien mira un monto lo ve con la cantidad de decimales que su moneda usa, y no con dos siempre.

**Why this priority**: Es la deuda anotada tres veces —**D8-05**, **D9-05**, **D10-02**— y la más
chica de las cinco historias. Va última porque es la única que toca el contrato, y porque su valor
sólo se nota el día que el catálogo tenga una moneda sin centavos.

**Independent Test**: Se muestra un monto de una moneda con 0 decimales y se verifica que no
aparecen centavos, en el listado, en el resumen y en el dashboard.

**Acceptance Scenarios**:

1. **Given** una moneda con 2 decimales en el catálogo, **When** se muestra un monto suyo,
   **Then** se ven dos decimales.
2. **Given** una moneda con 0 decimales en el catálogo, **When** se muestra un monto suyo, **Then**
   no se ven decimales, en el listado, en el resumen del mes y en el dashboard.
3. **Given** una moneda cuyo `decimales` del catálogo difiere de lo que `Intl` supone por su código
   ISO, **When** se muestra un monto suyo, **Then** gana el dato del catálogo.
4. **Given** un código de moneda que `Intl` no puede interpretar, **When** se muestra un monto suyo,
   **Then** se sigue viendo el número con su código al lado y ninguna pantalla se cae.

---

### Edge Cases

- **La tabla del listado a 360 px.** Seis columnas no entran en un teléfono. Se desplaza dentro de
  su contenedor; la página no desborda (`AC-03` de la Historia 1).
- **Una categoría dada de baja en el selector del acotado.** El catálogo devuelve sólo activas, así
  que no se puede elegir — pero sus movimientos siguen existiendo y aparecen cuando no se acota por
  categoría. El acotado no es el desglose del resumen: acá filtrar por activa es lo correcto, y por
  eso `verificar-desglose.sh` no se toca.
- **Dos acotados cambiados rápido.** El listado ya descarta la respuesta que llega tarde
  (`PantallaMovimientos`, la bandera `vigente`); tres controles en lugar de uno hacen más probable
  el caso, no distinto.
- **Eliminar mientras la ventana de edición está abierta sobre esa misma fila.** El `<dialog>` es
  modal y vuelve inerte el fondo, así que no es alcanzable; se documenta como no alcanzable en vez
  de defenderse contra ello.
- **Un color que la cuenta de contraste no puede leer.** `contraste.ts` lanza a propósito y no
  devuelve un valor por defecto: un color ilegible es un error de quien lo escribió, y devolver
  "negro" haría que el verificador informara un contraste que nadie va a ver.
- **Una pantalla nueva agregada después de esta feature.** `AC-04` y `AC-06` la incluirían sola sólo
  si la verificación recorre el código y no una lista escrita a mano. Es el mismo criterio que
  `verificar-monedas.sh` aplica al catálogo.
- **El foco después de eliminar la última fila.** El botón que se apretó dejó de existir; el foco no
  puede quedar en el `<body>` sin anuncio.

## Requirements *(mandatory)*

### Functional Requirements

**Maquetación (Historia 1)**

- **FR-001**: La hoja de estilos DEBE definir una regla para **cada** clase de disposición o de
  componente referenciada en el código, sin dejar ninguna referenciada y sin regla.
  *(`PRD-06:FR-04`, `PRD-06:NFR-03`)*
- **FR-002**: El sistema DEBE declarar su paleta —color de texto, de fondo, de acento, de error y de
  las barras del dashboard— en **un solo lugar**, del que salgan tanto la hoja de estilos como la
  verificación de contraste. *(D10-07; hoy son cuatro valores en `ui/contraste.ts`)*
- **FR-003**: El sistema DEBE presentar todas sus pantallas de forma utilizable en un ancho de
  ventana de 360 px, sin desborde horizontal de la página ni contenido inalcanzable.
  *(`PRD-06:FR-05`)*
- **FR-004**: El contenido que no entra a 360 px —la tabla del listado— DEBE desplazarse dentro de
  su propio contenedor.

**Accesibilidad (Historia 2)**

- **FR-005**: Todo control interactivo de **toda** pantalla DEBE mostrar foco visible al recibirlo y
  DEBE exponer una etiqueta accesible. *(`PRD:RNF-06`, `PRD-06:FR-02`)*
- **FR-006**: El orden del foco DEBE corresponder al orden de lectura de cada pantalla.
  *(`PRD-06:AC-08`)*
- **FR-007**: El formulario de registro DEBE poder completarse y enviarse íntegramente con el
  teclado, y el foco DEBE poder salir de él sin quedar atrapado. *(`PRD:AC-55`, `PRD-06:AC-09`)*
- **FR-008**: Todo mensaje de error de validación DEBE estar asociado al campo que lo origina, de
  modo que quien recorre con teclado o con lector reciba el motivo al llegar a ese campo.
  *(`PRD-06:FR-03`)*
- **FR-009**: La ventana de edición DEBE retener el foco mientras está abierta y devolverlo a un
  lugar previsible al cerrarse.

**Eliminación (Historia 3)**

- **FR-010**: El sistema DEBE permitir eliminar un movimiento propio desde el listado.
  *(`PRD:RF-15`, `PRD:AC-21`)*
- **FR-011**: La eliminación DEBE pedir confirmación antes de ejecutarse, y cancelarla NO DEBE
  llamar al servidor.
- **FR-012**: Al eliminar, el sistema DEBE quitar la fila del listado y DEBE recalcular el resumen
  del mes, sin recargar la pantalla.
- **FR-013**: Si el movimiento ya no existe, el sistema DEBE decirlo y DEBE dejar de mostrarlo. Si
  la sesión venció, DEBE reaccionar como el resto de la aplicación, diciendo qué pasó con ese
  movimiento.

**Acotado del listado (Historia 4)**

- **FR-014**: El sistema DEBE permitir acotar el listado por categoría, con "todas las categorías"
  como valor por omisión. *(`PRD:RF-17`)*
- **FR-015**: El sistema DEBE permitir acotar el listado por rango de fechas, con el mes actual como
  valor por omisión, y DEBE incluir ambos extremos. El mes que el control muestra por omisión DEBE
  venir del servidor, no de una cuenta hecha en la pantalla. *(`PRD:RF-18`)*
- **FR-016**: Los tres acotados —categoría, rango y moneda— DEBEN poder combinarse, y el listado
  DEBE mostrar únicamente lo que cumple los tres.
- **FR-017**: El acotado lo DEBE resolver el servidor: el sistema NO DEBE filtrar en la pantalla la
  lista que ya tenía.
- **FR-018**: Cuando el servidor rechaza un período —rango invertido, o un solo extremo— el sistema
  DEBE mostrar su motivo y DEBE conservar lo que el listado estaba mostrando.

**Formato del monto (Historia 5)**

- **FR-019**: El sistema DEBE mostrar cada monto con la cantidad de decimales que el catálogo
  declara para su moneda, y ese dato DEBE prevalecer sobre lo que se suponga a partir del código
  ISO. *(D8-05, D9-05, D10-02; `PRD:RF-32` — la moneda es un dato)*

**El blindaje del alcance**

- **FR-020**: El sistema DEBE conservar sin cambios el comportamiento de toda pantalla **salvo donde
  `FR-010` a `FR-019` lo agregan explícitamente**: qué pantallas hay, qué hace cada una y cómo se
  navega entre ellas no cambian. Ningún test existente DEBE modificarse para acomodar un cambio
  visual. *(`PRD-06:FR-06`, `PRD-06:AC-07`, reescritos por el alcance ampliado de esta feature)*

### Non-Functional Requirements

- **NFR-001**: El texto normal DEBE cumplir una relación de contraste de al menos **4,5:1**, y el
  texto grande y los componentes de interfaz de al menos **3:1**, en el 100 % de los elementos de
  todas las pantallas. *(`PRD:RNF-06`, `PRD-06:NFR-01`)*
- **NFR-002**: La suite DEBE verificar el recorrido por teclado y la presencia de etiqueta accesible
  en el 100 % de los controles interactivos de todas las pantallas. *(`PRD-06:NFR-02`)*
- **NFR-003**: La verificación de `FR-001` DEBE recorrer el código y no una lista escrita a mano: una
  pantalla agregada después tiene que quedar cubierta sola. *(mismo criterio que
  `verificar-monedas.sh`)*
- **NFR-004**: Esta feature NO DEBE agregar ninguna dependencia, y esa ausencia DEBE verificarse
  contra los manifiestos de las dos pilas al cerrar. *(`AGENTS.md`; ver *Clarifications*)*
- **NFR-005**: El acotado del listado NO DEBE degradar el rendimiento ya medido: sigue siendo una
  sola consulta al servidor por cambio de acotado.

### Key Entities

Esta feature **no agrega ninguna entidad ni ninguna columna**. Toca el contrato en un solo punto:

- **Moneda**: `decimales` empieza a viajar en `GET /api/monedas`. La columna existe desde FEAT-001a
  y hasta hoy no salía a la red porque nadie la consumía. Es el único cambio de backend de toda la
  feature, y arrastra a los tests de contrato, que comparan las dos definiciones.
- **Acotado del listado**: lo que hoy es sólo `monedaId` pasa a llevar también `categoriaId`,
  `desde` y `hasta`. Los cuatro ya los entiende el servidor; ninguno es nuevo del lado de la API.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: **0** clases de disposición o de componente referenciadas en el código sin una regla
  en la hoja de estilos, contadas sobre la aplicación entera.
- **SC-002**: **0** pantallas con desborde horizontal a 360 px de ancho.
- **SC-003**: **100 %** de los controles interactivos con foco visible y etiqueta accesible.
- **SC-004**: **100 %** de los pares texto/fondo de la paleta por encima de su umbral AA — 4,5:1 en
  texto normal, 3:1 en texto grande y en componentes.
- **SC-005**: Alguien que cargó un movimiento equivocado lo elimina desde el listado en un solo
  camino, sin salir de la pantalla y sin recargarla.
- **SC-006**: Alguien que busca lo que gastó en una categoría durante un rango de fechas lo obtiene
  eligiendo dos controles, sin escribir una URL ni hacer una cuenta a mano.
- **SC-007**: **0** tests existentes modificados para acomodar un cambio visual. Los tests que
  cambien están justificados por `FR-010` a `FR-019`, uno por uno.
- **SC-008**: **0** dependencias nuevas en los manifiestos de las dos pilas.
- **SC-009**: Los cuatro criterios del PRD del producto que hoy no tienen pantalla —`AC-21`,
  `AC-23`, `AC-24`, `AC-26`— quedan cubiertos por un test que los nombra.

## Assumptions

- **La aplicación tiene cinco superficies y son todas las que hay**: acceso, movimientos (con su
  formulario, su resumen y su listado), la ventana de edición, categorías y dashboard. "Todas las
  pantallas" quiere decir estas cinco; una sexta agregada después queda cubierta por `NFR-003`.
- **360 px es la decisión de este proyecto, no una traducción de `PRD-001`**, que no fija ningún
  objetivo de ancho. Está declarada en `PRD-06:FR-05` y se hereda tal cual.
- **La paleta se elige acá y por primera vez.** No hay marca, ni guía de estilo, ni referencia
  externa que respetar. El único requisito duro es `NFR-001`.
- **El acotado por categoría ofrece sólo categorías activas**, que es lo que el catálogo devuelve.
  Un acotado que ofreciera las dadas de baja necesitaría un endpoint que hoy no existe, y no lo pide
  ningún requisito.
- **La confirmación de borrado es un paso en la pantalla**, no un segundo endpoint ni un borrado
  lógico. `DELETE /api/movimientos/{id}` borra de verdad y así seguirá.
- **El período por omisión del listado se lee del resumen que la pantalla principal ya carga.**
  `GET /api/movimientos` devuelve un arreglo pelado y no dice qué período aplicó; `GET /api/resumen`
  sí lleva `desde` y `hasta`. Prefijar el control desde ahí es lo que permite cumplir `FR-015` sin
  poner un segundo intérprete de "hoy" en la pantalla.
- **`decimales` viaja tal como está en la base**, sin validación nueva: es un `tinyint unsigned` con
  valor por omisión 2 y la semilla lo respeta.
- **Modo oscuro, temas, animaciones, nivel AAA, internacionalización, impresión, adoptar un
  framework de estilos y anchos menores a 360 px quedan fuera**, tal como los declara el PRD.
- **Rediseñar el producto queda fuera**: `FR-020` lo blinda. Lo que esta feature agrega son cinco
  controles, no una pantalla nueva ni un flujo nuevo.

## Deuda registrada

Lo que esta feature **no** va a dejar hecho, con el ticket que lo cubre. Se hereda la forma de la
tabla de las features 004, 006, 007, 008, 009 y 010.

| # | Qué queda | Por qué no acá | Quién lo cubre |
|---|---|---|---|
| D11-01 | **El desborde horizontal a 360 px verificado por medición real**, en un navegador, y con él los pasos 1 a 4 del quickstart, que no se ejecutaron | jsdom no maqueta: no calcula anchos, así que un test de unidad no puede medir un desborde. Acá se verifica por **regla** —que ninguna pantalla declare un ancho fijo mayor a 360 px y que el contenido ancho tenga su contenedor desplazable—, que es una aproximación honesta y no una medición. `NFR-004` cierra la puerta a traer un navegador para esto | Quien decida que vale un runner de navegador en el CI; arrastra también **D10-09**, los pasos de quickstart que nunca se corrieron a mano |
| D11-02 | ~~**`CHECK` sobre `moneda.codigo` para exigir tres letras**~~ · **SALDADA por la feature 012**, en la migración `NotaDelMovimientoYCodigoDeTresLetras`. Y no era teórica: había tres códigos inválidos en los fixtures del proyecto —`XF1`, `XF2`, `XR1`— pasando en verde desde la 009, porque el `char(3)` sólo miraba el largo | Es **D9-09** y **D10-03**. Esta feature agrega un cuarto lugar donde un código que `Intl` no entiende se puede cruzar, y **no tiene ninguna migración**: el único cambio de esquema que necesitaba —`decimales`— ya está en la base desde FEAT-001a. Sigue esperando al ticket que abra una migración por otro motivo | El próximo ticket que toque el esquema |
| D11-03 | **Revisar el techo de 50 ms de `003:AC-12`** | Es **D9-08** y **D10-04**. Esta feature no toca el backend salvo un campo de un DTO, así que no re-decide el criterio de rendimiento de una feature anterior. **Pero sí cerró la mitad que faltaba de D10-10**: el fallo único que la 010 no pudo identificar apareció en la puerta de cierre de ésta, con nombre — `RendimientoLimiteTests.El_P95_De_La_Comprobacion_Agrega_Menos_De_Cincuenta_Milisegundos_AC12`. Falla en la corrida completa bajo carga y pasa aislado y en la corrida de cobertura, que es exactamente lo que D9-08 describe. Ya no hay nada que averiguar: hay que decidir el techo | El ticket que decida si ese techo sigue siendo el correcto |
| D11-04 | **Ponerle color a las barras del dashboard** | Es **D10-08**, y esta feature sí trae la paleta que le faltaba. Queda anotado igual porque D-04 de la 010 dejó todas las barras del mismo relleno **a propósito** —las categorías no se codifican por color— y revertir esa decisión es una decisión de producto, no una consecuencia de tener paleta | Nadie, salvo que producto lo pida |
| D11-05 | **Modo oscuro, temas y cualquier preferencia visual configurable** | Fuera de alcance explícito del PRD del ticket 6 | Nadie |
| D11-06 | **Auditoría completa de ARIA y verificación con lectores de pantalla concretos** | `PRD:RNF-06` fija tres exigencias —etiquetas, foco, asociación de errores— y esta feature las cumple. Ir más allá es alcance que nadie pidió, y `NFR-004` prohíbe la dependencia con la que se haría | Nadie |
| D11-07 | ~~**Nota descriptiva del movimiento**~~ (`PRD:RF-33`, `AC-50` a `AC-53`) · **SALDADA por la feature 012**, que era el ticket 2 y el último del plan DISC-001 | Es el ticket 2 del plan, que no tiene dependencias y entra en cualquier hueco. Esta feature agrega una columna al listado —el borrado— y sería tentador agregar la nota de paso; sería el mismo contrabando que `FR-020` blinda | Ticket 2 (Nota descriptiva del movimiento) |

## Dependencies

- **`DISC-001-05` mergeado en `main`** — está: la 010 se mergeó en `214e5e5`. Es la última pantalla
  que faltaba, y el PRD del ticket 6 dice que cada una que falte es una pantalla que la pasada no va
  a cubrir. **No falta ninguna.**
- **Los endpoints de FEAT-001b**: `GET /api/movimientos` con sus cuatro acotados y `DELETE
  /api/movimientos/{id}`. Existen y están probados; esta feature no los modifica.
- **`Dominio/PeriodoPedido.cs`**, que es de donde salen los mensajes de rechazo del período que
  `FR-018` muestra. La pantalla no reimplementa esas reglas: las muestra.
- **`frontend/src/ui/contraste.ts`**, la cuenta de WCAG que la 010 dejó escrita y probada.
- **La suite existente**, que `FR-020` y `SC-007` usan como referencia de que el comportamiento no
  cambió.
- **Los tests de contrato de `backend/GestionGastos.Api.Tests/Contrato/`**, que se van a poner en
  rojo en cuanto `decimales` aparezca de un solo lado. Es lo que tienen que hacer.

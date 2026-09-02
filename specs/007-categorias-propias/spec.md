# Feature Specification: Categorías propias del usuario

**Feature Branch**: `007-categorias-propias`

**Created**: 2026-09-02

**Status**: Draft

**Input**: DISC-001-03 del plan de implementación, con su PRD en
[`plan-de-implementacion/prds/pendientes/prd-DISC-001-03.md`](../../plan-de-implementacion/prds/pendientes/prd-DISC-001-03.md),
reconciliado contra el estado real del código.

---

## De dónde sale esta spec

**A diferencia de las features 005 y 006, este ticket sí tiene su PRD en el repositorio.** Es la
fuente: FR-01 a FR-08, NFR-01 a NFR-03 y AC-01 a AC-13 salen de ahí y no se reinventan.

Lo que sí hizo falta es reconciliarlo con el código. El PRD está fechado el **2026-08-20**, antes de
que se mergearan las features 005 y 006, y **tres de sus premisas ya no son ciertas**. Queda escrito
acá porque un PRD que describe un código que cambió es peligroso de otra manera que uno incompleto:
se lee como si fuera cierto.

| Lo que dice el PRD | Lo que hay en el código | Qué se hace |
|---|---|---|
| El índice único `(nombre, tipo)` es **global** y "el detalle que decide el ticket" es cambiarlo a por-usuario | Ya es `(usuario_id, nombre, tipo)`, con nombre `ux_categoria_ambito_nombre_tipo`. La feature 001 lo anticipó (su D-06), junto con las columnas `usuario_id` (nullable) y `activa` | Esa parte del ticket **ya está hecha**. Pero el índice tal como está **no alcanza** para FR-005 ni FR-009: ver *Lo que hace distinta a esta feature* |
| `FiltrosMovimientos` y `FormularioMovimiento` piden cada uno el catálogo al montar: 2 peticiones por arranque y una tercera al abrir la edición | `FiltrosMovimientos` **no existe**, y tampoco la pantalla de edición: las features 005 y 006 fueron de backend y dejaron sólo el contrato. El catálogo se pide **una sola vez**, en `PantallaMovimientos`, y baja por props | NFR-02 y AC-12 ya se cumplen. Se conservan como **regresión a no romper**, no como trabajo a hacer |
| FR-08 fija el límite del nombre en **60** caracteres, "el que ya tiene la columna" | La columna es `varchar(50)` | Se adopta **50**. El número del PRD era una cita equivocada de la columna, no un requisito de producto: agrandarla sería migrar por un motivo que nadie pidió |

Si mañana aparece una versión más nueva del PRD y dice otra cosa, esta tabla es el lugar donde se ve
qué se supuso y por qué.

---

## Lo que hace distinta a esta feature

Es la primera que **le da al usuario poder de escritura sobre un catálogo**. Hasta acá todo lo que
se podía crear era un hecho propio —un movimiento— que no le cambiaba el vocabulario a nadie. Una
categoría es distinta: es una fila que otras filas nombran, y que sobrevive a quien la dejó de usar.

De ahí salen las tres tensiones que gobiernan esta spec, y las tres viven en el mismo lugar: la
unicidad.

**Primera: la unicidad de la base no es la unicidad que el producto pide.** El índice
`(usuario_id, nombre, tipo)` impide que una cuenta tenga dos "Mascota" de gasto, y deja que dos
cuentas distintas tengan cada una la suya —que es AC-08, y ya funciona—. Pero las predefinidas
tienen `usuario_id = NULL`, y en un índice único de MySQL **dos NULL no chocan con nada**: crear una
categoría propia llamada "Comida" no viola el índice, aunque "Comida" ya exista como predefinida.
FR-005 pide rechazarla igual. Esa comprobación no la puede hacer el índice: la tiene que hacer la
aplicación, y hacerla bien —sin ventana entre la consulta y el alta— es trabajo del plan.

**Segunda: la baja lógica y la unicidad se pisan.** FR-009 exige poder volver a crear una categoría
con el mismo nombre y tipo que una que uno mismo dio de baja. El índice actual no mira `activa`, así
que la fila nueva choca contra la vieja y el alta falla. Es la única premisa del PRD que **sí**
obliga a tocar el esquema, y es justo la que el PRD no anticipó.

**Tercera: dar de baja no puede cambiar ni un número.** El resumen del mes ya suma por categoría
(feature 006), y su desglose sale de un `JOIN` contra `categorias`. Si esa lectura empieza a filtrar
por `activa` —que es el reflejo natural al agregar la columna a las consultas— los totales
históricos cambian solos y nadie se entera: un mes que cerró en 120.000 pasa a cerrar en 95.000
porque alguien archivó una categoría. La feature 006 dejó esto anotado como deuda D6-04 con esta
frase exacta: **el desglose no debe empezar a filtrar por `activa`**.

---

## Clarifications

### Session 2026-09-02

- Q: ¿Renombrar una categoría propia tiene que respetar la misma regla de unicidad que crearla (FR-005)? → A: Sí, la misma regla: contra las activas disponibles para esa cuenta, propias y predefinidas.
- Q: ¿Dónde vive la gestión de categorías —crear, renombrar, dar de baja— y cómo se corrige SC-001? → A: En una pantalla aparte, con su ruta. SC-001 se corrige: la promesa es volver y usarla sin recargar, no no moverse.
- Q: ¿Cómo se llega a la pantalla de gestión de categorías? → A: Con el mismo mecanismo de estado que ya usa `App.tsx` para alternar login ↔ movimientos. Sin dependencias nuevas y sin URL propia.
- Q: ¿El alta y la edición de un movimiento deben validar que la categoría esté disponible para esa cuenta? → A: Sólo se rechaza la **ajena**. Una categoría propia dada de baja se acepta: hay que poder modificar un movimiento que ya la usa sin verse obligado a reclasificarlo.
- Q: Existe un test que hoy rechaza dar de alta un movimiento con una categoría dada de baja. ¿Hasta dónde se relaja? → A: Sólo la **edición**, y sólo cuando es la categoría que el movimiento **ya tenía**. El alta la sigue rechazando y ese test se queda como está.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Nombrar mis gastos con mis propias palabras (Priority: P1) 🎯 MVP

Las diez categorías predefinidas no dicen lo que yo gasto. Pago la cuota del gimnasio y la cargo en
"Otros"; el mes que viene no tengo idea de qué era ese "Otros" de 45.000.

Quiero crear mis propias categorías y que aparezcan en el selector junto a las predefinidas, para
que el desglose del resumen me diga algo.

**Why this priority**: sin crear no hay nada que renombrar ni que dar de baja. Es la historia que
convierte el catálogo de fijo en propio, y sola ya sirve: el resto son correcciones sobre esto.

**Independent Test**: se prueba entero creando una categoría de gasto desde una cuenta, comprobando
que aparece en su selector y que no aparece en el de otra cuenta, y registrando un movimiento con
ella.

**Acceptance Scenarios**:

1. **Given** una cuenta cualquiera, **When** crea una categoría propia de tipo gasto, **Then** esa
   categoría aparece en su selector de categorías de gasto y no aparece en el de ninguna otra
   cuenta. *(AC-01)*
2. **Given** una cuenta recién registrada y sin categorías propias, **When** abre el formulario de
   registro de un gasto, **Then** ve las predefinidas de tipo gasto y ninguna de tipo ingreso.
   *(AC-02)*
3. **Given** una cuenta que ya tiene disponible una categoría activa de gasto llamada "Comida" —sea
   propia o **predefinida**—, **When** intenta crear otra propia con ese mismo nombre y tipo,
   **Then** la creación se rechaza indicando el motivo y no se crea ninguna categoría. *(AC-07)*
4. **Given** dos cuentas distintas, **When** cada una crea una categoría propia con el mismo nombre
   y el mismo tipo, **Then** las dos se aceptan y cada cuenta ve únicamente la suya. *(AC-08)*
5. **Given** una cuenta, **When** intenta crear una categoría con el nombre vacío o de más de 50
   caracteres, **Then** la operación se rechaza indicando el motivo y no se crea nada. *(AC-10)*

---

### User Story 2 - Corregir un nombre sin perder la historia (Priority: P2)

Escribí "Gimnacio" y lo veo cada vez que abro la aplicación. Quiero arreglarlo sin que los
movimientos que ya cargué queden nombrando algo que ya no existe.

**Why this priority**: es la corrección más barata y la que más se va a usar, y no arrastra ninguna
decisión estructural: el nombre es un dato de la fila y los movimientos apuntan al identificador.
Va después de P1 porque no se puede renombrar lo que no se puede crear.

**Independent Test**: se prueba renombrando una categoría propia que ya tiene movimientos y
comprobando que el nombre nuevo aparece tanto en el listado como en el desglose del resumen.

**Acceptance Scenarios**:

1. **Given** una categoría propia con movimientos asociados, **When** el usuario le cambia el
   nombre, **Then** el nombre nuevo aparece en esos movimientos del listado y en el desglose del
   resumen. *(AC-04)*
2. **Given** una categoría **predefinida**, **When** el usuario intenta modificarla, **Then** la
   operación se rechaza y esa categoría queda con el mismo nombre y el mismo tipo. *(AC-03)*
3. **Given** una categoría propia de **otra** cuenta, **When** el usuario intenta modificarla por su
   identificador, **Then** recibe el mismo código y el mismo cuerpo que ante un identificador
   inexistente, y esa categoría queda sin cambios. *(AC-11)*
4. **Given** una categoría propia, **When** el usuario la renombra con el nombre vacío o de más de
   50 caracteres, **Then** la operación se rechaza indicando el motivo y la categoría queda sin
   cambios. *(AC-10)*
5. **Given** una cuenta que ya tiene disponible una categoría activa de gasto llamada "Comida" —sea
   propia o predefinida—, **When** intenta renombrar otra propia suya a "Comida", **Then** el
   renombre se rechaza indicando el motivo y la categoría queda con su nombre anterior. *(FR-005)*

---

### User Story 3 - Dejar de usar una categoría sin borrar el pasado (Priority: P3)

Dejé el gimnasio. No quiero seguir viendo "Gimnasio" en el selector cada vez que cargo un gasto,
pero tampoco quiero que los seis meses que pagué desaparezcan del resumen.

**Why this priority**: es la que más cuidado necesita y la que menos urge — se puede vivir con un
selector largo—. Va última porque toda su dificultad está en **no** romper lo que las otras dos ya
dejaron andando.

**Independent Test**: se prueba dando de baja una categoría con movimientos y comprobando dos cosas
opuestas a la vez: que desapareció del selector, y que ni un número del resumen se movió.

**Acceptance Scenarios**:

1. **Given** una categoría propia con movimientos asociados, **When** el usuario la elimina,
   **Then** deja de ofrecerse en el formulario de registro y en el filtro por categoría, y su nombre
   sigue apareciendo en esos movimientos del listado. *(AC-05)*
2. **Given** el resumen de un período que incluye movimientos de una categoría propia, **When** el
   usuario elimina esa categoría, **Then** el total gastado, el total ingresado, el balance y el
   monto de esa categoría en el desglose quedan **exactamente** con los mismos valores que antes.
   *(AC-06, y la deuda D6-04 de la feature 006)*
3. **Given** una categoría propia dada de baja, **When** el usuario crea una nueva con el mismo
   nombre y el mismo tipo, **Then** la creación se acepta, la nueva se ofrece en el selector, y la
   dada de baja sigue nombrando los movimientos que la usan. *(AC-09)*
4. **Given** una categoría **predefinida**, **When** el usuario intenta eliminarla, **Then** la
   operación se rechaza y esa categoría sigue disponible. *(AC-03)*
5. **Given** un movimiento registrado con una categoría que después se dio de baja, **When** el
   usuario edita ese movimiento sin cambiarle la categoría, **Then** la edición se acepta. *(FR-023)*
5b. **Given** dos categorías dadas de baja y un movimiento que usa una de ellas, **When** el usuario
   intenta moverlo a la otra, **Then** la edición se rechaza. *(FR-023)*
6. **Given** una categoría propia de otra cuenta, **When** el usuario intenta registrar un
   movimiento con ese identificador, **Then** la operación se rechaza sin confirmar si esa categoría
   existe. *(FR-021)*
7. **Given** una categoría propia de **otra** cuenta, **When** el usuario intenta eliminarla por su
   identificador, **Then** recibe el mismo código y el mismo cuerpo que ante un identificador
   inexistente, y esa categoría queda sin cambios. *(AC-11)*

---

### User Story 4 - Un solo catálogo en pantalla (Priority: P4)

Creo una categoría y quiero usarla enseguida, sin recargar la página y sin que un control me la
muestre y otro no.

**Why this priority**: es la historia que vuelve usable a las tres anteriores desde la aplicación.
Va última en prioridad pero **no** es opcional: sin ella, las otras tres sólo son observables con un
cliente HTTP.

**Independent Test**: se prueba cargando la pantalla y contando las peticiones al catálogo, y
después creando y renombrando una categoría y comprobando que los controles que lo usan lo reflejan
sin recargar.

**Acceptance Scenarios**:

1. **Given** la pantalla principal, **When** se carga, **Then** el catálogo de categorías se
   solicita a lo sumo **una** vez. *(AC-12)*
2. **Given** la pantalla principal cargada, **When** el usuario crea o renombra una categoría
   propia, **Then** el cambio se refleja en todos los controles que usan el catálogo, sin recargar
   la página. *(AC-13)*
3. **Given** el usuario en la pantalla de gestión, **When** crea una categoría y vuelve a la
   pantalla principal, **Then** la categoría nueva está en el selector sin que la aplicación se
   haya recargado ni haya vuelto a pedir el catálogo entero. *(FR-019)*

---

### Edge Cases

- **¿Qué pasa si se da de baja una categoría que está seleccionada en el formulario?** Deja de
  ofrecerse en el selector (FR-010). Si quedó elegida en el formulario de **alta**, guardar falla
  (FR-022), así que el formulario tiene que sacarla de la selección al refrescarse el catálogo en
  vez de dejar que la persona choque contra un error que no puede entender. En la **edición** de un
  movimiento que ya la tenía, en cambio, se conserva y guardar funciona (FR-023).
- **¿Y si se le da de baja dos veces a la misma categoría?** La segunda no es un error nuevo: el
  estado final es el mismo y la respuesta tiene que ser la misma que la primera.
- **¿Qué pasa con un nombre que sólo difiere en mayúsculas o en espacios al borde?** "comida" y
  "Comida " tienen que contar como el mismo nombre para FR-005, o la unicidad no sirve de nada.
- **¿Y si dos pedidos crean la misma categoría a la vez?** La comprobación de FR-005 no puede ser
  sólo una consulta previa: entre la consulta y el alta hay una ventana.
- **¿Puede una cuenta quedarse sin ninguna categoría de un tipo?** No: las predefinidas no se pueden
  dar de baja, así que siempre queda el piso compartido.
- **¿Qué ve la cuenta B cuando pide por identificador una categoría propia de A?** Lo mismo que ante
  un identificador que no existe. Cualquier diferencia —otro código, otro mensaje, otro tiempo—
  confirma que la fila existe.

---

## Requirements *(mandatory)*

### Functional Requirements

**El catálogo de una cuenta**

- **FR-001**: El sistema DEBE permitir crear una categoría propia indicando nombre y tipo, gasto o
  ingreso. *(FR-01 del PRD, RF-07)*
- **FR-002**: El sistema DEBE ofrecerle a cada cuenta las categorías predefinidas del tipo
  correspondiente **más** sus propias categorías activas de ese tipo, y **ninguna** categoría propia
  de otra cuenta. *(FR-02 del PRD, RF-06, RF-07)*
- **FR-003**: El sistema DEBE permitir modificar el nombre de una categoría propia, conservando su
  tipo. *(FR-03 del PRD, RF-08)*
- **FR-004**: El sistema DEBE eliminar una categoría propia mediante **baja lógica**, conservando la
  fila y su nombre. *(FR-04 del PRD, RF-09)*

**Las reglas del nombre**

- **FR-005**: El sistema DEBE rechazar **tanto la creación como el renombre** de una categoría
  propia cuyo nombre y tipo coincidan con los de otra categoría **activa disponible para esa misma
  cuenta**, sea propia o **predefinida**, indicando el motivo. Es una sola regla y no dos: si el
  renombre no la aplicara, se esquivaría en dos pasos —crear con un nombre libre y renombrar al
  ocupado— y el selector terminaría con dos entradas que la persona no puede distinguir. *(FR-07 del
  PRD, ampliado al renombre en la sesión de clarificación)*
- **FR-006**: El sistema DEBE rechazar la creación y la modificación de una categoría propia cuyo
  nombre esté vacío o supere los **50** caracteres, indicando el motivo. *(FR-08 del PRD, con el
  límite corregido al real de la columna)*
- **FR-007**: El sistema DEBE comparar nombres para FR-005 ignorando las diferencias de mayúsculas y
  los espacios al principio y al final. *(decisión de esta spec; sin ella la unicidad se esquiva
  escribiendo "comida" en vez de "Comida")*

**Lo que no se toca**

- **FR-008**: El sistema DEBE rechazar la modificación y la eliminación de una categoría
  predefinida, dejándola sin cambios. *(FR-06 del PRD, RF-06)*
- **FR-009**: El sistema DEBE aceptar la creación de una categoría propia cuyo nombre y tipo
  coincidan con los de una categoría **dada de baja** de esa misma cuenta. *(AC-09 del PRD)*
- **FR-010**: El sistema DEBE dejar de ofrecer una categoría dada de baja en el formulario de
  registro y en el filtro por categoría, y DEBE seguir mostrando su nombre en los movimientos ya
  registrados con ella. *(FR-05 del PRD, RF-09)*
- **FR-011**: El sistema NO DEBE alterar el total ingresado, el total gastado, el balance ni el
  monto de ninguna categoría del desglose ante la baja lógica de una categoría con movimientos. La
  lectura que alimenta el resumen NO DEBE filtrar por el estado de baja. *(NFR-03 del PRD, AC-14 del
  PRD del producto, y la deuda **D6-04** de la feature 006)*

**Aislamiento**

- **FR-012**: El sistema DEBE acotar la creación, la modificación, la eliminación y la consulta de
  categorías propias a la cuenta de la sesión. *(NFR-01 del PRD, RF-04)*
- **FR-013**: Ante una categoría propia de otra cuenta pedida por identificador, el sistema DEBE
  responder con el mismo código y el mismo cuerpo que ante un identificador inexistente. *(AC-11 del
  PRD)*
- **FR-014**: La suite DEBE cubrir con al menos un caso de acceso cruzado entre dos cuentas el
  **100 %** de los endpoints de categorías. *(NFR-01 del PRD)*
- **FR-021**: El alta y la edición de un movimiento DEBEN rechazar una categoría que **no pertenece
  al ámbito de esa cuenta** —ni predefinida, ni propia suya—, sin confirmar si existe. Hasta esta
  feature no hacía falta: las diez categorías eran globales y cualquier identificador válido servía.
  En cuanto hay categorías propias, `categoriaId` pasa a ser el identificador de algo que puede ser
  de otro. *(consecuencia de FR-012, detectada en la sesión de clarificación)*
- **FR-022**: El **alta** de un movimiento DEBE seguir rechazando una categoría dada de baja: si no,
  la baja lógica sería puramente cosmética y se podrían seguir clasificando movimientos nuevos en
  una categoría archivada. *(comportamiento ya existente desde FEAT-001b; esta feature lo conserva)*
- **FR-023**: La **edición** de un movimiento DEBE aceptar una categoría dada de baja **cuando es la
  que ese movimiento ya tenía**. Cambiarle el monto o la fecha a un movimiento viejo no puede
  obligar a reclasificarlo: eso reescribiría la historia que la baja lógica existe para preservar.
  Cambiarlo a **otra** categoría dada de baja se DEBE rechazar, por el mismo motivo que FR-022.
  *(decisión de la sesión de clarificación)*

**La pantalla**

- **FR-015**: La aplicación DEBE solicitar el catálogo de categorías **a lo sumo una vez** por carga
  de la pantalla principal. *(NFR-02 del PRD; hoy ya se cumple y acá se vuelve una regresión a no
  romper)*
- **FR-016**: La aplicación DEBE reflejar el resultado de crear, renombrar o dar de baja una
  categoría en **todos** los controles que usan el catálogo, sin recargar la página, **incluso
  después de volver desde la pantalla de gestión**. *(NFR-02 del PRD, AC-13)*
- **FR-017**: La aplicación DEBE ofrecer la gestión de categorías propias —crear, renombrar y dar de
  baja— en una **pantalla aparte**, y no en el camino rápido de carga de un movimiento. *(alcance
  decidido en esta spec; el PRD lo pide implícitamente en AC-01, AC-03 y AC-05, que sin pantalla no
  son observables)*
- **FR-018**: La navegación a esa pantalla DEBE usar el mismo mecanismo de estado con el que la
  aplicación ya alterna entre el login y la pantalla de movimientos. **No se agrega ninguna
  dependencia nueva** para esto. *(clarificación de esta sesión, y la regla de `AGENTS.md`: no se
  agregan librerías sin justificarlas en la spec)*
- **FR-019**: Al volver de la pantalla de gestión a la principal, la aplicación NO DEBE volver a
  pedir el catálogo entero ni recargar la página para mostrar los cambios. FR-015 sigue valiendo: a
  lo sumo una petición del catálogo por carga de la pantalla principal. *(consecuencia de la
  clarificación: con la gestión en otra pantalla, el catálogo tiene que sobrevivir a la navegación)*

**Contrato**

- **FR-020**: Todo cambio en la forma de una petición o una respuesta DEBE quedar reflejado en la
  definición del contrato que el frontend declara, en el mismo movimiento. La verificación del
  contrato ya existente tiene que seguir en verde.

### Key Entities

- **Categoría**: el nombre con el que se clasifica un movimiento. Tiene nombre, tipo (gasto o
  ingreso), **ámbito** —predefinida del sistema, o propia de una cuenta— y **estado** —activa o dada
  de baja—. Su identidad no cambia nunca: renombrarla no la convierte en otra, y por eso los
  movimientos siguen apuntando a la misma.
- **Categoría predefinida**: las diez que vienen sembradas, sin dueño y compartidas por todas las
  cuentas. Son de solo lectura: no se renombran, no se dan de baja y no cambian de tipo.
- **Categoría propia**: la que crea una cuenta. Sólo esa cuenta la ve y sólo esa cuenta la toca.
- **Baja lógica**: el estado que saca a una categoría de los selectores **sin** sacarla de la
  historia. Es un camino de ida: no hay forma de reactivar una categoría dada de baja.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona puede crear una categoría propia, volver a la pantalla principal y usarla
  para registrar un movimiento **sin recargar la aplicación**.
- **SC-002**: Dos cuentas pueden tener cada una una categoría con el mismo nombre y tipo, y ninguna
  de las dos puede ver, nombrar ni tocar la de la otra por ningún camino.
- **SC-003**: Ante la baja lógica de una categoría con movimientos, **ningún** número del resumen
  cambia: ni los totales, ni el balance, ni el monto de esa categoría en el desglose.
- **SC-004**: Una categoría dada de baja no aparece en ningún selector ni filtro, y sigue apareciendo
  con su nombre en todos los movimientos que la usan.
- **SC-005**: Las diez categorías predefinidas siguen existiendo con el mismo identificador, el mismo
  nombre y el mismo tipo al terminar la feature, y ninguna cuenta puede cambiarlas.
- **SC-006**: La pantalla principal pide el catálogo **una** vez, y crear o renombrar una categoría
  se ve en todos los controles que lo usan sin recargar, también al volver desde la pantalla de
  gestión.
- **SC-007**: El 100 % de los endpoints de categorías tiene al menos un test de acceso cruzado entre
  dos cuentas.
- **SC-009**: Ninguna cuenta puede registrar ni editar un movimiento apuntando a una categoría de
  otra, por ningún camino, y el rechazo no revela si esa categoría existe.
- **SC-008**: El comportamiento del listado, del alta y del resumen no cambia para una cuenta que no
  usa categorías propias: esta feature no toca lo que ya se ve.

---

## Assumptions

- **El PRD manda, salvo donde el código lo desmiente.** Las tres diferencias están en la tabla de
  arriba, con lo que se hace en cada una.
- **El alcance incluye la pantalla de gestión de categorías** —crear, renombrar, dar de baja— porque
  sin ella AC-01, AC-03 y AC-05 no son observables desde la aplicación. **No** incluye el filtro por
  categoría ni la pantalla de edición de movimientos: son deuda de UI de la feature 005 y quedan
  registradas abajo.
- **El límite del nombre es 50 caracteres**, el de la columna. Ver la tabla de reconciliación.
- **No hay límite a la cantidad de categorías propias** de una cuenta, como fija el PRD.
- **La baja lógica es de ida.** AC-09 fija que el camino de vuelta es crear una nueva con el mismo
  nombre, no reactivar la vieja.
- **Las predefinidas se identifican por no tener dueño**, que es como ya están sembradas. No hace
  falta una marca nueva para distinguirlas.
- **La pantalla de gestión no tiene URL propia.** Se llega por estado, así que recargar el navegador
  vuelve a la pantalla principal. Nadie pidió poder compartir el link de la gestión de categorías, y
  darle URL costaba un enrutador. Si algún día hace falta, es un cambio acotado a `App.tsx`.
- **El tipo de una categoría no se puede cambiar** una vez creada: FR-003 habla del nombre y nada
  más. Cambiarlo movería de tipo a los movimientos que la usan, que es una reescritura de la
  historia por la puerta de atrás.

---

## Deuda registrada

Lo que esta feature **no** va a dejar hecho, con el ticket que lo cubre. Se hereda la forma de la
tabla de las features 004 y 006.

| # | Qué queda | Por qué no acá | Quién lo cubre |
|---|---|---|---|
| D7-01 | **El filtro por categoría en la pantalla.** El backend lo acepta desde la feature 005 y el frontend no lo ofrece | Es deuda de UI de 005, no de este ticket. Acá se cita en AC-05 y FR-010 como el segundo lugar que no debe ofrecer una categoría dada de baja: el día que exista, tiene que nacer cumpliéndolo | Ticket 6 (Maquetación) |
| D7-02 | **La pantalla de edición de un movimiento**, que también consume el catálogo | Igual que D7-01: la edición existe en el backend desde 005 y en la pantalla no | Ticket 6 |
| D7-03 | **Reasignar los movimientos de una categoría dada de baja** a otra | Fuera de alcance explícito del PRD: la baja lógica existe precisamente para no tocarlos | Nadie |
| D7-04 | **Restaurar una categoría dada de baja** | Fuera de alcance explícito del PRD; AC-09 fija que el camino es crear una nueva | Nadie |

---

## Dependencies

- **Lo que ya está y se reusa, no se reescribe**: las columnas `usuario_id` y `activa` de
  `categorias`, el índice `ux_categoria_ambito_nombre_tipo`, las diez predefinidas sembradas con ids
  fijos, `GET /api/categorias`, y el canal único de lectura de movimientos con su barrera. Todo eso
  salió de las features 001 a 006.
- **Lo que este ticket sí obliga a tocar en el esquema**: la unicidad tiene que dejar de chocar
  contra una fila dada de baja (FR-009). Es la única migración que la feature necesita, y el PRD no
  la anticipó porque creía que el índice todavía era global.
- **Lo que este ticket destraba**: el ticket 5 (Dashboard) grafica el desglose por categoría, y
  hasta acá esas categorías eran diez fijas para todo el mundo.
- **Lo que este ticket puede romper si se hace mal**: el resumen de la feature 006. FR-011 es la
  guarda, y su test es la traducción directa de la deuda D6-04.

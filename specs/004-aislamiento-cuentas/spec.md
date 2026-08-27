# Feature Specification: Aislamiento entre cuentas verificado

**Feature Branch**: `004-aislamiento-cuentas`

**Created**: 2026-08-27

**Status**: Draft

**Input**: Ticket DISC-001-01c. PRD de referencia:
[`plan-de-implementacion/prds/pendientes/prd-DISC-001-01c.md`](../../plan-de-implementacion/prds/pendientes/prd-DISC-001-01c.md)
(FR-01..FR-04, NFR-01..NFR-02, AC-01..AC-10).

---

## Por qué esta feature

`002-identidad-sesion` hizo que existieran cuentas de verdad, y con eso hizo observable algo que
hasta entonces no se podía ni mirar: que cada cuenta acceda **únicamente** a sus propios datos
(RF-04 del PRD del producto). Con un solo usuario semilla, un test de aislamiento no puede fallar
—no hay nadie de quien aislarse—, y por eso `001` lo dejó fuera de forma explícita.

Hoy el aislamiento existe en el código. Nadie lo comprobó nunca con dos cuentas reales. Ésa es toda
la feature: convertir una propiedad que hoy se sostiene por convención en una propiedad verificada,
y ponerle una barrera para que dejar de cumplirla haga ruido.

### Dos correcciones al PRD que este repositorio impone

Quedan escritas acá porque cambian **el alcance**, no sólo la forma de verificarlo. Las dos se
verificaron contra el código antes de escribir esta spec.

**1. La superficie son dos endpoints, no seis.** El PRD nombra
`POST /api/movimientos`, `GET /api/movimientos`, `GET /api/movimientos/{id}`,
`PUT /api/movimientos/{id}`, `DELETE /api/movimientos/{id}` y `GET /api/resumen`. En este
repositorio existen **los dos primeros**. La lectura individual, la modificación y la eliminación
son FEAT-001b, y el resumen es FEAT-001c: ninguno de los dos tickets se implementó acá, aunque la
tabla de `plan-de-implementacion/README.md` los liste como mergeados. El PRD de `01c` se escribió
contra ese README.

Esta feature cubre el **100 % de la superficie que existe**. Los cuatro endpoints que faltan quedan
registrados en *Deuda registrada* con el AC del PRD que cada uno arrastra, para que el ticket que
los cree los cubra al nacer y no haya que volver a pasar por acá.

**2. No hay filtro global de consulta.** El PRD explica que el aislamiento "llega heredado" del
`HasQueryFilter` sobre `Movimiento`. No existe ninguno: el acotado por cuenta es una condición
escrita a mano en la consulta del listado. Eso cambia dos cosas. La primera es que **nada llega
heredado**: el aislamiento de la lectura es tan convencional como el de la escritura, y depende de
que alguien se acuerde de escribir la condición. La segunda es que AC-10 —"si una consulta se
ejecuta con los filtros de consulta desactivados, la suite debe fallar"— se queda sin sujeto: no hay
filtro global que desactivar.

AC-10 se traduce a lo que sí existe, conservando su intención: que **desarmar el aislamiento sea
ruidoso**. La barrera vigila la condición que hoy hace el trabajo, en lugar de un mecanismo que este
repositorio no usa.

### Lo que NO entra

Es la lista de *Out of Scope* del PRD, más lo que las dos correcciones de arriba mueven de lugar:

- **Crear los cuatro endpoints que faltan.** Son FEAT-001b y FEAT-001c. Esta feature verifica lo que
  hay; no agrega superficie para después verificarla.
- **Aislamiento de las categorías por cuenta.** Es el ticket 3. Hoy el catálogo es global.
- **Compartir movimientos entre cuentas**, cuentas conjuntas o visibilidad parcial.
- **Roles y permisos.** Todas las cuentas son iguales y ninguna ve datos de otra.
- **Registro de auditoría de los intentos de acceso cruzado.**
- **Cifrado en reposo** o cualquier separación física por cuenta (una base o un esquema por
  usuario).
- **Alta, inicio de sesión, sesión y límite de intentos**: son `01a` y `01b`, de los que esta
  feature depende.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Nadie ve el dinero de otro (Priority: P1)

Dos personas usan la aplicación con sus propias cuentas. Cada una registra sus gastos e ingresos, y
cuando abre su listado ve los suyos: ni uno solo de la otra, ni siquiera un total que los incluya.

**Why this priority**: es la promesa central del producto y la razón de que existan cuentas. Sin
esto, la aplicación no es multiusuario: es una libreta compartida en la que cada quien ve el dinero
de todos. Entregada sola ya convierte el aislamiento de la lectura en algo comprobado.

**Independent Test**: crear dos cuentas, registrar movimientos con cada una, y comprobar que el
listado de cada cuenta devuelve exactamente los propios y ninguno de la otra.

**Acceptance Scenarios**:

1. **Given** dos cuentas con movimientos propios en el mes en curso, **When** una de ellas abre su
   listado, **Then** recibe únicamente sus movimientos y ninguno de la otra (AC-01).
2. **Given** dos cuentas con movimientos propios, **When** una de ellas abre su listado, **Then**
   ningún identificador de movimiento de la otra cuenta aparece en la respuesta, aunque sus fechas
   caigan en el mismo mes y sus categorías sean las mismas.
3. **Given** una cuenta sin ningún movimiento propio y otra con varios en el mes en curso, **When**
   la primera abre su listado, **Then** recibe una lista vacía y no los de la otra.

---

### User Story 2 - Lo que registro queda a mi nombre (Priority: P2)

Lo que una cuenta registra le pertenece a esa cuenta, y a ninguna otra. Da igual lo que traiga la
petición: el dueño lo decide la sesión.

**Why this priority**: es el hueco que la historia 1 no cubre. Acotar la lectura no sirve de nada si
al escribir se puede poner el nombre de otro: bastaría con registrar un movimiento a nombre ajeno
para ensuciar el listado de otra persona. Va después de la historia 1 porque para comprobar dónde
cayó lo que se escribió hay que poder leerlo aislado.

**Independent Test**: registrar un movimiento desde una cuenta indicando a otra como propietaria en
el cuerpo de la petición, y comprobar que aparece en el listado de quien lo registró y no en el de
la otra.

**Acceptance Scenarios**:

1. **Given** dos cuentas, **When** una registra un movimiento indicando a la otra como propietaria
   en el cuerpo de la petición, **Then** el movimiento queda a nombre de quien lo registró y el
   listado de la otra cuenta no cambia (AC-06).
2. **Given** dos cuentas con movimientos propios, **When** una de ellas registra un movimiento
   nuevo, **Then** los movimientos de la otra cuenta conservan exactamente los mismos valores que
   antes de la operación (AC-08, su mitad de alta).
3. **Given** una cuenta con sesión iniciada, **When** registra un movimiento sin indicar ningún
   propietario, **Then** el movimiento queda a su nombre.

---

### User Story 3 - Desarmar el aislamiento hace ruido (Priority: P3)

Quien toque la consulta del listado y le saque el acotado por cuenta se entera en el momento,
porque la suite se pone en rojo. No se entera la persona a la que se le mezclaron los gastos con
los de un desconocido.

**Why this priority**: es lo que hace que este ticket siga valiendo dentro de seis meses. Las
historias 1 y 2 comprueban el estado de hoy; ésta protege el de mañana. Va última porque su barrera
vigila exactamente la condición que las dos primeras historias dejan verificada: sin ellas no hay
nada cuyo desarme detectar.

**Independent Test**: quitarle a la consulta del listado el acotado por cuenta y comprobar que la
suite se pone en rojo; devolverlo y comprobar que vuelve al verde.

**Acceptance Scenarios**:

1. **Given** la consulta del listado con su acotado por cuenta puesto, **When** se corre la suite,
   **Then** está en verde.
2. **Given** la consulta del listado **sin** su acotado por cuenta, **When** se corre la suite,
   **Then** está en rojo (AC-10, reformulado).
3. **Given** el alta de movimientos, **When** deja de tomar el propietario de la sesión, **Then** la
   suite está en rojo.

---

### Edge Cases

- **Las dos cuentas del escenario terminan siendo la misma.** Es la forma más fácil de que un test
  de aislamiento pase sin probar nada: si el fixture reusa una cuenta, todo da verde. Cada escenario
  cruzado tiene que comprobar el estado de la **otra** cuenta, no sólo el resultado de la propia.
- **La segunda cuenta no tiene movimientos.** Un listado ajeno vacío hace pasar cualquier
  comparación. Las dos cuentas de los escenarios cruzados tienen movimientos propios en el mismo
  mes.
- **Los movimientos de las dos cuentas comparten fecha y categoría.** Es el caso que distingue un
  aislamiento real de uno que funciona porque los datos de prueba no se parecían.
- **Identificadores contiguos.** Los identificadores de movimiento son correlativos entre cuentas:
  la cuenta B puede nombrar sin adivinar nada el movimiento que la cuenta A acaba de crear. Importa
  cuando existan los endpoints por identificador; hoy queda registrado como el motivo de que esa
  deuda no sea teórica.
- **Una petición sin sesión.** No es un caso de aislamiento: la autorización global la rechaza antes
  y su barrera propia ya lo verifica. No se duplica acá.
- **Un movimiento con una categoría ajena.** El alta ya busca la categoría acotada a las
  predefinidas del sistema y las propias de la cuenta. Hoy todas son predefinidas, así que no hay
  nada que aislar; queda registrado como cubierto de antemano para el ticket 3.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** *(PRD FR-01)*: El sistema MUST acotar toda lectura de movimientos a aquellos cuyo
  propietario es la cuenta de la sesión.
- **FR-002** *(PRD FR-03)*: El sistema MUST asignar como propietario de todo movimiento creado a la
  cuenta de la sesión, descartando cualquier propietario que venga indicado en el cuerpo de la
  petición.
- **FR-003** *(PRD NFR-02, reescalado)*: La suite MUST cubrir con al menos un caso de acceso cruzado
  entre dos cuentas **cada uno de los endpoints de movimientos que existen** —el alta y el
  listado—, es decir el 100 % de esa superficie.
- **FR-004** *(PRD AC-10, reformulado)*: El sistema MUST contar con una barrera que se ponga en rojo
  cuando la consulta del listado deja de acotar por cuenta, de modo que desarmar el aislamiento no
  pueda pasar inadvertido.
- **FR-005** *(PRD AC-08)*: El sistema MUST dejar los movimientos de las demás cuentas con los
  mismos valores que antes de cualquier operación que una cuenta realice sobre los suyos.

### Key Entities

- **Movimiento**: el gasto o ingreso registrado. Ya tiene una cuenta propietaria; lo que esta
  feature agrega no es el campo, es la comprobación de que ese campo se respeta en las dos
  direcciones —al leer y al escribir—.
- **Cuenta**: quien registra y consulta. Dos cuentas distintas no comparten ningún movimiento, y
  ninguna operación de una altera lo que la otra ve.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ninguna cuenta obtiene, en ninguna respuesta, un movimiento cuyo propietario es otra
  cuenta. Verificable recorriendo cada endpoint de movimientos con dos cuentas que tienen datos
  propios en el mismo mes.
- **SC-002**: El 100 % de los endpoints de movimientos que existen —2 de 2— tiene al menos un caso
  de acceso cruzado entre dos cuentas. Es un conteo, no una impresión.
- **SC-003**: Quitarle a la consulta del listado el acotado por cuenta pone la suite en rojo, y
  devolverlo la pone en verde. Verificable ejecutando las dos variantes.
- **SC-004**: Después de cualquier operación de una cuenta sobre sus propios movimientos, los
  movimientos de la otra cuenta conservan los mismos valores. Se comprueba sobre la otra cuenta, no
  sobre la que operó.

## Deuda registrada

Los criterios del PRD que esta feature **no** puede verificar porque su endpoint no existe todavía.
No se dan por cumplidos ni se descartan: quedan acá con el ticket que los va a poder cubrir, para
que ese ticket los cubra al nacer.

| AC del PRD | Qué exige | Endpoint que falta | Ticket |
|---|---|---|---|
| AC-02 | El resumen se calcula sólo sobre los movimientos propios | `GET /api/resumen` | FEAT-001c |
| AC-03 | Consultar un movimiento ajeno responde como uno inexistente | `GET /api/movimientos/{id}` | FEAT-001b |
| AC-04 | Modificar un movimiento ajeno lo deja sin cambios y responde como uno inexistente | `PUT /api/movimientos/{id}` | FEAT-001b |
| AC-05 | Eliminar un movimiento ajeno lo deja en la base y responde como uno inexistente | `DELETE /api/movimientos/{id}` | FEAT-001b |
| AC-07 | Modificar un movimiento propio conserva su propietario original | `PUT /api/movimientos/{id}` | FEAT-001b |

NFR-01 del PRD —que la respuesta ante un dato ajeno sea indistinguible de la de un identificador
inexistente— queda entero en esta tabla: los tres endpoints que responden por identificador son los
que faltan. El listado no puede filtrar existencia porque no nombra identificadores ajenos; nunca
recibe uno.

## Assumptions

- **La superficie a cubrir es la que existe hoy**, y no la que el PRD describe. Verificado sobre el
  código: las rutas registradas son `POST /api/movimientos` y `GET /api/movimientos`, y el cliente
  del frontend no consume ninguna otra de movimientos.
- **No hay filtro global de consulta.** Verificado: no existe ningún `HasQueryFilter` en el
  backend. El acotado por cuenta es una condición escrita en la consulta del listado.
- **Esta feature no agrega endpoints ni cambia el contrato con el frontend.** Es verificación y
  barrera. Si algún escenario obligara a cambiar una respuesta, deja de ser esta feature.
- **La barrera de FR-004 sigue el patrón que el repositorio ya usa** para el contrato, la
  autorización y el linter: una verificación que se prueba a sí misma rompiendo a propósito lo que
  protege y comprobando el rojo (Principio V de la constitución).
- **Las cuentas de prueba se crean por la API**, como en `003`, y no sembrando filas: es lo que hace
  que las dos cuentas sean realmente dos y no una reusada.
- **Depende de `002-identidad-sesion`**, mergeado en `main`. Sin sesiones reales no hay dos cuentas
  entre las cuales aislar y estos criterios vuelven a no ser observables.

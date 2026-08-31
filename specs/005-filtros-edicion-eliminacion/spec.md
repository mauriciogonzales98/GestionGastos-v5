# Feature Specification: Filtros del listado, edición y eliminación

**Feature Branch**: `005-filtros-edicion-eliminacion`

**Created**: 2026-08-31

**Status**: Draft

**Input**: FEAT-001b del plan de implementación, reconstruido desde `PRD.md` y desde la tabla de
*Deuda registrada* de [`specs/004-aislamiento-cuentas/spec.md`](../004-aislamiento-cuentas/spec.md).

---

## De dónde sale esta spec

**El PRD de este ticket no existe en este repositorio.** `plan-de-implementacion/prds/` sólo trae
dos PRD de ticket; los de FEAT-001a/b/c nunca entraron. Eso está documentado en el README de esa
carpeta desde el PR #19. Así que el alcance se reconstruye de dos fuentes que sí están:

1. **`PRD.md`**, el PRD del producto: RF-14, RF-15, RF-17 y RF-18, con sus criterios AC-19, AC-20,
   AC-21, AC-23, AC-24, AC-25 y AC-26.
2. **La tabla de *Deuda registrada* de la spec de 004**, que dejó cuatro criterios de aislamiento
   anotados esperando exactamente los endpoints que este ticket crea.

Que la reconstrucción sea explícita importa: si mañana aparece el PRD original y dice otra cosa,
esta sección es el lugar donde se ve qué se supuso y por qué.

---

## Lo que hace distinta a esta feature

Es la primera que **agrega superficie a la que el aislamiento tiene que aplicarse desde el primer
día**. Hasta ahora había 2 endpoints de movimientos y la feature 004 los verificó los dos. Esta
suma 3 más y toca el que ya estaba.

La diferencia con 004 es de dirección. Allá se verificó una propiedad heredada: el aislamiento ya
funcionaba y había que demostrarlo. Acá se escribe código nuevo que **puede nacer sin aislamiento**,
y la barrera que 004 dejó en pie es la que tiene que atraparlo. Los cuatro criterios de la deuda no
son un extra de esta feature: son parte de su definición de terminado.

Dicho de otro modo: 004 preguntó *"¿esto está aislado?"*. Esta pregunta *"¿lo que estoy por escribir
nace aislado?"*, y la respuesta tiene que venir de un test que falle si no.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Corregir lo que cargué mal (Priority: P1) 🎯 MVP

Cargué un gasto de 1.500 y eran 15.000. Hoy no tengo forma de arreglarlo: el movimiento se crea y no
cambia más. La única salida sería borrarlo, y tampoco puedo.

Quiero abrir un movimiento propio, corregir su monto, su categoría o su fecha, y ver el listado
reflejando lo nuevo.

**Why this priority**: es el hueco más caro de los tres. Un dato mal cargado que no se puede
corregir contamina todo lo que se construya encima —el resumen de FEAT-001c, el dashboard del
ticket 5— y no hay forma de que la persona lo repare por su cuenta. Los filtros, en cambio, son una
comodidad sobre datos que ya se ven.

**Independent Test**: registrar un movimiento, modificarlo, y comprobar que el listado devuelve los
valores nuevos y no los viejos. Se puede entregar solo: sin filtros ni borrado, la app ya deja de
tener datos irreparables.

**Acceptance Scenarios**:

1. **AC-01** — **Given** un movimiento propio de 1.500, **When** su dueño lo modifica a 15.000,
   **Then** el listado lo muestra en 15.000 y no queda ningún rastro del valor anterior.
   *(PRD AC-19, mitad del listado)*
2. **AC-02** — **Given** un movimiento propio en una categoría y una fecha, **When** su dueño le
   cambia las dos, **Then** el listado lo muestra con la categoría nueva, y aparece o no según si
   su fecha nueva cae dentro del período consultado. *(PRD AC-20, mitad del listado)*
3. **AC-03** — **Given** un movimiento propio, **When** su dueño lo consulta por su identificador,
   **Then** recibe ese movimiento con la misma forma con que lo devuelve el listado.
4. **AC-04** — **Given** un movimiento propio, **When** su dueño lo modifica, **Then** el
   movimiento **conserva su propietario original**. *(Deuda de 004: AC-07 del PRD)*
5. **AC-05** — **Given** un movimiento de otra cuenta, **When** alguien lo consulta por su
   identificador, **Then** la respuesta es indistinguible de la de un movimiento que no existe.
   *(Deuda de 004: AC-03)*
6. **AC-06** — **Given** un movimiento de otra cuenta, **When** alguien intenta modificarlo,
   **Then** la respuesta es indistinguible de la de un movimiento que no existe **y el movimiento
   ajeno queda sin ningún cambio**. *(Deuda de 004: AC-04)*
7. **AC-07** — **Given** una modificación con datos inválidos, **When** se envía, **Then** se
   rechaza con los errores por campo, con la misma forma que ya usa el alta, y el movimiento queda
   sin cambios.

---

### User Story 2 - Borrar lo que no va (Priority: P2)

Cargué dos veces el mismo gasto. Quiero eliminar el que sobra y que deje de contar.

**Why this priority**: cierra el par con la edición y completa el ciclo de vida del movimiento, pero
es menos urgente: un duplicado se puede convivir, un monto mal cargado no.

**Independent Test**: registrar dos movimientos, eliminar uno, y comprobar que el listado devuelve
sólo el otro y que el eliminado no vuelve.

**Acceptance Scenarios**:

1. **AC-08** — **Given** un movimiento propio, **When** su dueño lo elimina, **Then** deja de
   aparecer en el listado. *(PRD AC-21, mitad del listado)*
2. **AC-09** — **Given** un movimiento de otra cuenta, **When** alguien intenta eliminarlo,
   **Then** la respuesta es indistinguible de la de un movimiento que no existe **y el movimiento
   ajeno sigue apareciendo en el listado de su dueño**. *(Deuda de 004: AC-05)*
3. **AC-10** — **Given** un movimiento que ya fue eliminado, **When** su dueño lo elimina otra vez,
   **Then** la respuesta es la de un movimiento que no existe, y no se produce ningún error
   inesperado.

---

### User Story 3 - Encontrar lo que busco (Priority: P3)

Tengo movimientos de varios meses y quiero ver los de marzo, o sólo los de "Supermercado".

**Why this priority**: es comodidad sobre datos que ya se ven. Sin ella la app sirve; sin US1 la app
acumula errores que nadie puede arreglar.

**Independent Test**: cargar movimientos en dos categorías y dos meses, y comprobar que cada
combinación de filtros devuelve exactamente el subconjunto que corresponde.

**Acceptance Scenarios**:

1. **AC-11** — **Given** movimientos en varias categorías, **When** se consulta el listado
   filtrando por una, **Then** devuelve únicamente los de esa categoría. *(PRD AC-23)*
2. **AC-12** — **Given** movimientos en varias categorías, **When** se consulta el listado sin
   filtro de categoría, **Then** devuelve los de todas. *(PRD AC-24)*
3. **AC-13** — **Given** movimientos en el mes en curso y en otros meses, **When** se consulta el
   listado sin filtro de fechas, **Then** devuelve únicamente los del mes en curso. *(PRD AC-25)*
4. **AC-14** — **Given** un rango de fechas, **When** se consulta el listado con él, **Then**
   devuelve únicamente los movimientos cuya fecha cae dentro del rango, **incluidos sus dos
   extremos**. *(PRD AC-26)*
5. **AC-15** — **Given** un filtro de categoría y uno de fechas a la vez, **When** se consulta el
   listado, **Then** devuelve los movimientos que cumplen **las dos** condiciones.
6. **AC-16** — **Given** un filtro que no deja pasar ningún movimiento, **When** se consulta el
   listado, **Then** devuelve una lista vacía y **no** un error.
7. **AC-17** — **Given** un filtro por una categoría que no existe o que es de otra cuenta,
   **When** se consulta el listado, **Then** el resultado no revela nada sobre esa categoría.

---

### Edge Cases

- **El rango invertido.** ¿Qué pasa si la fecha de inicio es posterior a la de fin? Se rechaza como
  petición inválida, no se devuelve una lista vacía: una lista vacía se lee como "no hay nada" y
  esconde el error.
- **La categoría cambia el tipo.** Las categorías están tipadas (gasto o ingreso) y el tipo del
  movimiento se deriva de ellas, igual que en el alta. Editar un gasto para ponerle una categoría de
  ingreso convierte el movimiento en un ingreso. Es consecuencia de cómo ya funciona el alta, y
  tiene que quedar dicho: es la única forma de cambiar el tipo de un movimiento.
- **Editar con la categoría de otra cuenta.** El ticket 3 introduce categorías propias. La edición
  tiene que buscar la categoría con el mismo criterio que ya usa el alta —predefinida o propia, y
  activa—, y no simplemente por identificador. El alta ya dejó esa cicatriz escrita.
- **Editar un movimiento sacándolo del período consultado.** Si se le cambia la fecha a un mes
  distinto del que se está viendo, desaparece del listado. No es un error: es AC-02.
- **Eliminar mientras otro lo edita.** Dos operaciones sobre el mismo movimiento en paralelo. La que
  llegue segunda tiene que encontrarse con "no existe" y no con un error inesperado.
- **Filtrar por un rango enorme.** Un rango de diez años sobre una cuenta con muchos movimientos. El
  listado no tiene paginación en este ticket, así que devuelve todo lo que caiga adentro.

---

## Requirements *(mandatory)*

### Functional Requirements

**Edición**

- **FR-001**: El sistema DEBE permitir consultar un movimiento propio por su identificador, y
  devolverlo con la misma forma con que lo devuelve el listado.
- **FR-002**: El sistema DEBE permitir modificar el monto, la categoría y la fecha de un movimiento
  propio ya registrado. *(RF-14, sin la moneda — ver Assumptions)*
- **FR-002b**: La modificación NO DEBE cambiar la moneda del movimiento. Un movimiento editado
  conserva la que tenía.
- **FR-003**: El sistema DEBE validar una modificación con las mismas reglas y la misma forma de
  error que el alta. Un movimiento no puede quedar, por vía de una edición, en un estado que el alta
  hubiera rechazado.
- **FR-004**: El sistema DEBE conservar el propietario original de un movimiento al modificarlo. El
  propietario NO puede ser una entrada de la petición.
- **FR-005**: El sistema DEBE derivar el tipo del movimiento de la categoría elegida, igual que en
  el alta.

**Eliminación**

- **FR-006**: El sistema DEBE permitir eliminar un movimiento propio ya registrado.
- **FR-007**: Un movimiento eliminado NO DEBE volver a aparecer en ningún listado.

**Aislamiento de la superficie nueva**

- **FR-008**: Consultar, modificar o eliminar un movimiento que no es propio DEBE responder de forma
  **indistinguible** de hacerlo sobre un movimiento que no existe. No puede haber ninguna diferencia
  observable —ni de código de respuesta, ni de cuerpo, ni de mensaje— que permita averiguar si un
  identificador ajeno corresponde a un movimiento real.
- **FR-009**: Modificar o eliminar un movimiento ajeno NO DEBE producir ningún cambio en ese
  movimiento.
- **FR-010**: Toda lectura de movimientos que esta feature agregue DEBE pasar por el canal único de
  lectura y acotar por cuenta, de modo que la barrera de aislamiento que ya existe se ponga en rojo
  si alguna no lo hace.

**Filtros del listado**

- **FR-011**: El sistema DEBE permitir filtrar el listado por categoría, tomando "todas las
  categorías" como comportamiento por omisión. *(RF-17)*
- **FR-012**: El sistema DEBE permitir filtrar el listado por un rango de fechas, **con sus dos
  extremos incluidos**. *(RF-18)*
- **FR-013**: El sistema DEBE seguir recortando al mes en curso cuando no se pide ningún rango, y
  ese mes en curso lo DEBE decidir el servidor. *(FR-007 de la feature 001)*
- **FR-014**: El sistema DEBE combinar los filtros con conjunción: un movimiento aparece si cumple
  todos los que se pidieron.
- **FR-015**: El sistema DEBE rechazar como petición inválida un rango cuya fecha de inicio sea
  posterior a la de fin, en lugar de devolver una lista vacía.
- **FR-016**: Un filtro que no deja pasar ningún movimiento DEBE producir una lista vacía y no un
  error. *(coherente con FR-012 de la feature 001)*

**Contrato**

- **FR-017**: Todo cambio en la forma de una petición o una respuesta DEBE quedar reflejado en la
  definición del contrato que el frontend declara, en el mismo movimiento. La verificación del
  contrato ya existente tiene que seguir en verde.

### Key Entities

- **Movimiento**: el hecho registrado. Hasta ahora se creaba y no cambiaba; esta feature le agrega
  un ciclo de vida —se modifica y se elimina—, sin cambiar sus atributos.
- **Filtro del listado**: el conjunto de condiciones con las que se pide el listado. No se persiste;
  vive en la petición. Sus valores por omisión son parte del contrato, no del cliente.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona puede corregir un dato mal cargado sin ayuda de nadie y sin perder el
  resto de la información del movimiento.
- **SC-002**: Una persona puede acotar su listado a un período y a una categoría, y obtener
  exactamente los movimientos que cumplen las dos condiciones, sin falsos positivos ni ausencias.
- **SC-003**: **Ninguna** operación sobre un movimiento ajeno —consulta, modificación o
  eliminación— permite distinguirlo de uno inexistente, y ninguna lo altera. Verificado con dos
  cuentas reales, no argumentado.
- **SC-004**: Los cuatro criterios que la feature 004 dejó en su tabla de *Deuda registrada* quedan
  cubiertos por tests que los nombran, y esa tabla queda vacía de esos cuatro.
- **SC-005**: La barrera de aislamiento sigue en pie y sigue sabiendo ponerse en rojo, ahora sobre
  una superficie de 5 endpoints de movimientos en lugar de 2.
- **SC-006**: El comportamiento por omisión del listado no cambia: quien no pide filtros sigue
  viendo exactamente lo que veía antes de esta feature.

---

## Assumptions

Decisiones tomadas donde el material de origen no alcanzaba. Están acá para que se vean y se puedan
discutir, no escondidas en el código.

- **La eliminación es definitiva, no una baja lógica.** El PRD usa el término "baja lógica"
  explícitamente para las categorías (RF-09) y **no** lo usa para los movimientos (RF-15). El
  contraste parece deliberado: quien escribió el PRD conocía el término y lo aplicó a un caso y no
  al otro. Si esto es un error del PRD, cambiarlo después cuesta una migración.
- **La moneda queda fuera de los campos editables.** RF-14 la nombra, pero hoy no se elige ni
  siquiera al registrar: el alta la toma de la predeterminada del catálogo (FR-009 de la feature
  001) y el contrato ni siquiera tiene el campo. Permitir cambiarla exigiría exponer el catálogo de
  monedas, que es el ticket 4a, y el plan lo pone después de éste. **No se puede editar lo que no
  se puede elegir**: la mitad faltante de RF-14 queda en *Deuda registrada* apuntando a 4a/4b, con
  el mismo criterio con que 004 dejó sus cinco AC esperando los endpoints que esta feature crea.
  Decidido con el usuario antes de cerrar la spec.
- **La modificación reemplaza el movimiento entero.** Se envían todos los campos editables y el
  resultado es el estado final, en vez de aplicar un parche con los que vengan. Es más simple de
  razonar y de validar, y coincide con la forma del alta.
- **No hay paginación.** El listado devuelve todo lo que caiga dentro del filtro, igual que hoy.
  Aparece cuando haga falta, con su propio ticket.
- **No hay registro de auditoría** de qué se editó o borró. El PRD no lo pide.
- **La forma del movimiento no cambia.** La respuesta de la consulta individual es la misma que ya
  usan el alta y el listado; no se agregan campos.
- **El tipo del movimiento se deriva de la categoría**, como en el alta. No es un campo editable por
  separado.

---

## Deuda registrada

Criterios del PRD que esta feature toca sólo a medias, porque su otra mitad depende de superficie
que todavía no existe. Se anotan acá con el ticket que los va a completar, igual que hizo 004.

| AC del PRD | La mitad que esta feature cubre | La mitad que falta | Ticket |
|---|---|---|---|
| AC-19 | El listado refleja el monto nuevo | El total por categoría y el balance del dashboard | FEAT-001c |
| AC-20 | El listado refleja la categoría y la fecha nuevas | El monto deja de sumar en la categoría anterior y suma en la nueva | FEAT-001c |
| AC-21 | El movimiento eliminado deja de aparecer en el listado | Su monto deja de sumar en el dashboard | FEAT-001c |
| RF-14 | Monto, categoría y fecha | **La moneda**: no se puede editar lo que no se puede elegir | 4a/4b |
| RF-28 | — | Filtrar el listado por moneda | 4a/4b |

---

## Lo que NO entra

- **El resumen y el dashboard.** `GET /api/resumen`, los totales por categoría y el balance: es
  FEAT-001c.
- **El filtro por moneda** (RF-28) y el catálogo de monedas: son los tickets 4a y 4b.
- **La nota descriptiva del movimiento**: es el ticket DISC-001-02.
- **Categorías propias del usuario**: es el ticket 3. Acá el catálogo sigue siendo el global.
- **Paginación y ordenamiento configurable** del listado. El orden sigue siendo el que ya está.
- **Edición o borrado masivo.** Un movimiento por vez.
- **Deshacer** una eliminación.

---

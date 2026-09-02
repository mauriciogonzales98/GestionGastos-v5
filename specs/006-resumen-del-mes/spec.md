# Feature Specification: Resumen del mes con desglose por categoría

**Feature Branch**: `006-resumen-del-mes`

**Created**: 2026-09-01

**Status**: Draft

**Input**: FEAT-001c del plan de implementación, reconstruido desde `PRD.md` y desde la tabla de
*Deuda registrada* de [`specs/004-aislamiento-cuentas/spec.md`](../004-aislamiento-cuentas/spec.md).

---

## De dónde sale esta spec

**El PRD de este ticket no existe en este repositorio.** `plan-de-implementacion/prds/` sólo trae
dos PRD de ticket; los de FEAT-001a/b/c nunca entraron, y eso está documentado en el README de esa
carpeta desde el PR #19. Igual que en la feature 005, el alcance se reconstruye de dos fuentes que
sí están:

1. **`PRD.md`**, el PRD del producto: RF-19, RF-20, RF-21, RF-22 y RF-29, con sus criterios AC-27,
   AC-28, AC-29, AC-30 y AC-31. Y de rebote AC-15, AC-16, AC-19, AC-20 y AC-21, que ya estaban
   escritos apoyándose en un resumen que todavía no existía.
2. **La tabla de *Deuda registrada* de la spec de 004**, cuya única fila que sigue Pendiente —AC-02—
   nombra el endpoint que este ticket crea.

Que la reconstrucción sea explícita importa: si mañana aparece el PRD original y dice otra cosa,
esta sección es el lugar donde se ve qué se supuso y por qué.

---

## Lo que hace distinta a esta feature

Es la primera que **deriva números en lugar de devolver hechos**. Todo lo que hubo hasta acá era
transporte: se guarda un movimiento y se devuelve el mismo movimiento. Un error de aislamiento se
veía —aparecía una fila ajena en el listado—. Acá el resultado es un total, y **un total contaminado
se ve idéntico a uno correcto**: nadie puede mirar `48.500` y darse cuenta de que adentro hay
`3.000` de otra cuenta.

De ahí salen las dos exigencias que gobiernan esta spec:

- **El aislamiento del resumen no se argumenta, se verifica con dos cuentas cargadas.** La
  comparación tiene que ser contra un número esperado calculado a mano, no contra "el resumen
  devuelve algo".
- **El resumen y el listado tienen que responder la misma pregunta con la misma respuesta.** Son dos
  vistas del mismo conjunto de movimientos: si difieren, una de las dos miente y la persona no tiene
  cómo saber cuál. AC-30 ya lo pide para el mes actual; esta feature lo extiende a cualquier período.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cómo vengo este mes (Priority: P1) 🎯 MVP

Cargué gastos e ingresos durante el mes y hoy sólo puedo verlos de a uno en el listado. Para saber
cómo vengo tengo que sumarlos yo, que es exactamente lo que la planilla ya me hacía hacer.

Quiero abrir la aplicación y ver, sin pedir nada, cuánto ingresé y cuánto gasté en el mes en curso,
y la diferencia entre los dos.

**Why this priority**: es la mitad de la promesa del producto —*cómo vengo este mes*— y es la más
barata de las tres historias: un total por tipo sobre el período que el listado ya sabe recortar.
Sin esto la aplicación registra pero no informa.

**Independent Test**: se prueba entero cargando gastos e ingresos de una cuenta dentro y fuera del
mes en curso y pidiendo el resumen sin ningún parámetro: los totales tienen que salir de los
movimientos de adentro y de ninguno de los de afuera.

**Acceptance Scenarios**:

1. **Given** una cuenta con ingresos y gastos cargados en el mes en curso, **When** pide el resumen
   sin indicar período, **Then** el total ingresado y el total gastado son iguales a la suma de los
   montos de cada tipo, y el balance es el primero menos el segundo. *(AC-30, RF-22, RF-20)*
2. **Given** una cuenta con movimientos en el mes en curso y otros en el mes anterior, **When** pide
   el resumen sin indicar período, **Then** los del mes anterior no suman en ningún total.
   *(FR-013 de la feature 005: el mes en curso lo decide el servidor)*
3. **Given** una cuenta sin ningún movimiento en el período, **When** pide el resumen, **Then**
   recibe una entrada por cada moneda del catálogo con sus totales, su balance y su desglose en
   cero, y no un error ni una respuesta vacía. *(AC-31)*
4. **Given** un movimiento propio recién registrado, **When** vuelve a pedir el resumen, **Then** su
   monto ya está sumado en el total de su tipo. *(AC-15, AC-16)*

---

### User Story 2 - En qué se me va la plata (Priority: P2)

Sé que gasté 120.000 este mes, pero no sé en qué. Necesito ver el total repartido por categoría para
darme cuenta de dónde está el agujero.

Quiero ver, para el mismo período del resumen, cuánto suma cada categoría, para poder compararlas
entre sí.

**Why this priority**: es la otra mitad de la promesa —*en qué se me va la plata*— y es la que
alimenta el gráfico del ticket 5. Va después de P1 porque el total general ya es útil solo, y el
desglose sin total general no.

**Independent Test**: se prueba cargando gastos en varias categorías dentro de un mismo período y
comprobando que el total de cada categoría es la suma de los suyos y que la suma de los desgloses
reconstruye el total gastado.

**Acceptance Scenarios**:

1. **Given** una cuenta con gastos en tres categorías distintas, **When** pide el resumen, **Then**
   para cada categoría el total es igual a la suma de los montos de sus movimientos dentro del
   período. *(AC-27)*
2. **Given** el desglose de un período, **When** se suman todos sus totales, **Then** el resultado
   es exactamente el total gastado de esa moneda en ese período. *(RF-29: ningún monto se pierde ni
   se cuenta dos veces)*
3. **Given** una cuenta con ingresos cargados en el período, **When** pide el resumen, **Then**
   ninguna categoría de ingreso aparece en el desglose, y esos montos sí suman en el total ingresado
   y en el balance. *(RF-19: el desglose es de gastos)*
4. **Given** una categoría de gasto sin movimientos en el período, **When** pide el resumen,
   **Then** esa categoría no aparece en el desglose, en lugar de aparecer en cero. *(el desglose
   describe lo que pasó, no el catálogo)*
5. **Given** un gasto que cambia de categoría por una edición, **When** vuelve a pedir el resumen,
   **Then** su monto deja de sumar en la categoría anterior y suma en la nueva. *(AC-20)*
6. **Given** un gasto que se elimina, **When** vuelve a pedir el resumen, **Then** su monto no suma
   en ningún total. *(AC-21)*

---

### User Story 3 - El mismo resumen, para el período que yo elija (Priority: P3)

El mes calendario no siempre es la pregunta. Quiero ver cómo me fue en una quincena, o en los tres
meses del verano, sin tener que sumar a mano lo que el listado ya me deja filtrar.

**Why this priority**: es una comodidad sobre un cálculo que ya existe, y el filtro de fechas del
listado —con sus dos extremos incluidos, su rechazo del rango invertido y su mes por omisión— ya
está resuelto y probado en la feature 005. Va última porque sin ella el resumen del mes ya sirve.

**Independent Test**: se prueba pidiendo el mismo conjunto de movimientos con un rango explícito y
comparando contra el listado filtrado con ese mismo rango: los dos tienen que hablar del mismo
conjunto.

**Acceptance Scenarios**:

1. **Given** movimientos repartidos dentro y fuera de un rango, **When** pide el resumen con ese
   rango, **Then** los totales y el desglose se calculan sólo con los de adentro, incluidos los
   fechados exactamente en los dos extremos. *(AC-29)*
2. **Given** un rango cuya fecha de inicio es posterior a la de fin, **When** pide el resumen,
   **Then** la petición se rechaza como inválida en lugar de devolver totales en cero. *(coherente
   con FR-015 de la feature 005)*
3. **Given** un rango cualquiera, **When** pide el listado y el resumen con ese mismo rango,
   **Then** los totales del resumen son iguales a los que se obtienen sumando el listado.

---

### Edge Cases

- **Medio rango.** Se indica sólo la fecha de inicio, o sólo la de fin. Se rechaza como petición
  inválida, igual que en el listado: suponer el extremo que falta es inventar un supuesto que nadie
  declaró.
- **Un rango de un solo día.** `desde` igual a `hasta` es un rango válido y contiene los movimientos
  de ese día.
- **Un rango enorme.** Un período de varios años no es un error: devuelve lo que haya.
- **Sesión ausente o vencida.** El resumen exige sesión como cualquier otro endpoint (RF-03), y no
  devuelve un resumen en cero al anónimo.
- **Períodos donde sólo hay ingresos, o sólo gastos.** El total del tipo ausente es cero y el
  balance queda con el signo que corresponda; un balance negativo es un resultado válido, no un
  error. Sólo con ingresos, el desglose queda vacío y eso no es un caso especial.
- **Una moneda del catálogo sin movimientos.** Aparece igual, con todo en cero. Hoy hay una sola en
  uso, así que este caso ya se da apenas el catálogo tenga dos filas.
- **Montos que se acumulan.** La suma de muchos movimientos no puede perder ni inventar centavos
  respecto de sumarlos de a uno.
- **Una categoría que se renombra.** El desglose muestra el nombre vigente de la categoría, no el
  que tenía cuando se cargó el movimiento. *(AC-13; el caso completo llega con el ticket 3)*

---

## Requirements *(mandatory)*

### Functional Requirements

**El resumen y su período**

- **FR-001**: El sistema DEBE ofrecer, para la cuenta autenticada, un resumen de sus movimientos
  dentro de un período.
- **FR-002**: El sistema DEBE tomar el mes en curso como período por omisión cuando no se pide
  ninguno, y ese mes en curso lo DEBE decidir el servidor. *(RF-22, y FR-013 de la feature 005)*
- **FR-003**: El sistema DEBE permitir acotar el resumen a un rango de fechas **con sus dos extremos
  incluidos**. *(RF-21, AC-29)*
- **FR-004**: El sistema DEBE rechazar como petición inválida un rango cuya fecha de inicio sea
  posterior a la de fin, y también un rango del que se indique un solo extremo, en lugar de devolver
  totales en cero.
- **FR-005**: El resumen y el listado DEBEN describir el mismo conjunto de movimientos ante el mismo
  período: los totales del resumen son iguales a los que se obtienen sumando el listado filtrado con
  ese período. *(AC-30)*

**Los totales**

- **FR-006**: El sistema DEBE informar, para el período, el total ingresado y el total gastado.
  *(RF-22)*
- **FR-007**: El sistema DEBE informar el balance del período, calculado como el total de ingresos
  menos el total de gastos. *(RF-20)*
- **FR-008**: El sistema DEBE informar el desglose **de gastos** por categoría, con el total de
  cada una. Los ingresos NO se desglosan: entran en el total ingresado y en el balance. *(RF-19)*
- **FR-009**: El sistema DEBE incluir en el desglose únicamente las categorías con al menos un
  gasto en el período; la suma de los totales del desglose DEBE ser igual al total gastado de esa
  moneda en ese período.
- **FR-010**: El sistema DEBE identificar cada categoría del desglose por su identificador y por su
  nombre vigente, para que quien lo muestre no tenga que cruzarlo contra el catálogo.

**Monedas**

- **FR-011**: El sistema DEBE calcular todo total, subtotal y balance sumando únicamente montos de
  una misma moneda, y NUNCA DEBE convertir entre monedas. *(RF-29)*
- **FR-012**: El sistema DEBE discriminar por moneda el total ingresado, el total gastado, el
  balance y el desglose de gastos por categoría. *(RF-19, RF-20, RF-22)*
- **FR-013**: El sistema DEBE devolver una entrada por **cada moneda del catálogo**, tenga o no
  movimientos en el período. Una moneda sin movimientos va con sus totales, su balance y su desglose
  en cero. *(AC-31)*
- **FR-014**: Cuando el período no tiene ningún movimiento, la respuesta DEBE seguir teniendo la
  misma forma —una entrada por moneda, todo en cero, desglose vacío— y NUNCA DEBE ser un error ni
  una respuesta vacía que obligue a quien la muestre a inventar los ceros. *(AC-31)*

**Aislamiento**

- **FR-015**: El resumen DEBE calcularse exclusivamente sobre los movimientos de la cuenta
  autenticada. Ningún monto de otra cuenta puede sumar en ningún total, subtotal ni balance.
  *(RF-04, AC-02 de la tabla de Deuda registrada de la feature 004)*
- **FR-016**: La lectura que alimenta el resumen DEBE nacer dentro del canal único de lectura de
  movimientos, acotada por cuenta en la consulta y no después de traer las filas.
- **FR-017**: La barrera de aislamiento DEBE cubrir el endpoint nuevo y DEBE saber ponerse en rojo
  si ese resumen deja de acotar por cuenta. *(Principio V de la constitución)*

**Contrato**

- **FR-018**: Todo cambio en la forma de una petición o una respuesta DEBE quedar reflejado en la
  definición del contrato que el frontend declara, en el mismo movimiento. La verificación del
  contrato ya existente tiene que seguir en verde.

### Key Entities

- **Resumen del período**: el resultado del cálculo. **No se persiste**: se deriva de los
  movimientos cada vez que se pide. No tiene identidad ni historia; dos pedidos idénticos sobre los
  mismos datos dan lo mismo.
- **Total por moneda**: dentro de un resumen, lo ingresado, lo gastado y el balance de una moneda.
  Es la unidad que RF-29 vuelve indivisible: nada se suma a través de dos de éstos.
- **Total por categoría**: dentro de un total por moneda, lo que suma una categoría. Lleva el
  identificador y el nombre vigente de la categoría.
- **Período**: el rango de fechas sobre el que se calcula. Vive en la petición, no se persiste, y su
  valor por omisión es parte del contrato y no del cliente. Es el mismo concepto que el filtro del
  listado.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona puede responder *cómo vengo este mes* de un vistazo, sin sumar nada a
  mano y sin pedir ningún filtro.
- **SC-002**: Una persona puede responder *en qué se me va la plata* viendo el peso relativo de cada
  categoría en el mismo período.
- **SC-003**: Para cualquier período, los totales del resumen coinciden **exactamente** con los que
  se obtienen sumando el listado filtrado con ese mismo período. Verificado sobre datos cargados, no
  argumentado.
- **SC-004**: **Ningún** monto de otra cuenta aparece en ningún total, subtotal ni balance.
  Verificado con dos cuentas reales cargadas, comparando contra un valor esperado calculado a mano.
- **SC-005**: La fila AC-02 de la tabla de *Deuda registrada* de la feature 004 queda saldada, con
  el nombre del test que la cubre, y esa tabla queda vacía.
- **SC-006**: La barrera de aislamiento sigue sabiendo ponerse en rojo, ahora también sobre el
  cálculo del resumen y no sólo sobre las lecturas que devuelven movimientos.
- **SC-007**: Ningún total mezcla montos de monedas distintas, en ningún caso, y quien muestre el
  resumen nunca tiene que inventar un cero: la respuesta ya lo trae.
- **SC-008**: El comportamiento del listado no cambia: esta feature no toca lo que ya se ve.

---

## Assumptions

Decisiones tomadas donde el material de origen no alcanzaba. Están acá para que se vean y se puedan
discutir, no escondidas en el código.

- **El resumen del mes (RF-22) y los datos del dashboard (RF-19, RF-20) son el mismo cálculo, no
  dos.** AC-30 exige que el total del mes de la pantalla principal sea igual al del dashboard
  filtrado por el mes actual. Dos cálculos separados que tienen que dar lo mismo son dos cálculos
  que algún día no van a darlo. La pantalla principal es el mismo resumen pedido sin período.
- **El ticket cubre el cálculo, no el gráfico.** RF-19 nombra una representación gráfica; la
  representación es del ticket 5 (Dashboard con gráficos), que va a consumir estos números. Acá se
  entrega el dato correcto y verificado.
- **El desglose por categoría se agrupa por moneda desde el primer día.** La moneda ya está en el
  modelo —el movimiento la guarda y el listado ya la devuelve—, y lo único que falta es poder
  elegirla al registrar, que es el ticket 4b. Estrenar el resumen con una forma de moneda única
  obligaría a cambiar la forma de la respuesta cuando llegue 4a, con el contrato y el frontend
  atrás. Hoy todos los movimientos caen en la predeterminada y va a haber una sola entrada; la forma
  ya soporta las que vengan.
- **El desglose es de gastos y no de ingresos.** Es lo literal de RF-19 y lo que responde *en qué
  se me va la plata*. AC-20 queda cubierto igual: un ingreso que cambia de categoría no aparece en
  ningún desglose ni antes ni después, y su monto sigue reflejado en el total ingresado y en el
  balance. Decidido el 2026-09-01 con el usuario.
- **La respuesta trae una entrada por cada moneda del catálogo, aunque quede en cero.** AC-31 se
  cumple en el dato y no en la pantalla: el caso vacío es justo donde cada cliente resuelve distinto
  —y aparece un "—" donde tenía que ir un "0"—. Cuando el ticket 4a sume el dólar, un mes sin gastos
  en dólares lo va a mostrar en cero, que es información y no ruido. Decidido el 2026-09-01 con el
  usuario.
- **El filtro por moneda del dashboard (RF-30) queda fuera.** Depende de que haya más de una moneda
  en uso, que es el ticket 4a. El resumen las discrimina; filtrarlas es otro ticket.
- **Los movimientos de categorías dadas de baja siguen sumando** (AC-14). El desglose describe lo
  que pasó, y lo que pasó no se borra porque la categoría deje de ofrecerse. Hoy no hay bajas
  lógicas —llegan con el ticket 3—, así que no hay nada que implementar; queda asentado para que ese
  ticket no lo rompa.
- **La feature es de backend.** Igual que FEAT-001b, el frontend recibe la declaración del contrato
  y nada más: la pantalla que muestre estos números es del ticket 5 y de la maquetación del ticket
  15. Si esto no es lo que se quiere, es el momento de decirlo.
- **El rendimiento se hereda del índice que ya existe.** RNF-01 pide el dashboard en menos de 2 s
  con 1000 movimientos; el resumen recorre el mismo conjunto que el listado, acotado por el mismo
  índice `(usuario_id, fecha DESC, id DESC)`. No se asume: el ticket lo mide con el sembrado de
  rendimiento que ya está en la suite.

---

## Deuda registrada

Lo que esta feature **no** dejó hecho. No se da por cumplido ni se descarta: queda acá con el ticket
que lo va a poder cubrir, para que ese ticket lo cubra al nacer. Es la misma tabla que la feature
004 dejó y que ésta terminó de vaciar.

| # | Qué queda | Por qué no acá | Quién lo cubre |
|---|---|---|---|
| D6-01 | **La pantalla del resumen.** El backend devuelve los números y nadie los muestra todavía | Igual que FEAT-001b, esta feature fue de backend: el frontend recibió sólo la declaración del contrato | Ticket 5 (Dashboard con gráficos) y ticket 6 (Maquetación) |
| D6-02 | **La representación gráfica** de los totales por categoría (RF-19 la nombra) | Acá se entregó el cálculo verificado, que es lo que un gráfico necesita para no mentir | Ticket 5 |
| D6-03 | **RF-30: filtrar el resumen por moneda.** El resumen las discrimina; no se pueden filtrar | Filtrar monedas requiere que haya más de una en uso, y hoy todo se registra en la predeterminada | Ticket 4a / 4b |
| D6-04 | **AC-14: los movimientos de una categoría dada de baja siguen sumando en el desglose** | Hoy no existen ni las categorías propias ni la baja lógica, así que no hay nada que implementar ni forma de testearlo | Ticket 3 (Categorías propias) — **el desglose no debe empezar a filtrar por `activa`** |
| D6-05 | **El índice por `categoria_id`** que ayudaría al `GROUP BY` | RNF-01 se cumple sin él, medido en los dos escalones. Un índice de más se paga en cada `INSERT` | Nadie, salvo que `RendimientoResumenTests` se ponga en rojo. Ver [D-10](./research.md#d-10--sin-migración-y-el-índice-se-deja-como-está) |
| D6-06 | **El resumen no se filtra por categoría**, y el listado sí desde FEAT-001b. Un `categoriaId` en la URL se ignora | El resumen es del período completo por diseño: filtrarlo es una vista distinta, no un parámetro más. Declarado en [`contracts/resumen.md`](./contracts/resumen.md) para que no se resuelva por omisión | Ticket 5 (Dashboard) — **si el filtro tapa el listado y no el resumen, la misma pantalla muestra dos cifras que se contradicen** |

### Deuda de proceso, no de producto

Cosas que quedaron por debajo del estándar del proyecto y conviene saber antes de leer el código:

- **Los tests de la historia 3 no vieron rojo antes de su implementación.** El endpoint nació
  aceptando `desde` y `hasta` en la tarea T024, así que al llegar a US3 los quince tests pasaron de
  entrada. En vez de fabricar un rojo ceremonial se verificó por **mutación**: con el endpoint
  ignorando el período, seis de ellos caen. Verifican algo, pero el orden del Principio I no se
  cumplió ahí, y queda dicho en lugar de disimulado.
- **El test de `401` sin sesión pasó desde que se escribió**, porque la autorización global responde
  `401` incluso a rutas que no existen. Recién dice algo ahora que el endpoint existe. No es un
  problema: es una guarda, y como guarda vale.
- **El quickstart se recorrió con `python3` y no con `jq`**, que no estaba instalado en la máquina
  donde se implementó. Los ocho pasos dan lo que el documento dice; las líneas literales de `jq` no
  se ejecutaron nunca. Quien lo corra con `jq` la primera vez, que avise si alguna no anda.

---

## Dependencies

- **Lo que ya está y se reusa, no se reescribe**: el rango de fechas con sus dos extremos incluidos
  y su invariante en el tipo, el mes en curso decidido por el servidor, el canal único de lectura de
  movimientos acotado por cuenta, y la barrera que lo hace cumplir. Todo eso salió de las features
  004 y 005.
- **Lo que este ticket destraba**: el ticket 5 (Dashboard con gráficos) consume estos números; sin
  ellos no tiene qué graficar.

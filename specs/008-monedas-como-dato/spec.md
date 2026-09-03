# Feature Specification: Monedas administrables como dato

**Feature Branch**: `008-monedas-como-dato`

**Created**: 2026-09-03

**Status**: Draft

**Input**: DISC-001-04a del plan de implementación, con su PRD en
[`plan-de-implementacion/prds/pendientes/prd-DISC-001-04a.md`](../../plan-de-implementacion/prds/pendientes/prd-DISC-001-04a.md),
reconciliado contra el estado real del código.

---

## De dónde sale esta spec

> **Cómo se leen los identificadores acá.** `AC-01`, `FR-001` y `SC-001` sin prefijo son **de esta
> spec**. Los ajenos van marcados con su origen: `PRD:AC-11` es del PRD de la 4a y `006:AC-31` es de
> la feature 006. Tres documentos numeran desde 1 y este ticket los cita a los tres — sin la marca,
> buscar "AC-10" devuelve dos cosas distintas y ninguna avisa.

**Este es el ticket más reconciliado de todos los que van hasta acá, y conviene leer esta sección
antes que ninguna otra.** El PRD de la 4a está fechado el **2026-08-20** y describe un punto de
partida que ya no existe: la mayor parte de lo que pide la construyeron FEAT-001a y FEAT-001c
mientras resolvían lo suyo, y una de sus premisas es directamente falsa.

Un PRD que describe un código que cambió es peligroso de una manera distinta a uno incompleto: se
lee como si fuera cierto, y el trabajo que propone parece pendiente cuando ya está verde y con
tests.

### Lo que el PRD pide y ya está construido

| Requisito del PRD | Estado real | Evidencia en el código |
|---|---|---|
| **FR-01** · catálogo de monedas no modificable por el usuario, con pesos y dólares | **Hecho** | Tabla `moneda`, sembrada con `ARS` y `USD` en la migración `Inicial` |
| **FR-02** · exactamente una moneda marcada como predeterminada | **Hecho** | `Moneda.EsPredeterminada`, y la migración `UnicaMonedaPredeterminada` lo sostiene **en la base**, no sólo en el código |
| **FR-05** · ningún total, subtotal ni balance mezcla monedas | **Hecho** | `MovimientosConsulta.Agrupado` agrupa por `MonedaId`: la separación es estructural, no una comprobación al final |
| **FR-06** · el resumen como totales por cada moneda, con sus tres números y su desglose | **Hecho** | `Resumen.Monedas[]` → `ResumenPorMoneda`, con `TotalIngresado`, `TotalGastado`, `Balance` y `GastosPorCategoria` |
| **NFR-02** · agregación en la consulta, nunca sumando en el cliente | **Hecho** | `CalculoDelResumen` compone en memoria a lo sumo `monedas × tipos × categorías` filas ya sumadas por el motor |

### La premisa falsa

> *"Hoy `"ARS"` es una constante en `Common/Moneda.cs`. No hay catálogo, no hay forma de agregar una
> moneda sin tocar código."*

**Ese archivo no existe.** `Dominio/Moneda.cs` es una entidad con su tabla desde la migración
`Inicial`, y su propio comentario ya anticipa este ticket. El alta de un movimiento no lleva la
moneda a una constante: la lee del catálogo con
`contexto.Monedas.SingleAsync(m => m.EsPredeterminada)`.

Lo que el PRD pide como trabajo —"que las monedas sean un dato administrable y no una constante
repartida por el código"— es, en su parte estructural, el estado actual.

### Lo que el PRD pide y NO aplica

| Requisito | Por qué no aplica |
|---|---|
| **FR-07** · que la migración deje los movimientos ya registrados con la moneda predeterminada | La columna `moneda_id` nació **con su clave foránea** en `Inicial`. Nunca hubo un movimiento sin moneda que normalizar, y no puede haberlo: la FK lo impide desde el primer día |
| **`PRD:AC-09`** · verificar esa normalización sobre una base con movimientos previos | No hay tal base. El escenario que describe no es alcanzable |

### La contradicción con la feature 006

**`PRD:AC-07` y `PRD:AC-08` piden lo contrario de lo que la feature 006 decidió y construyó**, y
esto no es un descuido de ninguno de los dos: son dos criterios razonables que no pueden convivir.

| | Qué pide | Con qué razón |
|---|---|---|
| **`PRD:AC-07` / `PRD:AC-08`** | Que el resumen **no** devuelva totales de una moneda sin movimientos en el período, y que un período vacío no devuelva totales de ninguna | Ninguna escrita. El PRD lo enuncia sin justificarlo |
| **`006:AC-31`** | Que devuelva una entrada por **cada moneda del catálogo**, con todo en cero si no hubo movimientos | Escrita y defendida: *"y no una respuesta vacía que obligue a quien la muestre a inventar los ceros"*. Es la razón **D-05** de su research, y está en el contrato, en `ResumenDtos`, en `frontend/src/api/tipos.ts` y en los tests de la 006 |

**Gana `006:AC-31` y el resumen no se toca.** Es la misma clase de decisión que la feature 007 tomó con
el límite del nombre —el PRD decía 60, la columna decía 50, ganó la columna—: entre un criterio con
el razonamiento escrito y otro sin él, gana el que se puede defender. Invertirlo costaría reescribir
el cálculo, el contrato, los tipos del frontend y los tests de la 006 **para que la pantalla tenga
que inventar los ceros que hoy le llegan servidos**.

Queda anotado en *Deuda registrada* como decisión tomada, no como pendiente.

### Lo que entonces queda para esta feature

Dos cosas, y las dos son **verificación de una propiedad que hoy es plausible pero no está probada**:

1. **Que sumar una moneda sea de verdad sólo un dato** (`PRD:FR-03`, `PRD:NFR-01`, `PRD:AC-02`). Hoy se cree porque el
   código está escrito así, no porque alguien lo haya hecho. Es exactamente la clase de afirmación
   que el proyecto ya aprendió a no dar por buena sin verla: el Principio V de la constitución
   existe por eso.
2. **Que la separación por moneda aguante el volumen** (`PRD:NFR-03`, `PRD:AC-12`). La 006 midió con una sola
   moneda; el desglose por par de moneda y categoría multiplica las filas y nadie lo midió.

**FR-04 (rechazar una moneda fuera del catálogo) se difiere a 4b**, y no por comodidad: hoy la
moneda **no viaja en ninguna petición** —ni `NuevoMovimientoDto` ni `MovimientoEditadoDto` la
llevan—, así que no hay ninguna entrada que validar. Un test de FR-04 hoy tendría que inventarse una
vía de entrada que no existe para después comprobar que la rechaza. Se registra como deuda con el
ticket que la cubre.

---

## Lo que hace distinta a esta feature

**Es la primera que no agrega comportamiento.** Todas las anteriores terminaban con algo que antes
no se podía hacer; ésta termina con dos afirmaciones que antes eran creencias y ahora son hechos
verificados.

Eso la vuelve fácil de despachar como trámite, y el propio PRD lo anticipa en sus riesgos: *"este
ticket no cambia nada visible, y eso lo vuelve fácil de saltear o de dar por hecho"*. La diferencia
entre hacerla y saltearla no se ve el día que se cierra: se ve el día que alguien agrega una moneda
en producción y descubre que hacía falta recompilar.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sumar una moneda sin tocar la aplicación (Priority: P1) 🎯 MVP

Quien administra la instalación necesita agregar el euro. No quiere pedirle a nadie que compile
nada, ni esperar un despliegue: quiere insertar una fila y que la aplicación la tome.

**Why this priority**: es la promesa entera del ticket (RF-32) y la única que, si falla, invalida el
diseño del catálogo. Las demás historias miden o documentan; ésta prueba.

**Independent Test**: se prueba entero insertando una moneda en el catálogo por fuera de la
aplicación, sin recompilar ni reiniciar nada que no sea lo que un cambio de datos exige, y
comprobando que aparece en el resumen con sus totales en cero.

**Acceptance Scenarios**:

1. **Given** una instalación migrada y en funcionamiento, **When** se agrega una moneda al catálogo
   únicamente como dato, **Then** el resumen la incluye con sus totales en cero, sin que se haya
   modificado ninguna línea de código ni recompilado la aplicación. *(AC-01)*
2. **Given** esa moneda recién agregada, **When** se registra un movimiento con ella por la vía que
   el sistema permita, **Then** el movimiento se acepta y sus montos suman en los totales de esa
   moneda y en los de ninguna otra. *(AC-02)*
3. **Given** una instalación recién migrada, **When** se consulta el catálogo, **Then** están pesos
   y dólares y **exactamente una** de las dos está marcada como predeterminada. *(AC-03)*

---

### User Story 2 - Los totales siguen separados cuando hay volumen (Priority: P2)

El desglose ya separa por moneda, pero con una sola moneda en uso esa separación nunca se ejerció
contra un volumen real. Con dos monedas, cada categoría puede aparecer dos veces y las filas del
agregado se multiplican.

**Why this priority**: no es una promesa nueva, es una promesa existente puesta a prueba en la
condición que la puede romper. Va después de P1 porque mide algo que P1 no cambia.

**Independent Test**: se prueba sembrando una cuenta con 1000 movimientos repartidos en dos monedas
y midiendo la respuesta del resumen sobre 100 ejecuciones.

**Acceptance Scenarios**:

1. **Given** una cuenta con 1000 movimientos repartidos en dos monedas, **When** se pide el resumen
   del período, **Then** responde en menos de 2 s en el percentil 95 sobre 100 ejecuciones.
   *(AC-04)*
2. **Given** una cuenta con gastos en pesos y en dólares en la **misma** categoría y período,
   **When** pide el resumen, **Then** esa categoría aparece una vez dentro de cada moneda, con el
   total de esa moneda, y ningún total incluye montos de la otra. *(AC-05)*
3. **Given** ingresos y gastos en las dos monedas dentro del período, **When** pide el resumen,
   **Then** el balance de cada moneda es sus ingresos menos sus gastos, calculado sin cruzar nada
   entre monedas. *(AC-06)*
4. **Given** una cuenta con movimientos en dos monedas y varias categorías, **When** se inspecciona
   la respuesta del resumen, **Then** trae por cada moneda sus tres totales ya sumados y **a lo sumo
   una fila por categoría**, y **no** trae la lista de movimientos individuales. *(AC-10, que es
   `PRD:AC-11`)*

---

### User Story 3 - Lo que ya funcionaba sigue funcionando (Priority: P3)

Con una sola moneda en uso, todo lo que la aplicación devuelve hoy tiene que seguir devolviéndolo
igual.

**Why this priority**: es la red de seguridad de las dos historias anteriores, no un entregable en
sí. Si P1 o P2 obligaran a tocar el cálculo, esto es lo que avisa que se rompió algo.

**Independent Test**: se prueba comparando la salida del resumen con una sola moneda con movimientos
contra la que la feature 006 ya verifica.

**Acceptance Scenarios**:

1. **Given** una cuenta con movimientos en una sola moneda, **When** pide el resumen, **Then**
   devuelve los mismos totales, el mismo balance y el mismo desglose que antes de esta feature.
   *(AC-07)*
2. **Given** una moneda del catálogo sin ningún movimiento en el período, **When** pide el resumen,
   **Then** esa moneda aparece igual, con sus totales, su balance y su desglose en cero, y sin
   ningún mensaje de error. *(AC-08, y es `006:AC-31` conservado a propósito)*
3. **Given** una cuenta sin ningún movimiento en el período, **When** pide el resumen, **Then**
   devuelve una entrada en cero por cada moneda del catálogo y ningún error. *(AC-09)*

---

### Edge Cases

- **Una moneda agregada como dato con un código que ya existe.** El catálogo tiene un índice único
  por código: la inserción falla en la base y la aplicación no se entera, que es el comportamiento
  correcto para algo que se administra por fuera.
- **Una moneda agregada con `es_predeterminada` en verdadero.** La migración
  `UnicaMonedaPredeterminada` lo impide en la base. Es la protección que hace que el alta pueda
  pedir la predeterminada sin preguntarse si hay dos.
- **Cero monedas en el catálogo.** No es alcanzable —la semilla siembra dos y no hay forma de
  borrarlas desde la aplicación— pero si lo fuera, el alta de un movimiento no tendría
  predeterminada que asignar. Queda fuera de alcance por inalcanzable, y anotado.
- **Una moneda con distinta cantidad de decimales.** La columna existe y es dato de la moneda. Esta
  feature no la ejercita: el formato es maquetación y va en el ticket 6.
- **Un período sin movimientos en ninguna moneda.** Cubierto por AC-09: ceros, no vacío.

## Requirements *(mandatory)*

### Functional Requirements

**El catálogo como dato**

- **FR-001**: El sistema DEBE aceptar una moneda agregada al catálogo únicamente como dato, sin
  ninguna modificación del código de la aplicación y sin recompilarla. *(FR-03 del PRD, RF-32)*
- **FR-002**: Una moneda agregada de ese modo DEBE aparecer en el resumen con sus totales, su
  balance y su desglose, en cero mientras no tenga movimientos. *(FR-03 del PRD, y consecuencia de
  conservar `006:AC-31`)*
- **FR-003**: El catálogo DEBE contener pesos y dólares tras la migración, y DEBE tener **exactamente
  una** moneda marcada como predeterminada. *(FR-01 y FR-02 del PRD, RF-25, RF-31)*
- **FR-004**: La unicidad de la moneda predeterminada DEBE sostenerse en la base de datos y no
  únicamente en el código, para que administrar el catálogo por fuera de la aplicación no pueda
  dejarlo en un estado que la aplicación no sabe leer.

**La separación por moneda**

- **FR-005**: El sistema NO DEBE sumar montos de monedas distintas en ningún total, subtotal ni
  balance, ni siquiera por descuido. *(FR-05 del PRD, RF-29)*
- **FR-006**: El desglose por categoría DEBE calcularse dentro de cada moneda, de modo que una misma
  categoría con movimientos en dos monedas produzca una entrada en cada una y ninguna que las
  mezcle. *(FR-06 del PRD, RF-29)*
- **FR-007**: Los totales DEBEN calcularse por agregación en la consulta a la base, transfiriendo a
  lo sumo una fila por par de moneda y categoría más los tres totales de cada moneda, y nunca
  sumando los montos en el cliente. *(`PRD:NFR-02`, verificado por AC-10)*

**Lo que no cambia**

- **FR-008**: Con una sola moneda con movimientos, el resumen DEBE devolver exactamente los mismos
  totales, el mismo balance y el mismo desglose que devolvía antes de esta feature. *(`PRD:AC-10`)*
- **FR-009**: El resumen DEBE seguir devolviendo una entrada por **cada** moneda del catálogo, tenga
  o no movimientos en el período. **Esto contradice `PRD:AC-07` y `PRD:AC-08` de la 4a, y es
  deliberado**: conserva `006:AC-31`, cuya razón está escrita y cuyo contrato ya
  consume el frontend. Ver *De dónde sale esta spec*.
- **FR-010**: El alta de un movimiento DEBE seguir asignándole la moneda predeterminada leída del
  catálogo, y NO DEBE ofrecer forma de elegir otra. *(el selector es el ticket 4b; AC-02 se apoya
  entero en esto —es la única vía por la que puede registrar en la moneda nueva—, así que
  verificarlo es verificar también este requisito)*

**Rendimiento**

- **FR-011**: El resumen DEBE responder en menos de 2 s en el percentil 95, medido sobre 100
  ejecuciones en una cuenta con 1000 movimientos repartidos en dos monedas. *(NFR-03 del PRD, RNF-01)*

### Key Entities

- **Moneda**: una unidad en la que se puede expresar un monto. Tiene código ISO, nombre, símbolo,
  cantidad de decimales y una marca de predeterminada. **Se administra como dato**: la aplicación la
  lee y nunca la escribe. Exactamente una del catálogo es la predeterminada.
- **Catálogo de monedas**: el conjunto de las monedas conocidas. No es de ninguna cuenta —igual que
  las categorías predefinidas— y es igual para todas.
- **Totales de una moneda**: los tres números de un período dentro de una sola moneda —ingresado,
  gastado y balance— más su desglose por categoría. **Son un universo cerrado**: nada se suma nunca
  a través de dos de ellos, y no hay conversión ni la va a haber.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una moneda nueva queda disponible en la aplicación con **0 líneas de código
  modificadas y 0 recompilaciones**, y eso se demuestra ejecutándolo, no leyendo el código.
- **SC-002**: Con movimientos cargados en dos monedas dentro de una misma categoría y período, el
  total de cada moneda es exactamente la suma de los montos de esa moneda: **ninguno de los dos
  incluye ni un centavo del otro**.
- **SC-003**: El resumen de una cuenta con 1000 movimientos repartidos en dos monedas se obtiene en
  **menos de 2 s en el percentil 95** sobre 100 ejecuciones.
- **SC-004**: Con una sola moneda con movimientos, la salida del resumen es **idéntica** a la
  anterior a esta feature — mismos totales, mismo balance, mismo desglose, mismo orden.
- **SC-005**: Un período sin movimientos devuelve una entrada en cero por cada moneda del catálogo,
  y **cero mensajes de error**.

## Assumptions

- **El catálogo se administra por fuera de la aplicación.** No hay ni va a haber en esta feature una
  pantalla ni un endpoint para crear, editar o borrar monedas: el PRD lo deja fuera de alcance
  explícitamente. "Como dato" significa una fila insertada en la base.
- **Agregar una moneda no exige reiniciar la aplicación.** El catálogo se lee en cada pedido y no se
  cachea. Si en algún momento se cacheara, NFR-01 obligaría a invalidarlo, y eso sería trabajo de
  esta feature. Hoy no lo es.
- **Nadie borra una moneda que tiene movimientos.** La clave foránea del movimiento lo impide, y el
  PRD deja el borrado fuera de alcance. No se prueba.
- **La moneda predeterminada no cambia una vez que hay movimientos registrados con la anterior.** El
  PRD lo deja fuera de alcance explícitamente.
- **La medición de rendimiento corre sólo en local.** Los tests de rendimiento miden tiempo de pared
  y el CI los excluye con `FullyQualifiedName!~Rendimiento`, porque en un runner compartido dan
  rojos que no dicen nada. Es la convención ya establecida en `AGENTS.md`, no una decisión de esta
  feature.
- **No hay conversión de divisas, y no la va a haber.** PRD-001 la excluye de forma explícita: los
  montos de cada moneda se suman y se muestran por separado, sin cotización ni total consolidado.

## Deuda registrada

Lo que esta feature **no** va a dejar hecho, con el ticket que lo cubre. Se hereda la forma de la
tabla de las features 004, 006 y 007.

| # | Qué queda | Por qué no acá | Quién lo cubre |
|---|---|---|---|
| D8-01 | **FR-04 del PRD: rechazar un movimiento cuya moneda no esté en el catálogo.** Con su `PRD:AC-03` y su `PRD:AC-04` | Hoy la moneda **no viaja en ninguna petición**: ni el alta ni la edición la llevan, y el servidor asigna la predeterminada. No hay entrada que validar, y un test de esto tendría que inventarse primero la vía que dice comprobar | **Ticket 4b**, que abre el selector y con él la primera vía por la que puede llegar una moneda elegida |
| D8-02 | **El selector de moneda, la columna del listado y el filtro por moneda** | Fuera de alcance explícito del PRD de la 4a: son la 4b entera | Ticket 4b |
| D8-03 | **RF-30: filtrar el resumen por moneda.** Es la deuda D6-03 de la feature 006, que apuntaba a "4a / 4b" | El resumen ya las discrimina; filtrarlas es una vista distinta y necesita que haya más de una moneda **en uso**, cosa que recién pasa en 4b | Ticket 4b |
| D8-04 | **`PRD:AC-07` y `PRD:AC-08` de la 4a**, que piden omitir las monedas sin movimientos | **No es deuda, es una decisión tomada**: gana `006:AC-31`, cuya razón está escrita. Queda acá para que quede constancia de que se miró y se resolvió, no de que se olvidó | Nadie. Si alguna vez se revierte, hay que revertir también el contrato, los tipos del frontend y los tests de la 006 |
| D8-05 | **El formato regional del monto por moneda** —separadores, posición del símbolo—, y la columna `decimales` que ya existe y nadie usa | Es maquetación | Ticket 6 (Maquetación y accesibilidad) |
| D8-06 | **El dashboard con gráficos y su filtro por moneda** | Es el ticket 5, que depende de éste | Ticket 5 (Dashboard con gráficos) |

## Dependencies

- **FEAT-001a mergeado en `main`**: la tabla `moneda`, su semilla y la clave foránea del movimiento.
- **FEAT-001c mergeado en `main`** (feature 006): el resumen por moneda con su desglose, que esta
  feature verifica y **no** reescribe.
- **La feature 007 mergeada en `main`**: la barrera del desglose (`verificar-desglose.sh`) vigila la
  misma consulta que esta feature mide, y las categorías propias multiplican los casos del desglose.
- **MySQL 8.4.10**, y la migración `UnicaMonedaPredeterminada`, que es lo que hace que FR-004 sea
  cierto en la base y no sólo en el código.
- **El filtro `FullyQualifiedName!~Rendimiento` del CI**, declarado en la sección *Stack* de
  `AGENTS.md`, que FR-011 necesita.

# Feature Specification: Cada cuenta con sus propias categorías predefinidas

**Feature Branch**: `021-predefinidas-por-cuenta`

**Created**: 2026-09-12

**Status**: Draft

**Input**: Saldar **D7-07** de la feature 007 — *"Ningún `CHECK` de esquema impide que un movimiento
apunte a una categoría fuera del ámbito de su dueño"*. La fila de la deuda proponía dos formas
posibles y pedía explícitamente diseñarlas antes de prometerlas. Se diseñaron, se midieron, y **las
dos quedaron descartadas**; esta spec construye la tercera, elegida por el usuario el 2026-09-12.

---

## De dónde sale esta spec

La regla que esta feature quiere hacer cumplir es una sola:

> Un movimiento puede clasificarse con una categoría **predefinida** o con una categoría **propia de
> su dueño**, nunca con la categoría propia de otra cuenta.

Hoy esa regla **la hace cumplir la aplicación y no la base**: dos comprobaciones en
`MovimientosEndpoints` —una en el alta, otra en la edición— con su test cruzado
`Un_Movimiento_No_Puede_Apuntar_A_Una_Categoria_Ajena_FR021_SC009`. Están puestas, están probadas y
**no hay ninguna exposición abierta hoy**. Lo que falta es que la regla siga siendo cierta para la
escritura que no pasa por la aplicación: un script de mantenimiento, una importación, un arreglo a
mano en la base.

### Lo que se midió antes de escribir esta spec (2026-09-12)

La memoria del proyecto avisa que las premisas de las deudas anotadas se recopian de spec en spec
sin verificarse, y que tres de tres resultaron falsas. Ésta se verificó contra la base real antes de
planificar nada.

**La premisa es cierta.** El DDL de `movimiento` en `gestiongastos_test` tiene `FK → categoria(id)`,
`ck_movimiento_monto_positivo` y `ck_movimiento_nota_sin_cadena_vacia`, y nada más. No hay nada que
ate `categoria_id` con `usuario_id`.

**La forma exacta de la invariante es la que arruina los dos caminos que la deuda proponía**:

```
categoria.usuario_id IS NULL  OR  categoria.usuario_id = movimiento.usuario_id
```

Ese `IS NULL` son las diez predefinidas del sistema, que se ven desde todas las cuentas y no son de
ninguna. Una restricción declarativa no sabe decir "o nula".

| Camino que proponía D7-07 | Resultado medido | Por qué |
|---|---|---|
| `CHECK` de esquema | Descartado sin probar | La condición cruza dos tablas y un `CHECK` de MySQL no puede consultar otra tabla. Es el propio enunciado de la deuda |
| **Clave foránea compuesta** `(categoria_id, usuario_id) → categoria (id, usuario_id)` | **Descartado con evidencia** | Se montó y se probaron los tres casos. Rechaza la categoría ajena, **y también rechaza las diez predefinidas** (`ERROR 1452`): una fila padre con `NULL` en la clave referenciada no puede ser referenciada por nadie. No rompe el ataque: rompe el uso normal |
| **Trigger** `BEFORE INSERT` / `BEFORE UPDATE` | **Descartado con evidencia, y por el motivo peor** | Crearlo falla con `ERROR 1419: You do not have the SUPER privilege and binary logging is enabled`, incluso teniendo `ALL PRIVILEGES` sobre la base de tests. Sobre la base de desarrollo el usuario de la aplicación ni siquiera tiene el privilegio `TRIGGER`. Y `ci.yml` conecta **como `root`**: una migración con un trigger adentro se crearía sin chistar en CI, saldría verde, y explotaría recién al migrar en local o en producción. Es exactamente el verde que miente contra el que están escritas las siete barreras del repo |

**La conclusión del spike es que el problema no es la restricción: es la forma de los datos.**
Mientras "predefinida" signifique `usuario_id IS NULL`, ninguna restricción declarativa puede
expresar la regla. Si en cambio toda categoría tiene dueño, la regla se vuelve una clave foránea
compuesta corriente — la misma que acaba de fallar, funcionando, porque desaparece el `NULL` que la
rompía.

### Qué cambia, entonces

Las diez categorías predefinidas dejan de ser **diez filas compartidas por todo el mundo** y pasan a
ser **diez filas por cuenta**, entregadas al registrarse. Es un cambio de producto y no sólo de
esquema, y esa es la razón por la que esta deuda se salda con una feature.

### Lo que ya está construido y esta feature sólo modifica

| Lo que hace falta | Dónde está | Desde |
|---|---|---|
| La categoría con su dueño anulable, su baja lógica y su discriminador | `Dominio/Categoria.cs` | 006 (anticipo) / 007 |
| El canal único de lectura de categorías, con su acotado por ámbito | `Categorias/CategoriasConsulta.cs` | 007 / D7-05 |
| La barrera que vigila ese canal | `Integracion/BarreraDeAislamientoTests.cs` | 007 / rama `017` |
| Las dos comprobaciones de la invariante, con su test cruzado | `Movimientos/MovimientosEndpoints.cs`, `Integracion/AislamientoDeCategoriasTests.cs` | 007 |
| La semilla de las diez, en la migración | `Persistencia/GestionGastosDbContext.cs`, método `Sembrar` | 001 |
| El alta de cuenta | `Cuentas/CuentasEndpoints.cs` | 002 |
| La pantalla de gestión, que lista las predefinidas sin botones | `frontend/src/categorias/PantallaCategorias.tsx` | 007 |
| El campo `esPropia` del contrato | `frontend/src/api/tipos.ts`, `Categorias/CategoriaDto.cs` | 007 (D-07) |

### El volumen real de la migración de datos (medido el 2026-09-12, base `gestiongastos`)

| Qué | Cuánto |
|---|---|
| Cuentas existentes | 9 |
| Categorías predefinidas (compartidas) | 10 |
| Categorías propias ya creadas | 8 |
| Movimientos | 6 |
| **Movimientos que hoy apuntan a una predefinida** | **5** |

Son las que hay que reapuntar a la copia de su dueño. El volumen es chico, pero la migración tiene
que estar escrita para ser correcta a cualquier escala: no puede depender de que sean cinco.

---

## Clarifications

### Session 2026-09-12

- P: Las diez categorías que cada cuenta recibe al registrarse, ¿quedan editables o siguen
  intocables como las predefinidas de hoy? → R: **Totalmente editables.** Se pueden renombrar y dar
  de baja como cualquier otra categoría propia. Con eso **deja de haber dos clases de categoría**:
  desaparece el rechazo que `FR-008` de la 007 le aplicaba a las predefinidas, y el campo `esPropia`
  del contrato —que existía sólo para que la pantalla supiera qué fila no ofrecer botones— se
  elimina de las dos pilas por quedar siempre en verdadero.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Empezar a usar la app con un catálogo propio (Priority: P1) 🎯 MVP

Quien se registra encuentra, desde el primer movimiento que carga, las mismas diez categorías de
siempre —Comida, Transporte, Vivienda, Servicios, Salud, Ocio, Otros, Sueldo, Ingreso extra,
Otros—, y desde el principio son suyas.

**Why this priority**: Es la feature entera vista desde la persona que la usa. Sin esto, una cuenta
nueva nace sin catálogo y no puede cargar un solo movimiento: FR-005 de la 001 exige que todo
movimiento tenga categoría.

**Independent Test**: Registrar una cuenta nueva y pedir el catálogo: tienen que venir diez
categorías, siete de gasto y tres de ingreso, todas activas y todas marcadas como de esta cuenta.

**Acceptance Scenarios**:

1. **Given** que no existe ninguna cuenta con ese correo, **When** se registra una cuenta nueva,
   **Then** esa cuenta queda con diez categorías propias: siete de gasto y tres de ingreso, con los
   nombres del catálogo original.
2. **Given** una cuenta recién registrada, **When** se carga un movimiento con cualquiera de esas
   diez, **Then** el movimiento se crea con normalidad.
3. **Given** dos cuentas recién registradas, **When** cada una pide su catálogo, **Then** cada una
   recibe diez categorías **distintas** de las de la otra, aunque se llamen igual.
4. **Given** una cuenta recién registrada, **When** se intenta clasificar un movimiento con una
   categoría de la otra cuenta —aunque tenga el mismo nombre—, **Then** la petición se rechaza.

---

### User Story 2 - Que la regla la sostenga la base y no sólo el código (Priority: P1)

Un movimiento no puede quedar clasificado con la categoría de otra cuenta **ni siquiera escribiendo
directo en la base**, sin pasar por la aplicación.

**Why this priority**: Es el motivo por el que esta feature existe. Comparte P1 con la historia 1
porque una sin la otra no sirve: la restricción sólo puede existir una vez que toda categoría tiene
dueño, y el catálogo por cuenta sin la restricción deja la deuda igual de abierta que hoy.

**Independent Test**: Con SQL directo, intentar insertar un movimiento que apunte a la categoría de
otra cuenta. La base tiene que rechazarlo, sin que la aplicación intervenga.

**Acceptance Scenarios**:

1. **Given** dos cuentas con sus catálogos, **When** se inserta con SQL directo un movimiento de la
   cuenta A que apunta a una categoría de la cuenta B, **Then** la base rechaza la escritura.
2. **Given** un movimiento existente de la cuenta A, **When** se modifica con SQL directo su
   categoría por una de la cuenta B, **Then** la base rechaza la escritura.
3. **Given** una cuenta con su catálogo, **When** se inserta con SQL directo un movimiento que
   apunta a una categoría **propia de esa misma cuenta**, **Then** la escritura se acepta.
4. **Given** la restricción puesta, **When** se corre la suite completa, **Then** sigue en verde: la
   restricción no puede estar rechazando ningún caso legítimo.

---

### User Story 3 - No perder nada de lo ya cargado (Priority: P1)

Quien ya venía usando la app no nota el cambio: sus movimientos siguen clasificados con las mismas
categorías, con los mismos nombres, y sus totales y su desglose dan exactamente lo mismo que antes.

**Why this priority**: También P1, y por una razón distinta a las anteriores: es la única parte
irreversible. Una migración de datos mal escrita no se arregla con un parche en el código siguiente.

**Independent Test**: Tomar una base con cuentas, categorías propias y movimientos repartidos entre
predefinidas y propias; migrar; y comprobar que el listado, los totales y el desglose de cada cuenta
dan lo mismo antes y después, categoría por categoría.

**Acceptance Scenarios**:

1. **Given** una base con cuentas existentes, **When** se aplica la migración, **Then** cada cuenta
   queda con su copia de las diez, y ninguna categoría queda sin dueño.
2. **Given** un movimiento que apuntaba a una predefinida compartida, **When** se aplica la
   migración, **Then** queda apuntando a la copia de esa misma categoría perteneciente a **su
   propio dueño**, con el mismo nombre y el mismo tipo.
3. **Given** las categorías propias que una cuenta ya había creado, **When** se aplica la migración,
   **Then** quedan intactas: mismo identificador, mismo nombre, mismo estado de baja.
4. **Given** un mes ya cerrado con su resumen y su desglose, **When** se aplica la migración,
   **Then** el total ingresado, el total gastado, el balance y el desglose por categoría dan los
   mismos números que antes.
5. **Given** una cuenta que había dado de baja una categoría propia, **When** se aplica la
   migración, **Then** esa categoría sigue dada de baja y sus movimientos siguen contando.

---

### User Story 4 - Un catálogo que ahora es enteramente mío (Priority: P2)

En la pantalla de gestión, las diez categorías entregadas al registrarse se comportan como
cualquier otra categoría propia.

**Why this priority**: P2 porque es consecuencia del cambio, no su motivo, y porque la app es usable
sin ella: si las copias quedaran de solo lectura, las tres historias anteriores igual se cumplen.
Pero es la parte que la persona **ve**, y la que decide si el cambio mejora el producto o sólo mueve
una restricción de lugar.

**Independent Test**: Entrar a la pantalla de gestión con una cuenta recién registrada y comprobar
que cada una de las diez ofrece renombrar y dar de baja, igual que una creada a mano.

**Acceptance Scenarios**:

1. **Given** una cuenta recién registrada, **When** se abre la pantalla de gestión, **Then** se ven
   las diez con el mismo tratamiento visual que las creadas a mano.
2. **Given** una de las diez entregadas, **When** se la renombra, **Then** el cambio se acepta y los
   movimientos ya clasificados con ella conservan su clasificación con el nombre nuevo.
3. **Given** una de las diez entregadas, **When** se le da de baja, **Then** deja de ofrecerse en el
   formulario y sus movimientos siguen contando en los totales y en el desglose.

---

### Edge Cases

- **El alta de cuenta falla después de crear las categorías.** La cuenta y su catálogo tienen que
  nacer o no nacer juntos: una cuenta sin catálogo no puede cargar un movimiento, y un catálogo sin
  cuenta son diez filas huérfanas que la restricción nueva ni siquiera admite.
- **Dos cuentas con categorías homónimas.** "Comida" existe ahora tantas veces como cuentas haya. La
  unicidad tiene que seguir siendo por ámbito, no global.
- **Una cuenta renombra su copia de "Comida" a "Supermercado" y después crea una nueva llamada
  "Comida".** Tiene que poder: el nombre quedó libre.
- **Una cuenta da de baja su copia de "Otros" (gasto) y crea otra con ese nombre.** Es FR-009 de la
  007 y tiene que seguir andando igual: el discriminador de la baja es lo que lo permite.
- **La migración corre dos veces.** Tiene que ser idempotente o fallar limpio, nunca dejar veinte
  categorías por cuenta.
- **Una base sin ninguna cuenta.** La migración tiene que dejarla consistente: sin cuentas, no queda
  ninguna categoría, y la primera que se registre recibirá las suyas.
- **Un movimiento que apunta a una predefinida cuyo dueño no existe.** No puede pasar hoy, pero si
  pasara, la migración tiene que fallar ruidosamente en vez de dejarlo sin categoría.

## Requirements *(mandatory)*

### Functional Requirements

#### El catálogo pasa a ser por cuenta

- **FR-001**: Toda categoría DEBE tener dueño. El sistema NO DEBE admitir categorías sin cuenta
  propietaria.
- **FR-002**: El sistema DEBE entregarle a cada cuenta nueva, en el momento de registrarse, su
  propio juego de las diez categorías iniciales: siete de gasto —Comida, Transporte, Vivienda,
  Servicios, Salud, Ocio, Otros— y tres de ingreso —Sueldo, Ingreso extra, Otros—, todas activas.
- **FR-003**: La creación de la cuenta y la entrega de su catálogo DEBEN ser una sola operación
  indivisible: o quedan las dos o no queda ninguna.
- **FR-004**: El sistema DEBE seguir ofreciéndole a cada cuenta únicamente las categorías activas de
  su propio ámbito, sin que ninguna cuenta vea las de otra.
- **FR-005**: El sistema DEBE seguir aceptando que dos cuentas distintas tengan categorías con el
  mismo nombre y el mismo tipo, y DEBE seguir rechazando dos activas con el mismo nombre y tipo
  dentro de una misma cuenta.

#### La invariante pasa a la base

- **FR-006**: La base de datos DEBE rechazar por sí misma toda escritura que deje un movimiento
  clasificado con una categoría que no pertenece a su dueño, tanto en la creación como en la
  modificación, y tanto si la escritura viene de la aplicación como si no.
- **FR-007**: El sistema DEBE conservar las comprobaciones de aplicación que hoy rechazan esa misma
  situación. La restricción de la base es la red, no el reemplazo: quien usa la aplicación tiene que
  seguir recibiendo el error de validación con su campo, y no el fallo crudo de una restricción.
- **FR-008**: El sistema DEBE seguir rechazando el alta de un movimiento con una categoría dada de
  baja, y DEBE seguir aceptando la edición de un movimiento que conserva la categoría dada de baja
  que ya tenía.
- **FR-009**: La suite DEBE cubrir con al menos un caso de escritura directa a la base —sin pasar
  por la aplicación— el rechazo de FR-006, y con al menos un caso el hecho de que una categoría del
  propio dueño sí se acepta.

#### La migración de lo ya cargado

- **FR-010**: El sistema DEBE convertir cada categoría predefinida compartida en una copia por
  cuenta existente, conservando nombre, tipo y estado.
- **FR-011**: El sistema DEBE reapuntar cada movimiento que hoy usa una categoría predefinida
  compartida a la copia equivalente perteneciente a su propio dueño.
- **FR-012**: El sistema NO DEBE alterar ningún total ingresado, total gastado, balance ni desglose
  por categoría de ningún período, para ninguna cuenta.
- **FR-013**: El sistema NO DEBE modificar las categorías propias ya existentes: conservan su
  identificador, su nombre, su tipo y su estado de baja.
- **FR-014**: La migración NO DEBE dejar ninguna categoría sin dueño ni ningún movimiento sin
  categoría; si no puede garantizarlo, DEBE fallar en vez de completarse a medias.

#### Lo que la persona ve

- **FR-015**: El sistema DEBE permitir renombrar y dar de baja cualquiera de las diez categorías
  entregadas al registrarse, con las mismas reglas que rigen para una categoría creada a mano.
- **FR-016**: El sistema NO DEBE conservar ninguna distinción entre categorías "del sistema" y
  categorías propias: desaparece el rechazo que la 007 aplicaba a la modificación y la eliminación
  de una predefinida, y desaparece del contrato el campo que marcaba esa diferencia.
- **FR-017**: La pantalla de gestión DEBE ofrecer renombrar y dar de baja sobre **todas** las filas
  del catálogo, sin excepciones.
- **FR-018**: Todo cambio en la forma de una petición o una respuesta DEBE quedar reflejado en la
  definición del contrato que comparten las dos pilas.

#### La barrera de la restricción nueva

- **FR-019**: El proyecto DEBE contar con una verificación ejecutable que compruebe que la
  comprobación de `FR-006` **sabe ponerse en rojo**: desarma la restricción, exige el fallo,
  la restaura y exige el verde. Sin eso, un test que afirma "la base rechaza esto" informa verde
  tanto cuando la restricción está puesta como cuando el test dejó de verificarla, y las dos cosas
  se ven igual. Es el Principio V de la constitución, y el precedente del repo es unánime: la nota
  y el código de moneda trajeron cada uno el suyo.

### Key Entities

- **Categoría**: pasa de tener dueño *opcional* a tener dueño *obligatorio*. Sigue teniendo nombre,
  tipo (gasto o ingreso), estado de baja y el discriminador que le permite a un nombre dado de baja
  liberar su casillero. Lo que desaparece es la categoría sin dueño y, con ella, la categoría de solo lectura: toda fila
  del catálogo se puede renombrar y dar de baja.
- **Movimiento**: no cambia de forma. Lo que cambia es que su pareja (dueño, categoría) pasa a estar
  verificada por la base.
- **Cuenta**: gana una consecuencia al nacer — su catálogo inicial.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una cuenta recién registrada puede cargar su primer movimiento sin crear ninguna
  categoría a mano, con las mismas diez opciones que antes del cambio.
- **SC-002**: El 100% de los intentos de clasificar un movimiento con una categoría de otra cuenta
  se rechaza, tanto desde la aplicación como escribiendo directo en la base.
- **SC-003**: Para toda cuenta y todo período, el total ingresado, el total gastado, el balance y el
  desglose por categoría dan exactamente los mismos números antes y después de la migración.
- **SC-004**: Ninguna categoría queda sin dueño y ningún movimiento queda sin categoría después de
  la migración, verificado sobre la base migrada.
- **SC-005**: La suite completa y las siete barreras existentes quedan en verde, y **ninguna se
  afloja** para que esta feature pase. Agregar un archivo a la lista de autorizados de una barrera
  —con nombre propio y el motivo escrito— es mantenimiento y está permitido: la barrera sigue
  saltando ante el archivo siguiente. Relajar el criterio con el que la barrera decide no lo está:
  una excepción genérica, un archivo eximido sin decir por qué o un patrón ampliado para que el
  código nuevo entre cuentan como barrera desarmada.
- **SC-006**: Una cuenta nueva y una cuenta migrada son indistinguibles en comportamiento: las dos
  ven diez categorías propias y ninguna ajena.
- **SC-007**: Ninguna fila del catálogo de una cuenta rechaza un renombre o una baja por ser "del
  sistema": no queda ninguna categoría de solo lectura en la aplicación.

## Assumptions

- **No hay datos en producción.** El único despliegue conocido es la base de desarrollo local, con 9
  cuentas, 18 categorías y 6 movimientos, medidos el 2026-09-12. La migración igual se escribe para
  ser correcta a cualquier escala, pero no se diseña una ventana de mantenimiento ni un plan de
  reversión en caliente.
- **Los nombres del catálogo inicial no cambian.** Son los mismos diez de FR-006 de la 001; esta
  feature cambia a quién pertenecen, no cuáles son.
- **El catálogo inicial se entrega una sola vez, al registrarse.** No se resincroniza después: si
  más adelante se agregara una categoría al catálogo inicial, las cuentas ya existentes no la
  reciben. Eso queda fuera de alcance y se anota como deuda si alguna vez hace falta.
- **La restricción nueva se despliega con las credenciales de la aplicación.** Después de lo que
  midió el spike, cualquier forma que exija privilegios que el usuario de la aplicación no tiene
  queda descartada por definición, aunque funcione en CI.
- **Las comprobaciones de aplicación no se tocan.** Siguen siendo las que producen el error de
  validación que ve la persona; la base sólo agrega el piso.

## Deuda que esta feature salda

| Deuda | De dónde viene | Cómo queda |
|---|---|---|
| **D7-07** | Feature 007, anotada al saldar D7-05 (PR #38) | Saldada, pero **no por el camino que la fila proponía**. Los dos que proponía se midieron y se descartaron; la fila hay que reescribirla con lo que este spike encontró, aunque el resultado final sea el que pedía: la invariante en la base |

## Dependencies

- Depende de la feature **007** (categorías propias), que introdujo el dueño de la categoría, el
  canal único de lectura y las dos comprobaciones que esta feature conserva.
- Depende de la feature **002** (identidad y sesión), que es la que crea las cuentas y por lo tanto
  el punto donde se entrega el catálogo.
- Toca la semilla que introdujo la feature **001**, que hasta ahora vivía entera en la migración
  inicial.

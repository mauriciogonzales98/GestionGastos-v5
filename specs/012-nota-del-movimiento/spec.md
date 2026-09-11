# Feature Specification: Nota descriptiva del movimiento

**Feature Branch**: `012-nota-del-movimiento`

**Created**: 2026-09-10

**Status**: Draft

**Input**: Ticket DISC-001-02 — "Nota descriptiva del movimiento"
(`plan-de-implementacion/prds/pendientes/prd-DISC-001-02.md`), el **último ticket pendiente** del
plan DISC-001 y el único que quedaba sin dependencias. Sus `FR-01` a `FR-05`, `NFR-01` a `NFR-03` y
`AC-01` a `AC-10` se respetan sin reinterpretarlos, y su *Out of Scope* se respeta tal cual está.

---

## De dónde sale esta spec

Las features 008 a 011 aprendieron, a fuerza de encontrarse el trabajo ya hecho, que el PRD hay que
verificarlo contra el código antes de planificar. Esta spec empieza igual: **verificado contra el
código el 2026-09-10**, sobre `main` con la feature 011 ya mergeada (PR #28).

**El resultado de la verificación es, por primera vez en cinco features, que no hay nada hecho.** La
nota no existe en ninguna de las dos pilas: no hay columna en la base, ni propiedad en el dominio, ni
campo en ninguno de los tres DTO, ni control en el formulario, ni columna en el listado, ni test de
rendimiento del listado. La búsqueda de `nota` y `descripcion` en `backend/GestionGastos.Api` y
`frontend/src` no devuelve un solo resultado de producto.

Eso invierte el reparto de las features anteriores. La 010 encontró el backend entero y el frontend
en cero; la 011 lo mismo. Ésta construye **las dos mitades**, y es la primera desde la 007 que
**abre una migración**.

### Lo que ya está construido y esta feature sólo extiende

| Lo que hace falta | Dónde está | Desde |
|---|---|---|
| El modelo del movimiento, con su ciclo de vida completo | `Dominio/Movimiento.cs` | FEAT-001a / FEAT-001b |
| Las tres formas del contrato del movimiento | `Movimientos/MovimientoDtos.cs`: `NuevoMovimientoDto`, `MovimientoDto`, `MovimientoEditadoDto` | FEAT-001a / FEAT-001b |
| **Una sola** validación para el alta y la edición, con la clave del error = nombre del campo | `Movimientos/ValidacionDelMovimiento.cs` | FEAT-001b |
| **Un solo** juego de campos para el alta y la edición | `frontend/src/movimientos/CamposDelMovimiento.tsx` (D-08 de la 009) | 009 |
| La tripleta `label` + `aria-invalid` + `aria-describedby` armada en un solo lugar | `frontend/src/ui/CampoConError.tsx` | 011 |
| El envoltorio desplazable de la tabla del listado, que es lo que permite una columna más a 360 px | `c-listado-movimientos__desborde` en `estilos/componentes.css` | 011 |
| El piso de accesibilidad verificado sobre la aplicación entera | `tests/Accesibilidad.test.tsx`, `ClasesConRegla`, `AnchoDeLasPantallas`, `Paleta` | 011 |
| La barrera que pone en rojo un contrato desalineado | `backend/verificar-contrato.sh`, `Contrato/ContratoMovimientosTests.cs` | FEAT-003 |
| El sembrado de volumen para medir con 1000 filas | `Rendimiento/SembradoDeRendimiento.cs` | FEAT-001c |

### Lo que falta de verdad

Todo lo que nombra la nota, en las dos pilas, más la migración que agrega su columna. Y un test de
rendimiento que **no existe**: hay `RendimientoAltaTests`, `RendimientoResumenTests` y
`RendimientoLimiteTests`, pero **ninguno mide el listado**. `AC-10` del PRD lo pide y es la primera
vez que alguien lo pide.

### La decisión de producto que no se discute

La nota es **descriptiva, no clasificatoria**. No se busca, no se filtra, no se agrupa, no entra en
ningún total ni en el desglose por categoría, y no aparece en el resumen ni en el dashboard. Viene
decidido en `PRD:RF-33` y el PRD del ticket explica el riesgo que la motiva: una nota libre que se
pudiera filtrar se vuelve una segunda taxonomía informal —"alquiler", "Alquiler", "alq"— que el
sistema no entiende y que da una falsa sensación de estar clasificando. **La categoría sigue siendo
el único eje de análisis.**

Esta spec la respeta de la forma más fuerte posible: no agrega la nota a ningún acotado del listado
aunque la barra de filtros exista desde la 011 y sumarle un campo de texto costaría poco. Ése es
exactamente el contrabando que hay que no hacer.

---

## Clarifications

### Session 2026-09-10

- P: ¿Esta feature salda **D11-02** —el `CHECK` sobre `moneda.codigo` para exigir tres letras—,
  aprovechando que es el primer ticket desde la 007 que abre una migración?
  → R: **Sí, en la misma migración.** La deuda viene de la 009 (como D9-09), pasó por la 010 (D10-03)
  y por la 011 (D11-02), y en las tres el motivo de no saldarla fue el mismo: *no había migración
  abierta*. Ésta la abre. Queda como `FR-010` y `AC-13`, con su alcance acotado y explícito: la
  restricción va al esquema, **no** se agrega validación de aplicación sobre el catálogo, porque el
  catálogo se administra como dato y nadie lo escribe desde la aplicación (`PRD:RF-32`). El `char(3)`
  que ya existe acota el largo; lo que falta y esto agrega es que sean **letras**.
- P: ¿Cómo se muestra la nota en el listado, que ya tiene seis columnas y tiene que funcionar a
  360 px?
  → R: **Como una séptima columna de la tabla**, con el texto acotado visualmente y el contenido
  completo siempre presente en el DOM. La alternativa —una segunda línea dentro de la fila— rompería
  la regularidad de la tabla, que es lo que hace que se lea con un lector de pantalla, y el
  envoltorio desplazable que la 011 dejó puesto (`c-listado-movimientos__desborde`) existe
  precisamente para que una columna más no desborde la página. Queda como `FR-006` y `NFR-004`.
- P: La nota se tiene que poder **vaciar** al editar (`FR-04` del PRD), pero `MovimientoEditado`
  declara desde la 009 la regla "**ausente nunca produce un cambio que nadie pidió**" —`monedaId`
  ausente significa "la que ya tenía"—. ¿Cómo se vacía entonces, sin romper esa regla?
  → R: **`nota` es obligatoria en la edición, como `fecha`.** Es la misma excepción y por el mismo
  motivo: `fecha` es obligatoria al editar porque ahí *ausente* significaría un valor nuevo —hoy— y
  el movimiento saltaría de fecha en silencio. Con la nota pasa lo simétrico: si *ausente* valiera
  "la que ya tenía", no habría forma de vaciarla sin inventar un valor centinela; si valiera "sin
  nota", un cliente que no la mande borraría en silencio lo que la persona escribió. Exigirla saca
  las dos trampas: `""` y `null` significan **sin nota**, y los dos son explícitos. Queda como
  `FR-004` y `AC-06`.
- P: ¿En qué unidad se cuentan los 120 caracteres? MySQL cuenta caracteres Unicode y tanto C# como
  JavaScript cuentan por omisión unidades UTF-16, donde un emoji vale 2.
  → R: **Caracteres Unicode (code points)**, que es lo que `varchar(120)` ya cuenta. Es la única
  unidad en la que las tres capas acuerdan qué significa 120 sin que ninguna tenga que convertir
  nada, y contarla así cuesta una línea en cada pila. **Ninguna de las dos validaciones usa el largo
  "natural" de la cadena**: en UTF-16 un emoji vale 2, así que a quien escribe emoji se le cortaría
  a mitad del límite que el mensaje le promete. Queda como `FR-003` y `AC-04`.
- P: Cuando un movimiento no tiene nota, ¿qué se guarda: `NULL` o la cadena vacía?
  → R: **Las dos se admiten**, y el esquema no elige una. La nota es la primera columna de texto
  anulable del proyecto —todas las de hoy son `IsRequired()`— y no se normaliza al escribir.
  **Eso mueve la invariante de `FR-005` del esquema a la lectura**: "sin nota" sigue siendo un solo
  estado *para quien lo mira*, pero ya no por construcción, así que todo camino que muestre una nota
  DEBE tratar `NULL` y `''` de la misma forma, y un test DEBE fijarlo. Sin ese test, dos movimientos
  que se ven idénticos en la pantalla pasan a ser distinguibles según cómo se guardaron, que es
  precisamente lo que `FR-005` no quiere. Queda como `FR-005`, `FR-011` y `AC-02`, con su costo
  anotado en D12-08. **Revertido en la rama `014`**: el esquema sí elige una —la ausencia de valor—
  y la cadena vacía pasa a ser irrepresentable (`FR-014`). Ver la nota de cierre al pie.
- P: ¿El campo de la nota es una entrada de una sola línea o un área de texto de varias?
  → R: **Un área de texto de varias líneas.** Con 120 caracteres, ver la nota entera sin desplazar
  vale más que la comodidad de tipearla en una línea. Tres consecuencias, todas encodeadas y ninguna
  descubierta después: **(1)** `PRD:AC-55` **no se rompe** —el recorrido con Tab hasta el botón y el
  Enter sobre el botón siguen funcionando igual, que es exactamente lo que ese AC pide y lo que su
  test verifica—, pero el test enumera el orden de tabulación y por lo tanto **se pone en rojo con el
  control nuevo**, que es su propósito declarado: extenderlo es parte del trabajo, no un daño
  colateral. **(2)** Lo que sí deja de ser cierto es el comentario de `CamposDelMovimiento.tsx` que
  afirma que el envío con Enter sale *de cualquier campo*: dentro de un área de texto Enter inserta un
  salto. El comentario se corrige, porque un comentario que describe otra cosa es peor que ninguno.
  **(3)** Los saltos de línea pasan a ser representables en el dato, y `FR-012` dice qué se hace con
  ellos. Queda como `FR-001`, `FR-012` y `FR-013`.
- P: ¿Qué devuelve la API en el campo de la nota cuando el movimiento no tiene una?
  → R: **El campo viaja siempre, y "sin nota" es la cadena vacía.** La lectura **normaliza**: una fila
  guardada sin valor sale como `""`, así que la dualidad que el almacenamiento admite **se detiene en
  el borde de la API** y no llega a la pantalla. Con esto `FR-011` se cumple en un solo lugar —el
  único por el que pasan todas las lecturas— en vez de repartido por cada consumidor, ninguna
  respuesta lleva nulos (hoy `MovimientoDto` no tiene ninguno) y el listado muestra la nota sin un
  solo condicional, porque una cadena vacía no pinta nada. Queda como `FR-009` y `FR-011`.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Anotar en qué gasté (Priority: P1) 🎯 MVP

Cargo un gasto de transporte por $8.500 y escribo "viaje al aeropuerto" en un campo de nota. Lo veo
en el listado al lado de ese movimiento, y tres días después sé cuál de mis gastos de transporte fue.
La otra mitad del valor es que **no tengo que escribir nada**: el movimiento siguiente lo cargo sin
tocar ese campo y se registra igual.

**Why this priority**: Es el ticket entero. `PRD:RF-33` existe porque "Transporte, $8.500" no
distingue el viaje al aeropuerto de la carga de la SUBE, y sin escribir y leer la nota no hay nada
entregado. Es además la historia que se puede demostrar sola: alta más listado, sin tocar la edición.

**Independent Test**: Se prueba entera registrando dos movimientos —uno con nota y uno sin— y
mirando el listado: aparece el texto del primero, y el segundo se ve sin relleno y sin error.
Entrega valor sin la US2: quien se equivoca al escribir la nota puede, hasta que exista la US2,
borrar el movimiento y cargarlo de nuevo.

**Acceptance Scenarios**:

1. **Given** el formulario de registro abierto, **When** completo monto, categoría y una nota y
   guardo, **Then** el movimiento queda registrado con esa nota y el listado la muestra junto a ese
   movimiento (`PRD:AC-01`)
2. **Given** el formulario de registro abierto, **When** guardo con el campo de nota vacío,
   **Then** el movimiento queda registrado sin nota y el listado lo muestra sin texto de relleno y
   sin ningún error (`PRD:AC-02`)
3. **Given** el formulario de registro abierto, **When** lo miro por primera vez, **Then** el campo
   de nota se presenta como opcional y puedo guardar sin haberlo tocado (`PRD:AC-09`)
4. **Given** una nota de exactamente 120 caracteres, **When** guardo, **Then** se acepta y el
   listado muestra los 120 caracteres (`PRD:AC-04`)
5. **Given** una nota de 121 caracteres, **When** intento guardar, **Then** se rechaza, el motivo
   aparece **al lado del campo de la nota** y no se crea ningún movimiento (`PRD:AC-03`)
6. **Given** una nota que contiene `<b>hola</b>` o `-- DROP`, **When** la guardo y miro el listado,
   **Then** veo exactamente esos caracteres, sin interpretar nada como marcado y sin ejecutar nada
   (`PRD:AC-08`)

---

### User Story 2 - Corregir o borrar lo que anoté (Priority: P2)

Escribí "viaje al areopuerto" con un error, o anoté algo que resultó no importar. Abro la ventana de
edición de ese movimiento, corrijo el texto o lo borro entero, guardo, y el listado muestra lo nuevo
—o nada, si lo borré— sin rastro de lo anterior.

**Why this priority**: `FR-04` del PRD, que traza además a `PRD:RF-14`. Es necesaria para que la nota
sea utilizable de verdad —un texto libre que no se puede corregir se abandona—, pero es posterior a
la US1: sin poder escribirla, no hay nada que corregir.

**Independent Test**: Se prueba sobre un movimiento que ya tiene nota, editándola y después
vaciándola, y verificando el listado después de cada guardado. Depende de la US1 sólo para tener un
movimiento con nota; se puede sembrar.

**Acceptance Scenarios**:

1. **Given** un movimiento propio con nota, **When** la modifico y guardo, **Then** el listado
   muestra el valor nuevo y no muestra el anterior (`PRD:AC-05`)
2. **Given** un movimiento propio con nota, **When** la borro por completo y guardo, **Then** ese
   movimiento queda sin nota y el listado lo muestra sin texto de relleno (`PRD:AC-06`)
3. **Given** la ventana de edición abierta sobre un movimiento con nota, **When** la miro, **Then**
   el campo trae la nota que el movimiento ya tenía, no vacío
4. **Given** una nota de 121 caracteres en la ventana de edición, **When** intento guardar, **Then**
   se rechaza con el motivo al lado del campo y el movimiento **no se altera en nada** —ni la nota,
   ni el monto, ni la categoría, ni la fecha (`PRD:AC-03`)
5. **Given** un movimiento al que le agrego, le cambio o le borro la nota, **When** mido el resumen
   antes y después, **Then** el total ingresado, el total gastado, el balance y el desglose por
   categoría quedan con los mismos valores (`PRD:AC-07`)

---

### User Story 3 - El catálogo de monedas no acepta un código que no es un código (Priority: P3)

No la ve nadie. Es la deuda **D11-02**, que viene esperando desde la feature 009 a que un ticket
abra una migración por otro motivo, y éste la abre: la columna `moneda.codigo` pasa a exigir en el
esquema que sean tres letras, de modo que un `12 ` o un `a1b` metido con SQL puro sea rechazado por
la base.

**Why this priority**: Va última porque es independiente del producto de esta feature y no bloquea
nada de lo anterior; si hubiera que recortar alcance, es lo que se recorta. Va **igual** porque el
costo de sumarla a una migración que ya se está escribiendo es casi cero, y el de abrir una
migración sólo para ella es lo que la dejó esperando tres features seguidas.

**Independent Test**: Se prueba con un `INSERT` directo de un código inválido contra el esquema
migrado, exigiendo que la base lo rechace. No toca ninguna pantalla ni ningún endpoint.

**Acceptance Scenarios**:

1. **Given** el esquema migrado, **When** se intenta escribir una moneda con código `1X2` o `ab1`,
   **Then** la base lo rechaza
2. **Given** el esquema migrado, **When** se escribe una moneda con código `EUR`, **Then** se acepta
3. **Given** el catálogo sembrado que ya existe, **When** se aplica la migración, **Then** ninguna
   fila existente la hace fallar

---

### Edge Cases

- **Una nota que es sólo espacios.** Se trata como vacía: se guarda sin nota. Guardar `"   "`
  produciría un movimiento que se ve sin nota y que sin embargo tiene una, y dos movimientos
  indistinguibles en la pantalla con distinto contenido en la base.
- **Espacios al principio y al final de una nota con texto.** Se recortan antes de guardar, por el
  mismo motivo: el valor guardado es el que se ve.
- **Una nota de 120 caracteres visibles más espacios alrededor.** El límite se mide sobre el valor
  ya recortado, así que se acepta. Medirlo antes de recortar rechazaría algo que, una vez guardado,
  entra.
- **Una nota con acentos, eñes o emoji.** Cada uno cuenta **uno**, porque el límite se mide en
  caracteres Unicode: 120 emoji es una nota válida. Es el caso que separa esta decisión de la fácil —
  contando unidades UTF-16, esos mismos 120 emoji se rechazarían por "superar 120" cuando la persona
  escribió exactamente 120. Los dos lados tienen que aceptar y rechazar exactamente el mismo
  conjunto de notas, o la pantalla deja pasar algo que el servidor rechaza con un mensaje que la
  persona no puede entender.
- **Una nota escrita en varias líneas.** Se guarda con sus saltos y se lee en el listado en una sola
  línea visual, sin que el salto agregue ni quite nada al texto (`FR-012`). Al volver a abrir la
  ventana de edición, el campo la trae con sus saltos intactos: lo guardado es lo que se escribió.
- **Una nota larga en el listado a 360 px.** La columna no puede empujar la página: el envoltorio
  desplazable que la 011 dejó puesto es el que absorbe el ancho. El texto completo queda en el DOM,
  así que un lector de pantalla lo lee entero aunque visualmente esté acotado.
- **Dos movimientos sin nota guardados de las dos formas posibles** —uno sin valor y uno con la
  cadena vacía—. La API devuelve lo mismo para los dos, así que se ven **idénticos** en el listado:
  los dos sin texto de relleno y sin error. Es el caso que `FR-011` existe para fijar, y el único que
  el esquema ya no impide por construcción.
- **Un movimiento registrado antes de esta feature.** No tiene nota, y se muestra igual que uno
  guardado sin nota: sin relleno y sin error. Es el caso que cubre toda la base existente.
- **Una nota que llega con más de 120 caracteres directo a la API**, sin pasar por la pantalla. Se
  rechaza con su clave de campo: la validación de la pantalla es comodidad, no la barrera.
- **La nota de un movimiento de otra cuenta.** No se expone, por la misma vía que todo lo demás: el
  movimiento ajeno no se lee. No es un caso nuevo, y eso hay que verificarlo y no suponerlo — la
  nota es el primer campo de texto libre que el aislamiento tiene que tapar.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Un movimiento PUEDE llevar una nota de texto libre de hasta 120 caracteres, y el campo
  es **opcional**: se tiene que poder registrar un movimiento sin tocarlo (`PRD:FR-01`, `PRD:AC-09`).
- **FR-002**: La nota se escribe al registrar un movimiento y viaja con él (`PRD:FR-01`).
- **FR-003**: El sistema DEBE rechazar el alta y la modificación de un movimiento cuya nota supere
  los 120 **caracteres Unicode** (code points, no unidades UTF-16), **indicando el motivo al lado del
  campo de la nota** y sin crear ni alterar nada (`PRD:FR-03`, `PRD:AC-03`). El límite se aplica en el
  servidor, que es la barrera, y se adelanta en la pantalla, que es comodidad, y **las dos cuentan en
  la misma unidad que el esquema**: las tres capas tienen que acordar qué significa 120 (Clarifications).
- **FR-004**: Se DEBE poder modificar y **vaciar** la nota de un movimiento propio ya registrado
  (`PRD:FR-04`). En la modificación la nota es un valor **obligatorio del contrato**, igual que la
  fecha: sin nota se expresa explícitamente, nunca por omisión (Clarifications).
- **FR-005**: Un movimiento enviado con la nota vacía queda **sin nota**, y el listado lo muestra sin
  texto de relleno y sin ningún error (`PRD:FR-05`, `PRD:AC-02`). Una nota de sólo espacios es una
  nota vacía, y los espacios de los extremos se recortan (Edge Cases). **"Sin nota" es un solo estado
  para quien lo mira y, desde `FR-014`, también un solo estado en el almacenamiento.** Cuando esta
  spec se escribió el esquema admitía dos formas de representarlo y lo que sostenía la invariante era
  la lectura (`FR-011`); eso era la deuda D12-08, saldada en la rama `014`.
- **FR-006**: El listado DEBE mostrar la nota de cada movimiento **junto al movimiento al que
  pertenece**, como una columna más de la tabla, con el texto completo presente para quien lo lee con
  un lector de pantalla (`PRD:FR-02`, Clarifications).
- **FR-007**: La nota NO participa de ningún acotado, orden, agrupamiento ni total. No se busca por
  ella, no se filtra por ella y no aparece en el resumen ni en el dashboard (`PRD:RF-33`, *Out of
  Scope* del PRD).
- **FR-008**: El campo de la nota DEBE tener nombre accesible, y su error DEBE quedar asociado al
  campo por la misma vía que todos los demás campos del formulario, no como un cartel suelto
  (piso de la feature 011).
- **FR-009**: El contrato de las tres formas del movimiento —lo que se manda al registrar, lo que se
  manda al modificar y lo que se devuelve— DEBE declarar la nota, y la verificación del contrato
  DEBE ponerse en rojo si una de las dos pilas la declara y la otra no. En **lo que se devuelve** el
  campo viaja **siempre** y nunca es nulo: "sin nota" es la cadena vacía (Clarifications).
- **FR-010**: El esquema DEBE rechazar un código de moneda que no sean **tres letras**. Es la deuda
  **D11-02**, saldada en la migración que esta feature abre por otro motivo. **No** se agrega
  validación de aplicación sobre el catálogo: el catálogo se administra como dato y nadie lo escribe
  desde la aplicación (`PRD:RF-32`, Clarifications).
- **FR-011**: La **lectura de la API** DEBE devolver siempre la cadena vacía cuando el movimiento no
  tiene nota —nunca un nulo—, y eso DEBE estar fijado por un test y no confiado a la disciplina de
  quien escriba la próxima consulta. Está puesto **en el borde de la API a propósito**: es el único
  punto por el que pasan todas las lecturas, así que la pantalla recibe una sola forma de "sin nota".
  **Perdió una mitad al saldarse D12-08**: hasta `FR-014` este requisito era además lo único que
  mantenía en pie `FR-005`, porque el esquema admitía dos representaciones y esta lectura las
  igualaba. Ahora la cadena vacía no es representable en la tabla, esa mitad la garantiza el
  almacenamiento, y lo que queda acá es el contrato de `FR-009`: el campo viaja siempre y nunca es
  nulo.
- **FR-012**: La nota se escribe en un control de **varias líneas**, así que los saltos de línea son
  representables y **se conservan tal como se escribieron**. En el listado la nota se muestra en una
  sola línea visual: el salto **no tiene significado de presentación**, porque el formato dentro de la
  nota está fuera de alcance (D12-06). Conservarlo sin darle significado es la única combinación que
  no miente — transformar el texto al guardarlo cambiaría en silencio lo que la persona escribió, y
  darle significado sería construir el formato que el PRD excluye.
- **FR-013**: El salto de línea **cuenta como un carácter** para el límite de `FR-003`, igual que
  cualquier otro. No se descuenta ni se trata de forma especial.
- **FR-014**: El **esquema** DEBE admitir una sola representación de "sin nota" —la ausencia de
  valor— rechazando la cadena vacía. Salda la deuda **D12-08** y va en el esquema y no en el código
  a propósito: la aplicación ya normalizaba al escribir desde esta feature
  (`ValidacionDelMovimiento.NotaNormalizada`), así que lo que faltaba no era normalizar sino
  **garantizar**. El camino por el que el segundo estado podía entrar es justamente el que no pasa
  por la aplicación, y una restricción de esquema es lo único que lo alcanza.

### Non-Functional Requirements

- **NFR-001**: La nota se muestra como **texto plano en el 100 % de los casos**, sin interpretar
  ninguna de sus secuencias como marcado y sin ejecutar contenido a partir de ella (`PRD:NFR-01`).
  Es la única entrada de texto libre de la aplicación y va a parar al listado. Esto tiene que estar
  **verificado con un test y no asumido**: el escape por omisión de la pantalla hace que el riesgo
  real sea que alguien lo desactive a propósito "para que se vea mejor", y el test existe para que
  eso rompa en rojo.
- **NFR-002**: Los totales, el balance y el desglose por categoría del resumen quedan **sin variación
  alguna** ante cualquier valor de la nota, incluido el vacío (`PRD:NFR-02`).
- **NFR-003**: El listado sigue cargando en **menos de 2 s en el percentil 95** sobre una cuenta con
  1000 movimientos, con la nota incluida en cada fila, medido sobre 100 ejecuciones (`PRD:NFR-03`,
  `PRD:AC-10`, `PRD:RNF-01`).
- **NFR-004**: La columna nueva **no hace desbordar la página a 360 px** y no rompe ninguno de los
  verificadores que la feature 011 dejó puestos: clases con regla, anchos declarados, contraste de
  la paleta y nombres accesibles de todos los controles.
- **NFR-005**: **No entra ninguna dependencia nueva** en ninguna de las dos pilas. Es la restricción
  que la 011 fijó y verificó, y la nota no necesita nada que el proyecto no tenga.

### Key Entities

- **Movimiento**: suma un atributo, la **nota** — texto libre opcional de varias líneas, de hasta 120
  caracteres Unicode, **descriptivo y no clasificatorio**. Ausente es un estado normal y no un dato
  faltante: la mayoría de los movimientos no va a tener nota, y los que ya existen no la tienen. El
  almacenamiento admite **dos formas** de representar esa ausencia y la lectura de la API devuelve
  **una sola** (`FR-011`). No participa de ninguna relación, de ningún índice de búsqueda y de ninguna
  agregación — y eso es una decisión de producto, no una omisión.
- **Moneda**: no suma atributos. Su `codigo` pasa a tener en el esquema la restricción que le
  faltaba: tres letras (`FR-010`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una persona puede registrar un movimiento con nota y leerla en el listado en el mismo
  intento, sin ningún paso adicional respecto de registrarlo sin nota.
- **SC-002**: Registrar un movimiento **sin** nota no agrega **ninguna** interacción respecto de como
  era antes de esta feature: cero teclas y cero clics de más.
- **SC-003**: El 100 % de las notas se muestra con exactamente los caracteres que se escribieron,
  incluidas las que contienen secuencias con forma de marcado o de guion, verificado con un test.
- **SC-004**: Una nota de 120 caracteres se acepta y una de 121 se rechaza, con el motivo visible al
  lado del campo, en las dos pantallas que la escriben.
- **SC-005**: Los totales, el balance y el desglose del resumen dan **el mismo número** antes y
  después de agregar, cambiar y borrar una nota.
- **SC-006**: El listado con 1000 movimientos con nota carga en menos de 2 s en el percentil 95 sobre
  100 ejecuciones.
- **SC-011**: Un movimiento guardado sin valor de nota y otro guardado con la cadena vacía producen
  **la misma respuesta de la API** —y por lo tanto la misma pantalla—, verificado con un test.
- **SC-007**: La nota no aparece en ningún acotado, orden ni total: la cuenta de lugares donde se
  puede filtrar o agrupar por nota es **cero**.
- **SC-008**: Los verificadores de la feature 011 siguen todos en verde con la columna nueva, y la
  suite entera de las dos pilas también.
- **SC-009**: Ni `frontend/package.json`, ni el lockfile, ni ningún `.csproj` cambian respecto de
  `main`.
- **SC-010**: Un código de moneda que no sean tres letras es rechazado por la base, y el catálogo
  sembrado que ya existe sobrevive a la migración sin una sola corrección.

## Assumptions

- **El límite de 120 es el del PRD y no se discute.** Ampliarlo o hacerlo configurable es una
  modificación de `PRD:RF-33`, no una decisión de este ticket, y está fuera de alcance explícito.
- **El límite se cuenta sobre el valor ya recortado, en caracteres Unicode**, y de la misma forma en
  las dos pilas (Clarifications). Es además la unidad del esquema, así que una nota que las dos
  validaciones aceptan entra siempre en la columna: no hay un cuarto criterio escondido en la base.
- **La nota no es obligatoria en ningún caso**, ni para ningún tipo de movimiento, ni para ninguna
  categoría. No hay un solo escenario en el que el sistema la exija.
- **Los movimientos que ya existen quedan sin nota**, y eso no necesita ni migración de datos ni
  valor por omisión: es el mismo estado que tiene un movimiento nuevo guardado sin nota.
- **El aislamiento entre cuentas ya cubre la nota** por la vía por la que cubre todo lo demás: un
  movimiento ajeno no se lee. Esta feature lo verifica en lugar de suponerlo, porque la nota es el
  primer campo de texto libre que ese aislamiento tiene que tapar.
- **`frontend/tests/TecladoFormulario.test.tsx` se extiende, y eso está previsto.** Ese test enumera
  el orden de tabulación del formulario completo, así que un control nuevo lo pone en rojo **por
  diseño** — su propio comentario lo dice: que se haya puesto en rojo al agregar un botón es la señal
  de que sirve. Es el único test existente que esta feature tiene que tocar por el control nuevo, y se
  declara acá para que no parezca un daño colateral descubierto durante la implementación.
- **La validación del contrato la verifica la barrera que ya existe.** El campo nuevo viaja en las
  tres formas del movimiento, así que `verificar-contrato.sh` participa del cierre.
- **`NFR-003` se mide sobre la respuesta de la API, no sobre el navegador.** Es lo que hacen los
  otros tres tests de rendimiento del proyecto, y es lo único medible sin traer un runner de
  navegador, que `NFR-005` prohíbe. La spec lo dice en lugar de afirmar "el listado carga en 2 s",
  que sería afirmar algo que no se midió — la misma honestidad que la 011 aplicó a los 360 px.
- **La nota no se registra en ningún log ni se repite en ningún mensaje de error.** Es la única
  entrada de texto libre de la aplicación, o sea el único lugar por el que contenido de la persona
  podría salir hacia donde nadie lo está mirando. El mensaje de `FR-003` dice que se pasó del límite;
  no devuelve el texto.
- **El test de rendimiento del listado queda fuera del CI**, como los otros tres, por el filtro
  `FullyQualifiedName!~Rendimiento` que `AGENTS.md` declara: mide tiempo de pared y en un runner
  compartido da rojos que no dicen nada. En local corre.
- **`FR-010` no agrega validación de aplicación.** La restricción vive en el esquema y nada más: el
  catálogo de monedas se administra como dato, y meterle una validación de aplicación sería darle a
  la aplicación una responsabilidad sobre una tabla que no escribe.

## Deuda registrada

| # | Qué queda sin hacer | Por qué | Quién lo hereda |
|---|---|---|---|
| D12-01 | **El desborde horizontal a 360 px verificado por medición real**, en un navegador, y con él los pasos a mano del quickstart | Es **D11-01** y arrastra **D10-09**. Esta feature agrega la séptima columna del listado, que es el caso más apretado que la tabla tuvo nunca, y lo verifica igual por **regla** y no por medición: jsdom no maqueta. La restricción de no agregar dependencias sigue cerrando la puerta a un runner de navegador | Quien decida que vale un runner de navegador en el CI |
| D12-02 | ~~**Revisar el techo de 50 ms de `003:AC-12`**~~ — **SALDADA en la rama `013`** | Es **D9-08**, **D10-04** y **D11-03**. El fallo ya tiene nombre desde la 011 —`RendimientoLimiteTests.El_P95_De_La_Comprobacion_Agrega_Menos_De_Cincuenta_Milisegundos_AC12`—: falla en la corrida completa bajo carga y pasa aislado. No hay nada más que averiguar, hay que decidir el techo, y decidirlo es una decisión de criterio que esta feature no tiene por qué tomar | El ticket que decida si ese techo sigue siendo el correcto |
| D12-03 | **Ponerle color a las barras del dashboard** | Es **D10-08** y **D11-04**. Sigue siendo una decisión de producto: D-04 de la 010 dejó todas las barras del mismo relleno a propósito, porque las categorías no se codifican por color | Nadie, salvo que producto lo pida |
| D12-04 | **Buscar, filtrar, agrupar o totalizar por la nota**, y las **etiquetas reutilizables** que serían la forma correcta de hacerlo | Fuera de alcance explícito del PRD, y es *la* restricción que impide que la nota se vuelva una segunda taxonomía informal. Si aparece la necesidad real de totalizar por algo más fino que la categoría, se resuelve con un catálogo de etiquetas, no estirando la nota | Nadie. Necesita una decisión de producto y un ticket propio |
| D12-05 | **Autocompletado o sugerencias** a partir de notas anteriores | Fuera de alcance explícito: sería la puerta de atrás a la misma taxonomía informal que `PRD:RF-33` evita | Nadie |
| D12-06 | **Formato dentro de la nota** —negrita, saltos de línea con significado, enlaces que se puedan seguir— y **adjuntar comprobantes** al movimiento | Fuera de alcance explícito del PRD. El formato además chocaría de frente con `NFR-001`, que es el requisito que hace que la nota sea segura | Nadie |
| D12-07 | **Modo oscuro, temas y cualquier preferencia visual configurable**, y la **auditoría completa de ARIA con lectores de pantalla concretos** | Son **D11-05** y **D11-06**, sin cambios: alcance que nadie pidió, y la dependencia con la que se haría está prohibida | Nadie |

| D12-08 | ~~**Una sola representación de "sin nota" garantizada por el esquema**~~ — **SALDADA en la rama `014`** | Decisión tomada en *Clarifications*: la columna admite tanto la ausencia de valor como la cadena vacía, y no se normaliza al escribir. El costo es que la invariante de `FR-005` pasa a depender de la lectura en vez del almacenamiento, y lo que la sostiene es `FR-011` con su test en lugar de una restricción. Es una deuda **aceptada a sabiendas, no un descuido**: queda anotada para que el día que aparezca un camino de lectura nuevo se sepa que hay una invariante que no se cumple sola | Quien decida normalizar al escribir, si alguna vez el test de `FR-011` resulta insuficiente |

### Estado al cierre (2026-09-10)

Confirmado contra lo que la implementación realmente dejó, no contra lo que la tabla anticipaba:

| # | Estado | Qué cambió respecto de lo previsto |
|---|---|---|
| D12-01 | **Abierta** | Sin cambios en el fondo, pero **más chica**: de los cinco pasos a mano del quickstart, tres quedaron cubiertos por tests durante la implementación. Lo que sigue sin ejecutar son los 360 px medidos y el lector de pantalla — no hay navegador en el entorno |
| D12-02 | **Abierta** | Sin cambios. El fallo apareció una vez en la corrida completa (87,6 ms contra un techo de 50) y pasó aislado y en la corrida siguiente. Es exactamente lo que D9-08 describe desde la feature 009. **Saldada después, en la rama `013`**: ver la nota de cierre al pie |
| D12-03 | **Abierta** | Sin cambios. Sigue siendo una decisión de producto |
| D12-04 | **Abierta**, y ahora **verificada** | Era una intención escrita; ahora hay una barrera que la sostiene. `verificar-nota.sh` impide que el listado acote por la nota y que el resumen la agrupe. Lo que queda abierto es el catálogo de etiquetas, que necesita su propia decisión de producto |
| D12-05 | **Abierta** | Sin cambios |
| D12-06 | **Abierta** | Sin cambios, y con una precisión que la implementación agregó: los saltos de línea **sí** existen en el dato (`FR-012`) y no significan nada en la presentación. Darles significado sigue fuera de alcance |
| D12-07 | **Abierta** | Absorbe además el paso 3 del quickstart, el del lector de pantalla |
| D12-08 | **Abierta, y es la única que esta feature creó** | Sin cambios: el esquema admite dos representaciones de "sin nota" y lo que sostiene la invariante es `FR-011` con su test sobre las cuatro rutas. Se aceptó a sabiendas. **Saldada después, en la rama `014`**: ver la nota de cierre al pie |

**Ninguna deuda se descubrió durante la implementación**: las ocho estaban anotadas antes de escribir
la primera línea. Lo que sí apareció fueron **tres errores propios**, corregidos y anotados donde
correspondía en vez de en esta tabla, porque no son deuda sino defectos que ya no existen:

1. **Una verificación mal hecha** en [research.md](./research.md) D-10 y
   [data-model.md](./data-model.md): la comprobación de que los códigos de moneda sembrados eran tres
   letras buscó un patrón de tres caracteres *alfanuméricos*, que por construcción no distingue una
   letra de un dígito. Eran quince códigos, no doce, y tres llevaban dígito. Los puso en rojo la
   migración.
2. **Un test que contaminaba la base compartida**: mientras `MonedaCodigoEsquemaTests` estaba en rojo,
   sus `INSERT` entraban y las filas inválidas quedaban. Ahora limpia en un `finally`.
3. **Cuatro tests de US2 que nacieron verdes** porque su código de producción se escribió junto con el
   de US1. Se comprobó que detectan desarmando el código a propósito y exigiendo el rojo, que es lo
   que el Principio V le exige a una barrera y lo que corresponde cuando el orden TDD se desordenó.

**Lo que esta feature salda**: **D11-02** (`FR-010`, el `CHECK` de tres letras sobre
`moneda.codigo`, que venía de la 009 como D9-09 y de la 010 como D10-03) y **D11-07** (la nota
descriptiva, que es el ticket entero).

**Con esto el plan DISC-001 queda sin tickets pendientes.**

### Cierre de D12-02 (2026-09-11, rama `013`)

La deuda venía anotada desde la feature 009 y se reescribió cuatro veces sin que nadie la midiera.
Medida, resultó que **el techo no era el problema y la premisa estaba al revés**:

- **El techo de 50 ms no cambió.** Sigue siendo el de `PRD:NFR-02`. Lo que estaba mal era el
  **estimador**: la distribución de lo que AC-12 mide es bimodal —un grupo de 5 a 9 ms y atascos
  sueltos de 20 a 65 ms— y los 50 ms caen **adentro** de esa cola. Con n=100, el p95 es una sola
  muestra ordenada parada justo en el borde entre los dos grupos, así que pasaba o fallaba según si
  la tasa de atascos quedaba abajo o arriba del 5 %. AC-12 pasó a afirmarse sobre la **mediana**,
  que es lo que AC-13 ya había hecho en la rama de `004` por un motivo emparentado.
- **"Falla bajo carga y pasa aislado" era al revés.** Medido sobre 500 muestras por régimen: bajo
  carga, 0 llegaron a 20 ms (p99 de 8,3 ms); aislado, 11 pasaron de 20 ms y 3 pasaron de 50 (p99 de
  46 ms, máximo 64,3). Los atascos son de **máquina fría**, no de contención — los tests de base son
  una sola colección y corren serializados, así que nada compite con la medición.
- **El p95 no protegía la cota de la purga**, que era el único costo de cola que podía justificarlo.
  Desarmada —sin el `LIMIT`, con 50.000 filas vencidas— las 50.000 se borran de una sola vez en la
  primera llamada, que cae en el calentamiento, y las 100 muestras medidas salen normales.
- **La mediana sabe fallar**, que es lo que el Principio V le exige a una barrera. Comprobado
  desarmando `ix_intento_de_acceso_ultimo_fallo` con 150.000 filas: la mediana pasa de 5,0 ms a
  80,5 ms —rojo— y vuelve al verde al restaurar el índice.

El detalle completo, con los números, está en el ajuste de
[`specs/003-limite-intentos/spec.md`](../003-limite-intentos/spec.md), que es donde vive el criterio.

### Cierre de D12-08 (2026-09-11, rama `014`)

Era la única deuda que esta feature **creó**, y la única que nació aceptada a sabiendas: las
*Clarifications* decidieron que el esquema no eligiera entre `NULL` y `''`, y anotaron el costo para
el día que apareciera un camino de lectura nuevo.

**Lo que faltaba no era normalizar al escribir: eso ya se hacía.**
`ValidacionDelMovimiento.NotaNormalizada` convierte la cadena vacía y los espacios en ausencia de
valor desde esta misma feature, así que la aplicación ya escribía una sola forma. Lo que no existía
era algo que lo **garantizara**. El cierre es entonces una restricción de esquema,
`ck_movimiento_nota_sin_cadena_vacia` (`FR-014`), con el mismo patrón con el que esta feature cerró
D11-02 sobre `moneda.codigo`: un `CHECK` y un test de esquema contra SQL directo, que es el único
camino por el que el daño podía entrar.

**La migración normaliza antes de restringir.** MySQL se niega a agregar un `CHECK` que las filas
existentes ya incumplen, así que sin el `UPDATE` previo la migración sería inaplicable sobre
cualquier base que tuviera un `''` guardado. No se conoce ninguna —la API nunca pudo escribirlo—, y
va precisamente por eso: lo que esta migración cierra es el camino que no pasa por la aplicación, que
es el mismo que pudo haber dejado una fila antes de hoy.

**Y le sacó una mitad a un test, que es la parte que conviene no pasar por alto.**
`Las_Cuatro_Rutas_No_Distinguen_Las_Dos_Formas_De_Sin_Nota_FR011` forzaba `nota = ''` con SQL para
comprobar que las dos representaciones salían iguales. Con la restricción ese `UPDATE` ya no escribe
una fila: lo rechaza la base. La premisa del test dejó de existir, así que pasó a llamarse
`Las_Cuatro_Rutas_Devuelven_La_Cadena_Vacia_Sin_Nota_FR011` y verifica lo que sigue siendo cierto y
sigue haciendo falta —`FR-009`: el campo viaja siempre y nunca es nulo, por las cuatro rutas—. La
mitad que perdió no se perdió: se mudó a `NotaSinCadenaVaciaEsquemaTests`, donde es una restricción
verificada en vez de una igualdad confiada a la disciplina. Es exactamente el canje que D12-08
pedía.

## Dependencies

- `main` con la feature 011 mergeada, que es de donde salen el piso de accesibilidad, el envoltorio
  desplazable del listado y el juego único de campos del formulario.
- FEAT-001a y FEAT-001b en `main`: el modelo del movimiento, el formulario, el listado y la
  modificación de un movimiento propio, de la que depende `FR-004`.
- MySQL 8.4.10 y **una migración nueva**, la primera desde la feature 007, que agrega la columna de
  la nota y —por `FR-010`— la restricción del código de moneda.
- El filtro `FullyQualifiedName!~Rendimiento` del CI, declarado en `AGENTS.md`, que `NFR-003`
  necesita para no dar rojos sin significado en un runner compartido.

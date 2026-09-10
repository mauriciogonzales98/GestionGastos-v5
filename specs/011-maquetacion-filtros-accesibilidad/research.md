# Research: Maquetación, filtros del listado y accesibilidad

Las decisiones de diseño de la feature 011, con su porqué y lo que se descartó. Se citan desde
[plan.md](./plan.md) y desde las tareas.

Trece decisiones. Las tres que más condicionan al resto son **D-01** (la paleta cambia de lado: la
declara el CSS y el test la lee), **D-05** (el intérprete del período no se duplica: el control del
dashboard sube y se reutiliza) y **D-12** (el presupuesto de tests existentes que se pueden tocar,
nombrados uno por uno).

---

## D-01 · La paleta se declara en el CSS, y el verificador la lee del archivo

**Decisión**: los colores nacen como propiedades personalizadas en `estilos/base.css`, bajo `:root`.
`ui/contraste.ts` deja de tener colores y se queda **sólo con la cuenta**; el test los extrae del
archivo CSS y mide los pares que declare una tabla de umbrales.

**Es una inversión de lo que hizo la 010, y a propósito.** Allá los cuatro colores vivían en el
módulo TypeScript y el componente los bajaba a variables CSS en su propio elemento; el comentario
que lo justifica dice que dos declaraciones darían dos copias *"y el día que una cambiara el test
seguiría midiendo la otra"*. El argumento sigue siendo válido y por eso hay que elegir **un** lado.
Cuál, cambia ahora que los colores dejan de ser cuatro y de pertenecer a un solo componente:

- Con el CSS como fuente, un color existe **antes de que corra una sola línea de JavaScript**. Con
  el módulo TS como fuente, la aplicación entera tendría que inyectar la paleta al montarse, y la
  primera pintura sería sin colores.
- El verificador que lee el archivo cubre **cualquier** color que alguien agregue, sin que nadie lo
  agende. El que lee un objeto TypeScript cubre lo que ese objeto tenga. Es `NFR-003`.
- La regla del proyecto —*"los colores NO se declaran acá... bajan como variables desde
  `ui/contraste.ts`"*— se escribió cuando `ui/contraste.ts` era el único lugar donde había colores.
  Deja de serlo en cuanto exista una paleta.

**Alternativas descartadas**: (a) mantener el TS como fuente e inyectar la paleta en `:root` al
montar — resuelve la copia única pero regala el destello sin estilos y ata los colores al ciclo de
vida de React; (b) declarar en los dos lados y sincronizar a mano — es exactamente el bug que la 010
describió; (c) un preprocesador o tokens de diseño — dependencia nueva, prohibida por `NFR-004`.

**Consecuencia**: `Contraste.test.ts` y `GastosPorCategoria.test.tsx` cambian. Están en el
presupuesto de D-12, con su justificación.

---

## D-02 · Los tres verificadores nuevos leen los archivos reales, nunca una lista

**Decisión**: las tres comprobaciones nuevas —`FR-001`, 0 clases referenciadas sin regla; `NFR-001`,
el contraste de la paleta; y `FR-003`/`FR-004`, las reglas de ancho— se hacen leyendo
`frontend/src/**/*.tsx` y `frontend/src/estilos/*.css` desde el disco, en tests que corren en
entorno `node` sobre un helper compartido.

**Son tres y no dos**, aunque la de anchos verifique reglas en vez de medir (D-04): lee los mismos
archivos por el mismo camino, así que le corresponden las mismas dos obligaciones —derivar del
código y no de una lista, y verse fallar—. El verificador de accesibilidad de la Historia 2 es un
cuarto, con otra forma —monta pantallas en vez de leer archivos— y la misma obligación.

**Por qué**: es `NFR-003`, y es el criterio que el proyecto ya aplica en `verificar-monedas.sh`: una
promesa que se sostiene en que alguien se acuerde de agregar una fila a una lista no es una promesa,
es un recordatorio. Una pantalla agregada en el ticket 2 queda cubierta sola.

**El costo, dicho**: dos lecturas de archivo con expresiones regulares. Una clase escrita de una
forma que la expresión no reconozca —concatenada en tiempo de ejecución, por ejemplo— se le escapa.
Se mitiga con la regla que el proyecto ya sigue y que `disposicion.css` deja escrita: *"nada de
clases utilitarias sueltas"*, los nombres son literales. Si alguna vez hace falta una clase armada
en tiempo de ejecución, el verificador deja de servir y hay que decirlo, no taparlo.

**Alternativa descartada**: un script de shell al lado de las cinco barreras existentes. Se descarta
en D-10, con su motivo.

---

## D-03 · Cada verificador nuevo se prueba contra una entrada que tiene que dar rojo

**Decisión**: **los cuatro** verificadores nuevos se ejercitan con una entrada sintética que **debe**
fallar, además de con la aplicación real:

| Verificador | La entrada que tiene que detectar |
|---|---|
| `ClasesConRegla.test.ts` | un fragmento que referencia una clase que la hoja sintética no declara |
| `Paleta.test.ts` | un par de colores por debajo de su umbral |
| `AnchoDeLasPantallas.test.ts` | una hoja con un ancho fijo de 400 px, y una tabla sin contenedor desplazable |
| `Accesibilidad.test.tsx` | un árbol con un control sin etiqueta accesible |

**Ninguno queda afuera, y el de anchos es el que estuvo a punto de quedar**: verifica reglas en vez
de medir, así que es fácil pensar que no cuenta. Cuenta: si su expresión regular dejara de reconocer
una declaración de ancho, informaría 0 violaciones por no haber encontrado ninguna regla, que es
exactamente la forma de fallo que este proyecto ya se comió con `verificar-desglose.sh`.

**Por qué**: es el Principio V de la constitución, y la 010 ya lo aplicó a la cuenta de contraste
por esta misma razón. Un verificador que nunca se vio fallar informa verde el día que deja de
verificar nada, y ése es exactamente el día que importa. La forma la fija D-02: como los dos leen
del disco, la entrada sintética entra como texto, sin tocar archivos del proyecto.

**Lo que esto NO es**: no convierte a estos tests en barreras de shell. La diferencia está en D-10.

---

## D-04 · Los 360 px se verifican por regla, no por medición, y se dice

**Decisión**: `FR-003` y `FR-004` se verifican comprobando en la hoja de estilos que ninguna regla
declare un ancho fijo mayor a 360 px y que el contenedor de la tabla del listado declare desborde
desplazable. La comprobación de que efectivamente no desborda queda como paso manual del quickstart
y como deuda **D11-01**.

**Por qué**: jsdom y happy-dom no maquetan. No calculan anchos, no resuelven `flex`, y
`getBoundingClientRect` devuelve ceros. Un test que afirmara "no desborda" sobre ese entorno estaría
afirmando algo que no midió, que es peor que no tenerlo: entrena a confiar en un verde vacío.

**Alternativa descartada**: traer un navegador sin cabeza. Resolvería `FR-003` de verdad y de paso
saldaría **D10-09**, pero es una dependencia nueva y un runner más en el CI, y `NFR-004` lo prohíbe
después de haberlo considerado. La deuda queda anotada con el argumento entero para que el día que
alguien decida pagarla no tenga que reconstruirlo.

**La honestidad del criterio**: la tarea de verificación se llama por lo que hace —comprobar reglas
de estilo— y no por lo que se quisiera que hiciera. `SC-002` se cumple por la vía de la regla más el
paso manual, y el quickstart lo dice en la misma línea.

---

## D-05 · `ControlesDelPeriodo` sube y se reutiliza; no nace un segundo intérprete del período

**Decisión**: el componente que hoy vive en `dashboard/ControlesDelPeriodo.tsx` se mueve a
`frontend/src/periodo/` y lo usan las dos pantallas. No se escribe un control de rango para el
listado.

**Por qué**: el componente ya está escrito con exactamente la regla que el listado necesita, y su
propio comentario explica por qué: *"no valida nada, y eso es la decisión"*. `PeriodoPedido` es
—textualmente— *"el único intérprete de `desde` y `hasta`"*, y comprobar el rango en la pantalla
sería el segundo intérprete, con su propio criterio de "hoy". Un segundo control de rango sería una
tercera copia de esa decisión esperando a divergir.

Es además la misma frontera que la 010 trazó entre `resumen/` y `dashboard/`: **lo que pinta algo
que dos pantallas usan, sube; lo que decide qué pedir, se queda**. `ControlesDelPeriodo` pinta dos
fechas y las devuelve tal cual; quién las usa y para qué es de cada pantalla.

**Lo que hay que cuidar al moverlo**: el mensaje de error que muestra viene bajo la clave `rango`
del ProblemDetails, y esa clave existe —según su propio comentario— *"porque el frontend la usa para
poner el mensaje al lado del control"*. El listado la va a recibir por el mismo camino: el
`GET /api/movimientos` usa `PeriodoPedido.Interpretar` igual que el resumen, así que el rechazo
llega con la misma forma. Es `FR-018`, y no cuesta nada del lado del servidor.

**Lo que el movimiento agrega, y que el componente hoy no tiene: props de valor inicial.** Los dos
extremos arrancan siempre vacíos, y para el listado eso no alcanza. `FR-015` pide que el control
**muestre** el mes actual sin haberlo elegido, y ahí hay una trampa que conviene ver antes de
empezar: **`GET /api/movimientos` devuelve un arreglo pelado y no dice qué período aplicó.** Sólo el
resumen lo dice — `Resumen` lleva `desde` y `hasta` puestos por el servidor.

La salida no es calcular el mes en la pantalla, que sería el segundo intérprete que esta misma
decisión prohíbe: **es prefijar el control con el `desde`/`hasta` del `Resumen` que la pantalla
principal ya tiene cargado**. El dato lo sigue decidiendo el servidor, viaja por un canal que ya
existe, y no aparece ningún criterio de "hoy" nuevo. Y mientras nadie toque los campos, la petición
del listado **igual sale sin parámetros de período**: lo prefijado se muestra, no se manda.

Las props nuevas no reabren la decisión D-08 de la feature 010: el componente sigue sin validar
nada. Recibir un valor inicial no es interpretarlo.

**Alternativa descartada**: dejarlo en `dashboard/` e importarlo desde `movimientos/`. Funciona y
está mal señalizado: una carpeta con nombre de pantalla que exporta a otra pantalla es la clase de
dependencia que nadie encuentra cuando busca qué rompe si toca el dashboard.

---

## D-06 · Un solo "Aplicar" para los tres acotados

**Decisión**: la barra de filtros del listado —categoría, rango de fechas y moneda— aplica los tres
juntos con un único botón. El acotado por moneda deja de dispararse solo al cambiar el `<select>`.

**Por qué**: con tres controles, aplicar en cada cambio significa hasta **tres peticiones para
expresar una sola pregunta**, y el listado ya arrastra la guarda contra la respuesta que llega tarde
justamente porque dos peticiones en vuelo se pisan. Peor: un rango se escribe dígito a dígito, y
aplicar en cada cambio dispararía una petición rechazada por cada tecla, con su cartel de error
apareciendo y desapareciendo mientras la persona escribe.

`NFR-005` lo pide con estas palabras: una sola consulta por cambio de acotado.

**El costo, dicho de frente**: es un cambio de comportamiento sobre un control que ya existía y
funcionaba, y toca tests de la feature 009. Está en el presupuesto de D-12 y justificado por
`FR-016`. Se acepta porque la alternativa —dos interacciones distintas en la misma barra, una que
aplica sola y dos que esperan un botón— es peor para quien la usa que para quien la escribe.

**Alternativa descartada**: aplicar al salir del control (`onBlur`). Evita la petición por tecla
pero hace impredecible cuándo se pidió, y con teclado el foco pasa por los tres controles camino al
botón: se dispararían igual las tres peticiones.

---

## D-07 · La confirmación del borrado es el patrón de dos botones que ya existe

**Decisión**: eliminar un movimiento pide confirmación con el mismo patrón que la baja de una
categoría — la fila cambia a dos botones y un `role="alert"` que dice la consecuencia entera — y no
con `window.confirm` ni con un `<dialog>` nuevo.

**Por qué**: el patrón ya está escrito en `PantallaCategorias`, con su motivo textual: no se usa
`window.confirm` porque *"ése no se puede maquetar —el ticket 6 no podría tocarlo— ni se comporta
igual en todos los navegadores"*. Ese comentario se escribió para este ticket. Usarlo ahora es
cumplir la promesa, no reinterpretarla.

Y el mensaje sigue la misma regla que la baja: **dicho entero y no "¿Seguro?"**. Lo que hay que
saber antes de apretar es que el movimiento se borra de verdad y que su monto sale de los totales.

**Alternativa descartada**: un `<dialog>` modal como el de la edición. La ventana de edición es modal
porque contiene un formulario con seis campos que hay que completar sin distracciones; una
confirmación de dos botones dentro de una fila no gana nada con volver inerte el resto de la página,
y sí pierde: saca de la vista la fila sobre la que se está decidiendo.

---

## D-08 · `decimales` viaja, y el catálogo le gana a `Intl`

**Decisión**: `MonedaDto` suma `Decimales`, `tipos.ts` suma `decimales`, y `formatearMonto` pasa a
recibirlo y a fijar con él `minimumFractionDigits` y `maximumFractionDigits`. Cuando el catálogo y
lo que `Intl` supone por el código ISO no coinciden, **gana el catálogo**.

**Por qué gana el catálogo**: es `PRD:RF-32` —la moneda es un dato, no código— y es la promesa que
`verificar-monedas.sh` protege en las dos pilas. Si `Intl` decidiera la escala, agregar una moneda al
catálogo con una escala distinta a la que su código ISO tiene asignada no funcionaría, y la falla
sería silenciosa: montos redondeados a una escala que nadie eligió.

**Lo que NO cambia**: el `try/catch` de `formatearMonto` se queda tal cual. Está ahí porque
`moneda.codigo` es `char(3)` y admite `'BT1'`, que hace lanzar a `Intl`; ese guardarraíl es
independiente de la escala y sigue haciendo falta hasta que exista el `CHECK` de **D11-02**. La
degradación —el número con su código al lado— también respeta los decimales del catálogo, porque el
`Intl.NumberFormat` sin `style: 'currency'` acepta las mismas opciones de escala.

**Lo que arrastra**: es el único cambio de backend de la feature, y pone en rojo los tests de
`Contrato/` en cuanto el campo aparezca de un solo lado. Eso es lo que tienen que hacer: la barrera
`verificar-contrato.sh` existe para probar que ese rojo llega.

---

## D-09 · El foco no se queda en el aire cuando la fila desaparece

**Decisión**: al eliminar, el foco se lleva explícitamente a un destino estable de la pantalla —el
encabezado del listado— y la confirmación se anuncia por la región `role="status"` que ya existe.

**Por qué**: el botón que se apretó deja de existir en el mismo render. El navegador manda el foco al
`<body>`, y quien navega con teclado queda al principio de la página sin ningún anuncio de que la
acción salió bien: se entera apretando Tab hasta volver a encontrar el listado. Es la variante de
`FR-005` que sólo aparece cuando algo se borra, y por eso ninguna pantalla anterior se la encontró.

**Alternativa descartada**: mover el foco a la fila siguiente. Es lo que hacen muchas listas, y
falla en los dos bordes —eliminar la última fila, y eliminar la única— con un caso especial cada
uno. El encabezado es un destino que siempre existe.

---

## D-10 · Ninguna barrera de shell nueva, y el criterio para decirlo

**Decisión**: esta feature no agrega un `verificar-*.sh`. Los cuatro verificadores nuevos son tests
de Vitest, probados contra una entrada que falla (D-03).

**El criterio, que conviene tener escrito**: las cinco barreras del proyecto existen porque protegen
algo que **un test verde no distingue de un test que dejó de verificar**. `verificar-desglose.sh`
existe porque hasta la feature 007 todas las categorías estaban activas: el filtro puesto dejaba la
suite entera en verde. `verificar-aislamiento.sh` existe porque un test de aislamiento roto devuelve
verde igual.

Los verificadores de esta feature no tienen esa forma. Si el de clases dejara de encontrar clases,
no informaría verde: informaría cero clases analizadas, y D-03 exige que se pruebe contra una
entrada con una clase sin regla. Una barrera de shell acá sería ceremonia sin daño que prevenir — y
las barreras cuestan: las cinco existentes suman ~11 minutos.

---

## D-11 · El borrado quita la fila y recalcula el resumen; no recarga el listado

**Decisión**: al confirmarse el borrado, la fila se saca del estado local y se llama a
`recargarResumen()`. No se vuelve a pedir el listado.

**Por qué**: es la regla que la pantalla ya sigue para el alta y para la edición, escrita en
`FR-014` de la feature 009: el servidor ya dijo lo que hacía falta, y volver a pedir la lista entera
para quitar una fila es traer todo para tirar uno. El resumen sí se recalcula, y por el mismo motivo
que después de cada alta: **un total no se puede editar en la pantalla, hay que recalcularlo**, y
quien recalcula es el servidor.

Se llama siempre y sin averiguar antes si el movimiento caía en el mes en curso, igual que hoy: esa
averiguación es la clase de cuenta que la pantalla no hace.

---

## D-12 · El presupuesto de tests existentes que se pueden tocar

`SC-007` pide **0 tests existentes modificados para acomodar un cambio visual**. La prohibición es
ésa y no "0 tests modificados": lo que el PRD quiere atrapar es el test que se retoca para que un
restyle deje de romperlo. Un test que cambia porque un requisito **de esta spec** cambió el
comportamiento es otra cosa, y para que la diferencia sea verificable y no una excusa, la lista va
escrita de antemano:

| Test | Qué cambia | Requisito que lo justifica |
|---|---|---|
| `Contraste.test.ts` | Se queda sólo con la cuenta y su caso que tiene que fallar; la medición de la paleta se va a `Paleta.test.ts`, que la lee del CSS | `FR-002`, D-01 |
| `GastosPorCategoria.test.tsx` | El componente deja de inyectar variables de color en su elemento; el test deja de afirmarlo | `FR-002`, D-01 |
| `PantallaMovimientos.test.tsx` | El acotado por moneda pasa a aplicarse con el botón, no al cambiar el `<select>` | `FR-016`, D-06 |
| `PantallaDashboard.test.tsx` | `ControlesDelPeriodo` se importa desde su ubicación nueva y recibe props de valor inicial; y los montos pasan a formatearse con los decimales del catálogo | `FR-014`/`FR-015`, D-05 · `FR-019`, D-08 |
| `ListadoMovimientos.test.tsx` | Suma la columna de eliminación; los montos pasan a formatearse con los decimales del catálogo | `FR-010`, `FR-019` |
| `ResumenDelPeriodo.test.tsx` y los fixtures de monedas | `Moneda` suma `decimales` | `FR-019`, D-08 |
| Los tests de `Contrato/` | `MonedaDto` suma un campo | `FR-019`, D-08 |
| `TecladoFormulario.test.tsx` | Suma el caso del foco que sale del formulario sin quedar atrapado | `FR-007`, `PRD-06:AC-09` |
| `VentanaDeEdicion.test.tsx` | Suma el caso de la apertura como modal, y el acotado se aplica con el botón | `FR-009` · `FR-016`, D-06 |
| `cliente.test.ts` | Suma los casos de `eliminarMovimiento` y de los cuatro acotados | `FR-010`, `FR-014`–`FR-018` |
| `App.test.tsx` | El acotado por moneda se aplica con el botón; y las opciones del catálogo se buscan dentro del selector del formulario, porque la barra agrega un segundo selector de categoría | `FR-016`, D-06 · `FR-014` |
| `CargaInicial.test.tsx` | El acotado por moneda se aplica con el botón | `FR-016`, D-06 |

**Dos formas de "tocar" un test, y sólo una es la que `FR-020` persigue.** Agregar un caso nuevo a un
archivo que ya existía no es acomodar nada: el archivo crece porque hay comportamiento nuevo que
verificar, y lo viejo sigue verde sin cambios. Lo que `FR-020` prohíbe es **retocar una aserción que
ya estaba** para que un restyle deje de romperla. Las nueve filas de arriba son de las dos clases y
van igual en la tabla, porque la tarea de cierre compara contra el diff y el diff no distingue.

**Cualquier test que haya que tocar y que no esté en esta tabla es la señal.** No se agrega una fila
durante la implementación sin decir en el commit qué requisito la justifica; si no hay ninguno, lo
que hay que cambiar es el código.

### Lo que la implementación cambió de esta tabla

La tabla se escribió antes de empezar y la comprobación de cierre —el diff contra lo que realmente
se tocó— la corrigió en las dos direcciones. Queda anotado, porque el valor de la tabla está en que
diga la verdad y no en haber acertado:

- **Tres filas se agregaron**: `cliente.test.ts`, `App.test.tsx` y `CargaInicial.test.tsx`. Las tres
  por lo mismo, y por algo que no se previó: los dos últimos **usaban el acotado por moneda como
  mecanismo para disparar una segunda carga del listado**, sin que el acotado fuera lo que estaban
  verificando. Al pasar a aplicarse con un botón (D-06), quedaron rotos por un cambio que no era
  suyo. La lección es del tipo de la tabla: un control usado como herramienta en tests de otra cosa
  no aparece cuando uno enumera "qué tests verifican este control".
- **Dos filas no se usaron**: `GastosPorCategoria.test.tsx` no necesitó cambios —nunca había
  afirmado sobre la inyección de variables de color, sólo sobre que ninguna barra trajera un color
  propio, que sigue siendo cierto— y los tests de `Contrato/` tampoco: se pusieron en rojo con el
  campo de un solo lado, como estaba previsto, y volvieron al verde solos al alinear el DTO, sin
  editar una línea.

---

## D-13 · El orden del trabajo lo fija una dependencia real, no la comodidad

**Decisión**: la paleta y las reglas de disposición (Historia 1) van primero; la accesibilidad
(Historia 2) segunda; después las tres historias funcionales.

**Por qué**: no es una preferencia. `NFR-001` mide contraste **sobre la paleta declarada**, y hoy no
hay ninguna: los dos colores que existen están anotados como *"los del navegador, no una elección"*.
Medir contraste antes de la paleta es medir contra un valor de relleno y tener que rehacerlo. Y la
barra de filtros de la Historia 4 necesita un contenedor que sepa acomodar tres controles en una
fila que también funcione a 360 px, que es una regla de disposición de la Historia 1.

**La contra, dicha**: las historias de más valor visible para quien usa la aplicación —poder borrar,
poder filtrar— quedan al final. Es la misma contra que el PRD del ticket 6 asume al ir último en el
plan, y se anota por el mismo motivo: para que sea una decisión y no una sorpresa. Si hiciera falta
mostrar algo antes, las Historias 3 y 4 son independientes y se pueden adelantar pagando una segunda
pasada de maquetación sobre los controles que agreguen.

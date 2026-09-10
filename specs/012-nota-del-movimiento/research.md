# Research: Nota descriptiva del movimiento

**Feature**: 012-nota-del-movimiento · **Fecha**: 2026-09-10

Las trece decisiones de diseño, cada una con su alternativa descartada y el motivo. Cuatro ya
vinieron decididas de [spec.md](./spec.md) (*Clarifications*) y acá se traducen a dónde vive cada
cosa: **no se vuelven a decidir**.

Todo lo que sigue se verificó contra el código en `main` el 2026-09-10, después del merge del PR #28.

---

## D-01 · La columna es `varchar(120)` anulable, y el largo no es una casualidad

**Decisión**: `movimiento.nota`, `varchar(120)`, anulable, sin valor por omisión y sin índice.

**El largo es exactamente el límite, no más.** Es una consecuencia directa de que el límite se cuente
en caracteres Unicode (*Clarifications*): `varchar(120)` en `utf8mb4` cuenta **caracteres**, no bytes
—se reserva hasta 480 bytes y guarda los que hagan falta—, así que la unidad de la columna y la del
requisito son la misma. Una nota que las dos validaciones aceptan entra siempre, y una que no entra
nunca llegó a la base.

**Anulable** es lo decidido en *Clarifications*: el esquema admite tanto la ausencia de valor como la
cadena vacía y **no normaliza al escribir**. Es la primera columna de texto anulable del proyecto —
las nueve que hay son todas `IsRequired()`— y por eso vale decir en voz alta lo que trae: la
invariante de `FR-005` deja de estar garantizada por el almacenamiento y pasa a estar garantizada por
la lectura (D-04), con su costo anotado como **D12-08**.

**Sin índice, y eso es una decisión, no un olvido.** Un índice sobre la nota sólo sirve para buscar
por ella, y buscar por ella es exactamente lo que `FR-007` prohíbe. Ponerlo "por si acaso" sería
dejar servido el camino que la feature entera está evitando.

**Alternativa descartada — `varchar(255)` o `text`**: daría margen "por si el límite cambia". El
margen es el problema: con la columna más ancha que el requisito, el día que una validación falle la
base acepta la nota larga en silencio y el límite deja de existir sin que nada se ponga en rojo.
`decimal(11,2)` ya sigue este criterio para el techo del monto — el esquema entra exacto en el
requisito, a propósito.

---

## D-02 · El límite se cuenta en caracteres Unicode, en las dos pilas, sin traer nada

**Decisión**: contar **code points** de las dos formas que cada plataforma ya ofrece.

| Pila | Cómo se cuenta | Por qué no lo obvio |
|---|---|---|
| Backend | Enumerando los *runes* de la cadena | `string.Length` cuenta unidades UTF-16: un emoji da 2 |
| Frontend | Recorriendo la cadena con el iterador que ya tiene, que itera por code points | `.length` cuenta unidades UTF-16, igual que en C# |

Las dos formas son de la biblioteca estándar de cada lenguaje: **cero dependencias nuevas**
(`NFR-005`).

**Lo que esto compra**: las tres capas acuerdan qué significa 120. Una nota de 120 emoji se acepta en
la pantalla, se acepta en el servidor y entra en la columna. Contando UTF-16, esa misma nota se
rechazaría con un mensaje que dice "no puede superar los 120 caracteres" cuando la persona escribió
exactamente 120 — un mensaje que no se puede entender ni corregir.

**Alternativa descartada — grafemas** (lo que la persona realmente percibe como un carácter): es más
fiel a la expectativa, pero ninguna de las tres capas lo hace sola, el esquema no puede expresarlo y
obligaría a dimensionar la columna más ancha que el límite, que es justo lo que D-01 no quiere. Un
emoji compuesto por varios code points es raro en la descripción de un gasto; la fidelidad extra no
paga su costo.

---

## D-03 · El recorte lo hace el servidor antes de medir, y el cliente no recorta mientras se escribe

**Decisión**: el servidor recorta los espacios de los extremos y **después** mide el largo. El
cliente recorta al enviar, nunca al tipear.

**El orden importa y es verificable**: una nota de 120 caracteres visibles con un espacio a cada lado
tiene 122 antes de recortar. Midiendo primero se rechazaría algo que, una vez guardado, entra exacto
en la columna — la persona vería un rechazo por 120 sobre un texto que tiene 120.

**Que el cliente no recorte mientras se escribe** es lo que hace que el campo se pueda usar: recortar
en cada pulsación borra el espacio que alguien acaba de escribir entre dos palabras. El recorte es
una operación del envío, no de la edición.

**Consecuencia sobre `FR-005`**: una nota de sólo espacios queda en la cadena vacía después de
recortar, o sea "sin nota". El valor guardado es siempre el que se ve.

---

## D-04 · La normalización de lectura vive en `MovimientoDto`, no en los cuatro lugares que lo arman

**Decisión**: `MovimientoDto` normaliza la nota al construirse — la ausencia de valor sale como la
cadena vacía— y ningún endpoint lo hace por su cuenta.

**Por qué es la decisión central de la feature en el backend**: `MovimientoDto` se construye en
**cuatro** lugares de `MovimientosEndpoints.cs` —el alta, el listado, la consulta individual y la
edición—, y `FR-011` exige que las cuatro devuelvan lo mismo para una fila guardada sin valor. Con la
normalización repartida, la regla se cumple cuatro veces y se rompe la primera vez que aparezca un
quinto lugar; con la normalización en el tipo, el quinto lugar la hereda sin saber que existe.

Es el mismo argumento con el que `DeLaCuenta` es privado en `MovimientosConsulta` y con el que
`ValidacionDelMovimiento` es una sola para el alta y la edición: **la regla se hereda por
construcción en vez de depender de que alguien se acuerde**.

**El detalle técnico que lo hace funcionar, y que hay que saber**: dos de esos cuatro lugares están
dentro de una proyección sobre `IQueryable`. EF Core traduce una proyección a un tipo que no es una
entidad seleccionando las columnas y llamando al constructor **en memoria**, así que la normalización
del constructor corre igual en los cuatro casos. No es una suposición cómoda: es lo que el test de
`FR-011` verifica recorriendo **las cuatro rutas** contra una fila guardada sin valor. Si alguna vez
EF materializara de otra forma, el test se pone en rojo en vez de dejar pasar dos formas de lo mismo.

**Alternativa descartada — `COALESCE` en cada consulta**: escribir la coalescencia en las cuatro
proyecciones la resuelve en SQL y es perfectamente correcta hoy. Se descarta porque son cuatro
lugares que tienen que estar de acuerdo, que es la forma exacta del problema que se está evitando.

---

## D-05 · La validación entra en `ValidacionDelMovimiento`, que ya es una sola para el alta y la edición

**Decisión**: la regla del largo se agrega a `ValidacionDelMovimiento`, con la clave de error `nota`.
No se escribe dos veces.

Ese archivo ya existe con este propósito exacto y su comentario lo dice: *"un movimiento no puede
quedar, por vía de una edición, en un estado que el alta habría rechazado"*. La nota es el sexto campo
que pasa por ahí y no necesita ninguna estructura nueva.

**La clave del error es `nota`**, el nombre del campo, porque es lo que permite a la pantalla poner el
mensaje al lado de su control en vez de volcar un texto suelto. Del lado del frontend hay que
**agregar `nota` a `CAMPOS_CON_LUGAR`** en `CamposDelMovimiento.tsx`: esa lista es la que decide qué
errores del servidor tienen dónde ir, y una clave que no está en ella cae en la región general del
formulario. Es un renglón, y omitirlo produce el fallo exacto que el reparto de errores existe para
evitar — un mensaje que llega y no se muestra al lado de su campo.

**El mensaje no repite la nota.** Dice que se pasó del límite. Es la única entrada de texto libre de
la aplicación, y devolver el valor lo haría viajar de vuelta y aparecer en cualquier lugar donde el
mensaje termine.

---

## D-06 · `nota` es obligatoria en la edición, y así se expresa

**Decisión** (viene de *Clarifications*, acá sólo se traduce): en el DTO de la edición la nota es un
campo que **siempre viaja**; en el DTO del alta es opcional.

| Forma | La nota | Ausente significa |
|---|---|---|
| Lo que se manda al registrar | Opcional | Sin nota |
| Lo que se manda al modificar | **Obligatoria** | — (no es una posibilidad válida) |
| Lo que se devuelve | Siempre presente, nunca nula | — |

Es exactamente la asimetría que `fecha` ya tiene, y por el mismo motivo escrito en
`MovimientoDtos.cs`: **ausente nunca puede producir un cambio que nadie pidió**. En el alta, ausente
significa "sin nota" y eso es lo correcto. En la edición, ausente tendría que significar o "la que ya
tenía" —y entonces no habría forma de vaciarla sin inventar un centinela— o "sin nota" —y entonces un
cliente que no la manda borra en silencio lo que la persona escribió—. Exigirla saca las dos trampas.

**Consecuencia en el contrato del frontend**: `NuevoMovimiento` la declara opcional y
`MovimientoEditado` obligatoria, igual que hacen hoy con `fecha` pero al revés. Los dos tipos siguen
siendo tipos aparte, que es la razón por la que esta divergencia se puede expresar.

---

## D-07 · Un control de varias líneas dentro de `CampoConError`, y un comentario que deja de ser cierto

**Decisión** (viene de *Clarifications*): el campo es un control de varias líneas, envuelto por
`CampoConError` como los otros cinco.

`CampoConError` recibe el control como función y le pasa la tripleta `id` + `aria-invalid` +
`aria-describedby`, así que **acepta un control de varias líneas sin ningún cambio**: `FR-008` sale
gratis, con la etiqueta y el error asociados en el mismo lugar único donde ya se arman.

**`PRD:AC-55` no se rompe, y conviene ser preciso al respecto.** Ese AC pide que el formulario *se
recorra, se complete y se envíe íntegramente con el teclado*, y su test tabula hasta el botón y manda
Enter **sobre el botón**. Un control de varias líneas no interfiere con eso. Dos consecuencias reales,
las dos previstas:

1. **El test de teclado se pone en rojo** porque enumera el orden de tabulación control por control, y
   hay uno nuevo. Es su propósito declarado —su propio comentario dice que haberse puesto en rojo al
   agregar un botón *es la señal de que sirve*—, así que extenderlo es trabajo previsto y está en la
   tabla de D-12.
2. **El comentario de `CamposDelMovimiento.tsx` que afirma que el envío con Enter sale de cualquier
   campo deja de ser cierto**, porque dentro de un control de varias líneas Enter inserta un salto. Se
   corrige. El proyecto ya tiene la costumbre —`e214b6d`, `df8ba4a`— y el motivo es que un comentario
   que describe otra cosa es peor que ninguno.

**Dónde va el campo**: **último**, después de la fecha y antes del botón. Es el único campo opcional
del formulario, así que ponerlo al final deja el camino rápido de carga intacto: quien no lo usa
tabula hasta el botón como antes, con un paso más y ningún dato más. El orden del DOM es el orden de
lectura y el de tabulación, que es lo que la feature 011 exige.

---

## D-08 · La columna nueva, y qué pasa con los saltos de línea

**Decisión**: una columna más en la tabla del listado, entre la moneda y las acciones. El texto
completo va en el DOM; el ancho lo absorbe el envoltorio desplazable que ya existe.

**Por qué no desborda**: `c-listado-movimientos__desborde` está puesto desde la feature 011
precisamente para esto —su comentario dice *"seis columnas no entran en 360 px"*—, así que la séptima
entra por el camino que ya estaba preparado. La regla de ancho de la columna vive en
`componentes.css`, con las demás.

**Los saltos de línea se conservan en el dato y no significan nada en la presentación** (`FR-012`).
Es la única combinación honesta de las tres posibles:

- Transformarlos al guardar cambiaría en silencio lo que la persona escribió.
- Darles significado de presentación sería construir el formato que el PRD excluye (D12-06).
- Conservarlos sin darles significado deja el dato intacto y la pantalla simple: en el listado la nota
  se lee en una sola línea visual.

**`NFR-001` no necesita código, necesita un test.** El framework escapa el texto por omisión, así que
la nota ya se muestra como los caracteres que se escribieron. El riesgo real no es que falte una
protección: es que alguien la desactive a propósito "para que se vea mejor". El test de `AC-08` existe
para que eso rompa en rojo, y ésa es la única razón por la que existe.

---

## D-09 · `FR-007` se verifica con una barrera, y acá el criterio de la 011 da el resultado opuesto

**Decisión**: esta feature **agrega una barrera de shell**, `verificar-nota.sh`, y con ella un test que
inspecciona el SQL del listado.

La feature 011 decidió no agregar ninguna y dejó escrito el criterio, que es el que se aplica acá: las
barreras existen para proteger algo que **un test verde no distingue de un test que dejó de
verificar**. La pregunta es si la verificación de `FR-007` tiene esa forma. La tiene, y por dos
razones que se suman:

1. **Es una afirmación de ausencia.** El test tiene que decir que la nota **no** aparece en el `WHERE`
   de la consulta del listado. Una afirmación de ausencia hecha inspeccionando texto informa verde de
   las dos maneras: cuando la ausencia es real y cuando la inspección dejó de encontrar nada. Es
   exactamente la forma de `verificar-desglose.sh`, que vigila que el desglose no filtre por
   `categoria.activa`.
2. **El cambio que hay que impedir es un reflejo, no un descuido.** "Obvio que uno querría buscar por
   la nota" es lo primero que piensa cualquiera que lea el listado, igual que filtrar por categorías
   activas era el reflejo natural de quien acababa de sumar la baja lógica. Los daños silenciosos que
   este proyecto ya se comió vinieron todos de un reflejo razonable.

**El detalle que la hace delicada, y que es la mitad del argumento**: la nota **sí** tiene que aparecer
en el `SELECT` de esa misma consulta, porque el listado la muestra. Así que la afirmación no puede ser
"la palabra no aparece en el SQL" sino "aparece en la proyección y nunca en el filtro". Una
verificación con esa precisión es precisamente la que hay que ver fallar antes de creerle.

**Costo**: la barrera desarma la protección de una forma —le agrega el acotado por nota a la consulta—,
exige el rojo, restaura y exige el verde. Una recompilación, ~1 min. La puerta de cierre pasa de seis
barreras a siete.

---

## D-10 · Una sola migración con los dos cambios, y cero validación de aplicación sobre el catálogo

**Decisión**: **una** migración que agrega `movimiento.nota` y la restricción de tres letras sobre
`moneda.codigo` (`FR-010`, la deuda D11-02).

**Por qué juntas y no en dos**: la deuda venía esperando desde la feature 009 a *"el próximo ticket que
abra una migración por otro motivo"*, y en la 010 y la 011 el motivo de no saldarla fue siempre el
mismo — no había ninguna abierta. Con el plan DISC-001 terminándose en esta feature, **ya no hay un
próximo ticket al que apuntar**: o entra acá o queda abierta sin heredero.

**La restricción se expresa en el esquema y nada más.** Tres letras, comparadas con una expresión
regular en la definición de la tabla, que MySQL 8.4 evalúa en cada escritura. **No** se agrega
validación de aplicación: el catálogo de monedas se administra como dato —`PRD:RF-32`, y es lo que
`verificar-monedas.sh` vigila—, así que nadie lo escribe desde la aplicación y darle a la aplicación
una responsabilidad sobre una tabla que no toca sería inventar código sin llamador.

**Lo que ya estaba y por eso no hace falta**: `codigo` es `char(3) NOT NULL` desde la migración
`Inicial`, así que el largo está resuelto. Lo que falta y esto agrega es que sean **letras** — hoy un
`1X2` metido con SQL puro entra sin protesta y llega hasta `Intl`, que es el cuarto lugar donde D11-02
se podía cruzar.

**El catálogo sembrado sobrevive sin una corrección**: `ARS` y `USD` son tres letras. La migración se
aplica sobre datos válidos, que es lo que `AC-13` verifica en vez de suponer.

**Alternativa descartada — dos migraciones separadas**: más prolijo de leer en el historial, y el doble
de costo para el mismo resultado. Una migración con dos cambios y un nombre que los nombre a los dos
es más honesta que dos migraciones de las cuales una existe sólo por prolijidad.

---

## D-11 · El test de rendimiento del listado mide la respuesta de la API, y se dice

**Decisión**: `RendimientoListadoTests`, nuevo, midiendo el endpoint del listado con 1000 movimientos
**con nota**, 100 ejecuciones, techo de 2000 ms en el p95.

**No existe hoy**: hay tres tests de rendimiento —alta, resumen y límite de intentos— y **ninguno mide
el listado**. `PRD:AC-10` lo pide y es la primera vez que alguien lo pide, así que la afirmación de
`NFR-003` hoy no está respaldada por nada, ni antes ni después de esta feature.

**Mide la respuesta de la API, no el navegador**, y la spec lo dice en lugar de afirmar "el listado
carga en 2 s". Es lo que hacen los otros tres y es lo único medible sin traer un runner de navegador,
que `NFR-005` prohíbe. Es la misma honestidad que la 011 aplicó a los 360 px: se afirma lo que se
midió, y lo que no se puede medir queda anotado como deuda (**D12-01**).

**Lo que la medición tiene que ejercitar de verdad**: las 1000 filas se siembran **con nota**, y con
notas de largo realista y no vacías. Sembrarlas sin nota mediría la consulta de antes de esta feature
y daría verde sin haber ejercitado la columna nueva — el mismo error que la 009 evitó al exigir dos
monedas en el sembrado del resumen, porque agrupar 1000 filas que caen todas en el mismo grupo no
ejercita el agrupamiento.

**Queda fuera del CI** por el filtro `FullyQualifiedName!~Rendimiento` que `AGENTS.md` declara, como
los otros tres: mide tiempo de pared y en un runner compartido da rojos que no dicen nada. En local
corre.

**Lo esperable, para saber si el número sorprende**: el resumen agrupa las mismas 1000 filas en 6 ms.
El listado con una columna de texto más debería quedar en el mismo orden de magnitud, muy lejos del
techo de 2000 ms. Si diera cerca, el que está mal es el diseño y no el techo.

---

## D-12 · El presupuesto de tests existentes que se pueden tocar

`FR-020` de la feature 011 introdujo esta tabla y funcionó: es lo que separa "extender un test porque
el requisito lo obliga" de "ajustar un test hasta que pase". Se escribe **antes** de implementar, y al
cierre se comprueba con `git diff --stat main -- frontend/tests backend/GestionGastos.Api.Tests`. Un
test modificado que no esté acá se justifica o se revierte.

| # | Test | Por qué se toca | Requisito |
|---|---|---|---|
| 1 | `Contrato/ContratoMovimientosTests.cs` | El campo nuevo viaja en las tres formas del movimiento. El caso del alta arma el cuerpo a partir de los nombres **del contrato** con un `switch` por campo, así que hay que agregarle con qué valor ejercitar `nota` — el `switch` lanza una excepción con instrucciones cuando no lo sabe, así que **el rojo viene explicándose solo** | `FR-009` |
| 2 | `frontend/tests/TecladoFormulario.test.tsx` | Enumera el orden de tabulación control por control. El campo nuevo lo pone en rojo por diseño (D-07) | `FR-001`, `FR-008` |
| 3 | `frontend/tests/ListadoMovimientos.test.tsx` | La columna nueva cambia la forma de la tabla que este test afirma | `FR-006` |
| 4 | `frontend/tests/cliente.test.ts` | El alta y la edición mandan un campo más | `FR-002`, `FR-004` |
| 5 | `frontend/tests/VentanaDeEdicion.test.tsx` | La ventana trae la nota que el movimiento ya tenía y la manda de vuelta | `FR-004` |
| 6 | `frontend/tests/Accesibilidad.test.tsx` | Deriva los controles del árbol montado, así que cubre el campo nuevo sola. **Se toca sólo si la lista de superficies necesita el dato nuevo**; si pasa en verde sin cambios, no se toca | `FR-008` |
| 7 | `backend/.../Movimientos/` (tests de alta y edición existentes) | El DTO suma un campo; los casos que construyen peticiones completas lo incluyen | `FR-002`, `FR-004` |

**Lo que NO se toca, y es la parte importante de la tabla**: ningún test de `Resumenes/`, ninguno de
`Rendimiento/` salvo el nuevo, ninguno de aislamiento, ninguno de `Categorias/` y ninguno de la
pantalla de dashboard. Si alguno de ésos se pusiera en rojo, **el que está mal es el código de esta
feature**: la nota no toca los totales (`NFR-002`) y no toca el aislamiento. Un rojo ahí es
información, no un test para arreglar.

---

## D-13 · El orden del trabajo lo fija una dependencia real

**Decisión**: el esquema y el contrato primero, la pantalla después, la barrera y el rendimiento al
final.

1. **La migración y el dominio.** Sin la columna no hay nada que guardar, y sin el dominio el DTO no
   tiene de dónde leer. La restricción de `FR-010` viaja en esta misma migración (D-10).
2. **El contrato y la validación** (backend). Acá vive el rojo más barato de toda la feature: agregar
   `nota` al contrato del frontend pone en rojo los tests de `Contrato/` **antes** de que exista el
   campo en el DTO, y con un mensaje que dice qué falta. Es el mismo rojo que la 011 usó para
   `decimales`.
3. **La pantalla**: el campo en el formulario compartido —que le llega al alta y a la edición de una
   sola vez, porque `CamposDelMovimiento` es uno— y la columna en el listado.
4. **La barrera de `FR-007`** (D-09) y **el test de rendimiento** (D-11). Van al final porque
   verifican propiedades del conjunto ya construido: uno afirma que la consulta no filtra por nota, el
   otro mide el listado con la columna puesta. Ninguno de los dos tiene sentido antes.

**La historia 3 —el `CHECK` de las tres letras— es independiente de las otras dos** y viaja con la
migración del paso 1 por comodidad, no por dependencia. Si hubiera que recortarla, se saca de la
migración sin tocar nada más: es la razón por la que está en la prioridad más baja de la spec.

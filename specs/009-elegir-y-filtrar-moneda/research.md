# Research — Elegir y filtrar la moneda de un movimiento

**Feature**: 009 · **Fecha**: 2026-09-04 · **Spec**: [spec.md](./spec.md)

Las decisiones de diseño que el plan da por tomadas. Cada una dice qué se eligió, por qué, y qué se
descartó — porque lo descartado es lo que alguien va a proponer de nuevo dentro de seis meses.

---

## D-01 — La moneda viaja por identificador, no por código

**Decisión**: el cuerpo de la petición lleva `monedaId` (numérico), no `moneda: "USD"`. La respuesta
sigue llevando `monedaCodigo`, como hasta ahora.

**Motivo**: es exactamente la forma que el contrato ya usa para la categoría — `categoriaId` para
elegir, `categoriaNombre` para mostrar. Estrenar una segunda convención de referencia dentro del
mismo cuerpo obliga a quien lo lee a saber cuál de los dos campos es cuál clase de cosa, y no compra
nada. Además el identificador es el que la base ya usa como clave foránea (`movimiento.moneda_id`),
así que la validación es una búsqueda por clave primaria y no una comparación de texto.

**Alternativas descartadas**:

- **El código ISO** (`moneda: "USD"`). Se lee mejor en un `curl` y sería estable frente a un
  re-sembrado del catálogo. Pero obliga a buscar por una columna de texto cuya collation decide si
  `usd` es lo mismo que `USD` — una decisión de comparación que hoy no existe y que habría que
  tomar, documentar y probar. Y no evita la validación: un código que no está en el catálogo hay
  que rechazarlo igual.
- **Las dos cosas, aceptando cualquiera**. Dos vías de entrada para el mismo dato es dos veces la
  superficie a validar y la pregunta permanente de qué gana si llegan las dos.

---

## D-02 — Ausente significa cosas distintas en el alta y en la edición, y las dos son "no cambies nada"

**Decisión**: `monedaId` es **opcional en las dos**. Ausente o `null` significa:

| | Significado | Por qué |
|---|---|---|
| Alta | La predeterminada del catálogo | `PRD:NFR-01`: quien usa una sola moneda no agrega ni un paso. Y es la compatibilidad hacia atrás del contrato: todo cliente que hoy no manda el campo sigue funcionando |
| Edición | **La que el movimiento ya tenía** | Ausente no puede significar un cambio silencioso |

**El contraste con `fecha` es la parte que hay que entender.** `fecha` es opcional en el alta y
**obligatoria** en la edición, y está documentado en `MovimientoDtos.cs` con su razón: ausente
significaría "hoy", o sea que una edición sin fecha movería el movimiento sin que nadie lo pidiera.
La moneda no tiene ese problema porque su ausencia en la edición no significa un valor nuevo:
significa el que ya estaba. La regla común, y la que hay que citar cuando alguien proponga
uniformar, es **"ausente nunca puede producir un cambio que nadie pidió"** — no "todos los campos se
comportan igual".

**Consecuencia sobre las barreras**: `ContratoMovimientosTests` arma el cuerpo del `POST` y del
`PUT` **con los nombres que declara el contrato** y tiene un `switch` que **lanza** ante un campo
que no sabe ejercitar. Agregar `monedaId` a `NuevoMovimiento` y a `MovimientoEditado` en `tipos.ts`
pone esos dos tests en rojo hasta que se les agregue el caso. Eso es la barrera funcionando, no un
obstáculo: es lo que garantiza que un campo del contrato no quede sin ejercitar.

---

## D-03 — `GET /api/monedas` es un endpoint nuevo, y **no** estrena un canal de lectura

**Decisión**: se agrega `GET /api/monedas`, que devuelve el catálogo entero ordenado, con el código,
el nombre, el símbolo y cuál es la predeterminada. La lectura se escribe directo contra
`contexto.Monedas`, **sin** una clase `MonedasConsulta` que haga de canal.

**Motivo**: los canales `MovimientosConsulta` y `CategoriasConsulta` existen por una razón concreta
y escrita — el acotado por cuenta o por ámbito se escribe a mano, así que una consulta nueva nace
sin él salvo que alguien se acuerde, y `BarreraDeAislamientoTests` vigila que nadie lea fuera del
canal. **La moneda no tiene dueño.** Es una tabla del sistema, igual para todas las cuentas, sin
`usuario_id` que acotar. Un canal acá sería una clase que no protege nada, y peor: sugeriría que hay
algo que aislar, que es justo la confusión que la barrera evita.

No es una excepción inventada para esta feature: `MovimientosEndpoints` ya lee `contexto.Monedas`
directo en dos lugares, desde FEAT-001a, y la barrera nunca tuvo nada que decir.

**Lo que sí exige el endpoint nuevo**: sesión. `verificar-autorizacion.sh` se pone en rojo ante un
endpoint sin proteger, así que esto se verifica solo — pero se anota acá para que nadie lo lea como
"un catálogo público no necesita sesión".

**Alternativa descartada**: **colgar el catálogo de la respuesta del resumen**, que ya devuelve una
entrada por moneda. Sería un endpoint menos, y está mal por dos motivos: el resumen devuelve monedas
*de un período*, con totales, y pedirlo para llenar un selector traería un cálculo entero que nadie
va a mirar; y ataría el selector al resumen, con lo cual el día que el resumen se filtre por moneda
(`RF-30`, D9-02) el selector se quedaría sin las monedas filtradas. Un catálogo es un catálogo.

---

## D-04 — La moneda se valida en `ValidacionDelMovimiento`, con la misma forma que la categoría

**Decisión**: el endpoint busca la moneda por identificador y le pasa la `Moneda?` encontrada al
validador, que reporta el error bajo la clave **`monedaId`** — el nombre del campo de la petición.
Vale para el alta y para la edición, en la **misma** función.

**Motivo**: es literalmente el patrón de la categoría, y la razón está escrita en
`ValidacionDelMovimiento`: la validación es una sola porque *"un movimiento no puede quedar, por vía
de una edición, en un estado que el alta habría rechazado"*. Dos validaciones parecidas divergen el
día que alguien toca una. Y la clave del error tiene que ser el nombre del campo porque es lo que le
permite al frontend poner el mensaje al lado de su control.

**Consecuencia en el frontend**: `CAMPOS_CON_LUGAR` en `FormularioMovimiento.tsx` enumera los campos
que tienen dónde mostrar su error. Si `monedaId` no se agrega ahí, el mensaje cae en la región
general en vez de al lado del selector. Funciona, pero peor, y en silencio: es una tarea, no un
detalle.

**Alternativa descartada**: validar en el endpoint con un `if` antes de llamar al validador. Es lo
que hace hoy la categoría con su búsqueda por ámbito, pero ahí el `if` decide *qué buscar*, no *si
es válido* — el veredicto lo da igual el validador. Mantener esa división.

---

## D-05 — El acotado por moneda entra en `DeLaCuenta`, y el resumen no lo recibe

**Decisión**: `MovimientosConsulta.Filtrado` recibe un `monedaId` opcional más, y la condición se
escribe dentro de `DeLaCuenta` —el método privado por el que ya pasan las tres consultas— con la
misma forma que la de categoría: `.Where(m => monedaId == null || m.MonedaId == monedaId)`.
`Agrupado`, que alimenta el resumen, lo pasa en `null` **explícitamente**.

**Motivo**: el `AND` de los acotados ya está escrito una sola vez y probado; el tercero se suma
donde están los otros dos o deja de estar garantizado que se combinen. Y `DeLaCuenta` es lo que hace
que el aislamiento se herede por construcción.

**El `null` explícito de `Agrupado` es la parte delicada.** El resumen **no** se filtra por moneda en
esta feature: eso es `RF-30`, la deuda D9-02, y es del ticket 5. Un `monedaId` que se colara hasta
`Agrupado` cambiaría los totales de un período sin que nadie tocara un movimiento — el mismo daño
silencioso, y por el mismo mecanismo, que `verificar-desglose.sh` vigila para `categoria.activa`.
Se escribe explícito y con el comentario que dice por qué, igual que allá.

---

## D-06 — El catálogo de monedas se pide una vez en la raíz y baja por props

**Decisión**: `App.tsx` pide `GET /api/monedas` una vez, junto al catálogo de categorías, y lo baja
por props a la pantalla de movimientos, que se lo pasa al formulario, al control de acotado y a la
ventana de edición.

**Motivo**: es la solución que la feature 007 ya construyó para categorías y documentó como su D-08,
y el propio `PRD:NFR-02` dice que hereda ese criterio. El riesgo que cierra está escrito en el PRD:
dos copias del catálogo que pueden discrepar — el selector ofreciendo una lista y el acotado otra.
Con una sola lectura en la raíz, discrepar es imposible por construcción, no por disciplina.

**Alternativa descartada**: que cada componente pida el suyo. Es lo que la 007 ya rechazó, y
repetirlo acá sería reintroducir el defecto que ese ticket sacó.

---

## D-07 — La ventana emergente es un `<dialog>` nativo

**Decisión**: la edición se abre en un `<dialog>` con `showModal()`. Sin librería nueva.

**Motivo**: `<dialog>` trae de fábrica lo que una ventana modal necesita y lo que una hecha con
`<div>` hay que reimplementar mal — el foco atrapado adentro, el cierre con `Escape`, el fondo
inerte, el rol de accesibilidad y el `::backdrop`. `AGENTS.md` dice que no se agregan librerías sin
justificarlas en la spec, y acá no hay nada que justificar: la plataforma ya lo hace.

**Verificado, no supuesto**: el entorno de tests del frontend es **happy-dom 20.11.15** —no jsdom,
por la razón que `vite.config.ts` documenta—, y la duda razonable era si implementa
`HTMLDialogElement`. Se comprobó corriendo: `showModal()` es una función, deja `open` en `true`, y
`close()` lo devuelve a `false`. Si no lo hubiera implementado, la decisión habría sido otra y esto
lo habríamos descubierto a mitad de la implementación.

**Alternativa descartada**: un `<div role="dialog">` con manejo de foco a mano. Más código, peor
accesibilidad, y una clase de defecto —el foco que se escapa al fondo— que sólo aparece cuando
alguien navega con teclado, o sea nunca durante el desarrollo y siempre para quien lo necesita.

---

## D-08 — La ventana de edición reusa el formulario, no lo copia

**Decisión**: los campos y las reglas del formulario de alta se extraen a un componente que sirve a
los dos usos, parametrizado por los valores iniciales y por la etiqueta del botón. La ventana de
edición lo monta adentro del `<dialog>`.

**Motivo**: es el mismo argumento que el backend ya escribió para su validación, y vale igual acá:
dos formularios parecidos divergen el día que alguien toca uno. La validación de cliente
—monto, categoría del tipo correcto, la selección que dejó de estar en el catálogo— es idéntica en
los dos casos, y ya está escrita y probada una vez.

**Las tres diferencias reales**, que la parametrización tiene que admitir: los valores iniciales
(vacíos contra los del movimiento), la etiqueta del botón, y que **la fecha es obligatoria al
editar** (D-02). Ninguna toca las reglas.

**Alternativa descartada**: un componente de edición propio. Más rápido de escribir hoy, y el
primer lugar donde va a aparecer una diferencia de comportamiento que nadie decidió.

---

## D-09 — La medición de rendimiento agrega un caso y deja el existente intacto

**Decisión**: `RendimientoAltaTests` ya mide el p95 del guardado sobre **100** ejecuciones y exige
< 1 s (AC-34, `RNF-02`). Se le agrega un caso que mide lo mismo **mandando la moneda explícita**, y
el caso existente **no se toca**.

**Motivo**: es el criterio D-03 de la feature 008, que ya se usó para el resumen con dos monedas y
funcionó: el caso viejo es la referencia que permite atribuir un rojo al costo de lo nuevo y no al
volumen o a la máquina. Sin él, un p95 que empeora no dice si la culpa es del `SELECT` de la moneda
o de que el runner estaba ocupado.

El arnés ya está en 100 ejecuciones, así que acá no hay que subir nada: la 008 hizo ese trabajo para
el resumen y `RendimientoAltaTests` ya venía así.

---

## D-10 — Ningún test escribe un número fijo sobre el tamaño del catálogo

**Decisión**: se hereda entera la regla que la feature 008 dejó anotada como **D8-08**. Los tests que
necesiten "otra moneda" la agregan con el helper `ConLaMonedaAsync` de `MonedaComoDatoTests`, que la
borra en un `finally`; y todo lo que se afirme sobre el catálogo se compara **contra el catálogo**,
nunca contra un literal.

**Motivo**: `verificar-monedas.sh` corre los tests **con una moneda de más puesta en la base**. Un
`Assert.Equal(2, monedas.Count)` pasa en la suite y se rompe en la barrera — y ya pasó una vez, con
el `2` escrito a mano de AC-09 de la 008, que lo encontró el quickstart y no la suite.

**Esta feature multiplica las oportunidades de romperlo**, y por eso la regla se repite acá en vez de
quedar en la spec de la anterior: `GET /api/monedas`, el selector, el control de acotado y la ventana
de edición son cuatro lugares nuevos donde a alguien le va a resultar natural escribir "tiene que
haber dos".

---

## D-11 — Que el selector salga del catálogo se verifica, y la barrera se extiende al frontend

**Decisión**: dos cosas.

1. Un test de frontend le pasa al selector y al control de acotado un catálogo **con una moneda que
   ninguna constante del código conoce** y exige que aparezca. Es `PRD:AC-04` del lado de la
   pantalla.
2. `verificar-monedas.sh` extiende su comprobación de árbol limpio de `backend/GestionGastos.Api/`
   a **`frontend/src/`**.

**Motivo**: la 008 verificó que sumar una moneda cuesta 0 líneas y 0 recompilaciones, y construyó una
barrera entera para sostenerlo. **Esa barrera hoy sólo mira el backend**, porque hasta hoy el
frontend no tenía nada que decir sobre monedas. Desde esta feature sí: un `const MONEDAS = ['ARS',
'USD']` escrito en el frontend pasaría `verificar-monedas.sh` en verde y dejaría la promesa de
producto rota del único lado que el usuario mira.

**No es una barrera nueva**: es la sexta que crece para cubrir la superficie que esta feature le
agrega. Sigue siendo la misma afirmación —sumar una moneda es sólo un dato— medida con los mismos
dos mecanismos.

**Alternativa descartada**: correr la suite del frontend dentro de `verificar-monedas.sh` con la
moneda puesta. No serviría: los tests del frontend no hablan con la base, le pasan el catálogo a
mano. Lo que el script puede afirmar sobre el frontend es que **nadie tuvo que tocarlo**, y eso es
exactamente lo que se le pide.

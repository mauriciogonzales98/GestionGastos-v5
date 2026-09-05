# Research: Dashboard con gráficos

**Feature**: `010-dashboard-con-graficos` · **Fecha**: 2026-09-05

Las decisiones de diseño, con su motivo y lo que se descartó. Las cita `plan.md` y las heredan
`tasks.md` y la implementación.

El contexto que las condiciona a todas, y que salió de reconciliar el PRD contra el código: **el
backend de esta feature ya está construido y medido**. `GET /api/resumen` devuelve exactamente lo
que el dashboard necesita, agregado en la base, y `RendimientoResumenTests` ya mide los dos
escalones de `RNF-01` incluido el de 10000 movimientos. Esta feature es, casi entera, **frontend**.
Por eso la mayoría de las decisiones de abajo son sobre la pantalla.

---

## D-01 · El gráfico se dibuja a mano, sin ninguna dependencia nueva

**Decisión**: no se agrega ninguna librería de gráficos. El gráfico son elementos del DOM
dimensionados por CSS a partir de un porcentaje calculado en el render.

**Motivo**: `AGENTS.md` exige justificar toda dependencia nueva en la spec, y `PRD:RF-19` justifica
que haya **un gráfico**, no que haya una librería. Lo que este dashboard necesita dibujar es una
barra por categoría cuya longitud sea proporcional a un total: es una división y un `width` en
porcentaje. La superficie que una librería trae —ejes, escalas, tooltips, animaciones, temas,
responsive observers— es toda superficie que hay que mantener, auditar y actualizar para no usarla.

Y hay un motivo que pesa más que el tamaño: **`PRD:RNF-06` y `FR-008` exigen que cada valor del
gráfico se pueda leer sin interpretarlo, y los tests de esta feature tienen que poder afirmar sobre
esos valores.** Una librería que dibuja en `<canvas>` —Chart.js es la más común— produce píxeles:
nada que un lector de pantalla anuncie, nada que happy-dom pueda consultar, nada sobre lo que
`@testing-library` pueda hacer una aserción. Elegir esa clase de librería obligaría a construir el
equivalente textual **igual**, y encima por duplicado.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| **Chart.js** | Dibuja en `<canvas>`. Invisible para los tests y para el árbol de accesibilidad; obligaría a mantener el dato dos veces |
| **Recharts** | Sí produce SVG, y es la opción seria. Pero son ~500 kB de dependencia transitiva (D3 incluido) para dibujar rectángulos, y arrastra su propio ciclo de compatibilidad con React 19 |
| **Una librería sólo para el eventual gráfico de torta** | No hay torta (D-02), y `PRD` deja el tipo de gráfico a criterio de diseño |

**Se registra como ADR**, que es lo que el PRD pide: [ADR-002](../../docs/adr/ADR-002-el-grafico-sin-dependencia.md).

---

## D-02 · Barras horizontales, no torta

**Decisión**: el gráfico es un conjunto de barras horizontales, una por categoría, ordenadas de
mayor a menor.

**Motivo**: tres razones, en orden de peso.

1. **El nombre de la categoría cabe al lado de su barra.** En una torta el nombre no cabe en el
   sector y termina en una leyenda, o sea a varios centímetros de su valor — que es exactamente la
   separación que `FR-008` existe para evitar.
2. **Escala con el catálogo.** Las categorías de gasto son siete predefinidas **más las propias**
   (007), que no tienen tope. Una torta con veinte sectores es ilegible; veinte barras son una lista
   larga, que es un problema mucho menor.
3. **La longitud se compara mejor que el ángulo.** Es lo que la barra codifica: `total / mayor`.

**Alternativa descartada**: torta o dona. Además de lo anterior, obligaría a calcular el total
general de la moneda para sacar los porcentajes — un número más que la pantalla tendría que derivar,
y `FR-014` prefiere que la pantalla no derive nada.

---

## D-03 · El equivalente textual no acompaña al gráfico: **es** el gráfico

**Decisión**: no hay un gráfico y al lado una tabla con los mismos números. Hay **una sola
estructura** —una fila por categoría, con su nombre, su total y una barra dimensionada
proporcionalmente— y la barra es un elemento decorativo (`aria-hidden`) dentro de esa fila.

**Motivo**: es la decisión más importante de la feature y la que hace que `FR-008` no cueste nada.

Un gráfico y una tabla con los mismos datos son **dos representaciones que pueden discrepar**, y es
la misma clase de problema que `ResumenEndpoints` resolvió con *"es un endpoint y no dos"* y que
`CalculoDelResumen` resolvió componiendo los cuatro números de las mismas filas. Acá se aplica el
mismo criterio una capa más arriba: **la barra no es un dibujo del número, es el número con un ancho
puesto.**

Consecuencias, todas buenas:

- `FR-008` se cumple por construcción: el nombre y el total están en el DOM porque son el contenido
  de la fila, no porque alguien se acordó de agregar una versión accesible.
- Los tests afirman sobre texto —`getByRole('row')`, el nombre, el total formateado— y **nunca sobre
  píxeles ni sobre el ancho de nada**. Un test que verificara el ancho de la barra estaría
  verificando la división, que es lo único que puede fallar ahí y se prueba aparte.
- Un lector de pantalla lee una tabla de totales, que es lo que el dato es.

---

## D-04 · Las categorías **no** se codifican por color

**Decisión**: todas las barras usan el mismo relleno. Las categorías se distinguen por su **nombre,
escrito al lado de su barra**, y por su posición en el orden.

**Motivo**: `PRD:NFR-03` exige distinguir cada categoría *"por algún atributo además del color"*. La
lectura habitual de eso es *"agregá un patrón encima del color"*. Hay una lectura mejor: **no
codificar por color en absoluto**. Si el color no lleva información, no hay nada que un daltonismo
pueda quitarle, y el requisito se cumple de la forma más fuerte posible en vez de la mínima.

Y hay una razón de proyecto que lo confirma. `frontend/src/estilos/base.css` dice, en su primer
comentario: *"Colores, espaciados y tipografía son del ticket 6. Acá va únicamente lo que sale caro
retrofitear."* **El proyecto no tiene paleta**: no hay un solo color declarado en todo `estilos/`.
Inventar acá una paleta categórica de siete a veinte colores —con su contraste verificado— sería
hacer el trabajo del ticket 6 a las apuradas, para que el ticket 6 lo rehaga.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Paleta categórica + patrones SVG | Es el trabajo del ticket 6, hecho antes de tiempo y sin su contexto. Y una paleta accesible de más de ocho categorías es un problema difícil de verdad, para un beneficio que acá no existe: el nombre ya está al lado |
| Un color por **tipo** (gasto/ingreso) | El desglose es sólo de gastos (`PRD` lo excluye explícitamente para ingresos). No hay dos tipos que distinguir |

**Lo que esto deja al ticket 6**: cuando exista la paleta, ponerle color a estas barras es cambiar un
relleno en un solo lugar. Queda anotado como deuda.

---

## D-05 · El acotado por moneda es de presentación y no toca el servidor

**Decisión**: el filtro de moneda del dashboard elige cuál de los bloques que el servidor ya devuelve
separados se muestra. No viaja ningún parámetro nuevo, no se pide nada de nuevo al cambiarlo.

**Motivo**: es la respuesta de la sesión de aclaración, y el código de la 009 la respalda. `Agrupado`
lleva escrito `monedaId: null` **explícito** con este comentario: *"El acotado por moneda es del
LISTADO. Si se colara hasta acá, los totales de un período ya cerrado darían otro número sin que
nadie tocara un movimiento"*, y lo sostiene
`ResumenDelPeriodoTests.El_Resumen_No_Hereda_El_Acotado_Por_Moneda_Del_Listado`.

Como `PRD:RF-29` ya obliga a que nada se sume a través de monedas, **ningún número del resumen
depende de qué monedas se pidan**. Así que filtrar es mostrar menos de lo que ya llegó: no es sumar
en el cliente —no se suma nada— y no puede hacer que un total difiera de sí mismo, que es lo que
`FR-013` pide.

**Beneficio no buscado**: cambiar de moneda es instantáneo y cuesta cero peticiones. El rango de
fechas sí viaja, porque sí cambia el cálculo.

**Alternativa descartada**: `monedaId` en `GET /api/resumen`. Ahorra unos bytes de respuesta y a
cambio reabre una decisión que la 009 blindó a propósito, obliga a decidir qué pasa con `AC-07` —las
monedas sin movimientos que tienen que aparecer en cero— y agrega una petición por cada cambio de
filtro.

---

## D-06 · El resumen **no** se iza a `App`, a diferencia de los catálogos

**Decisión**: cada pantalla pide su propio resumen. La principal lo pide sin período; el dashboard lo
pide con el suyo. El estado no vive en `App.tsx`.

**Motivo**: parece contradecir la D-08 de la feature 007 —*"el catálogo vive acá y en ningún otro
lado"*— y no la contradice: **la aplica**. Aquella decisión izó los catálogos porque las dos
pantallas necesitan **el mismo dato**, y dos copias se desincronizan. Acá pasa lo contrario: la
principal y el dashboard necesitan **dos períodos distintos** del mismo cálculo.

Izar un único `resumen` haría que elegir un trimestre en el dashboard cambiara los números de la
pantalla principal. Eso no es un riesgo teórico: **es exactamente lo que `FR-012` prohíbe**, y el
único requisito de esta feature cuya violación sería invisible en la pantalla donde se produce.

La regla que queda, y que conviene escribir porque las dos mitades importan: **se iza lo que es el
mismo dato para todos; no se iza lo que cada pantalla parametriza distinto.**

---

## D-07 · El dashboard es una tercera `Vista`, no una ruta

**Decisión**: `type Vista = 'movimientos' | 'categorias' | 'dashboard'` en `App.tsx`, con su botón
para ir y su botón para volver, igual que la pantalla de categorías.

**Motivo**: es la D-09 de la feature 007 sin cambios — *"no son dos rutas que enrutar sino un estado
con dos valores"*—, ahora con tres. No hay router en el proyecto y esta feature no es motivo para
traer uno: nadie navega a `/dashboard`, se llega apretando un botón.

`VISTA_INICIAL` sigue siendo `'movimientos'` y sigue siendo a donde se vuelve al cerrar sesión, por
la misma razón de siempre: que la próxima cuenta no entre donde salió la anterior.

---

## D-08 · Las reglas del período **no** se reimplementan en la pantalla

**Decisión**: los dos campos de fecha se mandan tal como están y el rechazo lo emite el servidor. La
pantalla muestra el mensaje que vuelve, junto al control, usando la clave `rango`.

**Motivo**: `PeriodoPedido` lleva escrito que es *"el único intérprete de `desde` y `hasta`"* y que
con dos intérpretes la igualdad entre vistas *"depende de que nadie toque uno sin tocar el otro"*.
Validar en la pantalla que la fecha de inicio no sea posterior a la de fin sería el segundo
intérprete, con sus propias palabras y su propio criterio de "hoy".

Y hay un detalle que lo hace gratis: la clave del error **ya es** `rango`, y su comentario dice
textualmente que existe *"porque el frontend la usa para poner el mensaje al lado del control"*. Se
escribió para este momento.

**Lo único que la pantalla decide**: que los dos campos vacíos significan *sin período pedido*, o
sea el mes en curso — que es lo que el servidor ya hace ante la ausencia de los dos parámetros.

---

## D-09 · Un pedido viejo nunca pisa al vigente

**Decisión**: el efecto que pide el resumen descarta la respuesta si el período que la pidió ya no es
el vigente. Es el mismo patrón que la 009 dejó en `PantallaMovimientos` para el acotado por moneda.

**Motivo**: es una cicatriz, no una precaución. La feature 009 lo encontró en revisión —*"la
respuesta de un acotado viejo deja de pisar a la vigente"*, `22e3e96`— y el dashboard tiene la misma
forma: un control que cambia y dispara una petición cuya respuesta puede volver desordenada. Con un
rango de un año sobre 10000 movimientos la ventana es más ancha que la del listado, no más angosta.

De la misma revisión se heredan otras dos, y por el mismo motivo:

- **El cartel de un fallo no sobrevive a una carga que salió bien** (`10a2e6d`): el error se limpia
  al empezar cada carga, no sólo al fallar.
- **Un fallo de carga se dice** (`b0bc50e`): nada de catch silencioso, que `AGENTS.md` prohíbe. Y se
  dice **como fallo**, que es `FR-010`: mostrar ceros ante un servidor caído sería la pantalla
  afirmando que no hubo movimientos.

---

## D-10 · "No hay datos" sale de los ceros del servidor, no de una cuenta de la pantalla

**Decisión**: la indicación de que no hay datos para graficar se decide mirando si el desglose que
llegó está vacío. No se suman totales ni se cuentan movimientos para averiguarlo.

**Motivo**: `FR-009` y `FR-014` juntos. El servidor ya garantiza una entrada por cada moneda del
catálogo con sus totales en cero (decisión D-05 de la feature 006, tomada *"para que un período sin
movimientos devuelva ceros en lugar de una lista vacía"*), así que la pantalla tiene la respuesta
servida: `gastosPorCategoria` vacío es *no hay nada que graficar*, y los ceros se muestran como lo
que son.

Los tres estados son distintos y no se confunden: **cargando**, **no hay datos** (ceros, sin error) y
**no se pudo cargar** (error, sin ceros).

---

## D-11 · La medición existente se cita, no se duplica

**Decisión**: `RendimientoResumenTests` suma la cita de `PRD:AC-11` y `PRD:AC-12` en la documentación
de sus casos. No se escribe una medición nueva del backend.

**Motivo**: el Principio II de la constitución pide que cada AC tenga un test **que lo nombre**, y
hoy ese test nombra `RNF-01`, `AC-04` y `FR-011` de la feature 006. Los dos escalones que `AC-11` y
`AC-12` piden —1000 en < 2 s, 10000 en < 4 s, 100 ejecuciones, p95— son exactamente los dos
`InlineData` que ya están escritos. Duplicar la medición sería agregar dos minutos de suite para
medir lo mismo dos veces.

**Lo que sí hay que hacer**: correrla. Está excluida del CI por medir tiempo de pared, así que el
número sale de la puerta local, y va anotado en el quickstart.

---

## D-12 · El contraste se verifica sobre los colores declarados, y el verificador se ve fallar

**Decisión**: un test calcula la relación de contraste WCAG de los pares color/fondo que el dashboard
declara y exige 4,5:1 y 3:1 según corresponda. El cálculo se prueba a su vez contra pares de
contraste conocido, incluido **uno que tiene que dar por debajo del umbral**.

**Motivo**: `PRD:AC-13` es un AC como cualquier otro y necesita su test (Principio II), pero un test
de contraste que nunca se vio fallar no dice nada — es el Principio V aplicado a algo que no es un
script. Probar el calculador contra un par que sabemos que falla es lo que separa *"el test pasa"* de
*"el test sabe distinguir"*.

**Alcance honesto, y conviene decirlo**: D-04 deja al dashboard sin paleta propia, así que lo que hay
para verificar es poco —el relleno de las barras contra su fondo, y el texto sobre el suyo—. El
contraste general de la aplicación es del ticket 6, junto con los colores. Este test nace chico y
queda listo para cuando haya más que medir.

**Alternativa descartada**: una herramienta de auditoría automática (`axe`, `pa11y`). Es una
dependencia nueva de peso, y sobre una aplicación sin paleta declarada no tendría casi nada que
auditar. Cuando el ticket 6 traiga los colores, ahí la discusión vale la pena.

---

## D-13 · Ninguna barrera nueva

**Decisión**: esta feature no agrega un séptimo script `verificar-*.sh`.

**Motivo**: las seis que hay ya cubren lo que esta feature podría romper, y una de ellas la cubre sin
que haya que tocarla. `verificar-monedas.sh` vigila `frontend/src/` desde la 009, así que **el
selector de moneda del dashboard nace protegido**: si alguien lo llenara con una lista escrita a
mano, la barrera se pone en rojo sin que este ticket agregue nada. `FR-007` se cumple con la barrera
que ya existe.

`verificar-desglose.sh` es la otra que importa acá: garantiza que el desglose que el dashboard pinta
no filtre por `categoria.activa`, o sea `FR-015`. Tampoco hay que tocarla.

Lo que sí hay que tener presente en la puerta: **el contrato no cambia** (ver `contracts/api.md`), así
que `verificar-contrato.sh` no tiene trabajo nuevo — pero se corre igual, porque el cierre de feature
lo exige y porque `tipos.ts` se toca aunque sea para nada.

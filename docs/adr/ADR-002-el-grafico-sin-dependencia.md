# ADR-002: El gráfico del dashboard se dibuja sin ninguna dependencia

**Estado**: aceptada · **Fecha**: 2026-09-05 · **Feature**: `010-dashboard-con-graficos`

## Contexto

`PRD:RF-19` pide que los totales por categoría se representen **gráficamente**. `AGENTS.md` pide que
toda librería nueva se justifique en la spec, y el PRD del ticket 5 lo anticipó como un riesgo:

> *el gráfico exige una dependencia nueva… Lo que el PLAN tiene que evaluar es el costo: una
> librería de gráficos suele traer más superficie de la que este dashboard necesita, y un gráfico de
> barras se puede dibujar con SVG sin dependencia alguna. La decisión se registra como ADR.*

Al mismo tiempo, `PRD:RNF-06` exige que cada valor representado se pueda leer **sin interpretar el
gráfico**, y la constitución exige que cada criterio de aceptación tenga un test automatizado.

## Decisión

**No se agrega ninguna librería de gráficos.** El gráfico son elementos del DOM: una fila por
categoría con su nombre, su total y una barra cuyo ancho es proporcional al mayor total de esa
moneda.

## Motivo

1. **Lo que hay que dibujar es una división.** El ancho de la barra es `total / mayor`. No hay ejes
   que escalar, ni series temporales, ni interacción: `PRD` excluye explícitamente la evolución en
   el tiempo, la elección del tipo de gráfico y la exportación.
2. **Una librería de `<canvas>` sería activamente peor.** Chart.js —la opción más común— produce
   píxeles: nada que un lector de pantalla anuncie, nada que happy-dom pueda consultar, nada sobre
   lo que se pueda hacer una aserción. Obligaría a construir el equivalente textual igual, y
   entonces el dato viviría dos veces y podría discrepar consigo mismo.
3. **La superficie no usada igual se mantiene.** Una librería de gráficos trae temas, animaciones,
   tooltips, responsive observers y su propio ciclo de compatibilidad con React. Todo eso hay que
   actualizarlo y auditarlo para no usarlo.
4. **El proyecto no tiene paleta.** `frontend/src/estilos/base.css` dice que colores y tipografía
   son del ticket 6. Una librería de gráficos llega con su tema por defecto y lo impone.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| **Recharts** | Produce SVG, que es lo correcto, y era la candidata seria. Pero son ~500 kB de dependencia transitiva —D3 incluido— para dibujar rectángulos, con su propio ciclo de compatibilidad con React 19 |
| **Chart.js** | `<canvas>`: invisible para los tests y para el árbol de accesibilidad |
| **Una librería de sparklines o micro-charts** | Menos peso, mismo problema de fondo: una dependencia para una división |

## Consecuencias

- **A favor**: cero dependencias nuevas; los tests afirman sobre texto y roles, nunca sobre píxeles;
  el equivalente textual de `RNF-06` no es un añadido sino la estructura misma del gráfico.
- **En contra**: si alguna vez el producto pide gráficos de línea, tendencias o comparación entre
  meses —hoy fuera de alcance explícito—, esta decisión hay que revisarla. Este ADR no dice *"nunca
  una librería de gráficos"*: dice que para **una barra proporcional por categoría** no se justifica.
- **Queda para el ticket 6**: ponerle color a las barras cuando exista la paleta. Es cambiar un
  relleno en un solo lugar.

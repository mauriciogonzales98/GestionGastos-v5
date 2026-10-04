# Research — Identidad visual, navegación y pantallas más compactas

**Feature**: [spec.md](./spec.md) · **Fecha**: 2026-09-29

Cada decisión dice qué se eligió, por qué, y qué se descartó. Lo que se **midió** está marcado como
medido; lo que no, dice que no.

---

## D-01 · El navegador de pruebas: Vitest en modo navegador, con Chromium vía Playwright

**Decisión**: sumar el **modo navegador de Vitest** con su proveedor de Playwright, corriendo sólo
Chromium sin ventana. Dos dependencias de desarrollo nuevas, fijadas a versión exacta:

| Paquete | Versión | Por qué exacta |
|---------|---------|----------------|
| `@vitest/browser-playwright` | `5.0.2` | su `peerDependency` es `vitest: 5.0.2` exacto: tienen que subir juntos |
| `playwright` | `1.63.0` | cada versión trae su propio Chromium; fijarla fija el navegador que mide |

Las pruebas que miden van en archivos `*.navegador.test.tsx`, en un **segundo proyecto de Vitest**
(`projects` en `vite.config.ts`): el existente sigue en jsdom sin cambios, y el nuevo corre esos
archivos en Chromium. `pnpm test` corre los dos.

**Por qué**:

- **El mismo runner, la misma forma de escribir pruebas.** `AGENTS.md` fija Vitest como runner del
  frontend. El modo navegador renderiza los mismos componentes con Testing Library, pero en un
  navegador que maqueta: `getBoundingClientRect`, `scrollWidth` y `matchMedia` devuelven números de
  verdad. Es exactamente lo que D11-01 decía que jsdom no puede hacer.
- **Se prueban componentes, no la app levantada.** Lo que hay que medir es disposición —anchos,
  alturas, dónde está la barra—, y eso no necesita backend: las pruebas le pasan datos a los
  componentes como ya lo hacen las de jsdom. El trabajo de CI del frontend no pasa a necesitar MySQL
  ni la API.
- **`page.viewport(360, 740)`** fija el ancho de la ventana dentro de cada prueba, así que una misma
  prueba recorre 360, 768 y 1440 px.
- **El modo oscuro se emula** con la preferencia de esquema de color de Playwright, sin tocar el
  sistema operativo.

**Qué se midió (2026-09-29)**: la versión `5.0.2` del proveedor existe y pide `vitest` `5.0.2` exacto,
que es la del proyecto. Se instaló en un directorio aparte —no en el repositorio— y **Chromium no
arrancó en el WSL de desarrollo**: le falta `libnspr4.so` y otras bibliotecas del sistema que se
instalan con `playwright install-deps chromium`, que pide `sudo`. En el CI (Ubuntu) se resuelve con
`playwright install --with-deps chromium`. **La prueba mínima de medición todavía no corrió en
local**: queda como primer paso de las tareas (ver *quickstart*, paso 0).

**Descartado**:

- **Playwright Test contra la app levantada** (end-to-end). Mide lo mismo, pero necesita la API y la
  base corriendo, un segundo runner con otra forma de escribir pruebas, y un job de CI mucho más
  pesado. Lo que esta feature necesita medir es disposición, y eso se mide en componentes.
- **Seguir verificando por reglas** (como la 011). Descartado por el usuario (`NFR-003`).
- **Firefox y WebKit además de Chromium.** Triplica el tiempo y la descarga, y lo que se mide —anchos
  y alturas de CSS estándar— no cambia entre motores de forma que importe acá. Queda para cuando lo
  mobile necesite Safari de verdad.

**Consecuencia para la puerta**: el CI agrega `pnpm exec playwright install --with-deps chromium`
antes de `pnpm test`. Si el navegador no puede arrancar, el proyecto de Vitest **falla**, no se
saltea: una puerta que pasara en verde sin Chromium estaría diciendo que midió algo que no midió.
Eso se comprueba en las tareas rompiendo la ruta del navegador a propósito (principio V).

**Dependabot**: el grupo `frontend-menores` ya junta las subidas menores y de parche de todo, así
que `vitest` y su proveedor suben juntos ahí. Los **majors** llegan hoy de a una dependencia por PR,
y un major de `vitest` sin el de `@vitest/browser-playwright` rompe la instalación (el proveedor pide
la versión exacta). Hace falta un grupo para los majors de `vitest` y `@vitest/*`.

---

## D-02 · La paleta: violeta sobrio, dos modos, los mismos roles

**Decisión**: estos valores, medidos con la misma fórmula de WCAG que usa `ui/contraste.ts`:

| Rol | Claro | Oscuro | Para qué |
|-----|-------|--------|----------|
| `--color-fondo` | `#f8f7fb` | `#16141b` | la página |
| `--color-superficie` | `#ffffff` | `#211e29` | tarjetas, barra de navegación, bloques |
| `--color-texto` | `#1d1a24` | `#ece9f2` | texto normal |
| `--color-texto-secundario` | `#5c5668` | `#b1abbd` | etiquetas de cifras, período, ayudas |
| `--color-acento` | `#6b46b0` | `#b89cf2` | botón principal, sección actual, enlaces |
| `--color-sobre-acento` | `#ffffff` | `#1b1328` | texto sobre el botón principal |
| `--color-foco` | `#4a2b86` | `#d6c5fa` | anillo de foco |
| `--color-error` | `#b3261e` | `#f28b82` | errores |
| `--color-exito` | `#1d6b40` | `#7fd3a2` | confirmaciones |
| `--color-borde` | `#817a8d` | `#7f788b` | bordes de campos, separadores |
| `--color-barra` | = acento | = acento | barras del dashboard |
| `--color-riel` | `#e9e4f2` | `#34303d` | riel de las barras |

**Medido (2026-09-29)**, contraste de cada par en cada modo:

| Par | Umbral | Claro | Oscuro |
|-----|--------|-------|--------|
| texto / fondo | 4,5 | 16,07 | 15,22 |
| texto / superficie | 4,5 | 17,14 | 13,65 |
| texto secundario / fondo | 4,5 | 6,59 | 8,20 |
| texto secundario / superficie | 4,5 | 7,03 | 7,35 |
| acento / fondo | 4,5 | 6,29 | 7,89 |
| acento / superficie | 4,5 | 6,71 | 7,08 |
| sobre acento / acento | 4,5 | 6,71 | 7,75 |
| foco / fondo | 3 | 9,94 | 11,50 |
| foco / superficie | 3 | 10,60 | 10,31 |
| error / fondo | 4,5 | 6,13 | 7,64 |
| error / superficie | 4,5 | 6,54 | 6,86 |
| éxito / fondo | 4,5 | 6,10 | 10,21 |
| éxito / superficie | 4,5 | 6,50 | 9,16 |
| borde / fondo | 3 | 3,86 | 4,31 |
| borde / superficie | 3 | 4,12 | 3,87 |
| barra / riel | 3 | 5,38 | 5,55 |

Todos cumplen. El acento lleva texto (4,5:1) y no sólo marca componentes, así que puede usarse para
la sección actual y para enlaces.

**Un par que NO se mide, y por qué**: foco contra acento da 1,58 (claro) y 1,46 (oscuro). No hace
falta: el anillo se dibuja **separado** del control por `outline-offset: 2px`, así que lo que tiene
al lado es el fondo o la superficie, no el botón. Esa separación pasa a ser parte del contrato (ver
[contracts/sistema-visual.md](./contracts/sistema-visual.md)): si alguien la quita, el par que se
mide deja de ser el que se ve.

**Carácter**: un solo tono de violeta, que tiñe apenas los neutros —el fondo claro es blanco
violáceo y el oscuro es casi negro violáceo— para que la app se lea violeta sin pintar superficies
grandes. Error y éxito conservan rojo y verde porque son estados, no decoración (`FR-021`).

**Descartado**: un violeta saturado de marca (`#7c3aed` y parecidos). Contra blanco llega a 5,7:1,
pero en superficies grandes —la barra lateral, la sección actual— se lee chillón, que es lo
contrario de "sobrio".

---

## D-03 · El modo oscuro, sin JavaScript

**Decisión**: los dos modos son **las mismas variables redefinidas** dentro de
`@media (prefers-color-scheme: dark)` en `estilos/base.css`, más `color-scheme: light dark` en
`:root` y `<meta name="color-scheme" content="light dark">` en `index.html`.

**Por qué**:

- **Primera pintura correcta por construcción** (`FR-019`): el navegador resuelve la media query
  antes de pintar. Con JavaScript eligiendo el tema, la primera pintura sería clara hasta que
  React monte, que es el destello que la spec prohíbe.
- **El cambio en vivo lo hace el navegador** (`FR-018`): al cambiar la preferencia, la media query se
  reevalúa y cambian los colores. React no se entera, así que no se desmonta nada y un formulario a
  medio llenar sigue igual.
- **Los controles nativos siguen el modo** (`FR-020`): `color-scheme` es lo que le dice al navegador
  que pinte el selector de fecha, las listas y las barras de desplazamiento en oscuro.
- **Sigue siendo la única fuente** que la 011 estableció (D-01 de la 011): la paleta vive en CSS.

**Consecuencia para `Paleta.test.ts`**: hoy lee los colores de `:root`. Pasa a leerlos **por modo**
—los de `:root` y los del bloque oscuro— y a medir cada par de `PARES` en los dos. Un color
declarado en un modo y no en el otro pone la prueba en rojo (`NFR-001`), y es la forma de que agregar
un color obligue a decidir su versión oscura.

**Descartado**: un selector dentro de la app (fuera de alcance por la spec) y una clase `.oscuro` en
`<html>` puesta por JavaScript (destello en la primera pintura).

---

## D-04 · Tipografía, espaciado, radios

**Decisión**:

- **Fuente**: `system-ui, -apple-system, "Segoe UI", Roboto, sans-serif` (`FR-006`).
- **Tamaño base 1rem (16 px)**, también en campos (`FR-007`): con menos, Safari de iPhone agranda la
  página al tocar un campo.
- **Escala**: `--texto-chico` 0,875rem · `--texto` 1rem · `--texto-grande` 1,25rem ·
  `--texto-titulo` 1,5rem · `--texto-marca` 1,75rem. Cinco niveles; la spec pide al menos cuatro.
- **Espaciado**: se conservan `--espacio-chico` 0,5 · `--espacio` 0,75 · `--espacio-grande` 1,5 y se
  agregan `--espacio-minimo` 0,25 y `--espacio-enorme` 2,5 (rem).
- **Radios**: `--radio` 0,5rem para controles, `--radio-grande` 0,75rem para tarjetas.
- **Alto táctil**: `--alto-tactil: 2.75rem` (44 px), usado como `min-height` y `min-width` de todo
  control tocable (`FR-010`, `FR-025`, `FR-043`).
- **Números del resumen** con `font-variant-numeric: tabular-nums`, para que las cifras se alineen.

---

## D-05 · La navegación: un solo `<nav>`, reubicado por CSS

**Decisión**: un componente `MarcoDeLaApp` envuelve a toda pantalla con sesión. Contiene, **una sola
vez en el DOM**: el nombre de la app, un `<nav aria-label="Secciones">` con tres botones, el bloque
de la cuenta (email y "Cerrar sesión") y el `<main>` de la pantalla. La disposición la decide CSS:

- **Desde 48rem (768 px)**: grilla de dos columnas. A la izquierda, una barra de 14rem, fija a la
  altura de la ventana (`position: sticky`), con marca, secciones y cuenta al pie. A la derecha, el
  contenido, que conserva los 34rem de ancho útil como mínimo.
- **Por debajo de 48rem**: una franja arriba con la marca, el email y "Cerrar sesión"; las tres
  secciones en una barra `position: fixed` abajo, con `padding-bottom: env(safe-area-inset-bottom)`;
  y el `<main>` con un relleno inferior igual al alto de esa barra (`FR-026`).
  `viewport-fit=cover` en la etiqueta `viewport` de `index.html` hace que `env()` devuelva algo en
  los teléfonos con zona de gestos.

**Botones con `aria-current="page"` y no enlaces**. La app no tiene rutas: cambiar de sección es
cambiar el estado `vista` de `App.tsx`, como hoy. Un enlace sin dirección a la que ir no es un
enlace, y sumar un enrutador es una dependencia que la spec no pide. `aria-current` vale en
cualquier elemento y es lo que los lectores anuncian como "página actual" (`FR-024`). La sección
actual se distingue sin color por el relleno de fondo más una marca lateral (escritorio) o superior
(teléfono), y por el peso de la letra.

**Orden de tabulación**: marca, secciones, cuenta, contenido. En el teléfono las secciones se **ven**
abajo pero se **recorren** antes del contenido. Es el patrón de las barras inferiores de las apps
nativas, y ponerlas al final del recorrido obligaría a atravesar un formulario entero para cambiar
de sección. Queda anotado para que no se lea como un descuido.

**Por qué un solo nav y no dos (uno lateral, uno inferior) que se muestran y ocultan**: dos `<nav>`
con las mismas secciones son dos puntos de referencia iguales para un lector de pantalla, y dos
lugares donde marcar la sección actual que pueden quedar desincronizados.

**Descartado**: ancho de corte en 40rem (el que ya usa `.l-cabecera`). A 640 px, 14rem de barra dejan
26rem de contenido y la tabla del listado empieza a desplazarse de costado apenas se abre.

**Lo que desaparece**: la cabecera de `PantallaMovimientos` (email y los tres botones) y los botones
"Volver a movimientos" de categorías y dashboard (`FR-022`).

---

## D-06 · Íconos propios, en SVG, en un solo archivo

**Decisión**: `ui/iconos.tsx` exporta un componente por ícono —`IconoMovimientos`,
`IconoDashboard`, `IconoCategorias`, `IconoEditar` (lápiz), `IconoEliminar` (tacho),
`IconoError`, `IconoExito`—, cada uno un `<svg viewBox="0 0 24 24">` de trazo, con
`stroke="currentColor"`, `aria-hidden="true"` y `focusable="false"`. Toman el color del texto, así
que siguen a los dos modos sin declarar colores propios.

Un componente `BotonIcono` concentra las reglas de `FR-045`: recibe `nombre` (el nombre accesible
completo, "Renombrar Supermercado"), `accion` (el rótulo corto visible, "Renombrar") y el ícono.
Pone `aria-label={nombre}`, dibuja el ícono, y agrega un rótulo visual con `accion` que CSS muestra
en `:hover` y `:focus-visible`. El rótulo es `aria-hidden`: el nombre ya lo da `aria-label`, y
anunciarlo dos veces sería ruido.

**Por qué no `title`**: no aparece al llegar con el teclado, que `FR-042` exige, y en el teléfono no
aparece nunca.

**Descartado**: una librería de íconos. Son siete, y `NFR-002` pide no sumar dependencias de estilo.
Es la misma razón del ADR-002 para el gráfico.

---

## D-07 · El formulario de movimiento compacto

**Decisión**: la moneda **se mueve en el DOM** —no con `order` de CSS— para quedar después del monto.
Así el orden de tabulación y el de lectura coinciden con el visual por construcción (`FR-031`).
Orden nuevo: tipo, monto, moneda, categoría, fecha, nota, enviar.

Disposición, con una clase de disposición nueva `l-par`:

- **Tipo**: el `<fieldset>` con los dos radios se pinta como control segmentado —dos mitades pegadas,
  la elegida rellena de acento—. Siguen siendo radios, así que el teclado (flechas) y el anuncio no
  cambian.
- **Monto + moneda**: `l-par--monto`, que **no se parte nunca**. El monto se lleva lo que sobra y la
  moneda ocupa lo que necesite su nombre (`flex: 0 1 auto`).

  **La cuenta original estaba equivocada y se corrigió midiendo (2026-10-01)**: decía
  `minmax(0, 1fr) 6rem` porque daba por hecho que el selector muestra "un código de tres letras".
  No: muestra el **nombre**. En la base de desarrollo son "Peso argentino" (143 px con la flecha) y
  "Dólar estadounidense", y con 6rem salían cortados.

  Tampoco se arregla poniendo 9rem o 12rem: sería un ancho ajustado a los nombres de hoy, y el día
  que alguien sume una moneda al catálogo —que según `PRD:RF-32` tiene que costar **sólo un dato**—
  volvería a cortarse. Es la misma clase de problema que `verificar-monedas.sh` vigila del lado del
  backend: una lista escrita a mano con otra forma. `auto` lo resuelve por contenido.
  `FormularioCompacto.navegador.test.tsx` lo prueba con el nombre largo real, en los seis anchos.
- **Categoría + fecha**: `l-par` con `flex-wrap` y ancho base de **9rem** para cada una.

  **Corregido midiendo (2026-10-01)**: la cuenta decía 10rem y que a 360 px se apilaran. Con 9rem
  (144 px) entran las dos a 360 px y se leen bien —el `<select>` muestra el principio del nombre y
  el campo de fecha la fecha entera—, así que **comparten renglón también a 360**. La cuenta estaba
  cerca y era una cuenta.

  Lo que la prueba afirma no es el número sino **la regla** de `FR-030`: si comparten renglón, cada
  una mide al menos su mínimo legible; si no, cada una usa el ancho entero. Así el día que el ancho
  base cambie, la prueba sigue diciendo lo que importa.
- **Nota**: a lo ancho, con alto inicial de dos líneas.

**Altura de hoy — MEDIDA el 2026-10-01**, antes de tocar nada, con
`tests/LineaBase.navegador.test.tsx` a 360 px de ancho:

| Qué | Alto de hoy | Contra qué se compara |
|-----|-------------|-----------------------|
| Formulario de alta, sin errores a la vista | **527 px** | `FR-032`: el compacto tiene que medir **menos** |
| Resumen del mes, dos monedas (una sin movimientos), con desglose | **740 px** | `SC-011`: el nuevo tiene que medir **menos de 370 px** |

El alto incluye los márgenes propios del elemento, que es lo que empuja al resto de la pantalla.
Verificado estable: dos montajes del mismo componente dan el mismo número ±1 px, que es el redondeo
de media línea de texto (principio IV). La prueba **no escribe estos números como constantes**: los
imprime. Un número escrito en el archivo habría que editarlo al recomponer, y un número que se edita
para que pase no mide nada.

Era la deuda que `FR-032` y `SC-011` arrastraban: pedían comparar contra una altura que no existía.
Una comparación contra un número estimado habría sido la misma trampa que la memoria del proyecto
tiene documentada tres veces.

---

## D-08 · El resumen compacto

**Decisión**:

- `ResumenDelPeriodo` recibe `conDesglose` (por defecto `true`). Movimientos lo pasa en `false`
  (`FR-035`); el dashboard no lo pasa (`FR-036`). El servidor no cambia: sigue mandando el desglose,
  y la prueba de la 010 que compara las dos respuestas queda intacta (ver la corrección en la spec).
- **Encabezado en un renglón**: "Resumen del mes" y el período al lado, en texto secundario.
- **Cada moneda en un renglón**: el código y una grilla de tres columnas iguales, cada una con la
  etiqueta arriba en texto chico y la cifra abajo con `tabular-nums`. Sigue siendo un `<dl>`, así que
  lo que se anuncia no cambia.
- **Siete cifras a 360 px** (`FR-037`): el peor caso es `$ 9.999.999,99`, 14 caracteres a 1rem, unos
  7,5rem; tres columnas de 6,3rem no alcanzan. Las cifras bajan a `--texto-chico` por debajo de
  48rem.

  **Esta cuenta era optimista, y darla por confirmada fue un error (corregido el 2026-10-04).** El
  2026-10-03 se anotó acá que la medición la había validado. No era cierto: la prueba medía el
  resumen **montado suelto**, donde a 360 px recibe los 360 enteros, y en la app recibe 286 —antes
  están el relleno del contenido y el de la tarjeta—. Cincuenta píxeles, que son exactamente los que
  separan "entran" de "cada monto pinta encima del de al lado". Bajar la letra no alcanzaba: las
  cifras se apilan por debajo de 48rem. Ver D-13, punto 5.

  Es la tercera vez que este proyecto anota una premisa sin verificarla, y la primera en que el autor
  de la nota fue el mismo que la escribió como verificada.
- **Moneda sin movimientos** (`FR-038`): si `totalIngresado` y `totalGastado` son cero, una sola línea
  "USD — sin movimientos en el período". "Del mes" no sirve, porque el dashboard puede mostrar otro
  rango.
- **Balance negativo**: ya se muestra con signo, que es la distinción sin color. Se suma el color de
  error sobre ese signo, nunca en lugar de él.

---

## D-13 · Lo que corrigieron las mediciones de cierre (2026-10-03)

Se agrega al final porque no es una decisión de diseño previa: son **cuatro defectos reales que
encontraron las últimas dos pruebas de la feature**, las que cruzan combinaciones en lugar de
verificar una historia. Se anotan acá y no sólo en el CSS porque las cuatro son trampas que se
repiten.

1. **El texto oculto para lectores de pantalla se escapaba del envoltorio desplazable y desplazaba
   la página entera** (`FR-004`). `.u-solo-lectores` es `position: absolute`, y un elemento
   posicionado se ubica contra el bloque contenedor más cercano **que esté posicionado**. Dentro de
   la tabla del listado no había ninguno, así que ese bloque era la página: el span del encabezado
   de acciones quedaba en x ≈ 675 —su posición estática dentro de la tabla desplazada— pero colgado
   de la página, y el `overflow-x` del envoltorio no lo alcanzaba. A 360 px con el listado lleno, la
   página se corría de costado.

   **Estuvo roto desde la feature 011**, que es cuando nació esa columna, y ninguna prueba lo vio:
   las que miden el desborde montaban el listado **vacío**, así que no había tabla que desbordar.
   Lo encontró `Responsive.navegador.test.tsx`, que monta las cuatro pantallas con datos. Se arregla
   con `position: relative` en `.c-listado-movimientos__desborde`: el recorte es de quien recorta.

2. **Con el texto del navegador al 200 %, la tarjeta del acceso se iba a 531 px dentro de una
   ventana de 360.** Dos causas distintas, las dos del mismo tipo —un ancho **mínimo de contenido**
   que no se puede encoger—:
   - un `<input>` sin `size` propio pide unos 20 caracteres, que al doble de letra son más de
     400 px. Se arregla con `min-width: 0` en `.c-tarjeta`, que le saca a la pista de la grilla ese
     piso;
   - `overflow-wrap: break-word` en la marca **no baja** el mínimo de contenido —parte la palabra al
     pintar, no al medir—, así que "Gestión" a 56 px seguía pidiendo 242 px. Se arregla con
     `overflow-wrap: anywhere`, que sí lo baja. Es la diferencia entre las dos palabras clave, y es
     la razón por la que existe `anywhere`.

3. **El conmutador del acceso no entra en un renglón con la letra al doble**, por mucho que se
   encoja: las palabras miden lo que miden. Se apila, con `flex-wrap: wrap` y tamaño base de 7rem en
   cada opción. **El tamaño base tiene que ser distinto de cero o el envoltorio no sirve de nada**:
   con base cero, dos opciones nunca suman más que el renglón y la fila no se parte nunca. Es la
   misma lección que apareció dos veces más en esta feature —en `.c-categoria__nombre`, donde el
   nombre largo tenía que **no** forzar el corte, y es por eso que ahí la base es cero—: envolver lo
   decide el tamaño base, encogerse lo decide `min-width`, y confundirlos sale carísimo.

4. **Un rótulo escondido con `opacity: 0` sigue maquetado y sigue desplazando la página.** El rótulo
   de `BotonIcono` aparece arriba del botón; con dos botones por fila pegados al borde derecho,
   asomaba 16 px más allá de los 360. Se esconde con `display: none`, que no genera caja, y se
   alinea por el borde derecho: en una pantalla de izquierda a derecha, lo que se sale por la
   izquierda no agrega desplazamiento.

5. **Las tres cifras del resumen se pisaban en el teléfono, y dos pruebas en verde lo tapaban.**
   Encontrado el 2026-10-04, **levantando la app y mirándola**, no corriendo la suite.

   Con montos de siete cifras a 360 px cada columna mide 90 px y el texto necesita 107. Con
   `white-space: nowrap` el monto no se corta: **se sale y pinta encima del de al lado**
   —`$ 1.850.000,0` y `0$ 764.850,75` superpuestos—. Las dos aserciones que existían eran
   verdaderas: las tres compartían renglón y la página no desbordaba. Describían una pantalla
   ilegible.

   **Por qué ninguna prueba lo vio**: montaban `ResumenDelPeriodo` **suelto**, y suelto a 360 px el
   componente recibe los 360 enteros mientras que en la app recibe 286 —antes están el relleno del
   contenido y el de la tarjeta—. Una medición en un contenedor que no es el real no mide la
   pantalla: mide otra. Ahora la prueba lo monta dentro de `MarcoDeLaApp` y comprueba lo que la
   persona ve —que ningún monto invada el espacio del siguiente— con `scrollWidth`, que mide el
   texto, y no con la caja, que mide la columna.

   La salida fue **apilarlas por debajo de 48rem**, con el nombre a la izquierda y el monto a la
   derecha, y corregir `FR-037`. Achicar la letra habría dejado el dato más importante de la
   pantalla en unos 12 px.

   Es el argumento entero a favor del paso manual del quickstart: ninguna de las 580 pruebas vio
   esto, y se ve a simple vista en la primera pantalla que uno abre en un teléfono.

---

## D-09 · La pantalla de acceso

- **Tarjeta** (`c-tarjeta`, sobre `--color-superficie`, `--radio-grande`, borde de 1px) con ancho
  máximo de 24rem, centrada horizontalmente y con margen superior proporcional a la altura de la
  ventana.
- **Marca**: "Gestión de gastos" a `--texto-marca`, en acento, arriba de la tarjeta.
- **Conmutador**: control segmentado, el mismo aspecto que el tipo de movimiento. La opción elegida
  se distingue por el relleno y por el peso de la letra, y el estilo sigue colgando de
  `[aria-pressed='true']` (`FR-009`).
- **Botón de envío** a todo el ancho de la tarjeta: su ancho no depende del texto, así que
  "Creando…" no lo cambia (`FR-012`).
- **Mensajes**: el error general y la confirmación llevan borde lateral, fondo tenue y un ícono
  (`IconoError`, `IconoExito`) además del texto (`FR-005`, `SC-004`). El error de un campo agrega
  borde de error y grosor al campo, vía `aria-invalid='true'`, que el campo ya pone.
- **Nada del comportamiento cambia**: los textos, los roles y los atributos que buscan las pruebas de
  `FormularioAcceso.test.tsx` quedan iguales (`NFR-004`).

---

## D-10 · Movimiento reducido

Las únicas transiciones son de color y de fondo en botones y secciones, de 120 ms, y van dentro de
`@media (prefers-reduced-motion: no-preference)` (`FR-016`). No hay animaciones de posición.

---

## D-11 · Qué pruebas existentes cambian, y por qué

Revisado contra el código el 2026-09-29:

| Prueba | Qué cambia | Requisito |
|--------|-----------|-----------|
| `App.test.tsx` — ir a categorías y volver | "Volver a movimientos" pasa a ser la sección "Movimientos" de la barra | `FR-022` |
| `PantallaCategorias.test.tsx:170` | ídem | `FR-022` |
| `TecladoFormulario.test.tsx` — orden de tabulación | los tres botones de cabecera pasan a ser Movimientos, Dashboard, Categorías y Cerrar sesión; y la moneda pasa a ir después del monto | `FR-022`, `FR-029` |
| `VentanaDeEdicion.test.tsx` — buscan `'Editar'` | el nombre pasa a decir qué movimiento edita | `FR-047` |
| `ResumenDelPeriodo.test.tsx:57` — "una moneda sin movimientos aparece con sus totales en cero" | pasa a aparecer en una línea que dice que no tuvo movimientos. **Cambia una decisión de la feature 006** (su `FR-009`: el período vacío devuelve ceros *para que la moneda no parezca inexistente*). La razón se conserva —la moneda sigue apareciendo—; cambia la forma, por decisión del usuario. La prueba vecina (`:78`, balance en cero con movimientos) queda y cobra más peso: es la que separa los dos casos | `FR-038` |
| Las pruebas de `PantallaMovimientos` | ninguna afirma hoy sobre el desglose —revisado—, así que sólo se **agrega** la que dice que ya no está | `FR-035` |

### Y seis archivos más, que cambian por compilación y no por aserción

**Esta mitad de la tabla faltaba, y es la que más archivos toca.** Se descubrió al analizar los
artefactos antes de implementar (2026-10-01), contando quién le pasa a las tres pantallas las props
que `FR-022` elimina. La búsqueda de arriba se había hecho por **texto de botón** —"Volver a
movimientos", "Cerrar sesión"— y por eso encontró sólo los tres archivos que *aprietan* esos
botones. Los que simplemente *montan* la pantalla y le pasan las props no dicen ninguno de esos
textos, así que no aparecían.

`onVolver`, `onCerrarSesion`, `onGestionarCategorias` y `onVerDashboard` son props **obligatorias**
(`: () => void`, sin `?`). Cuando desaparecen de la interfaz, cada `onVolver={...}` que quede escrito
es un error de TypeScript, y la puerta incluye `tsc --noEmit`. O sea que estos archivos **no pueden**
quedar sin tocar:

| Prueba | Props que le quedan de más | Qué cambia |
|--------|---------------------------|-----------|
| `Accesibilidad.test.tsx` | las cuatro, 5 usos | sólo las props. **Sus aserciones no cambian** y no deben cambiar: son el piso de accesibilidad de la 011 (`FR-014`) |
| `CargaInicial.test.tsx` | tres, 12 usos | sólo las props |
| `EliminarMovimiento.test.tsx` | tres, 6 usos | sólo las props |
| `FiltrosDelListado.test.tsx` | tres, 3 usos | sólo las props |
| `PantallaDashboard.test.tsx` | `onVolver`, 3 usos | sólo las props |
| `PantallaMovimientos.test.tsx` | tres, 6 usos | las props, **además** de la aserción que se agrega por `FR-035` |

Y los tres que ya estaban en la tabla de arriba también pasan props, no sólo texto de botón:
`PantallaCategorias.test.tsx` (`onVolver`, 1 uso), `TecladoFormulario.test.tsx` (tres, 6 usos) y
`VentanaDeEdicion.test.tsx` (tres, 3 usos).

**Total: 11 archivos de prueba cambian.** Cinco por lo que afirman; seis sólo por compilar. La
distinción importa para `SC-006`: un cambio de aserción es una decisión de producto que hay que
justificar, y borrar una prop obligatoria que ya no existe no lo es.

**Lo que no cambia**: `FormularioAcceso.test.tsx` (`NFR-004`), las **aserciones** de categorías que
buscan "Renombrar X" y "Dar de baja X", las **aserciones** de `EliminarMovimiento.test.tsx`, las de
`ResumenDelPeriodo` que no tratan monedas vacías (su `conDesglose` por defecto es `true`),
`Contraste.test.ts` y `ui/contraste.ts` —que desde la 011 no declara ningún color, sólo la
fórmula—, y todo el backend.

---

## D-12 · Dos programas de TypeScript, no uno

**Medido al implementar (2026-10-01).** No estaba previsto y hay que escribirlo porque cambia un
comando de la puerta.

**El problema**: importar `vitest/browser` —que es de dónde sale `page`, y no hay forma de fijar el
ancho de la ventana sin él— **reemplaza la interfaz `Assertion` de Vitest** por la del modo
navegador, que es de estilo Playwright: su `toHaveTextContent` acepta `string | number`. Las pruebas
de hoy usan la de `@testing-library/jest-dom`, que acepta `string | RegExp`, y hay **19 usos de
`toHaveTextContent(/regex/)`** repartidos en 7 archivos. Con un solo `tsconfig` sobre `src` y `tests`,
agregar un archivo que importe `vitest/browser` deja esos 19 usos sin compilar (TS2345). Se vio al
primer `tsc --noEmit` después de escribir `tests/anchos.ts`.

Los dos juegos de matchers **no conviven en un programa**. Sí conviven en dos.

**Decisión**:

- `tsconfig.json` sigue cubriendo `src` y `tests`, y **excluye** `tests/**/*.navegador.test.tsx`,
  `tests/anchos.ts` y `tests/setup.navegador.ts`.
- `tsconfig.navegador.json` extiende al anterior y sólo cambia `types`: carga `vitest/browser` en
  lugar de `@testing-library/jest-dom`. Incluye `src` —estas pruebas montan los componentes de
  verdad— y los tres archivos que el otro excluye.
- El comando de la puerta pasa de `pnpm exec tsc --noEmit` a **`pnpm typecheck`**, que corre los dos:
  `tsc --noEmit && tsc -p tsconfig.navegador.json --noEmit`. Sigue siendo un comando. `build` pasa a
  usarlo también, así que `vite build` no puede salir con un programa sin chequear.

**Consecuencia para la documentación**: la fila *Typecheck* de la tabla de `AGENTS.md` y el paso
*Type checker* de `ci.yml` cambian de comando. Es el único cambio de la puerta que esta feature
introduce.

**Descartado**:

- **Un solo `tsconfig` con los dos `types`.** No es cuestión de orden: el segundo gana y el primero
  deja de compilar. No hay orden en el que los 19 usos y los matchers del navegador convivan.
- **Reescribir los 19 usos** a `toHaveTextContent('texto')`. Son pruebas de la 006 a la 013 que esta
  feature no tiene por qué tocar, varias usan `/i` para no depender de mayúsculas, y `NFR-004` y
  `SC-006` dicen que las pruebas existentes no cambian salvo por un requisito que lo cause. Un
  problema de configuración de tipos no es ese requisito.
- **`composite` con `references`.** Obliga a `tsc -b`, que con `noEmit` en los proyectos
  referenciados entra en conflicto, y agrega archivos de estado al árbol. Dos invocaciones en un
  script hacen lo mismo sin nada de eso.

---

## D-13 · Montar componentes en el navegador sin una tercera dependencia

**Medido al implementar (2026-10-01).** La documentación del modo navegador sugiere
`vitest-browser-react` para renderizar componentes. **Sería una tercera dependencia**, y ni D-01 ni
la justificación de `NFR-003` la previeron: la spec contabilizó dos —el proveedor y Playwright— y
`SC-007` afirma que son las únicas.

**Decisión**: `tests/montar.tsx`, doce líneas sobre `react-dom/client`, que ya es dependencia de la
app. Monta en un contenedor limpio **sin estilos propios** —lo que se mide es cómo maqueta la hoja de
la app, así que un relleno puesto por el ayudante falsearía la medición— y envuelve el render en
`act` para que los efectos corran antes de medir.

Tampoco sirve el `render` de `@testing-library/react`: arrastra los matchers de jest-dom, que por
D-12 no pueden estar en este programa.

**Lo que esto preserva**: la cuenta de `SC-007` queda verdadera por construcción, y
`tests/SinDependenciasDeEstilo.test.ts` la verifica —exige que `dependencies` sea exactamente
`react` y `react-dom`—, así que el día que alguien agregue la tercera, la barrera lo dice.

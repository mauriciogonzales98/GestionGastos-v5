import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * La lectura del código y de la hoja de estilos que comparten los verificadores de la feature 011.
 *
 * **Recorre el árbol, nunca una lista escrita a mano** (`NFR-003`). Es la misma promesa que
 * `verificar-monedas.sh` protege del lado del catálogo: una comprobación que se sostiene en que
 * alguien se acuerde de agregar una fila a una lista no es una comprobación, es un recordatorio.
 * Una pantalla nueva —la nota descriptiva del ticket 2, por ejemplo— queda cubierta sola.
 *
 * Vive en `tests/` y no en `src/` porque no lo consume la aplicación: sólo lo consumen los tests
 * que corren en entorno `node`, que son los únicos que pueden tocar el disco.
 */

const RAIZ = join(fileURLToPath(new URL('.', import.meta.url)), '..', 'src');

/** Todos los archivos bajo `dir` cuyo nombre termina en alguno de `extensiones`, recursivo. */
function archivosBajo(dir: string, extensiones: readonly string[]): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entrada) => {
    const ruta = join(dir, entrada.name);

    if (entrada.isDirectory()) {
      return archivosBajo(ruta, extensiones);
    }

    return extensiones.some((extension) => entrada.name.endsWith(extension)) ? [ruta] : [];
  });
}

/** El contenido de cada `.tsx` de `frontend/src/`, concatenado. Es donde se escriben las clases. */
export function codigoDeLasPantallas(): string {
  return archivosBajo(RAIZ, ['.tsx'])
    .map((ruta) => readFileSync(ruta, 'utf8'))
    .join('\n');
}

/**
 * Cada archivo de código de la app, con su ruta al lado.
 *
 * **Con la ruta y sin concatenar**, al revés que `codigoDeLasPantallas`: lo que lo usa informa en
 * qué archivo está lo que encontró, y un verificador que dice "hay una elección de tema en algún
 * lugar de `src/`" obliga a buscarla a mano.
 *
 * Entran los `.ts` además de los `.tsx`: una elección de tema escrita en JavaScript no tiene por qué
 * vivir en un componente — el lugar natural sería un ayudante de `ui/`, que es un `.ts`.
 */
export function archivosDeLaApp(): { ruta: string; contenido: string }[] {
  return archivosBajo(RAIZ, ['.ts', '.tsx']).map((ruta) => ({
    // Relativa a `frontend/`, que es como la nombran las specs y los mensajes de error.
    ruta: ruta.slice(ruta.indexOf('src')),
    contenido: readFileSync(ruta, 'utf8'),
  }));
}

/**
 * El contenido de cada `.css` de `frontend/src/estilos/`, concatenado y **sin comentarios**.
 *
 * Los comentarios se quitan porque **no son CSS**, y dejarlos puestos hacía que los verificadores
 * leyeran la prosa como si fueran reglas. Dos casos reales, los dos encontrados al escribir Lila:
 *
 *   - El comentario que explica *"un `outline: none` sin sustituto deja a quien navega con teclado
 *     sin saber dónde está parado"* hacía que `Foco.test.ts` denunciara una anulación del foco que
 *     no existe. El verificador estaba leyendo la explicación de por qué eso no se hace.
 *   - El comentario que precede a un `@media` quedaba pegado al prefacio del bloque, así que el
 *     at-rule dejaba de empezar con `@` y `transicionesSinGuardia` no lo reconocía como guardia:
 *     reportaba como suelta una transición que estaba adentro.
 *
 * Los dos son la misma clase de error, y arreglarlo en cada verificador habría sido arreglarlo tres
 * veces y olvidarlo en el cuarto. Se arregla acá, que es por donde pasan todos.
 */
export function hojaDeEstilos(): string {
  return sinComentarios(
    archivosBajo(join(RAIZ, 'estilos'), ['.css'])
      .map((ruta) => readFileSync(ruta, 'utf8'))
      .join('\n'),
  );
}

/** El CSS sin sus comentarios `/* ... *\/`, que no son reglas y confunden a los verificadores. */
export function sinComentarios(css: string): string {
  return css.replace(/\/\*[\s\S]*?\*\//g, '');
}

/**
 * Las clases `l-`, `c-` y `u-` que el código referencia.
 *
 * **Se buscan sólo dentro de un `className`**, y no en cualquier parte del archivo: `--c-riel` es
 * una variable CSS y `c-riel` no es una clase. Buscar el prefijo suelto las confundía.
 *
 * El proyecto escribe los nombres como literales —`disposicion.css` lo declara: *"nada de clases
 * utilitarias sueltas"*—, así que esto alcanza. El día que haga falta una clase armada en tiempo de
 * ejecución, este verificador deja de servir y hay que decirlo, no taparlo.
 */
export function clasesReferenciadas(codigo: string): string[] {
  const enClassName = /className=(?:"([^"]*)"|\{`([^`]*)`\}|\{'([^']*)'\})/g;
  const encontradas = new Set<string>();

  for (const coincidencia of codigo.matchAll(enClassName)) {
    const valor = coincidencia[1] ?? coincidencia[2] ?? coincidencia[3] ?? '';

    for (const nombre of valor.split(/\s+/)) {
      if (/^[lcu]-[a-zA-Z0-9_-]+$/.test(nombre)) {
        encontradas.add(nombre);
      }
    }
  }

  return [...encontradas].sort();
}

/**
 * Las clases que la hoja de estilos declara: todo `.x` que aparezca en un selector.
 *
 * **Se toma cada trozo de texto que precede a una llave de apertura**, y no lo que hay antes de la
 * primera llave de cada bloque. La diferencia importa para los selectores anidados dentro de un
 * at-rule: con la forma ingenua, `@media (...) { .l-cabecera { ... } }` reportaba `@media (...)`
 * como selector y perdía la clase de adentro — que fue exactamente lo que pasó al maquetar la
 * cabecera, y el test lo detectó.
 *
 * El prefacio de un at-rule no aporta falsos positivos: `@media (width >= 40rem)` no tiene ningún
 * `.` seguido de letra, y un `.5rem` tampoco lo tiene.
 */
export function clasesDeclaradas(css: string): string[] {
  const declaradas = new Set<string>();

  for (const prefacio of css.matchAll(/([^{}]+)\{/g)) {
    for (const coincidencia of prefacio[1].matchAll(/\.([a-zA-Z][a-zA-Z0-9_-]*)/g)) {
      declaradas.add(coincidencia[1]);
    }
  }

  return [...declaradas].sort();
}

/**
 * Las propiedades personalizadas de color declaradas en el CSS, como `{ '--color-texto': '#111' }`.
 *
 * Se limita a las que empiezan con `--color-` para no arrastrar espaciados ni tipografía: lo que
 * `Paleta.test.ts` mide son colores, y una relación de contraste sobre `1.5rem` no significa nada.
 *
 * **Aplana la hoja entera**, así que con dos modos declarados el valor que queda es el del último
 * bloque que lo declara. Para medir por modo está `coloresPorModo`; esta función se conserva porque
 * la usan los casos sintéticos de `Paleta.test.ts`, que le pasan un CSS de una línea.
 */
export function coloresDeclarados(css: string): Record<string, string> {
  const paleta: Record<string, string> = {};

  for (const coincidencia of css.matchAll(/(--color-[a-zA-Z0-9_-]+)\s*:\s*([^;]+);/g)) {
    paleta[coincidencia[1]] = coincidencia[2].trim();
  }

  return paleta;
}

/** Los dos modos del sistema visual Lila. El claro es el de `:root`; el oscuro lo redefine. */
export type Modo = 'claro' | 'oscuro';

/**
 * La paleta de cada modo, resuelta como la resolvería el navegador.
 *
 * **Por qué hace falta y no alcanza con `coloresDeclarados`** (feature 014, `NFR-001`): desde que
 * hay modo oscuro, cada `--color-*` está declarado **dos veces** —una en `:root` y otra dentro de
 * `@media (prefers-color-scheme: dark)`— y aplanar la hoja deja sólo el segundo. Medir contraste
 * sobre esa mezcla no mide ninguno de los dos modos: mide uno inexistente.
 *
 * El modo claro es `:root`. El oscuro es `:root` **con el bloque oscuro encima**, que es lo que el
 * navegador aplica: el bloque oscuro sólo tiene que redefinir lo que cambia, y si mañana un color
 * fuera igual en los dos modos, no haría falta repetirlo. Lo que `Paleta.test.ts` exige por separado
 * es que ningún color quede declarado en un solo modo; eso es una decisión del contrato, no de cómo
 * resuelve el CSS.
 *
 * **Extrae el bloque oscuro contando llaves** y no con una expresión regular sobre el contenido: el
 * bloque tiene reglas anidadas adentro, y un `[^}]*` se cortaría en la primera llave de cierre, que
 * pertenece a `:root` y no al `@media`. Es el mismo error que `clasesDeclaradas` ya tuvo una vez con
 * los at-rules, y por eso esta vez se cuenta.
 */
export function coloresPorModo(css: string): Record<Modo, Record<string, string>> {
  const claro = coloresDeclarados(bloqueDeRaiz(css));
  const oscuro = { ...claro, ...coloresDeclarados(bloqueOscuro(css)) };

  return { claro, oscuro };
}

/**
 * El `:root` de nivel superior, sin lo que esté dentro de un at-rule.
 *
 * **Primero se quitan los at-rules y después se busca `:root`**, y el orden es el punto: el bloque
 * oscuro tiene su propio `:root` adentro, así que buscar `:root` sobre la hoja entera encuentra los
 * dos y el segundo sobrescribe al primero — el modo claro terminaba con los valores del oscuro. Lo
 * detectó el caso sintético `separa los dos modos en vez de aplanarlos`, que para eso está.
 */
function bloqueDeRaiz(css: string): string {
  return cuerpoDe(sinAtRules(css), /:root\s*\{/g).join('\n');
}

/** La hoja sin ningún bloque `@...{ }`, para quedarse con las reglas de nivel superior. */
function sinAtRules(css: string): string {
  let limpio = '';
  let i = 0;

  while (i < css.length) {
    if (css[i] !== '@') {
      limpio += css[i];
      i += 1;
      continue;
    }

    // Desde la arroba hasta su llave de apertura, y de ahí hasta la de cierre que le corresponde.
    const apertura = css.indexOf('{', i);

    if (apertura === -1) {
      // Un at-rule sin bloque (`@import ...;`): no envuelve nada, así que no hay nada que saltear.
      limpio += css.slice(i);
      break;
    }

    let profundidad = 1;
    let j = apertura + 1;

    while (j < css.length && profundidad > 0) {
      if (css[j] === '{') profundidad += 1;
      else if (css[j] === '}') profundidad -= 1;
      j += 1;
    }

    i = j;
  }

  return limpio;
}

/** El cuerpo del `@media (prefers-color-scheme: dark)`, vacío si no existe. */
function bloqueOscuro(css: string): string {
  return cuerpoDe(css, /@media[^{]*prefers-color-scheme\s*:\s*dark[^{]*\{/g).join('\n');
}

/**
 * El cuerpo de cada bloque cuyo prefacio coincide con `apertura`, equilibrando llaves.
 *
 * Devuelve lo que hay entre la llave de apertura y **su** llave de cierre, no la primera que
 * aparezca. Sin esto, un bloque con reglas anidadas se corta por la mitad y los colores que están
 * después del primer `}` se pierden en silencio, que es la peor forma de perderlos: el test seguiría
 * pasando y estaría midiendo media paleta.
 */
function cuerpoDe(css: string, apertura: RegExp): string[] {
  const cuerpos: string[] = [];

  for (const coincidencia of css.matchAll(apertura)) {
    let profundidad = 1;
    let i = coincidencia.index + coincidencia[0].length;
    const desde = i;

    while (i < css.length && profundidad > 0) {
      if (css[i] === '{') profundidad += 1;
      else if (css[i] === '}') profundidad -= 1;
      i += 1;
    }

    cuerpos.push(css.slice(desde, i - 1));
  }

  return cuerpos;
}

/**
 * Los anchos fijos declarados en píxeles: `width`, `min-width` y `max-width`.
 *
 * **Sirve para las dos fuentes**: la hoja de estilos y los `style` en línea de un `.tsx`. De ahí las
 * dos formas que reconoce — `min-width: 400px` en CSS y `minWidth: '400px'` en JSX, con la comilla
 * que el valor lleva ahí. Sin la variante en línea, un ancho fijo escrito en una pantalla pasaba sin
 * que nadie lo viera (hallazgo 8 de la revisión del PR #28).
 *
 * `max-width` entra porque un `max-width: 400px` no desborda pero sí deja contenido inalcanzable si
 * lo que hay adentro no se adapta; se informa igual y el test decide qué hacer con cada uno.
 */
export function anchosFijosEnPx(fuente: string): { propiedad: string; valor: number }[] {
  const declaracion = /\b(min-?|max-?)?[wW]idth\s*:\s*['"`]?(\d+(?:\.\d+)?)px/g;

  return [...fuente.matchAll(declaracion)].map((coincidencia) => ({
    // Se normaliza a la forma del CSS para que el filtro de `max-width` valga en los dos lados.
    propiedad: `${(coincidencia[1] ?? '').replace(/-?$/, coincidencia[1] ? '-' : '')}width`,
    valor: Number(coincidencia[2]),
  }));
}

/**
 * Las clases referenciadas que la hoja no declara. **Es el verificador de `FR-001`**, y está acá y
 * no dentro del test para que el caso sintético que tiene que dar rojo ejercite exactamente el
 * mismo código que la comprobación real (D-03). Dos implementaciones divergen, y entonces lo que se
 * vio fallar no es lo que corre.
 */
export function clasesSinRegla(codigo: string, css: string): string[] {
  const declaradas = new Set(clasesDeclaradas(css));

  return clasesReferenciadas(codigo).filter((clase) => !declaradas.has(clase));
}

/**
 * La dirección contraria: las clases `l-`, `c-` y `u-` que la hoja **declara y nadie nombra**.
 *
 * `clasesSinRegla` responde "¿esta clase existe en el CSS?". Esto responde "¿a esta regla la usa
 * alguien?", y son dos preguntas distintas: una clase usada sin regla deja una pantalla sin estilo
 * —se ve— y una regla sin uso deja CSS muerto, que no se ve nunca. El daño del segundo es que la
 * próxima feature lo lee como si fuera una decisión y lo respeta.
 *
 * Se limita a los tres prefijos del proyecto: lo que se estila por selector de elemento —`button`,
 * `input`, `[role='alert']`— no se nombra en ningún `className` y no tiene por qué.
 */
export function reglasSinUso(codigo: string, css: string): string[] {
  const usadas = new Set(clasesReferenciadas(codigo));

  return clasesDeclaradas(css).filter((clase) => /^[lcu]-/.test(clase) && !usadas.has(clase));
}

/** Los anchos declarados por encima del objetivo. El verificador de `FR-003`, por la misma razón. */
export function anchosPorEncimaDe(css: string, objetivoPx: number) {
  return anchosFijosEnPx(css).filter(({ propiedad, valor }) => {
    // `max-width` acota hacia arriba: un `max-width: 800px` no produce desborde en una ventana de
    // 360 px, porque el elemento igual se encoge. Los otros dos sí lo producen.
    if (propiedad === 'max-width') {
      return false;
    }

    return valor > objetivoPx;
  });
}

/**
 * Los selectores que declaran `transition` o `animation` **fuera** de la guardia de movimiento
 * reducido. El verificador de `FR-016`.
 *
 * Está acá y no dentro del test para que el caso sintético que tiene que dar rojo ejercite
 * exactamente el mismo código que la comprobación real (D-03). Dos implementaciones divergen, y
 * entonces lo que se vio fallar no es lo que corre.
 *
 * **Sólo `no-preference` cuenta como guardia**, no `reduce`: el patrón de declarar la transición
 * suelta y anularla en un bloque `reduce` deja que una transición nueva nazca animada, que es lo que
 * research D-10 descartó. Una hoja que mencione `prefers-reduced-motion` en algún lado tampoco
 * alcanza: lo que se mira es dónde está **cada** transición.
 */
export function transicionesSinGuardia(css: string): string[] {
  /**
   * ¿Este cuerpo de regla declara movimiento **que anima algo**?
   *
   * Se parten las declaraciones y se mira cada propiedad con su valor, en lugar de buscar un patrón
   * sobre el texto entero. Con una sola expresión regular no sale: el `\s*` después de los dos
   * puntos puede matchear cero espacios y entonces el lookahead que tenía que descartar `none` se
   * salva por backtracking — `transition: none` daba positivo con un espacio y negativo sin
   * espacio. Se vio corriendo el caso sintético de la guardia `reduce`.
   *
   * `transition: none` y `animation: none` no cuentan: no animan nada, así que pueden estar donde
   * sea. Es justamente lo que declara el patrón `reduce` para apagar lo de arriba.
   */
  const animaAlgo = (cuerpo: string): boolean =>
    cuerpo.split(';').some((declaracion) => {
      const [propiedad, ...resto] = declaracion.split(':');

      if (!/^\s*(transition|animation)(-[a-z]+)?\s*$/.test(propiedad)) {
        return false;
      }

      const valor = resto.join(':').trim();

      return valor !== '' && valor !== 'none';
    });
  const guardia = /prefers-reduced-motion\s*:\s*no-preference/;
  const sinGuardia: string[] = [];

  // Se recorre bloque por bloque llevando la cuenta de dentro de qué at-rule está cada uno, que es
  // lo que una expresión regular sobre el texto entero no puede saber.
  let profundidad = 0;
  const guardias: boolean[] = [];
  let prefacio = '';
  let i = 0;

  while (i < css.length) {
    const caracter = css[i];

    if (caracter === '{') {
      const titulo = prefacio.trim();
      const esAtRule = titulo.startsWith('@');

      if (esAtRule) {
        guardias[profundidad] = guardia.test(titulo);
      } else {
        const cierre = cierreDe(css, i);
        const cuerpo = css.slice(i + 1, cierre);

        if (animaAlgo(cuerpo) && !guardias.slice(0, profundidad + 1).some(Boolean)) {
          sinGuardia.push(titulo);
        }
      }

      profundidad += 1;
      prefacio = '';
      i += 1;
      continue;
    }

    if (caracter === '}') {
      profundidad -= 1;
      guardias.length = Math.max(profundidad, 0);
      prefacio = '';
      i += 1;
      continue;
    }

    prefacio += caracter;
    i += 1;
  }

  return sinGuardia;
}

/** El índice de la llave que cierra la que abre en `apertura`. */
function cierreDe(css: string, apertura: number): number {
  let profundidad = 1;
  let i = apertura + 1;

  while (i < css.length && profundidad > 0) {
    if (css[i] === '{') profundidad += 1;
    else if (css[i] === '}') profundidad -= 1;
    i += 1;
  }

  return i - 1;
}

/**
 * Las formas de **elegir el tema desde JavaScript** que aparecen en un archivo. El verificador de
 * `FR-019`.
 *
 * Está acá y no dentro del test para que el caso sintético que tiene que dar rojo ejercite
 * exactamente el mismo código que la comprobación real (D-03). Dos implementaciones divergen, y
 * entonces lo que se vio fallar no es lo que corre.
 *
 * **Por qué esto es un requisito y no una preferencia de estilo** (feature 014, D-03): la paleta que
 * corresponde tiene que estar aplicada en la **primera** pintura. Un color declarado en CSS existe
 * antes de que corra una línea de JavaScript; uno elegido en JavaScript llega después de que React
 * monte, así que la primera pintura sale con el otro modo y se ve un destello —blanco, si el
 * dispositivo está en oscuro— que es justo lo que `FR-019` prohíbe. La consulta de medios del CSS es
 * la única fuente, y de paso el cambio en vivo de `FR-018` sale gratis: lo hace el navegador, no se
 * remonta nada y un formulario a medio llenar sigue igual.
 *
 * Lo que se busca son las tres formas de romperlo, y ninguna es un detalle de implementación:
 *
 *   1. **Leer la preferencia desde JavaScript** (`matchMedia`). Se denuncia cualquier `matchMedia`,
 *      no sólo el que nombre `prefers-color-scheme`: la consulta puede estar en una constante de
 *      otro archivo, y esta app no tiene ningún motivo para consultar una media query en código. El
 *      día que lo tenga, actualizar este verificador es una decisión que alguien toma a propósito.
 *   2. **Poner el tema a mano** en una clase o en un atributo (`classList`, `data-tema`).
 *   3. **Escribir la paleta o el esquema desde JavaScript** (`setProperty('--color-…')`,
 *      `style.colorScheme`), que es la variante que no necesita ni clase ni atributo.
 */
export function eleccionDeTemaEnJavaScript(codigo: string): string[] {
  const formas: { patron: RegExp; que: string }[] = [
    { patron: /\bmatchMedia\b/, que: 'consulta una media query desde JavaScript (matchMedia)' },
    {
      patron: /prefers-color-scheme/,
      que: 'nombra prefers-color-scheme fuera del CSS',
    },
    {
      patron: /\bclassList\b/,
      que: 'agrega o quita clases a mano (classList), que es como se pone un tema',
    },
    {
      patron: /data-(tema|theme|color-scheme)/,
      que: 'marca el tema en un atributo del documento',
    },
    {
      patron: /setProperty\(\s*['"`]--/,
      que: 'escribe una variable CSS desde JavaScript (setProperty)',
    },
    { patron: /\bcolorScheme\b/, que: 'escribe color-scheme desde JavaScript' },
  ];

  return formas.filter(({ patron }) => patron.test(codigo)).map(({ que }) => que);
}

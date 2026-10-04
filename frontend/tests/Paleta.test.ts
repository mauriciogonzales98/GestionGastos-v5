// @vitest-environment node

import { describe, expect, it } from 'vitest';
import { relacionDeContraste } from '../src/ui/contraste';
import { coloresDeclarados, coloresPorModo, hojaDeEstilos, type Modo } from './fuentes';

/**
 * NFR-001, PRD:RNF-06, PRD-06:AC-06 — el contraste AA de **Lila**, en sus dos modos.
 *
 * **Mide lo que está en el CSS, no un objeto de TypeScript** (D-01 de la feature 011). La feature
 * 010 tenía los cuatro colores del dashboard en `ui/contraste.ts` y el componente los bajaba a
 * variables; desde que existe una paleta entera el lado correcto es el CSS, porque un color tiene
 * que existir antes de que corra una línea de JavaScript, y porque un verificador que lee el archivo
 * cubre cualquier color que alguien agregue sin que nadie lo agende (NFR-003).
 *
 * **Y desde la feature 014 mide los DOS modos.** La paleta clara y la oscura tienen los mismos roles
 * y valores distintos, así que un par que cumple en claro puede no cumplir en oscuro: medir uno solo
 * dejaría la mitad del producto sin verificar, y es la mitad que alguien usa de noche. Un color
 * declarado en un modo y no en el otro también es rojo (`NFR-001`), y ésa es la forma de que agregar
 * un color obligue a decidir su versión oscura en el mismo momento.
 */

/** AA: 4,5:1 en texto normal; 3:1 en texto grande y en componentes de interfaz. */
const TEXTO_NORMAL = 4.5;
const COMPONENTE = 3;

const MODOS: Modo[] = ['claro', 'oscuro'];

/**
 * Qué se mide contra qué. Es lo único escrito a mano de este archivo, y tiene que serlo: la hoja
 * declara colores, no dice cuál va sobre cuál. Un par que falte acá es un par sin medir, así que
 * agregar un color a la paleta obliga a decir contra qué fondo se lo va a usar.
 *
 * **Cada color que lleva texto se mide contra los DOS fondos** —la página y la superficie— porque se
 * usa sobre los dos: el mismo texto secundario está sobre el fondo en el período del resumen y sobre
 * la superficie dentro de una tarjeta. Medir contra uno solo deja pasar el otro.
 */
const PARES: { frente: string; fondo: string; umbral: number; que: string }[] = [
  { frente: '--color-texto', fondo: '--color-fondo', umbral: TEXTO_NORMAL, que: 'el texto normal' },
  {
    frente: '--color-texto',
    fondo: '--color-superficie',
    umbral: TEXTO_NORMAL,
    que: 'el texto sobre una tarjeta',
  },
  {
    frente: '--color-texto-secundario',
    fondo: '--color-fondo',
    umbral: TEXTO_NORMAL,
    que: 'el texto secundario',
  },
  {
    frente: '--color-texto-secundario',
    fondo: '--color-superficie',
    umbral: TEXTO_NORMAL,
    que: 'el texto secundario sobre una tarjeta',
  },
  { frente: '--color-acento', fondo: '--color-fondo', umbral: TEXTO_NORMAL, que: 'el acento' },
  {
    frente: '--color-acento',
    fondo: '--color-superficie',
    umbral: TEXTO_NORMAL,
    que: 'el acento sobre una tarjeta',
  },
  {
    frente: '--color-sobre-acento',
    fondo: '--color-acento',
    umbral: TEXTO_NORMAL,
    que: 'el texto del botón principal',
  },
  { frente: '--color-foco', fondo: '--color-fondo', umbral: COMPONENTE, que: 'el anillo de foco' },
  {
    frente: '--color-foco',
    fondo: '--color-superficie',
    umbral: COMPONENTE,
    que: 'el anillo de foco sobre una tarjeta',
  },
  {
    frente: '--color-error',
    fondo: '--color-fondo',
    umbral: TEXTO_NORMAL,
    que: 'el texto de error',
  },
  {
    frente: '--color-error',
    fondo: '--color-superficie',
    umbral: TEXTO_NORMAL,
    que: 'el error sobre una tarjeta',
  },
  { frente: '--color-exito', fondo: '--color-fondo', umbral: TEXTO_NORMAL, que: 'la confirmación' },
  {
    frente: '--color-exito',
    fondo: '--color-superficie',
    umbral: TEXTO_NORMAL,
    que: 'la confirmación sobre una tarjeta',
  },
  { frente: '--color-borde', fondo: '--color-fondo', umbral: COMPONENTE, que: 'los bordes' },
  {
    frente: '--color-borde',
    fondo: '--color-superficie',
    umbral: COMPONENTE,
    que: 'los bordes sobre una tarjeta',
  },
  {
    frente: '--color-barra',
    fondo: '--color-riel',
    umbral: COMPONENTE,
    que: 'la barra sobre su riel',
  },
];

/**
 * **Dos pares que NO se miden, y son decisiones, no olvidos.**
 *
 * 1. **El riel contra el fondo.** Lo que WCAG 1.4.11 exige que se distinga son las partes que hacen
 *    falta para identificar un componente. En el desglose eso es **la barra**: el riel es sólo hasta
 *    dónde podría llegar. Pedirle 3:1 contra la página obligaría a un gris oscuro que se leería como
 *    si fuera el dato, que es justo la confusión que hay que evitar. El riel entra igual a la tabla
 *    —por `barra/riel`—, así que ningún color declarado queda sin medir.
 *
 * 2. **El foco contra el acento.** Da 1,58 en claro y 1,46 en oscuro, y no hace falta: el anillo se
 *    dibuja **separado** del control por `outline-offset`, así que lo que tiene al lado es el fondo o
 *    la superficie, que son los pares que sí se miden. Esa separación es parte del contrato del
 *    sistema y tiene su propia prueba en `Foco.test.ts`: si alguien la quita, el par que se mide
 *    deja de ser el que se ve.
 */

describe('NFR-001 · Lila cumple contraste AA en los dos modos', () => {
  const paleta = coloresPorModo(hojaDeEstilos());

  describe.each(MODOS)('modo %s', (modo) => {
    it.each(PARES)(
      '$que llega a su umbral (NFR-001, PRD:RNF-06, PRD-06:AC-06)',
      ({ frente, fondo, umbral }) => {
        const unColor = paleta[modo][frente];
        const otroColor = paleta[modo][fondo];

        // Se comprueba que existan antes de medir: un `undefined` haría lanzar a `canales` con un
        // mensaje sobre un color ilegible, que apunta al lugar equivocado.
        expect(unColor, `falta ${frente} en el modo ${modo}`).toBeDefined();
        expect(otroColor, `falta ${fondo} en el modo ${modo}`).toBeDefined();

        expect(relacionDeContraste(unColor, otroColor)).toBeGreaterThanOrEqual(umbral);
      },
    );
  });

  it('mide todos los colores que la hoja declara, sin dejar ninguno afuera (NFR-003)', () => {
    const medidos = new Set(PARES.flatMap(({ frente, fondo }) => [frente, fondo]));
    const declarados = Object.keys(paleta.claro);

    // Ningún número fijo sobre cuántos colores hay (regla D-10 de la 009, heredada): lo que se
    // exige es que la tabla de arriba cubra lo que la hoja declara. Agregar un color a la paleta
    // sin decir contra qué se lo mide pone este test en rojo, que es exactamente lo que tiene que
    // pasar.
    expect(declarados.filter((color) => !medidos.has(color))).toEqual([]);
  });

  /**
   * **Todo color existe en los dos modos** (`NFR-001`).
   *
   * No se deduce de los pares: `coloresPorModo` resuelve el modo oscuro como el claro con el bloque
   * oscuro encima, igual que el navegador, así que un color sin versión oscura hereda la clara y
   * mediría bien. Lo que esta prueba exige es que esté **declarado** en los dos bloques, que es una
   * decisión del contrato: un color heredado del modo claro en modo oscuro es un color que nadie
   * eligió para el fondo oscuro, y se va a ver mal aunque la cuenta pase.
   */
  it('declara cada color en los dos modos, ninguno heredado por descuido (NFR-001)', () => {
    const css = hojaDeEstilos();
    const soloEnOscuro = /@media[^{]*prefers-color-scheme\s*:\s*dark/.test(css);

    expect(soloEnOscuro, 'la hoja no declara ningún bloque de modo oscuro').toBe(true);

    const enElBloqueOscuro = new Set(
      Object.keys(coloresPorModo(css).oscuro).filter((color) => {
        // Un color está declarado en oscuro si su valor difiere del claro, o si el bloque oscuro lo
        // nombra explícitamente. Lo segundo es lo que importa, y se comprueba sobre el texto.
        const declaracion = new RegExp(`${color}\\s*:`, 'g');
        return [...css.matchAll(declaracion)].length >= 2;
      }),
    );

    const sinVersionOscura = Object.keys(coloresPorModo(css).claro).filter(
      (color) => !enElBloqueOscuro.has(color),
    );

    expect(sinVersionOscura, 'estos colores no tienen valor para el modo oscuro').toEqual([]);
  });
});

/**
 * D-03 · el verificador se ve fallar.
 *
 * Es la misma forma que la feature 010 le dio a `Contraste.test.ts` y por el mismo motivo: sin un
 * caso que tenga que dar por debajo del umbral, no sabemos que la cuenta sabe rechazar. Desde la
 * feature 014 se agrega el caso del modo: una paleta a la que le falta el bloque oscuro tiene que
 * ponerse en rojo, que es el agujero que esta feature podría haber dejado abierto.
 */
describe('D-03 · el verificador de contraste sabe fallar', () => {
  it('rechaza un par por debajo del umbral de texto normal', () => {
    // Gris medio sobre blanco: se lee mal y da por debajo de 4,5:1.
    expect(relacionDeContraste('#999999', '#ffffff')).toBeLessThan(TEXTO_NORMAL);
  });

  it('extrae los colores del CSS y no de otro lado', () => {
    const css = ':root { --color-texto: #111111; --color-fondo: #ffffff; --espaciado: 1rem; }';

    // El espaciado no entra: una relación de contraste sobre `1rem` no significa nada.
    expect(coloresDeclarados(css)).toEqual({
      '--color-texto': '#111111',
      '--color-fondo': '#ffffff',
    });
  });

  it('devuelve vacío cuando no hay colores, en vez de aparentar que midió', () => {
    expect(coloresDeclarados('.c-algo { color: red; }')).toEqual({});
  });

  it('separa los dos modos en vez de aplanarlos (NFR-001)', () => {
    const css = `
      :root { --color-fondo: #ffffff; --color-texto: #000000; }
      @media (prefers-color-scheme: dark) {
        :root { --color-fondo: #000000; --color-texto: #ffffff; }
      }
    `;

    const { claro, oscuro } = coloresPorModo(css);

    expect(claro['--color-fondo']).toBe('#ffffff');
    expect(oscuro['--color-fondo']).toBe('#000000');
  });

  /**
   * **El caso que esta feature podría haber dejado abierto** (`FR-002`, `NFR-001`).
   *
   * Un color declarado sólo en `:root` hereda su valor en modo oscuro, así que la cuenta de
   * contraste le pasa: se mide el color claro contra el fondo claro, que es el par que cumple. El
   * problema es que ese par no es el que se ve — en modo oscuro ese color va sobre fondo oscuro —, y
   * un verificador que sólo mirara los pares diría que todo está bien.
   *
   * Por eso la prueba real exige que cada color esté **declarado** en los dos bloques, y por eso
   * hace falta este caso: sin él no sabríamos que esa exigencia sabe rechazar.
   */
  it('detecta un color declarado en un solo modo, aunque su contraste cumpla (FR-002, NFR-001)', () => {
    const css = `
      :root { --color-fondo: #ffffff; --color-texto: #000000; --color-acento: #6b46b0; }
      @media (prefers-color-scheme: dark) {
        :root { --color-fondo: #111111; --color-texto: #ffffff; }
      }
    `;

    const { claro, oscuro } = coloresPorModo(css);

    // El acento existe en los dos, porque el oscuro lo hereda...
    expect(oscuro['--color-acento']).toBe(claro['--color-acento']);

    // ...pero el bloque oscuro no lo nombra, y eso es lo que la prueba real cuenta: cuántas veces
    // aparece declarado en la hoja. Una sola vez significa que nadie eligió su versión oscura.
    const veces = [...css.matchAll(/--color-acento\s*:/g)].length;

    expect(veces).toBe(1);
  });

  it('no se corta en la primera llave de un bloque oscuro con reglas anidadas', () => {
    // El caso que una expresión regular ingenua pierde: el `@media` tiene DOS bloques adentro, y lo
    // que se busca está en el segundo. Es el mismo error que `clasesDeclaradas` tuvo una vez.
    const css = `
      :root { --color-fondo: #ffffff; }
      @media (prefers-color-scheme: dark) {
        :root { --color-fondo: #111111; }
        body { color: white; }
        .c-tarjeta { --color-superficie: #222222; }
      }
    `;

    expect(coloresPorModo(css).oscuro).toMatchObject({
      '--color-fondo': '#111111',
      '--color-superficie': '#222222',
    });
  });
});

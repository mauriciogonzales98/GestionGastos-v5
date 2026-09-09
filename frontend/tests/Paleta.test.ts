// @vitest-environment node

import { describe, expect, it } from 'vitest';
import { relacionDeContraste } from '../src/ui/contraste';
import { coloresDeclarados, hojaDeEstilos } from './fuentes';

/**
 * NFR-001, PRD:RNF-06, PRD-06:AC-06 — el contraste AA de la paleta declarada.
 *
 * **Mide lo que está en el CSS, no un objeto de TypeScript** (D-01). La feature 010 tenía los
 * cuatro colores del dashboard en `ui/contraste.ts` y el componente los bajaba a variables; desde
 * que existe una paleta entera el lado correcto es el CSS, porque un color tiene que existir antes
 * de que corra una línea de JavaScript, y porque un verificador que lee el archivo cubre cualquier
 * color que alguien agregue sin que nadie lo agende (NFR-003).
 */

/** AA: 4,5:1 en texto normal; 3:1 en texto grande y en componentes de interfaz. */
const TEXTO_NORMAL = 4.5;
const COMPONENTE = 3;

/**
 * Qué se mide contra qué. Es lo único escrito a mano de este archivo, y tiene que serlo: la hoja
 * declara colores, no dice cuál va sobre cuál. Un par que falte acá es un par sin medir, así que
 * agregar un color a la paleta obliga a decir contra qué fondo se lo va a usar.
 */
const PARES: { frente: string; fondo: string; umbral: number; que: string }[] = [
  { frente: '--color-texto', fondo: '--color-fondo', umbral: TEXTO_NORMAL, que: 'el texto normal' },
  {
    frente: '--color-error',
    fondo: '--color-fondo',
    umbral: TEXTO_NORMAL,
    que: 'el texto de error',
  },
  { frente: '--color-acento', fondo: '--color-fondo', umbral: TEXTO_NORMAL, que: 'el acento' },
  { frente: '--color-foco', fondo: '--color-fondo', umbral: COMPONENTE, que: 'el anillo de foco' },
  { frente: '--color-borde', fondo: '--color-fondo', umbral: COMPONENTE, que: 'los bordes' },
  {
    frente: '--color-barra',
    fondo: '--color-fondo',
    umbral: COMPONENTE,
    que: 'la barra sobre la página',
  },
  {
    frente: '--color-barra',
    fondo: '--color-riel',
    umbral: COMPONENTE,
    que: 'la barra sobre su riel',
  },
];

/**
 * **El riel no se mide contra el fondo, y es una decisión, no un olvido.**
 *
 * Lo que WCAG 1.4.11 exige que se distinga son las partes que hacen falta para identificar un
 * componente. En el desglose eso es **la barra**: el riel es sólo hasta dónde podría llegar. Pedirle
 * 3:1 contra la página obligaría a un gris oscuro que se leería como si fuera el dato, que es
 * justo la confusión que hay que evitar. El riel entra igual a la tabla —por `barra/riel`—, así
 * que ningún color declarado queda sin medir.
 */

describe('NFR-001 · la paleta cumple contraste AA', () => {
  const paleta = coloresDeclarados(hojaDeEstilos());

  it.each(PARES)(
    '$que llega a su umbral (NFR-001, PRD:RNF-06, PRD-06:AC-06)',
    ({ frente, fondo, umbral }) => {
      const unColor = paleta[frente];
      const otroColor = paleta[fondo];

      // Se comprueba que existan antes de medir: un `undefined` haría lanzar a `canales` con un
      // mensaje sobre un color ilegible, que apunta al lugar equivocado.
      expect(unColor, `falta ${frente} en la hoja de estilos`).toBeDefined();
      expect(otroColor, `falta ${fondo} en la hoja de estilos`).toBeDefined();

      expect(relacionDeContraste(unColor, otroColor)).toBeGreaterThanOrEqual(umbral);
    },
  );

  it('mide todos los colores que la hoja declara, sin dejar ninguno afuera (NFR-003)', () => {
    const medidos = new Set(PARES.flatMap(({ frente, fondo }) => [frente, fondo]));
    const declarados = Object.keys(paleta);

    // Ningún número fijo sobre cuántos colores hay (regla D-10 de la 009, heredada): lo que se
    // exige es que la tabla de arriba cubra lo que la hoja declara. Agregar un color a la paleta
    // sin decir contra qué se lo mide pone este test en rojo, que es exactamente lo que tiene que
    // pasar.
    expect(declarados.filter((color) => !medidos.has(color))).toEqual([]);
  });
});

/**
 * D-03 · el verificador se ve fallar.
 *
 * Es la misma forma que la feature 010 le dio a `Contraste.test.ts` y por el mismo motivo: sin un
 * caso que tenga que dar por debajo del umbral, no sabemos que la cuenta sabe rechazar.
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
});

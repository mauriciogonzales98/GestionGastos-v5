// @vitest-environment node

import { describe, expect, it } from 'vitest';
import { hojaDeEstilos } from './fuentes';

/**
 * La invariante del foco del contrato de Lila: `outline-offset` de al menos 2px.
 *
 * **Es lo que hace verdadero el par de contraste que `Paleta.test.ts` mide.** El anillo de foco se
 * mide contra `--color-fondo` y `--color-superficie`, y llega a 9,94 y 10,60 en claro. Contra el
 * acento daría 1,58, que no cumple nada — y el acento es justo lo que el anillo tiene al lado
 * cuando rodea al botón principal.
 *
 * La separación es lo que resuelve eso: con 2px de aire, lo que el anillo tiene al lado es el fondo
 * o la superficie, o sea los pares que sí se miden. Si alguien quita el `outline-offset`, los dos
 * tests siguen en verde y el anillo deja de verse sobre el botón principal: la medición pasaría a
 * ser de un par que ya no es el que se ve.
 *
 * Por eso la invariante tiene prueba propia, y por eso está escrita en
 * `contracts/sistema-visual.md` como parte del contrato y no como un detalle de la hoja.
 */

/** Lo que el contrato exige como mínimo. */
const SEPARACION_MINIMA_PX = 2;

describe('Lila · el anillo de foco se dibuja separado del control', () => {
  it('declara outline-offset de al menos 2px en :focus-visible (NFR-001, PRD:RNF-06)', () => {
    const css = hojaDeEstilos();
    const bloque = css.split('}').find((trozo) => trozo.includes(':focus-visible'));

    expect(bloque, 'falta la regla de :focus-visible').toBeDefined();

    const separacion = /outline-offset\s*:\s*(\d+(?:\.\d+)?)px/.exec(bloque!);

    expect(separacion, 'la regla de foco no declara outline-offset').not.toBeNull();
    expect(Number(separacion![1])).toBeGreaterThanOrEqual(SEPARACION_MINIMA_PX);
  });

  /**
   * Y el foco **nunca** se anula sin reemplazo.
   *
   * Un `outline: none` suelto deja a quien navega con teclado sin saber dónde está parado. Se
   * acepta sólo si en el mismo bloque hay otro indicador —un `box-shadow` o un `border`—, que es el
   * patrón legítimo de reemplazarlo en lugar de quitarlo.
   */
  it('no anula el foco en ningún lugar sin poner otro indicador (PRD:RNF-06)', () => {
    const sinIndicador = hojaDeEstilos()
      .split('}')
      .filter((bloque) => /outline\s*:\s*(none|0)\b/.test(bloque))
      .filter((bloque) => !/box-shadow|border/.test(bloque))
      .map((bloque) => bloque.split('{')[0].trim());

    expect(sinIndicador).toEqual([]);
  });
});

// @vitest-environment node

import { describe, expect, it } from 'vitest';
import { hojaDeEstilos, transicionesSinGuardia } from './fuentes';

/**
 * FR-016 — si Lila tiene transiciones, se suprimen cuando la persona pidió reducir movimiento.
 *
 * **La forma elegida es la guardia por omisión y no la cancelación** (research D-10): toda
 * transición vive **dentro** de `@media (prefers-reduced-motion: no-preference)`, en lugar de
 * declararse suelta y después anularse con un bloque `reduce` que la apague.
 *
 * La diferencia importa y es la razón de que esto tenga una prueba propia. Con el patrón de
 * cancelación, una transición nueva nace animada y queda animada hasta que alguien se acuerde de
 * agregarla a la lista de anulaciones — o sea que el caso correcto depende de la memoria de quien
 * escribe. Con la guardia, una transición escrita fuera del bloque pone **este** test en rojo, así
 * que lo que depende de la memoria de nadie es el caso incorrecto.
 *
 * Hoy Lila declara transiciones sólo de color y de fondo, de 120 ms, en botones y en las secciones
 * de la barra. No hay ninguna de posición.
 */
describe('FR-016 · ninguna transición queda fuera de la guardia de movimiento reducido', () => {
  it('toda transición y animación vive dentro de prefers-reduced-motion: no-preference (FR-016)', () => {
    expect(transicionesSinGuardia(hojaDeEstilos())).toEqual([]);
  });
});

/**
 * D-03 · el verificador se ve fallar.
 *
 * **Acá pesa más que en los otros**, porque hoy Lila casi no tiene transiciones: una comprobación
 * que recorre una hoja sin transiciones pasa sin mirar nada, y un verde que no miró nada es peor
 * que no tener el test. Estos casos son los que prueban que cuando haya algo que mirar, lo va a
 * mirar.
 */
describe('D-03 · el verificador de movimiento reducido sabe fallar', () => {
  it('detecta una transición declarada al aire', () => {
    const css = '.c-boton { transition: background-color 120ms; }';

    expect(transicionesSinGuardia(css)).toEqual(['.c-boton']);
  });

  it('detecta una animación al aire, no sólo una transición', () => {
    const css = '.c-cargando { animation: girar 1s linear infinite; }';

    expect(transicionesSinGuardia(css)).toEqual(['.c-cargando']);
  });

  it('acepta la que está dentro de la guardia', () => {
    const css = `
      @media (prefers-reduced-motion: no-preference) {
        .c-boton { transition: background-color 120ms; }
      }
    `;

    expect(transicionesSinGuardia(css)).toEqual([]);
  });

  it('no se conforma con que la guardia esté en algún lado de la hoja', () => {
    // El caso que una búsqueda ingenua —"¿la hoja menciona prefers-reduced-motion?"— daría por
    // bueno: la guardia existe, y la transición que importa está afuera.
    const css = `
      @media (prefers-reduced-motion: no-preference) {
        .c-seccion { transition: color 120ms; }
      }
      .c-boton { transition: background-color 120ms; }
    `;

    expect(transicionesSinGuardia(css)).toEqual(['.c-boton']);
  });

  it('tampoco acepta la guardia de reduce, que es el patrón que D-10 descartó', () => {
    // `reduce` apaga lo que ya nació animado. Una transición nueva no entra sola a esta lista, así
    // que el patrón se sostiene en que alguien se acuerde. No cuenta como guardia.
    const css = `
      .c-boton { transition: background-color 120ms; }
      @media (prefers-reduced-motion: reduce) {
        .c-boton { transition: none; }
      }
    `;

    expect(transicionesSinGuardia(css)).toEqual(['.c-boton']);
  });
});

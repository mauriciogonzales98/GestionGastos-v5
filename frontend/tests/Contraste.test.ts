import { describe, expect, it } from 'vitest';
import { COLORES_DEL_DASHBOARD, relacionDeContraste } from '../src/ui/contraste';

/**
 * `PRD:AC-13` y `NFR-003`: contraste AA en el dashboard.
 *
 * **El primer bloque prueba al verificador y el segundo prueba al dashboard, en ese orden.** Un
 * test de contraste que nunca se vio fallar no dice nada: es el Principio V de la constitución
 * —"las barreras se verifican a sí mismas"— aplicado a algo que no es un script. Sin el caso que
 * tiene que dar POR DEBAJO del umbral, no sabríamos si la función distingue o si devuelve un número
 * grande siempre.
 *
 * **Alcance honesto**: D-04 deja al dashboard sin paleta categórica, y el proyecto no tiene paleta
 * en absoluto —`estilos/base.css` dice que colores y tipografía son del ticket 6—. Así que lo que
 * hay para medir acá es poco. Este verificador nace chico y queda listo para cuando haya más.
 */
describe('relacionDeContraste — el verificador, antes que lo verificado', () => {
  it('negro sobre blanco da el máximo posible, 21:1', () => {
    expect(relacionDeContraste('#000000', '#ffffff')).toBeCloseTo(21, 1);
  });

  it('un color contra sí mismo da 1:1', () => {
    expect(relacionDeContraste('#767676', '#767676')).toBeCloseTo(1, 2);
  });

  it('es simétrico: da lo mismo cuál es el frente y cuál el fondo', () => {
    expect(relacionDeContraste('#595959', '#ffffff')).toBeCloseTo(
      relacionDeContraste('#ffffff', '#595959'),
      5,
    );
  });

  /**
   * **El caso que tiene que fallar.**
   *
   * `#999999` sobre blanco da ~2,85:1, por debajo del 4,5:1 que AA pide para texto normal. Es el
   * gris que todo el mundo elige para "texto secundario" y el que más se cuela en una paleta sin
   * verificar. Si este caso pasara el umbral, el verificador estaría roto y los de abajo no
   * significarían nada.
   */
  it('un gris claro sobre blanco NO llega a 4,5:1, y el verificador lo dice', () => {
    const relacion = relacionDeContraste('#999999', '#ffffff');

    expect(relacion).toBeLessThan(4.5);
    expect(relacion).toBeCloseTo(2.85, 1);
  });

  it('el límite justo de AA se reconoce como tal', () => {
    // #767676 sobre blanco es el gris más claro que todavía cumple 4,5:1. Es el borde, y un
    // verificador que se equivoque por poco se equivoca justo acá.
    expect(relacionDeContraste('#767676', '#ffffff')).toBeGreaterThanOrEqual(4.5);
  });

  it('acepta las tres formas en que un color puede estar escrito', () => {
    expect(relacionDeContraste('#000', '#fff')).toBeCloseTo(21, 1);
    expect(relacionDeContraste('rgb(0, 0, 0)', '#ffffff')).toBeCloseTo(21, 1);
  });
});

describe('los colores del dashboard cumplen AA PRD:AC-13', () => {
  it('el texto normal llega a 4,5:1 contra su fondo', () => {
    expect(
      relacionDeContraste(COLORES_DEL_DASHBOARD.texto, COLORES_DEL_DASHBOARD.fondo),
    ).toBeGreaterThanOrEqual(4.5);
  });

  /**
   * La barra es un componente de interfaz, no texto: el umbral es 3:1.
   *
   * Es lo que `NFR-003` fija, y es lo que distingue a una barra visible de una que se pierde contra
   * su propio riel para quien tiene baja visión.
   */
  it('la barra llega a 3:1 contra el fondo sobre el que se dibuja', () => {
    expect(
      relacionDeContraste(COLORES_DEL_DASHBOARD.barra, COLORES_DEL_DASHBOARD.rielDeLaBarra),
    ).toBeGreaterThanOrEqual(3);
  });

  it('el riel de la barra se distingue del fondo de la página', () => {
    expect(
      relacionDeContraste(COLORES_DEL_DASHBOARD.rielDeLaBarra, COLORES_DEL_DASHBOARD.fondo),
    ).toBeGreaterThanOrEqual(1.1);
  });
});

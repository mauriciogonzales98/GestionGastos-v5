// @vitest-environment node

import { describe, expect, it } from 'vitest';
import { anchosPorEncimaDe, codigoDeLasPantallas, hojaDeEstilos } from './fuentes';

/**
 * FR-003, FR-004 — el ancho objetivo de 360 px, **verificado por regla y no por medición** (D-04).
 *
 * **Lo que este archivo NO hace, dicho antes que lo que hace**: no mide anchos. jsdom y happy-dom
 * no maquetan —no resuelven `flex`, y `getBoundingClientRect` devuelve ceros—, así que un test que
 * afirmara "no desborda" estaría afirmando algo que no midió, y eso es peor que no tenerlo: entrena
 * a confiar en un verde vacío.
 *
 * Lo que sí hace es comprobar las **reglas de estilo que podrían producir desborde**: un ancho fijo
 * mayor al objetivo, y contenido ancho sin un contenedor desplazable. Un verde acá significa
 * "ninguna regla puede producir desborde", no "no desborda".
 *
 * La comprobación real es el paso 1 del quickstart, con un navegador y la ventana en 360 px. Está
 * anotada como deuda **D11-01**.
 */

const OBJETIVO_PX = 360;

describe('FR-003 · ninguna regla puede producir desborde a 360 px', () => {
  it('no declara ningún ancho fijo mayor al objetivo (FR-003, PRD-06:AC-05)', () => {
    // `max-width` no cuenta: acota hacia arriba y el elemento igual se encoge en una ventana
    // angosta. Los que desbordan son `width` y `min-width`.
    expect(anchosPorEncimaDe(hojaDeEstilos(), OBJETIVO_PX)).toEqual([]);
  });

  /**
   * **Los estilos en línea también cuentan** (hallazgo 8 de la revisión del PR #28).
   *
   * El verificador miraba sólo la hoja de estilos, así que un `style={{ width: '400px' }}` escrito
   * en un `.tsx` pasaba sin que nadie lo viera. Hoy el único ancho en línea es el de la barra del
   * desglose, que va en porcentaje; el punto es que la promesa de `NFR-003` no dependa de que nadie
   * se acuerde de mirar el otro lado.
   */
  it('tampoco los declara en línea, dentro de una pantalla (FR-003)', () => {
    expect(anchosPorEncimaDe(codigoDeLasPantallas(), OBJETIVO_PX)).toEqual([]);
  });

  it('le da a la tabla del listado un contenedor que se desplaza solo (FR-004)', () => {
    // Seis columnas no entran en un teléfono. Lo que no puede pasar es que desborde la PÁGINA: la
    // tabla se desplaza dentro de lo suyo.
    const css = hojaDeEstilos();
    const bloqueDelListado = css
      .split('}')
      .find((bloque) => bloque.includes('c-listado-movimientos__desborde'));

    expect(bloqueDelListado, 'falta el contenedor desplazable de la tabla').toBeDefined();
    expect(bloqueDelListado).toMatch(/overflow-x\s*:\s*auto/);
  });
});

/**
 * D-03 · el verificador se ve fallar.
 *
 * Es el tercero de los tres que leen del disco y el que estuvo a punto de quedarse sin su caso
 * rojo, porque verifica reglas en vez de medir y es fácil pensar que no cuenta. Cuenta: si la
 * expresión regular dejara de reconocer una declaración de ancho, informaría 0 violaciones por no
 * haber encontrado ninguna regla — el mismo modo de fallo que `verificar-desglose.sh` documenta
 * desde la feature 007.
 */
describe('D-03 · el verificador de anchos sabe fallar', () => {
  it('detecta un ancho fijo por encima del objetivo', () => {
    const css = '.c-algo { width: 400px; }';

    expect(anchosPorEncimaDe(css, OBJETIVO_PX)).toEqual([{ propiedad: 'width', valor: 400 }]);
  });

  it('detecta un min-width por encima del objetivo, que es el que de verdad desborda', () => {
    const css = '.c-tabla { min-width: 640px; }';

    expect(anchosPorEncimaDe(css, OBJETIVO_PX)).toEqual([{ propiedad: 'min-width', valor: 640 }]);
  });

  it('detecta un ancho fijo escrito en línea dentro de un .tsx', () => {
    const codigo = `<div style={{ width: '400px' }} />`;

    expect(anchosPorEncimaDe(codigo, OBJETIVO_PX)).toEqual([{ propiedad: 'width', valor: 400 }]);
  });

  it('deja pasar un max-width grande y un ancho por debajo del objetivo', () => {
    const css = '.c-uno { max-width: 800px; } .c-dos { width: 320px; }';

    expect(anchosPorEncimaDe(css, OBJETIVO_PX)).toEqual([]);
  });
});

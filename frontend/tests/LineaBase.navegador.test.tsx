import { afterEach, describe, expect, it } from 'vitest';
import { page } from 'vitest/browser';
import { FormularioMovimiento } from '../src/movimientos/FormularioMovimiento';
import { ResumenDelPeriodo } from '../src/resumen/ResumenDelPeriodo';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import { RESUMEN } from './resumen.fixture';
import { desmontar, montar } from './montar';
import '../src/estilos/index.css';

/**
 * FR-032, SC-011 — **la altura de hoy**, medida antes de tocar nada.
 *
 * `FR-032` pide que el formulario compacto ocupe menos alto que el actual, y `SC-011` que el resumen
 * ocupe menos de la mitad. Las dos comparan contra un número que **no existía**: hasta esta feature
 * no había con qué medir una altura (deuda D11-01).
 *
 * Por eso este archivo es lo primero que se escribe de la fase 2, y corre **antes** de modificar
 * `CamposDelMovimiento.tsx` y `ResumenDelPeriodo.tsx`. Comparar contra un número estimado sería la
 * trampa que la memoria del proyecto tiene documentada tres veces: una premisa que se recopia de
 * spec en spec sin verificarse.
 *
 * **Lo que este archivo NO es**: una prueba de requisito. No afirma que la altura de hoy esté bien
 * ni mal. Afirma que las mediciones son **estables** —dos montajes del mismo componente dan el mismo
 * alto— y deja los números anotados en `specs/014-identidad-visual/research.md`, D-07. Cuando el
 * formulario y el resumen se recompongan, las pruebas de US6 y US7 comparan contra esos números.
 *
 * Por eso tampoco se escriben los números acá como constantes a cumplir: si quedaran escritos en el
 * archivo, al recomponer habría que editarlos, y un número que se edita para que pase no mide nada.
 */

/** El ancho en que la spec pide la comparación: el teléfono objetivo (`FR-032`). */
const ANCHO_OBJETIVO = 360;

const ALTO = 900;

/** El alto que ocupa `elemento` contando su propio margen, que es lo que empuja al resto. */
function altoOcupado(elemento: Element): number {
  const estilo = getComputedStyle(elemento);
  const margenes = parseFloat(estilo.marginTop) + parseFloat(estilo.marginBottom);

  return elemento.getBoundingClientRect().height + margenes;
}

describe('FR-032, SC-011 · la altura de hoy, para poder comparar después', () => {
  afterEach(desmontar);

  it('mide el formulario de alta a 360 px, sin errores a la vista (FR-032)', async () => {
    await page.viewport(ANCHO_OBJETIVO, ALTO);

    const contenedor = await montar(
      <FormularioMovimiento
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        hoy="2026-10-01"
        onGuardar={() => {}}
      />,
    );

    const formulario = contenedor.querySelector('form')!;
    const alto = altoOcupado(formulario);

    // Un formulario con seis campos no puede medir menos de un par de cientos de píxeles: el tope
    // de abajo es lo que separa "midió" de "devolvió cero porque no maqueta".
    expect(alto).toBeGreaterThan(200);

    // Y el número, a la salida, para anotarlo en research D-07.
    console.log(`[LÍNEA BASE] formulario de alta a ${ANCHO_OBJETIVO} px: ${Math.round(alto)} px`);
  });

  it('mide el resumen del mes con dos monedas, una sin movimientos (SC-011)', async () => {
    await page.viewport(ANCHO_OBJETIVO, ALTO);

    // `RESUMEN` trae exactamente las dos formas que `SC-011` nombra: una moneda con movimientos y
    // otra en cero. No se escribe cuántas son: sale del fixture (regla D-10 de la feature 009).
    const contenedor = await montar(<ResumenDelPeriodo resumen={RESUMEN} monedas={MONEDAS} />);

    const alto = altoOcupado(contenedor.firstElementChild!);

    expect(alto).toBeGreaterThan(200);

    console.log(`[LÍNEA BASE] resumen con desglose a ${ANCHO_OBJETIVO} px: ${Math.round(alto)} px`);
  });

  /**
   * **La medición tiene que ser estable, o no sirve para comparar.**
   *
   * Es la mitad que hace confiable a las otras dos: si el mismo componente montado dos veces diera
   * alturas distintas, el "menos alto que antes" de `FR-032` sería ruido. Se tolera 1 px por el
   * redondeo de media línea de texto.
   */
  it('da el mismo alto en dos montajes del mismo componente (principio IV)', async () => {
    await page.viewport(ANCHO_OBJETIVO, ALTO);

    const medir = async () => {
      const contenedor = await montar(<ResumenDelPeriodo resumen={RESUMEN} monedas={MONEDAS} />);
      return altoOcupado(contenedor.firstElementChild!);
    };

    const primera = await medir();
    const segunda = await medir();

    expect(Math.abs(primera - segunda)).toBeLessThanOrEqual(1);
  });
});

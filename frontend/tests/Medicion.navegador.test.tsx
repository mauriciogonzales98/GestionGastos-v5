import { afterEach, describe, expect, it } from 'vitest';
import { page } from 'vitest/browser';
import { desmontar, montar } from './montar';

/**
 * NFR-003 — la prueba de que este proyecto **mide**.
 *
 * No verifica ningún requisito del producto: verifica la herramienta. Es la primera que se escribió
 * de la feature 014 y la razón es la deuda D11-01, abierta desde la 011: jsdom y happy-dom no
 * maquetan, así que `getBoundingClientRect()` devuelve ceros y un test que afirmara "esto mide
 * 200 px" pasaría igual sin medir nada. Eso es peor que no tener el test: entrena a confiar en un
 * verde vacío.
 *
 * Si esta prueba pasa, las demás del proyecto `navegador` están midiendo de verdad. Si Chromium no
 * arranca, falla acá y se ve el motivo una sola vez, en lugar de verlo en veinte archivos.
 */
describe('NFR-003 · el proyecto navegador mide de verdad', () => {
  afterEach(desmontar);

  it('devuelve el ancho real de un bloque, y no cero (NFR-003, D11-01)', async () => {
    const contenedor = await montar(<div style={{ width: '200px', height: '80px' }} />);

    const caja = contenedor.firstElementChild!.getBoundingClientRect();

    // El número exacto, porque este ancho lo fija un `style` en píxeles y no depende de la fuente
    // del sistema. Es la única prueba de la feature que puede permitirse un número exacto; las que
    // miden disposición afirman invariantes.
    expect(caja.width).toBe(200);
    expect(caja.height).toBe(80);
  });

  it('cambia el ancho de la ventana cuando se lo pide (NFR-003)', async () => {
    await page.viewport(360, 740);
    expect(window.innerWidth).toBe(360);

    await page.viewport(1440, 900);
    expect(window.innerWidth).toBe(1440);
  });

  it('resuelve la preferencia de esquema de color, que es lo que US9 necesita (FR-017)', () => {
    // `matchMedia` en un DOM simulado devuelve siempre `false`, así que el modo oscuro no se podía
    // verificar. Acá la consulta la resuelve el navegador.
    const claro = window.matchMedia('(prefers-color-scheme: light)').matches;
    const oscuro = window.matchMedia('(prefers-color-scheme: dark)').matches;

    // Uno de los dos, no los dos ni ninguno: lo que se comprueba es que la consulta se evalúe.
    expect(claro !== oscuro).toBe(true);
  });
});

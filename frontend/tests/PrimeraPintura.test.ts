// @vitest-environment node

import { describe, expect, it } from 'vitest';
import { archivosDeLaApp, eleccionDeTemaEnJavaScript } from './fuentes';

/**
 * FR-019, SC-008 — **el modo lo elige el navegador, y nadie más** (research D-03).
 *
 * `ModoOscuro.navegador.test.tsx` mide el resultado: con la preferencia en oscuro, la pantalla se
 * pinta oscura. Lo que ese test **no puede** medir es *cuándo*: una app que elige el tema con
 * JavaScript termina igual de oscura un cuadro después, y en ese cuadro de más se ve el destello
 * claro que `FR-019` prohíbe. Para medir la primera pintura habría que capturar el primer cuadro del
 * navegador, que no es algo que una prueba pueda afirmar de forma estable.
 *
 * Así que se verifica **la causa** en lugar del síntoma: que no haya una línea de JavaScript que
 * elija el tema. Si no hay ninguna, la única fuente es la consulta de medios del CSS, y entonces la
 * primera pintura ya es la correcta — eso no es una esperanza, es cómo funciona la cascada.
 *
 * **Recorre el árbol, nunca una lista escrita a mano** (`NFR-003`): un archivo nuevo queda cubierto
 * solo. Es la misma promesa que protegen `ClasesConRegla.test.ts` y `Paleta.test.ts`.
 */
describe('FR-019 · ningún archivo de la app elige el tema desde JavaScript', () => {
  it('no hay una sola elección de tema en código (FR-019, SC-008)', () => {
    const culpables = archivosDeLaApp().flatMap(({ ruta, contenido }) =>
      eleccionDeTemaEnJavaScript(contenido).map((que) => `${ruta}: ${que}`),
    );

    expect(culpables).toEqual([]);
  });

  /**
   * La premisa de la prueba de arriba: que efectivamente esté mirando archivos.
   *
   * Sin esto, el día que `archivosDeLaApp` dejara de encontrar nada —un cambio de carpeta, un
   * `include` mal escrito— la prueba seguiría en verde informando que no hay culpables, que es la
   * forma más tranquila de dejar de verificar algo.
   */
  it('mira los archivos de la app y no una carpeta vacía', () => {
    const archivos = archivosDeLaApp();

    expect(archivos.length).toBeGreaterThan(10);
    expect(archivos.map(({ ruta }) => ruta)).toContain('src/App.tsx');
  });
});

/**
 * **El verificador sabe fallar** (principio V).
 *
 * Cada forma de elegir el tema desde JavaScript se le pasa escrita, y tiene que denunciarla. Sin
 * estos casos, la prueba de arriba pasaría igual con un verificador roto: no hay forma de
 * distinguir "no encontró nada porque no hay nada" de "no encontró nada porque no busca".
 *
 * Son el mismo código que corre contra la app de verdad, no una copia (D-03).
 */
describe('FR-019 · el verificador denuncia cada forma de elegir el tema', () => {
  it.each([
    ['leer la preferencia', "const oscuro = window.matchMedia('(prefers-color-scheme: dark)');"],
    ['nombrar la consulta', "const CONSULTA = '(prefers-color-scheme: dark)';"],
    ['poner una clase', "document.body.classList.add('oscuro');"],
    ['marcar un atributo', "raiz.setAttribute('data-tema', 'oscuro');"],
    ['escribir la paleta', "raiz.style.setProperty('--color-fondo', '#16141b');"],
    ['escribir el esquema', "document.documentElement.style.colorScheme = 'dark';"],
  ])('denuncia %s', (_, codigo) => {
    expect(eleccionDeTemaEnJavaScript(codigo)).not.toEqual([]);
  });

  it('no denuncia código que no tiene nada que ver con el tema', () => {
    const inocente = `
      export function formatearMonto(monto: number): string {
        return monto.toFixed(2);
      }
    `;

    expect(eleccionDeTemaEnJavaScript(inocente)).toEqual([]);
  });
});

// @vitest-environment node

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

/**
 * NFR-002, SC-007 — cero dependencias de estilo, y las de prueba se quedan donde corresponde.
 *
 * **Esta es la barrera de la única restricción que la feature 014 se levantó a sí misma.** La 011
 * se había prohibido toda dependencia nueva (su `NFR-004`); la 014 levantó esa prohibición para dos
 * paquetes de prueba, con su justificación escrita en la spec. Lo que no se levantó es la
 * prohibición de dependencias **de estilo**, y la diferencia entre las dos cosas es `dependencies`
 * contra `devDependencies`.
 *
 * Sin esto, la distinción vive sólo en la intención de quien escribe el `pnpm add`: un `-D` olvidado
 * mete Playwright en el paquete que se le sirve a la persona y nadie se enteraría, porque la app
 * seguiría funcionando igual.
 */

const PAQUETE = JSON.parse(
  readFileSync(fileURLToPath(new URL('../package.json', import.meta.url)), 'utf8'),
) as { dependencies: Record<string, string>; devDependencies: Record<string, string> };

/**
 * Lo único que llega al navegador de la persona. Es una lista cerrada **a propósito**: una lista de
 * paquetes prohibidos habría que mantenerla cada vez que aparece un framework de CSS nuevo, y esa
 * es la clase de comprobación que se sostiene en que alguien se acuerde. Agregar una dependencia de
 * runtime tiene que costar venir hasta acá y justificarla.
 */
const DEPENDENCIAS_DE_LA_APP = ['react', 'react-dom'];

/** Las dos que la feature 014 agregó, que son de prueba y no pueden dejar de serlo. */
const SOLO_DE_DESARROLLO = ['playwright', '@vitest/browser-playwright'];

describe('NFR-002, SC-007 · ninguna dependencia de estilo llega a la app', () => {
  it('las dependencias de runtime son exactamente las dos de React (NFR-002, SC-007)', () => {
    // Ordenadas para que el mensaje de un fallo diga qué sobra y no en qué orden está escrito.
    expect(Object.keys(PAQUETE.dependencies).sort()).toEqual([...DEPENDENCIAS_DE_LA_APP].sort());
  });

  it.each(SOLO_DE_DESARROLLO)(
    '%s es una dependencia de desarrollo y no de la app (NFR-003, SC-007)',
    (paquete) => {
      expect(
        PAQUETE.devDependencies[paquete],
        `${paquete} no está en devDependencies`,
      ).toBeDefined();
      expect(PAQUETE.dependencies[paquete], `${paquete} se filtró a dependencies`).toBeUndefined();
    },
  );

  /**
   * Las dos van a versión **exacta**, sin `^` (research D-01).
   *
   * `@vitest/browser-playwright` pide `vitest: 5.0.2` exacto como peerDependency, así que tienen que
   * subir juntos; y cada `playwright` trae su propio Chromium, así que fijarla fija el navegador que
   * mide. Un `^` acá significa que la medición cambia de versión sola.
   */
  it.each(SOLO_DE_DESARROLLO)('%s está fijada a versión exacta (D-01)', (paquete) => {
    expect(PAQUETE.devDependencies[paquete]).toMatch(/^\d+\.\d+\.\d+$/);
  });
});

/**
 * D-03 · la barrera se ve fallar.
 *
 * Es la forma que el proyecto le da a toda comprobación desde la feature 010: sin un caso que tenga
 * que dar en rojo, no sabemos que la cuenta sabe rechazar.
 */
describe('D-03 · la barrera sabe fallar', () => {
  it('rechaza un paquete de estilo colado en las dependencias de la app', () => {
    const colado = { ...PAQUETE.dependencies, tailwindcss: '^4.0.0' };

    expect(Object.keys(colado).sort()).not.toEqual([...DEPENDENCIAS_DE_LA_APP].sort());
  });

  it('rechaza una versión con rango donde tiene que haber una exacta', () => {
    expect('^1.63.0').not.toMatch(/^\d+\.\d+\.\d+$/);
  });
});

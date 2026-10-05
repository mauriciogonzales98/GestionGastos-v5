// @vitest-environment node

import { describe, expect, it } from 'vitest';
import {
  clasesReferenciadas,
  clasesSinRegla,
  codigoDeLasPantallas,
  hojaDeEstilos,
  reglasSinUso,
} from './fuentes';

/**
 * FR-001, NFR-003, PRD-06:AC-04 — ninguna clase referenciada sin una regla que la respalde.
 *
 * Corre en entorno `node` y no en happy-dom porque lee del disco: lo que verifica no es una
 * pantalla montada sino la relación entre dos archivos.
 */
describe('FR-001 · las clases que el código nombra tienen una regla', () => {
  it('no deja ninguna clase referenciada sin regla en la hoja de estilos (FR-001, PRD-06:AC-04)', () => {
    const huerfanas = clasesSinRegla(codigoDeLasPantallas(), hojaDeEstilos());

    // Se afirma sobre la LISTA y no sobre la cantidad. Un fallo que dice "esperaba 0, recibí 5"
    // obliga a averiguar a mano lo que el test ya sabe.
    expect(huerfanas).toEqual([]);
  });

  it('deriva las clases del código y no de una lista escrita a mano (NFR-003)', () => {
    // No se afirma CUÁNTAS son: el número cambia con cada pantalla nueva y un test que lo fije
    // convierte a la próxima feature en una que tiene que venir a tocar este archivo. Lo que sí se
    // exige es que el recorrido encuentre algo — si devolviera vacío, la comprobación de arriba
    // pasaría por no haber mirado nada, que es el fallo que este proyecto ya se comió una vez.
    expect(clasesReferenciadas(codigoDeLasPantallas()).length).toBeGreaterThan(0);
  });
});

/**
 * D-03 · el verificador se ve fallar.
 *
 * El Principio V de la constitución: una barrera que nunca se vio fallar no es una barrera. Acá el
 * modo de fallo concreto es que la expresión regular deje de reconocer un `className` — entonces
 * `clasesSinRegla` devolvería `[]` por no haber encontrado ninguna clase, y el test de arriba
 * pasaría en verde sin haber verificado nada.
 */
/**
 * FR-009 — la contracara del atributo: lo que el componente **anuncia** también tiene regla.
 *
 * Vive en este archivo y no con las pruebas del conmutador porque es la misma idea que el resto del
 * archivo —lo que el código nombra tiene que tener una regla que lo respalde— sólo que el nombre es
 * un atributo en lugar de una clase. Y porque lee del disco, así que necesita entorno `node`: una
 * prueba de componente no puede hacerlo.
 *
 * **Por qué importa**: `FR-009` exige que el modo elegido del conmutador se vea y se anuncie desde
 * el **mismo** dato, para que no puedan desincronizarse. El lado "se anuncia" lo verifica
 * `ConmutadorDeAcceso.test.tsx`; el lado "se ve" es esta regla. Sin ella, `aria-pressed` quedaría
 * puesto y la opción elegida no se distinguiría de la otra.
 */
describe('FR-009 · el estado elegido tiene regla propia en la hoja', () => {
  it('declara el aspecto de [aria-pressed=true] (FR-009)', () => {
    expect(hojaDeEstilos()).toMatch(/\[aria-pressed=['"]?true['"]?\]/);
  });
});

describe('D-03 · el verificador de clases sabe fallar', () => {
  it('detecta una clase referenciada que la hoja no declara', () => {
    const codigo = `<div className="l-pila c-inventada">`;
    const css = `.l-pila { display: flex; }`;

    expect(clasesSinRegla(codigo, css)).toEqual(['c-inventada']);
  });

  it('no confunde una variable CSS con una clase', () => {
    // `--c-riel` existía en el proyecto antes de esta feature y hacía que un buscador ingenuo del
    // prefijo `c-` informara una clase inexistente.
    const codigo = `<div style={{ '--c-riel': '#eee' }} className="c-desglose__riel">`;
    const css = `.c-desglose__riel { width: 100%; }`;

    expect(clasesSinRegla(codigo, css)).toEqual([]);
  });

  it('ve los selectores anidados dentro de un at-rule', () => {
    // La cabecera apaga su empuje a la derecha cuando la fila se envolvió, así que su regla vive
    // dentro de un `@media`. Un parser que sólo mirara lo anterior a la primera llave de cada
    // bloque la daba por no declarada — y este caso es el que lo dejó dicho.
    const codigo = `<div className="l-cabecera">`;
    const css = '@media (width >= 40rem) { .l-cabecera > :nth-child(2) { margin-left: auto; } }';

    expect(clasesSinRegla(codigo, css)).toEqual([]);
  });

  it('no toma por clase un valor decimal de una declaración', () => {
    // `.75rem` dentro de un bloque no es un selector. Sin la separación por llaves, `clasesDeclaradas`
    // lo daba por declarado y tapaba clases que sí faltaban.
    const codigo = `<div className="c-real">`;
    const css = `.otra { gap: 0.75rem; }`;

    expect(clasesSinRegla(codigo, css)).toEqual(['c-real']);
  });
});

/**
 * FR-042 — **el rótulo del botón de sólo ícono tiene regla para el puntero y para el foco**.
 *
 * Misma idea que el bloque de arriba y mismo motivo para vivir acá: lo que el código nombra tiene
 * que tener una regla que lo respalde, y comprobarlo exige leer el disco.
 *
 * `BotonIcono` pone el rótulo en el DOM y lo deja oculto; **mostrarlo es responsabilidad de la
 * hoja**. Si la regla de `:hover` faltara, el componente seguiría siendo correcto y el rótulo no
 * aparecería nunca: un botón sin palabra y sin forma de averiguarla, que es justo lo que `FR-042`
 * vino a impedir.
 *
 * Las dos hacen falta por separado: `:hover` sirve a quien usa un puntero y `:focus-visible` a quien
 * llega con el teclado, que es la mitad que el `title` del navegador no cubre y el motivo por el que
 * research D-06 lo descartó. Que el rótulo efectivamente se vea lo mide
 * `FilasConIconos.navegador.test.tsx`; esto dice qué falta y dónde.
 */
describe('FR-042 · el rótulo de la acción tiene regla para el puntero y para el foco', () => {
  it.each([':hover', ':focus-visible'])('muestra el rótulo en %s (FR-042, US8:AC3)', (estado) => {
    const queMuestran = [...hojaDeEstilos().matchAll(/([^{}]+)\{/g)]
      .map(([, selector]) => selector.trim())
      .filter(
        (selector) =>
          selector.includes('c-boton-icono__rotulo') && selector.includes(`c-boton-icono${estado}`),
      );

    expect(
      queMuestran,
      `ninguna regla muestra el rótulo cuando el botón está en ${estado}`,
    ).not.toEqual([]);
  });
});

/**
 * FR-001 — **la dirección que faltaba: ninguna regla sin nadie que la nombre.**
 *
 * El bloque de arriba mira que toda clase usada tenga regla. Esto mira lo contrario, y es la mitad
 * que estuvo ciega hasta la feature 014: `l-cabecera` sobrevivió a la mudanza de la cuenta y el
 * cierre de sesión al marco de la app, con su regla escrita y dos `className` nombrándola, alineando
 * un segundo hijo que ya no existía en ninguna de las dos cabeceras. Las dos comprobaciones de
 * arriba la daban por buena: estaba usada **y** tenía regla. Lo único que no hacía era algo.
 *
 * Esta comprobación no habría atrapado ese caso —la clase estaba nombrada— y sí atrapa el siguiente:
 * la regla que queda cuando el `className` se va. Se adopta ahora porque hoy la lista está vacía, y
 * una barrera que nace en rojo no es una barrera, es una deuda con nombre nuevo.
 */
describe('FR-001 · la hoja no declara reglas que nadie use', () => {
  it('ninguna clase l-, c- o u- declarada queda sin un className que la nombre (FR-001)', () => {
    expect(reglasSinUso(codigoDeLasPantallas(), hojaDeEstilos())).toEqual([]);
  });

  it('el verificador sabe fallar (principio V)', () => {
    // Una clase declarada que ningún código nombra: es exactamente el CSS muerto que busca.
    expect(
      reglasSinUso('<div className="l-pila">', '.l-pila {} .c-fantasma { color: red; }'),
    ).toEqual(['c-fantasma']);

    // Y no denuncia lo que se estila por selector de elemento, que nadie nombra en un className.
    expect(reglasSinUso('<div className="l-pila">', '.l-pila {} button { border: none; }')).toEqual(
      [],
    );
  });
});

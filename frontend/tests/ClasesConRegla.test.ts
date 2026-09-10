// @vitest-environment node

import { describe, expect, it } from 'vitest';
import {
  clasesReferenciadas,
  clasesSinRegla,
  codigoDeLasPantallas,
  hojaDeEstilos,
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

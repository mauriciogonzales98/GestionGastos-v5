import { page } from 'vitest/browser';

/**
 * Los anchos en los que esta app tiene que verse bien, y el ayudante que los recorre.
 *
 * **Es una constante compartida y no una lista repetida en cada archivo** (feature 014). Es la misma
 * promesa que `tests/fuentes.ts` protege del lado del CSS: una comprobación que se sostiene en que
 * alguien se acuerde de agregar una fila a una lista no es una comprobación, es un recordatorio. El
 * día que el producto agregue un ancho objetivo, se agrega acá y lo heredan todas las pruebas que
 * miden.
 *
 * Por qué estos seis:
 *
 * - **360** y **1440** son los extremos que fija la spec (`FR-011`), y 360 es el mínimo objetivo
 *   que viene de la feature 011.
 * - **480** y **1024** son el medio. Una disposición pensada para los extremos se rompe acá, y es
 *   donde nadie mira.
 * - **767** y **768** son los dos lados del corte de 48rem, el único ancho donde la navegación
 *   cambia de forma (`FR-023`, research D-05). Un corte probado de un solo lado deja pasar que el
 *   valor del `@media` se escriba mal.
 */
export const ANCHOS = [360, 480, 767, 768, 1024, 1440] as const;

/** El alto de la ventana en las mediciones. Es fijo: lo que se mide es el ancho. */
const ALTO = 900;

/** El corte de disposición, en píxeles. Por debajo la barra va abajo; desde acá, al costado. */
export const CORTE_PX = 768;

/**
 * Corre `cuerpo` una vez por cada ancho de `ANCHOS`, con la ventana ya fijada.
 *
 * Se usa **dentro** de un `it`, y no como generador de `it`s, a propósito: un fallo tiene que decir
 * en qué ancho pasó, y para eso el mensaje de la aserción lleva el ancho. Partirlo en seis pruebas
 * costaría seis montajes del componente por cada cosa que se mide.
 */
export async function paraCadaAncho(
  cuerpo: (ancho: number) => Promise<void> | void,
  anchos: readonly number[] = ANCHOS,
): Promise<void> {
  for (const ancho of anchos) {
    await page.viewport(ancho, ALTO);
    await cuerpo(ancho);
  }
}

/**
 * Lo que mide "no hay desplazamiento horizontal de la **página**".
 *
 * Se compara `scrollWidth` del elemento raíz contra el ancho de la ventana y **se tolera 1 px**: un
 * ancho impar dividido en dos columnas deja un redondeo de medio píxel que el navegador reporta como
 * uno, y eso no es desborde, es aritmética. Dos píxeles sí lo son.
 */
export function desbordaLaPagina(anchoDeLaVentana: number): boolean {
  return document.documentElement.scrollWidth > anchoDeLaVentana + 1;
}

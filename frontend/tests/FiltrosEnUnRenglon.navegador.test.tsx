import { afterEach, describe, expect, it } from 'vitest';
import { page } from 'vitest/browser';
import { FiltrosDelListado } from '../src/movimientos/FiltrosDelListado';
import { MarcoDeLaApp } from '../src/ui/MarcoDeLaApp';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import { desmontar, montar } from './montar';
import '../src/estilos/index.css';

/**
 * **La barra de acotado del listado, en un solo renglón.**
 *
 * Eran dos: los dos selectores arriba y el rango con su botón abajo. La barra ocupaba el doble de
 * alto para decir una sola cosa, y en la pantalla de movimientos ese alto sale del listado, que es
 * lo que la persona vino a mirar.
 *
 * Se mide en el navegador y no en happy-dom porque esto **es** maquetación: happy-dom devuelve cero
 * en toda caja y una barra partida en dos renglones pasaría en verde.
 *
 * Dentro del marco de la app y de un `<main>`, que es el ancho que la barra tiene de verdad — es la
 * lección de research D-13, repetida en `FormularioCompacto.navegador.test.tsx`.
 */
const ALTO = 900;

async function abrirFiltros(ancho: number): Promise<HTMLElement> {
  await page.viewport(ancho, ALTO);

  return montar(
    <MarcoDeLaApp
      seccion="movimientos"
      email="ana@ejemplo.com"
      onIrA={() => {}}
      onCerrarSesion={() => {}}
    >
      <main className="l-pila">
        <FiltrosDelListado
          categorias={CATEGORIAS}
          monedas={MONEDAS}
          desdeInicial="2026-10-01"
          hastaInicial="2026-10-31"
          onAplicar={() => {}}
          errorDelPeriodo={null}
        />
      </main>
    </MarcoDeLaApp>,
  );
}

/**
 * Los cinco controles de la barra, en el orden en que se leen.
 *
 * Se miden los **controles** y no sus contenedores: lo que tiene que estar a la misma altura es lo
 * que se toca.
 */
function controles(raiz: HTMLElement): HTMLElement[] {
  const barra = raiz.querySelector<HTMLElement>('.l-filtros')!;

  return [...barra.querySelectorAll<HTMLElement>('select, input[type="date"], button')];
}

/**
 * ¿Comparten renglón? Sus franjas verticales se solapan.
 *
 * Solape y no `top` igual, porque los controles no miden todos lo mismo: los selectores llevan su
 * etiqueta encima y los campos de fecha la llevan al lado, así que sus bordes superiores no
 * coinciden ni tienen por qué.
 */
function seSolapanVerticalmente(uno: HTMLElement, otro: HTMLElement): boolean {
  const a = uno.getBoundingClientRect();
  const b = otro.getBoundingClientRect();

  return a.top < b.bottom && b.top < a.bottom;
}

describe('la barra de acotado del listado', () => {
  afterEach(desmontar);

  it('pone los cuatro acotados y el botón en un solo renglón en escritorio', async () => {
    const raiz = await abrirFiltros(1440);
    const [categoria, moneda, desde, hasta, aplicar] = controles(raiz);

    // Los cinco están: si alguno faltara, la prueba diría que comparten renglón sin haber medido.
    expect(controles(raiz)).toHaveLength(5);

    // Cada uno con el siguiente, y de corrido: el solape es transitivo sólo si se verifica en
    // cadena, y comparar todos contra el primero dejaría pasar un último control desplazado.
    for (const [uno, otro] of [
      [categoria, moneda],
      [moneda, desde],
      [desde, hasta],
      [hasta, aplicar],
    ]) {
      expect(seSolapanVerticalmente(uno, otro)).toBe(true);
    }

    // Y de izquierda a derecha, en el orden en que se leen.
    for (const [uno, otro] of [
      [categoria, moneda],
      [moneda, desde],
      [desde, hasta],
      [hasta, aplicar],
    ]) {
      expect(uno.getBoundingClientRect().right).toBeLessThanOrEqual(
        otro.getBoundingClientRect().left,
      );
    }
  });

  it('se envuelve en un teléfono en lugar de desbordar la página (FR-004)', async () => {
    const raiz = await abrirFiltros(360);
    const barra = raiz.querySelector<HTMLElement>('.l-filtros')!;

    // El renglón único es para la pantalla que lo permite. A 360 px la barra se parte, y lo que no
    // puede pasar es que se corra de costado: es la razón por la que `l-fila` se envuelve.
    expect(barra.getBoundingClientRect().right).toBeLessThanOrEqual(360);
    expect(document.documentElement.scrollWidth).toBeLessThanOrEqual(360);
  });
});

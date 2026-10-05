import { afterEach, describe, expect, it } from 'vitest';
import { page } from 'vitest/browser';
import { MarcoDeLaApp } from '../src/ui/MarcoDeLaApp';
import { desmontar, montar } from './montar';
import { ANCHOS, CORTE_PX, desbordaLaPagina } from './anchos';
import '../src/estilos/index.css';

/**
 * US5, FR-023, FR-025, FR-026 — **dónde está la barra**, medido.
 *
 * Es lo que ninguna prueba podía afirmar hasta esta feature: que la barra esté al costado en
 * escritorio y abajo en el teléfono es una afirmación sobre posiciones, y un DOM simulado devuelve
 * ceros. Con reglas escritas lo más que se podía comprobar era que el `@media` existiera, no que
 * hiciera lo que dice.
 */

const ALTO = 740;

async function abrirMarco(ancho: number, contenido = <p>Contenido</p>): Promise<HTMLElement> {
  await page.viewport(ancho, ALTO);

  return montar(
    <MarcoDeLaApp
      seccion="movimientos"
      email="mauri@ejemplo.com"
      onIrA={() => {}}
      onCerrarSesion={() => {}}
    >
      {contenido}
    </MarcoDeLaApp>,
  );
}

const barra = (raiz: HTMLElement) => raiz.querySelector<HTMLElement>('nav')!;
const contenido = (raiz: HTMLElement) => raiz.querySelector<HTMLElement>('main')!;

describe('FR-023 · la barra al costado en escritorio y abajo en el teléfono', () => {
  afterEach(desmontar);

  it.each([1024, 1440])(
    'a %i px está al costado del contenido (FR-023, US5:AC1)',
    async (ancho) => {
      const raiz = await abrirMarco(ancho);

      const nav = barra(raiz).getBoundingClientRect();
      const main = contenido(raiz).getBoundingClientRect();

      // Al costado = su borde derecho queda a la izquierda del contenido. No se compara contra un
      // número de píxeles: se compara una posición con la otra.
      expect(nav.right).toBeLessThanOrEqual(main.left);
    },
  );

  it.each([360, 480])('a %i px está abajo del contenido (FR-023, US5:AC2)', async (ancho) => {
    const raiz = await abrirMarco(ancho);

    const nav = barra(raiz).getBoundingClientRect();
    const main = contenido(raiz).getBoundingClientRect();

    expect(nav.top).toBeGreaterThanOrEqual(main.top);
    // Y pegada al borde inferior de la ventana, que es lo que la pone al alcance del pulgar.
    expect(nav.bottom).toBeCloseTo(window.innerHeight, 0);
  });

  /**
   * **El corte probado de los dos lados.**
   *
   * Es el único ancho donde la navegación cambia de forma, y un corte probado de un solo lado deja
   * pasar que el valor del `@media` se escriba mal: con `48rem` escrito como `480px`, la prueba a
   * 360 px y la de 1440 px pasarían las dos y la barra estaría abajo hasta los 480 px.
   */
  it('a 767 px todavía está abajo y a 768 px ya está al costado (FR-023, D-05)', async () => {
    const antes = await abrirMarco(CORTE_PX - 1);
    expect(barra(antes).getBoundingClientRect().bottom).toBeCloseTo(window.innerHeight, 0);

    const desde = await abrirMarco(CORTE_PX);
    const nav = barra(desde).getBoundingClientRect();
    expect(nav.right).toBeLessThanOrEqual(contenido(desde).getBoundingClientRect().left);
  });
});

/**
 * `FR-023` — **la barra lateral es un panel continuo y se queda a la vista.**
 *
 * Los tres casos de acá salieron de mirar una captura del marco a 1440 px, y los tres son cosas
 * que una prueba de estructura no puede ver: el DOM estaba bien, el `<nav>` era uno, las secciones
 * estaban marcadas. Lo que estaba mal era cómo se veía.
 *
 *   - La superficie del panel cubría sólo al `<nav>`, así que el bloque de la cuenta quedaba suelto
 *     sobre el fondo de la página y la barra se leía **cortada** a media altura.
 *   - Nada separaba la barra del contenido, así que el borde del panel parecía un corte y no un
 *     límite.
 *   - Y `FR-023` pide que la barra **permanezca a la vista mientras se desplaza el contenido**; no
 *     lo hacía, y ninguna prueba lo miraba.
 */
describe('FR-023 · la barra lateral es un panel continuo que se queda a la vista', () => {
  afterEach(desmontar);

  const lateral = (raiz: HTMLElement) => raiz.querySelector<HTMLElement>('.c-lateral')!;

  it.each([768, 1024, 1440])(
    'el panel llega de arriba abajo de la ventana a %i px (FR-023)',
    async (ancho) => {
      const raiz = await abrirMarco(ancho);
      const caja = lateral(raiz).getBoundingClientRect();

      // Sin huecos arriba ni abajo: lo que se veía cortado era el panel terminando a media altura.
      expect(caja.top).toBeCloseTo(0, 0);
      expect(caja.height).toBeCloseTo(window.innerHeight, 0);
    },
  );

  it.each([768, 1024, 1440])('se distingue del contenido con un borde a %i px', async (ancho) => {
    const raiz = await abrirMarco(ancho);
    const estilo = getComputedStyle(lateral(raiz));

    // Un borde de verdad, no `none` ni 0px: es lo que convierte el final del panel en un límite en
    // lugar de en un corte.
    expect(parseFloat(estilo.borderRightWidth)).toBeGreaterThan(0);
    expect(estilo.borderRightStyle).not.toBe('none');
  });

  it('se queda a la vista cuando el contenido se desplaza (FR-023, US5:AC1)', async () => {
    const largo = (
      <div>
        {Array.from({ length: 60 }, (_, i) => (
          <p key={i}>Un movimiento más, el número {i}</p>
        ))}
      </div>
    );

    const raiz = await abrirMarco(1440, largo);

    // La premisa: hay de dónde desplazarse. Sin esto la prueba pasaría sin verificar nada.
    expect(document.documentElement.scrollHeight).toBeGreaterThan(window.innerHeight);

    window.scrollTo(0, document.documentElement.scrollHeight);

    const caja = lateral(raiz).getBoundingClientRect();

    // Abajo del todo, el panel sigue ocupando la ventana entera: no se fue para arriba.
    expect(caja.top).toBeCloseTo(0, 0);
    expect(caja.bottom).toBeCloseTo(window.innerHeight, 0);
  });
});

describe('FR-025 · cada sección es tocable en todos los anchos', () => {
  afterEach(desmontar);

  it.each(ANCHOS)('las secciones miden al menos 44 x 44 px a %i px (FR-025)', async (ancho) => {
    const raiz = await abrirMarco(ancho);

    const chicos = [...barra(raiz).querySelectorAll<HTMLElement>('button')]
      .map((boton) => ({ boton, caja: boton.getBoundingClientRect() }))
      .filter(({ caja }) => caja.width < 44 || caja.height < 44)
      .map(({ boton, caja }) => `${boton.textContent}: ${caja.width}x${caja.height}`);

    expect(chicos).toEqual([]);
  });

  it.each(ANCHOS)('no produce desplazamiento horizontal a %i px (FR-015)', async (ancho) => {
    await abrirMarco(ancho);

    expect(desbordaLaPagina(ancho)).toBe(false);
  });
});

/**
 * Dos cosas que se vieron en una captura a 360 px y que ninguna prueba miraba.
 *
 * La primera: el rótulo de la sección actual salía cortado —"Movimien…"—. Medido: necesitaba
 * 101 px y tenía 96, porque los botones de la barra heredaban el relleno horizontal genérico de
 * 12 px por lado. Un rótulo cortado en la **sección actual** es el peor lugar para que pase: es
 * justo el que dice dónde está parada la persona.
 *
 * La segunda: el contenido empezaba a 447 px de una ventana de 740, o sea al 60 % de la pantalla.
 * Las tres filas de la grilla se repartían el alto en partes iguales en lugar de que creciera sólo
 * la del contenido.
 */
describe('La barra en el teléfono no corta rótulos ni empuja el contenido', () => {
  afterEach(desmontar);

  it.each([360, 480, 767])('ningún rótulo de sección se corta a %i px (FR-022)', async (ancho) => {
    const raiz = await abrirMarco(ancho);

    const cortados = [...barra(raiz).querySelectorAll<HTMLElement>('span')]
      // Se tolera 1px de redondeo del ancho del texto.
      .filter((span) => span.scrollWidth > span.clientWidth + 1)
      .map(
        (span) => `${span.textContent}: necesita ${span.scrollWidth}, tiene ${span.clientWidth}`,
      );

    expect(cortados).toEqual([]);
  });

  it.each([360, 480, 767])(
    'el contenido empieza arriba, no a media pantalla, a %i px',
    async (ancho) => {
      const raiz = await abrirMarco(ancho);
      const main = contenido(raiz).getBoundingClientRect();

      // Lo que hay encima del contenido —la marca y la cuenta— ocupa lo suyo y nada más. Con las
      // filas repartiéndose el alto en partes iguales, esto daba el 60 % de la ventana.
      expect(main.top).toBeLessThan(window.innerHeight * 0.25);
    },
  );
});

describe('FR-026 · la barra inferior no tapa contenido', () => {
  afterEach(desmontar);

  it('el final del contenido queda por encima de la barra a 360 px (FR-026, US5:AC6)', async () => {
    // Contenido largo a propósito: el caso que importa es el que obliga a desplazarse hasta el
    // final, porque es ahí donde una barra fija tapa lo último.
    const largo = (
      <div>
        {Array.from({ length: 40 }, (_, i) => (
          <p key={i}>Un movimiento más, el número {i}</p>
        ))}
        <button type="button" id="el-ultimo">
          El último control
        </button>
      </div>
    );

    const raiz = await abrirMarco(360, largo);

    window.scrollTo(0, document.documentElement.scrollHeight);

    const ultimo = raiz.querySelector<HTMLElement>('#el-ultimo')!.getBoundingClientRect();
    const nav = barra(raiz).getBoundingClientRect();

    // Desplazado hasta el fondo, el último control tiene que quedar ENTERO por encima de la barra.
    // Sin el relleno inferior del `<main>`, queda debajo y no hay forma de alcanzarlo.
    expect(ultimo.bottom).toBeLessThanOrEqual(nav.top);
  });
});

describe('Casos borde · un email largo no deforma la barra', () => {
  afterEach(desmontar);

  it.each([360, 1440])('se corta con puntos suspensivos a %i px', async (ancho) => {
    await page.viewport(ancho, ALTO);

    const raiz = await montar(
      <MarcoDeLaApp
        seccion="movimientos"
        email="una-direccion-de-correo-electronico-absurdamente-larga@un-dominio-igual-de-largo.com.ar"
        onIrA={() => {}}
        onCerrarSesion={() => {}}
      >
        <p>Contenido</p>
      </MarcoDeLaApp>,
    );

    const cuenta = raiz.querySelector<HTMLElement>('.c-cuenta__email')!;

    // Se corta: el ancho que ocupa es menor que el que necesitaría el texto completo.
    expect(cuenta.scrollWidth).toBeGreaterThan(cuenta.clientWidth);
    // Y no empuja nada: la página no desborda.
    expect(desbordaLaPagina(ancho)).toBe(false);
  });
});

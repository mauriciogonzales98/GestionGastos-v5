import { afterEach, describe, expect, it } from 'vitest';
import { page } from 'vitest/browser';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';
import { desmontar, montar } from './montar';
import { ANCHOS, desbordaLaPagina } from './anchos';
import '../src/estilos/index.css';

/**
 * US1 y US2 — la pantalla de acceso, **medida**.
 *
 * Es la pantalla que originó la feature: *"se ve totalmente crudo, sin colores ni nada"*. Lo que se
 * verifica acá es lo que sólo un navegador puede decir —que la tarjeta esté centrada, que su ancho
 * no cambie, que nada desborde, que todo lo tocable mida 44px— y que hasta esta feature se deducía
 * de las reglas escritas (deuda D11-01).
 *
 * **Ninguna aserción usa un número de píxeles que dependa del texto.** La fuente del sistema no es
 * la misma en WSL que en el runner del CI, así que un ancho de texto difiere en unos píxeles y una
 * prueba que lo fijara parpadearía. Lo que se afirma son invariantes: "no desborda", "≥ 44px", "mide
 * lo mismo antes y después", "está centrada".
 */

const ALTO = 900;

/** Todo lo que una persona puede tocar. Se recorre el DOM, no una lista escrita a mano: un control
 *  nuevo en la pantalla queda cubierto solo (`NFR-003`). */
function controlesTocables(raiz: HTMLElement): HTMLElement[] {
  return [...raiz.querySelectorAll<HTMLElement>('button, input, select, textarea, a[href]')];
}

/** La tarjeta: el único bloque visual que contiene marca y formulario (`FR-008`). */
function tarjeta(raiz: HTMLElement): HTMLElement {
  const encontrada = raiz.querySelector<HTMLElement>('.c-tarjeta');

  if (!encontrada) {
    throw new Error('la pantalla de acceso no tiene tarjeta (.c-tarjeta)');
  }

  return encontrada;
}

async function abrirAcceso(ancho: number): Promise<HTMLElement> {
  await page.viewport(ancho, ALTO);

  return montar(<FormularioAcceso onEntrar={() => {}} />);
}

describe('US1 · la pantalla de acceso se ve terminada', () => {
  afterEach(desmontar);

  it('pone marca y formulario dentro de un único bloque visual (FR-008, US1:AC1)', async () => {
    const raiz = await abrirAcceso(1440);
    const bloque = tarjeta(raiz);

    // Uno solo: dos tarjetas serían dos bloques, que es justo lo que `FR-008` prohíbe.
    expect(raiz.querySelectorAll('.c-tarjeta')).toHaveLength(1);

    // Y la marca y el formulario están los dos adentro.
    expect(bloque.querySelector('h1')).not.toBeNull();
    expect(bloque.querySelector('form')).not.toBeNull();
  });

  it('centra la tarjeta horizontalmente (FR-008, US1:AC1)', async () => {
    const raiz = await abrirAcceso(1440);
    const caja = tarjeta(raiz).getBoundingClientRect();

    const aLaIzquierda = caja.left;
    const aLaDerecha = window.innerWidth - caja.right;

    // Centrada = los dos márgenes miden lo mismo. Se tolera 1px por el redondeo de un ancho impar.
    expect(Math.abs(aLaIzquierda - aLaDerecha)).toBeLessThanOrEqual(1);
  });

  it('acota el ancho de lectura en escritorio y no se estira (FR-008, US2:AC4)', async () => {
    const raiz = await abrirAcceso(1440);
    const caja = tarjeta(raiz).getBoundingClientRect();

    // La invariante, no un número: la tarjeta ocupa bastante menos que la ventana. Si se estirara a
    // todo el ancho, una línea de texto cruzaría 1440px y sería ilegible.
    expect(caja.width).toBeLessThan(window.innerWidth * 0.6);
  });

  it('deja margen lateral a 360 px y usa el ancho disponible (FR-008, US2:AC1)', async () => {
    const raiz = await abrirAcceso(360);
    const caja = tarjeta(raiz).getBoundingClientRect();

    // Hay margen: la tarjeta no toca los bordes.
    expect(caja.left).toBeGreaterThan(0);
    // Y lo aprovecha: no se queda angosta en el medio de la pantalla.
    expect(caja.width).toBeGreaterThan(window.innerWidth * 0.8);
  });

  /**
   * `FR-012` — el bloque no cambia de ancho al alternar de modo ni al enviarse.
   *
   * **Es la prueba de que la pantalla no salta.** El ancho de la tarjeta no puede depender de su
   * contenido: "Crear mi cuenta" y "Entrar" tienen largos distintos, y "Creando…" aparece mientras
   * la persona espera. Si el ancho saliera del texto, el bloque se movería justo cuando alguien está
   * mirando si salió bien.
   */
  it('no cambia de ancho al alternar entre los dos modos (FR-012, US1:AC4)', async () => {
    const raiz = await abrirAcceso(1440);

    const anchoAlEntrar = tarjeta(raiz).getBoundingClientRect().width;

    const crearCuenta = [...raiz.querySelectorAll('button')].find(
      (boton) => boton.textContent === 'Crear cuenta',
    )!;
    crearCuenta.click();

    const anchoAlCrear = tarjeta(raiz).getBoundingClientRect().width;

    expect(anchoAlCrear).toBe(anchoAlEntrar);
  });

  it('el botón de envío no cambia de ancho al ponerse en espera (FR-012)', async () => {
    const raiz = await abrirAcceso(1440);
    const enviar = raiz.querySelector<HTMLElement>('button[type="submit"]')!;

    const antes = enviar.getBoundingClientRect().width;

    // El botón ocupa el ancho de la tarjeta, así que su ancho no sale del texto: cambiar el texto
    // por el de espera no lo mueve. Se comprueba sobre el texto más largo de los cuatro posibles.
    enviar.textContent = 'Crear mi cuenta';
    const conTextoLargo = enviar.getBoundingClientRect().width;

    expect(conTextoLargo).toBe(antes);
  });
});

describe('US2 · la pantalla de acceso desde el celular', () => {
  afterEach(desmontar);

  it.each(ANCHOS)(
    'no produce desplazamiento horizontal a %i px (FR-011, US2:AC1)',
    async (ancho) => {
      await abrirAcceso(ancho);

      expect(desbordaLaPagina(ancho)).toBe(false);
    },
  );

  it('todo control tocable mide al menos 44 x 44 px (FR-010, SC-003, US2:AC3)', async () => {
    const raiz = await abrirAcceso(360);

    const chicos = controlesTocables(raiz)
      .map((control) => ({ control, caja: control.getBoundingClientRect() }))
      .filter(({ caja }) => caja.width < 44 || caja.height < 44)
      .map(({ control, caja }) => `${control.tagName.toLowerCase()}: ${caja.width}x${caja.height}`);

    expect(chicos).toEqual([]);
  });

  it('ningún campo baja de 16 px de letra, para que el navegador no amplíe (FR-007, US2:AC2)', async () => {
    const raiz = await abrirAcceso(360);

    const chicos = [...raiz.querySelectorAll<HTMLElement>('input, select, textarea')]
      .map((campo) => ({ campo, px: parseFloat(getComputedStyle(campo).fontSize) }))
      .filter(({ px }) => px < 16)
      .map(({ campo, px }) => `${campo.getAttribute('name') ?? campo.tagName}: ${px}px`);

    expect(chicos).toEqual([]);
  });
});

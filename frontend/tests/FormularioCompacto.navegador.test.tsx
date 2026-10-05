import { afterEach, describe, expect, it } from 'vitest';
import { page } from 'vitest/browser';
import { FormularioMovimiento } from '../src/movimientos/FormularioMovimiento';
import { MarcoDeLaApp } from '../src/ui/MarcoDeLaApp';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import type { Moneda } from '../src/api/tipos';
import { desmontar, montar } from './montar';
import { ANCHOS, desbordaLaPagina } from './anchos';
import '../src/estilos/index.css';

/**
 * US6, FR-029 a FR-032 — **el formulario compacto, medido.**
 *
 * Es lo que más se usa de la app: se carga un movimiento por cada gasto, todos los días. Hasta esta
 * feature era una columna de seis campos apilados que a 360 px medía **527 px de alto** —medido en
 * `LineaBase.navegador.test.tsx` antes de tocar nada— y no entraba en una pantalla de teléfono.
 *
 * La agrupación que pide `FR-029`: el tipo en un renglón propio, monto y moneda juntos, categoría y
 * fecha juntas, la nota a lo ancho.
 */

const ALTO = 900;

/**
 * El formulario **dentro del marco de la app**, que es el ancho que tiene de verdad.
 *
 * Montado suelto recibe la ventana entera; dentro de la app recibe bastante menos, porque antes
 * están el relleno del contenido y, desde que el formulario es un recuadro, el suyo propio. Esa
 * diferencia no es cosmética: con el formulario suelto, el control del tipo resolvía su ancho de una
 * forma y dentro de la app de otra, y una mutación que apilaba sus dos opciones **pasaba en verde**.
 *
 * Es la segunda vez en esta feature que una medición en un contenedor que no es el real da por bueno
 * algo que en la pantalla está roto. La primera fueron las tres cifras del resumen (research D-13).
 */
async function abrirFormulario(ancho: number, monedas: Moneda[] = MONEDAS): Promise<HTMLElement> {
  await page.viewport(ancho, ALTO);

  return montar(
    <MarcoDeLaApp
      seccion="movimientos"
      email="ana@ejemplo.com"
      onIrA={() => {}}
      onCerrarSesion={() => {}}
    >
      <main className="l-pila">
        <FormularioMovimiento
          categorias={CATEGORIAS}
          monedas={monedas}
          hoy="2026-10-01"
          onGuardar={() => {}}
        />
      </main>
    </MarcoDeLaApp>,
  );
}

/**
 * El catálogo como está en la base de desarrollo, con el nombre largo de verdad.
 *
 * El fixture trae "Dólar" a secas; la base dice "Dólar estadounidense". La diferencia importa
 * justo acá, que es donde se mide si el nombre entra, así que esta prueba usa el nombre real en
 * lugar del corto — con el corto pasaría sin verificar el caso que importa.
 */
const CON_NOMBRES_LARGOS: Moneda[] = [
  MONEDAS[0],
  { ...MONEDAS[1], nombre: 'Dólar estadounidense' },
];

/** El contenedor del campo cuya etiqueta dice `etiqueta`. Es lo que se mide, no el `<input>`: lo
 *  que comparte renglón es el campo con su rótulo y su error, no el control pelado. */
function campo(raiz: HTMLElement, etiqueta: string): HTMLElement {
  const rotulo = [...raiz.querySelectorAll('label')].find(
    (l) => l.textContent?.trim() === etiqueta,
  );

  if (!rotulo) {
    throw new Error(`no hay ningún campo rotulado "${etiqueta}"`);
  }

  return rotulo.closest('.c-campo') as HTMLElement;
}

/** ¿Estos dos campos comparten renglón? Se compara el borde superior, con 2px de tolerancia por si
 *  uno tiene el rótulo en dos líneas. */
function compartenRenglon(uno: HTMLElement, otro: HTMLElement): boolean {
  return Math.abs(uno.getBoundingClientRect().top - otro.getBoundingClientRect().top) <= 2;
}

/**
 * Lo mismo, para dos controles **de distinta altura**.
 *
 * El tipo del movimiento y la fecha comparten el renglón de arriba, pero no miden lo mismo: la fecha
 * lleva su etiqueta encima y el tipo no, así que alineados por abajo —que es como se ven— sus bordes
 * superiores no coinciden. Comparar `top` diría que están en renglones distintos y sería falso.
 *
 * Lo que define "mismo renglón" entre cosas de distinto alto es que **sus franjas verticales se
 * solapen**.
 */
/**
 * El ancho que un campo "a lo ancho" puede ocupar.
 *
 * No es el ancho del formulario: desde que el formulario es un recuadro tiene su propio relleno y su
 * borde, así que la caja de contenido mide 26 px menos que la caja entera.
 */
function anchoUtil(raiz: HTMLElement): number {
  const formulario = raiz.querySelector<HTMLElement>('form')!;
  const estilo = getComputedStyle(formulario);

  return formulario.clientWidth - parseFloat(estilo.paddingLeft) - parseFloat(estilo.paddingRight);
}

function seSolapanVerticalmente(uno: HTMLElement, otro: HTMLElement): boolean {
  const a = uno.getBoundingClientRect();
  const b = otro.getBoundingClientRect();

  return a.top < b.bottom && b.top < a.bottom;
}

describe('FR-029 · la agrupación de los campos en escritorio', () => {
  afterEach(desmontar);

  it('el tipo y la fecha arriba, uno en cada punta; el monto con su moneda (US6:AC1)', async () => {
    const raiz = await abrirFormulario(1440);

    const tipo = raiz.querySelector<HTMLElement>('.c-segmentado--radios')!;
    const fecha = campo(raiz, 'Fecha');

    // El renglón de arriba: los dos juntos, y el tipo a la izquierda de la fecha.
    expect(seSolapanVerticalmente(tipo, fecha)).toBe(true);
    expect(tipo.getBoundingClientRect().right).toBeLessThan(fecha.getBoundingClientRect().left);

    // El tipo **no se estira**: ocupa lo que necesitan sus dos palabras y deja la punta derecha
    // para la fecha. Antes tomaba el ancho entero del formulario.
    //
    // **El ancho útil no es el del formulario**: desde que el formulario es un recuadro tiene su
    // propio relleno y su borde, así que lo que un campo "a lo ancho" ocupa es la caja de contenido
    // y no la caja entera. Comparar contra la caja entera fallaba por 26 px, que son exactamente el
    // relleno y el borde.
    expect(tipo.getBoundingClientRect().width).toBeLessThan(anchoUtil(raiz) / 2);

    // El par que no se separa nunca.
    expect(compartenRenglon(campo(raiz, 'Monto'), campo(raiz, 'Moneda'))).toBe(true);

    // Y cada cosa en su renglón: si todo quedara en uno solo, lo de arriba pasaría igual.
    expect(seSolapanVerticalmente(fecha, campo(raiz, 'Categoría'))).toBe(false);
    expect(compartenRenglon(campo(raiz, 'Categoría'), campo(raiz, 'Monto'))).toBe(false);

    // **Las dos opciones del tipo, lado a lado y no apiladas.** Es lo que se rompió al achicar el
    // control: encogido a su contenido y con envoltorio permitido, el contenedor resolvía su ancho
    // al de una sola columna y "Gasto" quedaba encima de "Ingreso".
    const opciones = [...tipo.querySelectorAll<HTMLElement>(':scope > span')];
    expect(opciones).toHaveLength(2);
    expect(seSolapanVerticalmente(opciones[0], opciones[1])).toBe(true);

    // La categoría y la nota ocupan el ancho útil entero.
    for (const etiqueta of ['Categoría', 'Nota']) {
      expect(campo(raiz, etiqueta).getBoundingClientRect().width, etiqueta).toBeCloseTo(
        anchoUtil(raiz),
        0,
      );
    }
  });
});

describe('FR-030 · la agrupación aguanta en todos los anchos', () => {
  afterEach(desmontar);

  /**
   * **Monto y moneda nunca se separan**, y es la invariante dura de `FR-030`.
   *
   * Se leen juntos —"1500 ARS"— y la moneda es un código de tres letras, así que entra en 6rem en
   * cualquier ancho. Si este par se apilara, el formulario volvería a tener seis renglones.
   */
  it.each(ANCHOS)('monto y moneda siguen juntos a %i px (FR-030, US6:AC2)', async (ancho) => {
    const raiz = await abrirFormulario(ancho);

    expect(compartenRenglon(campo(raiz, 'Monto'), campo(raiz, 'Moneda'))).toBe(true);
  });

  it.each(ANCHOS)('no produce desplazamiento horizontal a %i px (FR-030)', async (ancho) => {
    await abrirFormulario(ancho);

    expect(desbordaLaPagina(ancho)).toBe(false);
  });

  /**
   * **Categoría y fecha se apilan cuando no entran, y eso es lo correcto.**
   *
   * `FR-030` las deja compartir renglón sólo cuando cada una conserva un ancho mínimo legible. El
   * cálculo de research D-07 decía 10rem cada una, o sea que a 360 px se apilan y desde unos 364
   * comparten. **Era una cuenta, no una medición**: lo que esta prueba afirma no es el número sino
   * la regla — si comparten renglón, cada una mide al menos su mínimo legible.
   */
  it.each(ANCHOS)(
    'el tipo y la fecha arriba, y la categoría a lo ancho, a %i px',
    async (ancho) => {
      const raiz = await abrirFormulario(ancho);

      const tipo = raiz.querySelector<HTMLElement>('.c-segmentado--radios')!;
      const fecha = campo(raiz, 'Fecha');
      const categoria = campo(raiz, 'Categoría');

      // **La categoría siempre a lo ancho.** Dejó de compartir renglón con la fecha en el
      // reordenamiento de la 014: es un `<select>` con nombres que pueden ser largos.
      expect(categoria.getBoundingClientRect().width, `categoría a ${ancho} px`).toBeCloseTo(
        anchoUtil(raiz),
        0,
      );

      // **La fecha se lee en todo ancho.** 9rem = 144 px es el piso por debajo del cual un campo de
      // fecha se corta, y vale compartan renglón o no.
      expect(fecha.getBoundingClientRect().width, `fecha a ${ancho} px`).toBeGreaterThanOrEqual(
        144,
      );

      // **El tipo y la fecha**: o comparten el renglón de arriba —y entonces el tipo va a la
      // izquierda y la fecha a la derecha—, o no entran juntos y `l-extremos` baja la fecha a su
      // propio renglón. Las dos formas son correctas: a 360 px los dos suman más que el ancho útil,
      // así que ahí la fecha baja, y eso es lo que tiene que pasar.
      if (seSolapanVerticalmente(tipo, fecha)) {
        expect(tipo.getBoundingClientRect().right).toBeLessThan(fecha.getBoundingClientRect().left);
      } else {
        expect(fecha.getBoundingClientRect().top).toBeGreaterThanOrEqual(
          tipo.getBoundingClientRect().bottom - 1,
        );
      }
    },
  );
});

describe('FR-032, SC-010 · el formulario entra en una pantalla de teléfono', () => {
  afterEach(desmontar);

  /** Lo medido en `LineaBase.navegador.test.tsx` antes de tocar nada, a 360 px. */
  const ALTO_DE_ANTES = 527;

  it('mide menos alto que el de antes, a 360 px y sin errores a la vista (FR-032)', async () => {
    const raiz = await abrirFormulario(360);
    const formulario = raiz.querySelector('form')!;
    const estilo = getComputedStyle(formulario);
    const alto =
      formulario.getBoundingClientRect().height +
      parseFloat(estilo.marginTop) +
      parseFloat(estilo.marginBottom);

    // El número de antes está escrito acá porque es una medición histórica: el formulario viejo ya
    // no existe, así que no hay forma de volver a medirlo. Está anotado en research D-07.
    expect(alto).toBeLessThan(ALTO_DE_ANTES);
  });
});

/**
 * `RF-32` del PRD — **sumar una moneda al catálogo es sólo un dato, también del lado de la
 * pantalla.**
 *
 * Es la misma promesa que `verificar-monedas.sh` protege del lado del backend, aplicada a la
 * disposición: un ancho de columna ajustado a los nombres de hoy es una lista escrita a mano con
 * otra forma. El día que alguien agregue una moneda de nombre largo, el renglón tiene que seguir
 * funcionando sin que nadie toque CSS.
 */
describe('RF-32 · el renglón del monto aguanta cualquier nombre de moneda', () => {
  afterEach(desmontar);

  it.each(ANCHOS)(
    'el nombre de la moneda no se corta a %i px (FR-029, PRD:RF-32)',
    async (ancho) => {
      const raiz = await abrirFormulario(ancho, CON_NOMBRES_LARGOS);

      const selector = [...raiz.querySelectorAll('select')].find(
        (s) => s.options.length === CON_NOMBRES_LARGOS.length,
      )!;

      // Lo que necesita el nombre más largo, medido con la misma tipografía del control.
      const regla = document.createElement('span');
      regla.style.cssText = `position:absolute;visibility:hidden;white-space:nowrap;font:${getComputedStyle(selector).font}`;
      regla.textContent = 'Dólar estadounidense';
      document.body.appendChild(regla);
      const necesita = regla.getBoundingClientRect().width;
      regla.remove();

      // El `<select>` tiene que darle lugar al texto más la flecha que dibuja el navegador.
      expect(selector.getBoundingClientRect().width).toBeGreaterThanOrEqual(necesita);
    },
  );

  it.each(ANCHOS)('y el par sigue sin partirse ni desbordar a %i px (FR-030)', async (ancho) => {
    const raiz = await abrirFormulario(ancho, CON_NOMBRES_LARGOS);

    expect(compartenRenglon(campo(raiz, 'Monto'), campo(raiz, 'Moneda'))).toBe(true);
    expect(desbordaLaPagina(ancho)).toBe(false);
  });
});

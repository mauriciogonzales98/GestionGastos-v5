import { afterEach, describe, expect, it } from 'vitest';
import { page, userEvent } from 'vitest/browser';
import { PantallaCategorias } from '../src/categorias/PantallaCategorias';
import type { Categoria } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { desmontar, montar } from './montar';
import { ANCHOS, desbordaLaPagina, paraCadaAncho } from './anchos';
import '../src/estilos/index.css';

/**
 * US8, FR-043, AC5 — **la fila de una categoría, medida**.
 *
 * `FR-043` es una afirmación sobre píxeles: el nombre y sus dos íconos entran en un renglón a
 * 360 px, un nombre largo se corta con puntos suspensivos **antes** de empujar los íconos a otra
 * línea, y cada ícono mide 44 × 44. Nada de eso se puede deducir de las reglas escritas, y un DOM
 * simulado devuelve ceros — que es la deuda D11-01 por la que esta feature trajo un navegador.
 *
 * Acá también se mide lo que `BotonIcono.test.tsx` sólo puede comprobar en la hoja: que el rótulo
 * **aparezca** al apoyar el puntero y al llegar con el teclado (`FR-042`). `:hover` no existe en
 * happy-dom; existe acá.
 */

const ALTO = 900;

/** El máximo que acepta el campo de nombre, que es el peor caso que la fila tiene que aguantar. */
const NOMBRE_LARGO = 'C'.repeat(50);

const CON_NOMBRE_LARGO: Categoria[] = [
  { id: 99, nombre: NOMBRE_LARGO, tipo: 'gasto' },
  ...CATEGORIAS,
];

async function abrirCategorias(categorias: Categoria[] = CATEGORIAS): Promise<HTMLElement> {
  return montar(
    <PantallaCategorias
      categorias={categorias}
      onCrear={async () => {}}
      onRenombrar={async () => {}}
      onDarDeBaja={async () => {}}
    />,
  );
}

/** La fila de una categoría, buscada por su nombre accesible como la busca cualquier prueba. */
function fila(raiz: HTMLElement, nombre: string): HTMLElement {
  const encontrada = raiz.querySelector<HTMLElement>(`li[aria-label="${nombre}"]`);

  if (!encontrada) {
    throw new Error(`no hay fila para la categoría "${nombre}"`);
  }

  return encontrada;
}

/** Los dos botones de sólo ícono de una fila. */
function iconosDe(unaFila: HTMLElement): HTMLElement[] {
  return [...unaFila.querySelectorAll<HTMLElement>('.c-boton-icono')];
}

/** ¿Estos elementos comparten renglón? Se comparan sus centros verticales, no sus bordes. */
function enElMismoRenglon(elementos: Element[]): boolean {
  const centros = elementos.map((elemento) => {
    const caja = elemento.getBoundingClientRect();

    return caja.top + caja.height / 2;
  });

  // Un elemento más alto que otro corre su centro unos píxeles sin estar en otra línea; dos líneas
  // distintas se separan por el alto de una, que acá nunca baja de 20 px.
  return Math.max(...centros) - Math.min(...centros) < 12;
}

describe('FR-043 · el nombre y sus dos íconos entran en un renglón', () => {
  afterEach(desmontar);

  it('en cada ancho de referencia, la fila es un solo renglón (FR-043, US8:AC5)', async () => {
    const raiz = await abrirCategorias();

    await paraCadaAncho(async (ancho) => {
      const laFila = fila(raiz, 'Comida');
      const nombre = laFila.querySelector('.c-categoria__nombre')!;
      const iconos = iconosDe(laFila);

      expect(iconos, `la fila no tiene dos botones de ícono a ${ancho} px`).toHaveLength(2);
      expect(
        enElMismoRenglon([nombre, ...iconos]),
        `el nombre y sus íconos caen en renglones distintos a ${ancho} px`,
      ).toBe(true);

      await Promise.resolve();
    });
  });

  it('un nombre de 50 caracteres se corta en vez de empujar los íconos (FR-043, US8:AC5)', async () => {
    const raiz = await abrirCategorias(CON_NOMBRE_LARGO);

    await paraCadaAncho(async (ancho) => {
      const laFila = fila(raiz, NOMBRE_LARGO);
      const nombre = laFila.querySelector<HTMLElement>('.c-categoria__nombre')!;
      const iconos = iconosDe(laFila);

      expect(
        enElMismoRenglon([nombre, ...iconos]),
        `el nombre largo empujó los íconos a otra línea a ${ancho} px`,
      ).toBe(true);

      // Y la página no se corre de costado por culpa de ese nombre.
      expect(desbordaLaPagina(ancho), `la página desborda a ${ancho} px`).toBe(false);

      await Promise.resolve();
    });
  });

  it('el corte del nombre largo es con puntos suspensivos (FR-043)', async () => {
    // A 360 px el nombre de 50 caracteres no entra en ningún caso, así que acá sí tiene que estar
    // recortado — es la premisa de la aserción, y por eso se mide en el ancho más chico.
    await page.viewport(360, ALTO);
    const raiz = await abrirCategorias(CON_NOMBRE_LARGO);

    const nombre = fila(raiz, NOMBRE_LARGO).querySelector<HTMLElement>('.c-categoria__nombre')!;

    expect(getComputedStyle(nombre).textOverflow).toBe('ellipsis');
    // Recortado de verdad: el texto mide más de lo que la caja muestra.
    expect(nombre.scrollWidth).toBeGreaterThan(nombre.clientWidth);
  });

  it('cada ícono mide al menos 44 × 44 px en todos los anchos (FR-043, US8:AC5)', async () => {
    const raiz = await abrirCategorias();

    await paraCadaAncho(async (ancho) => {
      const chicos = iconosDe(fila(raiz, 'Comida'))
        .map((boton) => boton.getBoundingClientRect())
        .filter((caja) => caja.width < 44 || caja.height < 44)
        .map((caja) => `${Math.round(caja.width)}×${Math.round(caja.height)} a ${ancho} px`);

      expect(chicos).toEqual([]);

      await Promise.resolve();
    });
  });
});

/**
 * `FR-042` — **el rótulo aparece de verdad**, con el puntero y con el teclado.
 *
 * Es la otra mitad de lo que `BotonIcono.test.tsx` deja comprobado en la hoja. Lo que se mide es la
 * **visibilidad** y no la existencia del nodo: el rótulo está siempre en el DOM —tiene que estar,
 * para poder aparecer sin que React lo monte— así que buscarlo no prueba nada.
 */
describe('FR-042 · el rótulo de la acción aparece al apoyar el puntero y al recibir el foco', () => {
  afterEach(desmontar);

  /**
   * ¿Se ve?
   *
   * `checkVisibility()` contesta exactamente la pregunta —cuenta el `display`, la `visibility` y la
   * `opacity` de él y de sus ancestros— y por eso se usa en lugar de mirar una propiedad. Mirar sólo
   * la opacidad daría un falso positivo con el rótulo escondido por `display: none`, que es como
   * está escondido desde que se midió que un rótulo transparente desborda la página.
   */
  const seVe = (rotulo: HTMLElement): boolean => rotulo.checkVisibility();

  /**
   * **Saca el puntero de encima de los íconos.**
   *
   * Hace falta y no es ceremonia: la posición del mouse la guarda el navegador y **sobrevive al
   * desmontaje**. Sin esto, la prueba del puntero dejaba el cursor sobre el botón de la primera
   * fila y la del teclado se montaba con el mismo botón en el mismo lugar, o sea ya apuntado: daba
   * verde sin que `:focus-visible` existiera en la hoja. Se descubrió borrando ese selector a
   * propósito, que es para lo que se borran.
   */
  async function sacarElPuntero(raiz: HTMLElement): Promise<void> {
    await userEvent.hover(raiz.querySelector<HTMLElement>('h1')!);
  }

  it('está escondido hasta que alguien lo busca (FR-042)', async () => {
    await page.viewport(1440, ALTO);
    const raiz = await abrirCategorias();

    await sacarElPuntero(raiz);
    const [renombrar] = iconosDe(fila(raiz, 'Comida'));

    expect(seVe(renombrar.querySelector<HTMLElement>('span')!)).toBe(false);
  });

  it('aparece al apoyar el puntero (FR-042, US8:AC3)', async () => {
    await page.viewport(1440, ALTO);
    const raiz = await abrirCategorias();

    const [renombrar] = iconosDe(fila(raiz, 'Comida'));
    await userEvent.hover(renombrar);

    expect(seVe(renombrar.querySelector<HTMLElement>('span')!)).toBe(true);
  });

  it('aparece al llegar con el teclado, que es lo que title no hace (FR-042, US8:AC3)', async () => {
    await page.viewport(1440, ALTO);
    const raiz = await abrirCategorias();

    await sacarElPuntero(raiz);
    const [renombrar] = iconosDe(fila(raiz, 'Comida'));

    // El foco puesto por código cuenta como `:focus-visible` en un botón que no recibió un clic,
    // que es la situación de quien llega con Tab.
    renombrar.focus();

    expect(renombrar.matches(':focus-visible')).toBe(true);
    expect(seVe(renombrar.querySelector<HTMLElement>('span')!)).toBe(true);
  });
});

/**
 * `FR-043` en la otra pantalla que gana íconos: el listado de movimientos.
 *
 * La fila de un movimiento es una `<tr>` y no un bloque flexible, así que lo que se mide es otra
 * cosa: que los dos botones de la celda de acciones compartan renglón y lleguen a 44 × 44. El
 * desborde de la tabla ya está acotado a su envoltorio desde la feature 011, y
 * `PantallasConSesion.navegador.test.tsx` lo mide.
 */
describe('FR-043 · los íconos del listado también llegan al área tocable', () => {
  afterEach(desmontar);

  it.each(ANCHOS)('los dos íconos de una fila miden 44 × 44 a %i px (FR-043)', async (ancho) => {
    await page.viewport(ancho, ALTO);

    const { ListadoMovimientos } = await import('../src/movimientos/ListadoMovimientos');
    const raiz = await montar(
      <ListadoMovimientos
        movimientos={[
          {
            id: 1,
            tipo: 'gasto',
            monto: 1500,
            categoriaId: 1,
            categoriaNombre: 'Comida',
            monedaCodigo: 'ARS',
            fecha: '2026-10-01',
            nota: '',
          },
        ]}
        onEditar={() => {}}
        onEliminar={() => {}}
      />,
    );

    const iconos = [...raiz.querySelectorAll<HTMLElement>('.c-boton-icono')];

    expect(iconos).toHaveLength(2);
    expect(enElMismoRenglon(iconos)).toBe(true);

    const chicos = iconos
      .map((boton) => boton.getBoundingClientRect())
      .filter((caja) => caja.width < 44 || caja.height < 44)
      .map((caja) => `${Math.round(caja.width)}×${Math.round(caja.height)}`);

    expect(chicos).toEqual([]);
  });
});

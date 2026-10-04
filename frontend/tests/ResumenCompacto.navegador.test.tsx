import { afterEach, describe, expect, it } from 'vitest';
import { page } from 'vitest/browser';
import { ResumenDelPeriodo } from '../src/resumen/ResumenDelPeriodo';
import { MarcoDeLaApp } from '../src/ui/MarcoDeLaApp';
import { MONEDAS } from './monedas.fixture';
import { construirMoneda, EN_PESOS, RESUMEN } from './resumen.fixture';
import { desmontar, montar } from './montar';
import { ANCHOS, CORTE_PX, desbordaLaPagina } from './anchos';
import '../src/estilos/index.css';

/**
 * US7, FR-037, SC-011 — **el resumen compacto, medido.**
 *
 * Hoy el resumen de la pantalla de movimientos mide **740 px** a 360 px de ancho con dos monedas,
 * una de ellas sin movimientos —medido en `LineaBase.navegador.test.tsx` antes de tocarlo—, y
 * empuja el formulario y el listado, que es lo que se usa todos los días, muy abajo.
 */

const ALTO = 900;

/** Lo medido antes de tocar nada, a 360 px, con desglose. */
const ALTO_DE_ANTES = 740;

async function abrirResumen(ancho: number, conDesglose = false): Promise<HTMLElement> {
  await page.viewport(ancho, ALTO);

  return montar(
    <ResumenDelPeriodo resumen={RESUMEN} monedas={MONEDAS} conDesglose={conDesglose} />,
  );
}

/** El renglón de las tres cifras de una moneda. */
function cifras(raiz: HTMLElement): HTMLElement[] {
  const lista = raiz.querySelector<HTMLElement>('dl');

  if (!lista) {
    throw new Error('el resumen no tiene ninguna lista de cifras');
  }

  return [...lista.children] as HTMLElement[];
}

/** El monto más largo que `FR-037` nombra: `$ 9.999.999,99`, catorce caracteres. */
const SIETE_CIFRAS = 9999999.99;

/**
 * Las tres cifras con el peor monto posible, **montadas dentro del marco de la app**.
 *
 * El marco no es decoración de la prueba: es lo que le da al resumen **el ancho que tiene de
 * verdad**. Suelto a 360 px el componente recibe los 360 enteros; dentro de la app recibe 286,
 * porque antes están el relleno del contenido y el de la tarjeta. Cincuenta píxeles de diferencia,
 * y son exactamente los que separan "las tres cifras entran" de "cada monto pinta encima del de al
 * lado".
 *
 * **Así se escapó el defecto**: la prueba anterior montaba el resumen solo, medía 106 px por columna
 * y daba verde, mientras la pantalla de verdad tenía 90 y se veía rota. Se descubrió levantando la
 * app y mirándola. Una medición en un contenedor que no es el real no mide la pantalla: mide otra.
 */
async function abrirConMontosLargos(ancho: number): Promise<HTMLElement> {
  await page.viewport(ancho, ALTO);

  const grandota = construirMoneda(EN_PESOS, {
    totalIngresado: SIETE_CIFRAS,
    totalGastado: SIETE_CIFRAS,
    balance: -SIETE_CIFRAS,
  });

  return montar(
    <MarcoDeLaApp
      seccion="movimientos"
      email="ana@ejemplo.com"
      onIrA={() => {}}
      onCerrarSesion={() => {}}
    >
      {/* `main.l-pila` y no un `div`: es el elemento que usan las pantallas de verdad, y la regla
          general de `main` le agrega su propio relleno. Ese relleno es parte de los 50 px que
          separan la medición de la pantalla. */}
      <main className="l-pila">
        <ResumenDelPeriodo
          resumen={{ ...RESUMEN, monedas: [grandota] }}
          monedas={MONEDAS}
          conDesglose={false}
        />
      </main>
    </MarcoDeLaApp>,
  );
}

/**
 * **Lo que un monto ocupa de verdad, que no es el ancho de su caja.**
 *
 * Un monto lleva `white-space: nowrap`, así que cuando no entra en su columna **no se corta: se
 * sale**, y pinta encima de lo que tenga al lado. La caja sigue midiendo lo que mide la columna, y
 * por eso mirar `getBoundingClientRect` no alcanza para saber si dos cifras se pisan. `scrollWidth`
 * es lo que mide el texto.
 */
function loQuePinta(celda: HTMLElement): { izquierda: number; derecha: number } {
  const caja = celda.getBoundingClientRect();
  const ancho = Math.max(caja.width, celda.scrollWidth);

  return { izquierda: caja.left, derecha: caja.left + ancho };
}

/**
 * FR-037 — **las tres cifras, sin pisarse, con el peor monto, en todo ancho.**
 *
 * Es la prueba que faltaba, y la que convirtió a esta feature en una que se levantó y se miró. Hasta
 * acá se verificaba que las tres **compartieran renglón** y que la **página no desbordara**: las dos
 * cosas eran ciertas a 360 px y aun así la pantalla era ilegible, porque cada monto se salía de su
 * columna y pintaba sobre el de al lado —`$ 1.850.000,0` y `0$ 764.850,75` pegados—. Dos aserciones
 * verdaderas describiendo una pantalla rota.
 *
 * Lo que se mide ahora es lo que la persona ve: **que ningún monto invada el espacio del siguiente**.
 */
describe('FR-037 · las tres cifras nunca se pisan entre sí', () => {
  afterEach(desmontar);

  it.each(ANCHOS)('con montos de siete cifras, ninguna invade a la otra a %i px', async (ancho) => {
    const raiz = await abrirConMontosLargos(ancho);
    const montos = cifras(raiz).map((celda) => celda.querySelector<HTMLElement>('dd')!);

    const pisados: string[] = [];

    for (const [i, monto] of montos.entries()) {
      const pinta = loQuePinta(monto);

      for (const otro of montos.slice(i + 1)) {
        const suyo = loQuePinta(otro);
        // Sólo cuentan los que comparten renglón: apilados, uno empieza a la izquierda del otro y
        // eso no es pisarse.
        const mismoRenglon =
          Math.abs(monto.getBoundingClientRect().top - otro.getBoundingClientRect().top) < 4;

        if (mismoRenglon && pinta.derecha > suyo.izquierda + 1) {
          pisados.push(
            `"${monto.textContent?.trim()}" pinta hasta ${Math.round(pinta.derecha)} y "${otro.textContent?.trim()}" empieza en ${Math.round(suyo.izquierda)}`,
          );
        }
      }
    }

    expect(pisados, `a ${ancho} px`).toEqual([]);
    expect(desbordaLaPagina(ancho)).toBe(false);
  });
});

/**
 * `FR-037` — **un renglón desde 48rem; apiladas por debajo.**
 *
 * La spec pedía las tres en un renglón **también a 360 px y con siete cifras**, y la cuenta no da:
 * cada monto necesita 107 px y la columna mide 90, así que para que entraran habría que bajar la
 * letra a unos 12 px — por debajo del escalón más chico de Lila, y justo en el dato más importante
 * de la pantalla. Se corrigió el requisito en lugar de achicar el número: apiladas, cada cifra
 * conserva su tamaño y su nombre al lado, y no hay monto que pueda romperlas.
 *
 * El corte es el mismo 48rem de todo lo demás.
 */
describe('FR-037 · un renglón desde 48rem, apiladas por debajo', () => {
  afterEach(desmontar);

  it.each(ANCHOS.filter((ancho) => ancho >= CORTE_PX))(
    'comparten renglón a %i px (US7:AC1)',
    async (ancho) => {
      const raiz = await abrirConMontosLargos(ancho);
      const arriba = cifras(raiz).map((celda) => celda.getBoundingClientRect().top);

      expect(Math.max(...arriba) - Math.min(...arriba)).toBeLessThanOrEqual(2);
    },
  );

  it.each(ANCHOS.filter((ancho) => ancho < CORTE_PX))(
    'cada una en su renglón a %i px (US7:AC1)',
    async (ancho) => {
      const raiz = await abrirConMontosLargos(ancho);
      const cajas = cifras(raiz).map((celda) => celda.getBoundingClientRect());

      // Cada una empieza por debajo de la anterior: tres renglones, no uno.
      for (let i = 1; i < cajas.length; i += 1) {
        expect(cajas[i].top, `la cifra ${i + 1} a ${ancho} px`).toBeGreaterThanOrEqual(
          cajas[i - 1].bottom - 1,
        );
      }
    },
  );

  it('apiladas, el nombre queda a la izquierda y el monto a la derecha (US7:AC1)', async () => {
    const raiz = await abrirConMontosLargos(360);

    for (const celda of cifras(raiz)) {
      const nombre = celda.querySelector<HTMLElement>('dt')!.getBoundingClientRect();
      const monto = celda.querySelector<HTMLElement>('dd')!.getBoundingClientRect();

      expect(nombre.right).toBeLessThanOrEqual(monto.left + 1);
      // En el mismo renglón que su nombre: es lo que los mantiene leídos como una sola cosa.
      expect(Math.abs(nombre.top - monto.top)).toBeLessThan(8);
    }
  });
});

describe('SC-011 · el resumen ocupa menos de la mitad que antes', () => {
  afterEach(desmontar);

  it('con dos monedas, una sin movimientos, a 360 px (SC-011)', async () => {
    const raiz = await abrirResumen(360);
    const bloque = raiz.firstElementChild as HTMLElement;
    const estilo = getComputedStyle(bloque);
    const alto =
      bloque.getBoundingClientRect().height +
      parseFloat(estilo.marginTop) +
      parseFloat(estilo.marginBottom);

    // El número de antes está escrito acá porque es una medición histórica: el resumen viejo ya no
    // existe, así que no hay forma de volver a medirlo. Está anotado en research D-07.
    expect(alto).toBeLessThan(ALTO_DE_ANTES / 2);
  });
});

describe('FR-038 · la moneda sin movimientos ocupa una línea, no un bloque', () => {
  afterEach(desmontar);

  it.each(ANCHOS)('mide mucho menos que la moneda con datos, a %i px', async (ancho) => {
    const raiz = await abrirResumen(ancho);

    const regiones = [...raiz.querySelectorAll<HTMLElement>('[aria-label^="Totales en"]')];
    expect(regiones).toHaveLength(2);

    const [conDatos, sinDatos] = regiones;

    // La que no tuvo movimientos no puede ocupar lo mismo que la que sí: ése era el problema.
    expect(sinDatos.getBoundingClientRect().height).toBeLessThan(
      conDatos.getBoundingClientRect().height / 2,
    );
  });
});

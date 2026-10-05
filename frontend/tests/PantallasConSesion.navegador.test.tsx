import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { page } from 'vitest/browser';
import { MarcoDeLaApp, type Seccion } from '../src/ui/MarcoDeLaApp';
import { PantallaCategorias } from '../src/categorias/PantallaCategorias';
import { PantallaDashboard } from '../src/dashboard/PantallaDashboard';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import { RESUMEN } from './resumen.fixture';
import { desmontar, montar } from './montar';
import { ANCHOS, desbordaLaPagina } from './anchos';
import '../src/estilos/index.css';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');

  return {
    ...real,
    obtenerResumen: vi.fn(),
    obtenerMovimientos: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

/**
 * US4, FR-015 — **las tres pantallas con sesión heredan los controles de Lila sin romperse**, en
 * todos los anchos.
 *
 * Es la consecuencia inevitable de la US1: los botones, los campos y los mensajes se estilan por
 * tipo de elemento para toda la app, así que cambiarlos cambia estas tres pantallas también. La
 * spec dice que su disposición interna **no** cambia en esta feature (deuda D14-01); lo que sí hay
 * que verificar, en lugar de suponerlo, es que nada quedó desbordado, tapado ni ilegible.
 *
 * **Dos cosas se miden, y la segunda es la que ninguna comprobación de desborde ve.** Que la página
 * no se desplace de costado es la primera. La segunda es que nada quede **tapado por la barra ni
 * fuera de la ventana**: una pantalla puede no desbordar y tener el botón de guardar debajo de la
 * barra inferior, o un bloque dibujado encima del contenido — que es exactamente el bug que la
 * prueba del email largo encontró en la barra lateral.
 */

const ALTO = 740;

/** Las tres pantallas, cada una con lo mínimo para montarse. Se enumeran acá y no en cada prueba
 *  para que el producto pantallas × anchos salga de una sola lista. */
const PANTALLAS: { seccion: Seccion; nombre: string; contenido: () => React.ReactNode }[] = [
  {
    seccion: 'movimientos',
    nombre: 'movimientos',
    contenido: () => (
      <PantallaMovimientos
        hoy="2026-10-01"
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        errorDelCatalogo={null}
        errorDelCatalogoDeMonedas={null}
        onSesionVencida={() => {}}
      />
    ),
  },
  {
    seccion: 'dashboard',
    nombre: 'dashboard',
    contenido: () => <PantallaDashboard monedas={MONEDAS} onSesionVencida={() => {}} />,
  },
  {
    seccion: 'categorias',
    nombre: 'categorías',
    contenido: () => (
      <PantallaCategorias
        categorias={CATEGORIAS}
        onCrear={async () => {}}
        onRenombrar={async () => {}}
        onDarDeBaja={async () => {}}
      />
    ),
  },
];

beforeEach(() => {
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([]);
});

afterEach(desmontar);

/** Monta una pantalla dentro del marco, como la usa la app de verdad. */
async function abrir(seccion: Seccion, contenido: React.ReactNode, ancho: number) {
  await page.viewport(ancho, ALTO);

  return montar(
    <MarcoDeLaApp
      seccion={seccion}
      email="ana@ejemplo.com"
      onIrA={() => {}}
      onCerrarSesion={() => {}}
    >
      {contenido}
    </MarcoDeLaApp>,
  );
}

describe('FR-015 · ninguna pantalla con sesión desborda la página, en ningún ancho', () => {
  for (const { seccion, nombre, contenido } of PANTALLAS) {
    it.each(ANCHOS)(`${nombre} no desborda a %i px (FR-015, US4:AC2, SC-006)`, async (ancho) => {
      await abrir(seccion, contenido(), ancho);

      // El desplazamiento de la tabla del listado sigue confinado a su contenedor: lo que no puede
      // desplazarse es la PÁGINA (`FR-004` de la feature 011).
      expect(desbordaLaPagina(ancho)).toBe(false);
    });
  }
});

describe('FR-015 · ningún control queda fuera de la ventana por los costados', () => {
  for (const { seccion, nombre, contenido } of PANTALLAS) {
    it.each(ANCHOS)(`en ${nombre} todo control es alcanzable a %i px (FR-015)`, async (ancho) => {
      const raiz = await abrir(seccion, contenido(), ancho);
      const main = raiz.querySelector<HTMLElement>('main')!;

      const afuera = [...main.querySelectorAll<HTMLElement>('button, input, select, textarea')]
        .map((control) => ({ control, caja: control.getBoundingClientRect() }))
        // Los que no se muestran no cuentan: un `<dialog>` cerrado mide cero.
        .filter(({ caja }) => caja.width > 0 && caja.height > 0)
        // Se tolera 1px por el redondeo de un ancho impar repartido en columnas.
        .filter(({ caja }) => caja.left < -1 || caja.right > window.innerWidth + 1)
        .map(
          ({ control, caja }) =>
            `${control.tagName.toLowerCase()} en x=${Math.round(caja.left)}..${Math.round(caja.right)}`,
        );

      expect(afuera).toEqual([]);
    });
  }
});

/**
 * **Todo control escribe con la letra de la app.**
 *
 * La causa de que esto haga falta: un control de formulario **no hereda `font-family`**. El navegador
 * le pone la suya —y a `textarea` le pone `monospace`—, así que la tipografía de Lila llega a los
 * campos sólo porque una regla se la da. Una regla que enumera elementos a mano se olvida de uno, y
 * eso ya pasó: `textarea` no estaba, y la nota del movimiento era el único texto de la app fuera de
 * Figtree.
 *
 * Se mide la fuente **efectiva** y no el texto del CSS: lo que importa es con qué letra se dibuja,
 * no qué reglas se escribieron. Y se compara contra la del `body` en lugar de contra el nombre de la
 * fuente, para que el día que Lila cambie de tipografía esta prueba siga diciendo la verdad sin que
 * haya que tocarla.
 *
 * En un solo ancho: la tipografía no depende del ancho, y repetirlo seis veces sería medir seis
 * veces lo mismo.
 */
describe('la tipografía de Lila llega a todos los controles', () => {
  for (const { seccion, nombre, contenido } of PANTALLAS) {
    it(`en ${nombre} ningún campo ni botón usa otra letra que la del documento`, async () => {
      const raiz = await abrir(seccion, contenido(), 1440);
      await document.fonts.ready;

      const delDocumento = getComputedStyle(document.body).fontFamily;

      const ajenos = [...raiz.querySelectorAll<HTMLElement>('button, input, select, textarea')]
        .map((control) => ({ control, familia: getComputedStyle(control).fontFamily }))
        .filter(({ familia }) => familia !== delDocumento)
        .map(
          ({ control, familia }) =>
            `${control.tagName.toLowerCase()}${control.getAttribute('type') ? `[type=${control.getAttribute('type')}]` : ''} usa ${familia}`,
        );

      expect(ajenos).toEqual([]);
    });
  }
});

/**
 * `FR-026` — el `<main>` **reserva** el alto de la barra inferior.
 *
 * Es la invariante, no el síntoma. Que el último control quede por encima de la barra después de
 * desplazarse hasta el fondo lo mide `Navegacion.navegador.test.tsx` con contenido largo de
 * verdad; eso es el caso concreto. Lo que se verifica acá es la **causa** de que eso funcione: que
 * el relleno inferior del contenido cubra a la barra, en las tres pantallas y en todo ancho donde
 * la barra esté abajo.
 *
 * Son dos pruebas de la misma cosa a propósito: si alguien quita el `padding-bottom`, esta falla en
 * las tres pantallas y dice exactamente qué falta, en lugar de fallar una sola vez con un control
 * tapado y dejar que alguien averigüe por qué.
 */
describe('FR-026 · el contenido reserva el alto de la barra donde la barra va abajo', () => {
  for (const { seccion, nombre, contenido } of PANTALLAS) {
    it.each([360, 480, 767])(
      `${nombre} reserva el alto de la barra a %i px (FR-026)`,
      async (ancho) => {
        const raiz = await abrir(seccion, contenido(), ancho);

        const nav = raiz.querySelector<HTMLElement>('nav')!.getBoundingClientRect();
        const main = raiz.querySelector<HTMLElement>('main')!;
        const reservado = parseFloat(getComputedStyle(main).paddingBottom);

        // La barra está abajo en estos anchos: es la premisa de la prueba, y si dejara de cumplirse
        // la prueba estaría verificando otra cosa sin avisar.
        expect(nav.bottom).toBeCloseTo(window.innerHeight, 0);

        expect(reservado).toBeGreaterThanOrEqual(nav.height);
      },
    );
  }
});

describe('FR-003 · ninguna pantalla toma el aspecto de acción principal por accidente', () => {
  for (const { seccion, nombre, contenido } of PANTALLAS) {
    it(`${nombre} tiene como máximo un botón principal por formulario (FR-003, US4:AC3)`, async () => {
      const raiz = await abrir(seccion, contenido(), 1440);

      const formularios = [...raiz.querySelectorAll('form')];
      const conVarios = formularios
        .map((form) => ({ form, principales: form.querySelectorAll('.c-boton--principal').length }))
        .filter(({ principales }) => principales > 1)
        .map(({ principales }) => `un formulario con ${principales} botones principales`);

      expect(conVarios).toEqual([]);
    });
  }
});

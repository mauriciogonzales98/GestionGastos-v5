import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cdp } from 'vitest/browser';
import type { ReactNode } from 'react';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';
import { MarcoDeLaApp, type Seccion } from '../src/ui/MarcoDeLaApp';
import { PantallaCategorias } from '../src/categorias/PantallaCategorias';
import { PantallaDashboard } from '../src/dashboard/PantallaDashboard';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import { RESUMEN } from './resumen.fixture';
import { desmontar, montar } from './montar';
import { desbordaLaPagina, paraCadaAncho } from './anchos';
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
 * **El barrido de cierre: las cuatro pantallas × los seis anchos × los dos modos.**
 *
 * Cada historia verificó lo suyo —US2 la pantalla de acceso en el teléfono, US4 las tres con sesión,
 * US5 la navegación, US9 el modo oscuro— y en ninguna se cruzaron. **Lo responsive se rompe justo
 * en los cruces**: el dashboard a 767 px en oscuro es una combinación que ninguna prueba miró, y es
 * tan válida como las que sí.
 *
 * Lo que se afirma son **las tres invariantes transversales**, las que valen para toda pantalla en
 * todo ancho y en todo modo:
 *
 *   1. la página no se desplaza de costado (`FR-004`, `FR-011`);
 *   2. ningún control tocable queda por debajo de 44 px de alto (`FR-010`, `FR-025`, `FR-043`);
 *   3. ningún control queda fuera de la ventana ni tapado por otra cosa (`FR-026`).
 *
 * **Las pantallas no se enumeran a mano.** `CONTENIDO` es un `Record<Seccion, …>`, así que una
 * sección nueva en la app **no compila** hasta que alguien le escriba acá cómo montarla: es el
 * compilador y no la memoria de nadie lo que mantiene el barrido completo. El acceso se suma aparte
 * porque no es una sección: vive fuera del marco, sin sesión.
 *
 * **No reemplaza a las pruebas de cada historia** y no mide lo que ellas miden: acá no hay anchos de
 * tarjeta, ni alturas de formulario, ni posiciones de barra. Es la red que atrapa la combinación que
 * nadie miró, y por eso las aserciones son las mismas en los cuatro casos.
 */

/** Un movimiento, para que el listado tenga una fila de verdad con sus dos botones de ícono. */
const MOVIMIENTOS: Movimiento[] = [
  {
    id: 1,
    tipo: 'gasto',
    monto: 1234.56,
    categoriaId: CATEGORIAS[0].id,
    categoriaNombre: CATEGORIAS[0].nombre,
    monedaCodigo: MONEDAS[0].codigo,
    fecha: '2026-10-01',
    nota: 'Una nota cualquiera',
  },
];

/**
 * Cómo se monta cada sección. El tipo es lo que obliga a que estén todas: con `Record<Seccion, …>`,
 * agregar una sección a `Seccion` sin agregarla acá es un error de compilación.
 */
const CONTENIDO: Record<Seccion, () => ReactNode> = {
  movimientos: () => (
    <PantallaMovimientos
      hoy="2026-10-01"
      categorias={CATEGORIAS}
      monedas={MONEDAS}
      errorDelCatalogo={null}
      errorDelCatalogoDeMonedas={null}
      onSesionVencida={() => {}}
    />
  ),
  dashboard: () => <PantallaDashboard monedas={MONEDAS} onSesionVencida={() => {}} />,
  categorias: () => (
    <PantallaCategorias
      categorias={CATEGORIAS}
      onCrear={async () => {}}
      onRenombrar={async () => {}}
      onDarDeBaja={async () => {}}
    />
  ),
};

/** Las cuatro pantallas: las tres secciones, derivadas del tipo, más el acceso. */
const PANTALLAS: { nombre: string; montar: () => Promise<HTMLElement> }[] = [
  {
    nombre: 'acceso',
    montar: () => montar(<FormularioAcceso onEntrar={() => {}} />),
  },
  ...(Object.keys(CONTENIDO) as Seccion[]).map((seccion) => ({
    nombre: seccion,
    montar: () =>
      montar(
        <MarcoDeLaApp
          seccion={seccion}
          email="ana@ejemplo.com"
          onIrA={() => {}}
          onCerrarSesion={() => {}}
        >
          {CONTENIDO[seccion]()}
        </MarcoDeLaApp>,
      ),
  })),
];

const MODOS = [
  { modo: 'claro', preferencia: 'light' },
  { modo: 'oscuro', preferencia: 'dark' },
] as const;

/** Todo lo que una persona puede tocar, recorriendo el DOM y no una lista (`NFR-003`). */
function tocables(raiz: HTMLElement): HTMLElement[] {
  return [...raiz.querySelectorAll<HTMLElement>('button, input, select, textarea, a[href]')].filter(
    (control) => {
      const caja = control.getBoundingClientRect();

      // Lo que no se muestra no se toca: un `<dialog>` cerrado mide cero, y el radio del control
      // segmentado está escondido a propósito —lo que se toca es su etiqueta (`FR-034`)—.
      return caja.width > 0 && caja.height > 0;
    },
  );
}

/**
 * ¿Se puede llegar a este control?
 *
 * **Se lo desplaza a la vista primero, y eso es la definición de la invariante.** Dos cosas que
 * parecen fallas no lo son y se confundían sin este paso:
 *
 *   - un botón dentro de la tabla del listado está fuera de la ventana por la derecha, y está bien:
 *     lo que `FR-004` exige es que se desplace **el envoltorio** y no la página;
 *   - un control que cae debajo de la barra fija antes de desplazarse tampoco es un problema: el
 *     `padding-bottom` del contenido garantiza que al llegar al fondo quede libre (`FR-026`).
 *
 * `scrollIntoView` desplaza a todos los ancestros desplazables, así que después de llamarlo la
 * pregunta que queda es la que importa: **puesto en el centro de la ventana, ¿se ve y se puede
 * tocar?** Si ahí sigue fuera de la ventana o sigue tapado, es inalcanzable de verdad.
 */
function inalcanzable(control: HTMLElement): string | null {
  control.scrollIntoView({ block: 'center', inline: 'center' });

  const caja = control.getBoundingClientRect();

  // Se tolera 1 px por el redondeo de un ancho impar repartido en columnas.
  if (caja.left < -1 || caja.right > window.innerWidth + 1) {
    return `fuera de la ventana en x=${Math.round(caja.left)}..${Math.round(caja.right)}`;
  }

  const encima = document.elementFromPoint(caja.left + caja.width / 2, caja.top + caja.height / 2);

  // Tapado = en su propio centro hay dibujado algo que no es él ni nada suyo.
  if (encima !== null && encima !== control && !control.contains(encima)) {
    return `tapado por ${encima.tagName.toLowerCase()}.${String(encima.className)}`;
  }

  return null;
}

beforeEach(() => {
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue(MOVIMIENTOS);
});

afterEach(async () => {
  await desmontar();
  await cdp().send('Emulation.setEmulatedMedia', { features: [] });
});

for (const { nombre, montar: abrir } of PANTALLAS) {
  for (const { modo, preferencia } of MODOS) {
    describe(`barrido · ${nombre} en modo ${modo}`, () => {
      it(`no desplaza la página de costado en ningún ancho (FR-004, FR-011)`, async () => {
        await cdp().send('Emulation.setEmulatedMedia', {
          features: [{ name: 'prefers-color-scheme', value: preferencia }],
        });
        const raiz = await abrir();

        await paraCadaAncho(async (ancho) => {
          expect(desbordaLaPagina(ancho), `${nombre}/${modo} desborda a ${ancho} px`).toBe(false);

          await Promise.resolve();
        });

        // La premisa: hubo una pantalla montada con controles. Sin esto, un montaje que falle en
        // silencio dejaría pasar las tres invariantes por no haber mirado nada.
        expect(tocables(raiz).length, `${nombre} no montó ningún control`).toBeGreaterThan(0);
      });

      it(`mantiene 44 px de alto en todo control tocable (FR-010, FR-025)`, async () => {
        await cdp().send('Emulation.setEmulatedMedia', {
          features: [{ name: 'prefers-color-scheme', value: preferencia }],
        });
        const raiz = await abrir();

        await paraCadaAncho(async (ancho) => {
          const chicos = tocables(raiz)
            .map((control) => ({ control, caja: control.getBoundingClientRect() }))
            .filter(({ caja }) => caja.height < 44)
            .map(
              ({ control, caja }) =>
                `${control.tagName.toLowerCase()} de ${Math.round(caja.height)} px de alto a ${ancho} px`,
            );

          expect(chicos, `${nombre}/${modo}`).toEqual([]);

          await Promise.resolve();
        });
      });

      it(`no deja ningún control fuera de la ventana ni tapado (FR-026)`, async () => {
        await cdp().send('Emulation.setEmulatedMedia', {
          features: [{ name: 'prefers-color-scheme', value: preferencia }],
        });
        const raiz = await abrir();

        await paraCadaAncho(async (ancho) => {
          const inalcanzables = tocables(raiz)
            .map((control) => ({ control, motivo: inalcanzable(control) }))
            .filter(({ motivo }) => motivo !== null)
            .map(
              ({ control, motivo }) => `${control.tagName.toLowerCase()} ${motivo} a ${ancho} px`,
            );

          expect(inalcanzables, `${nombre}/${modo}`).toEqual([]);

          await Promise.resolve();
        });
      });
    });
  }
}

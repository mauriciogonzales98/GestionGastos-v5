import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cdp, page, userEvent } from 'vitest/browser';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';
import { MarcoDeLaApp, type Seccion } from '../src/ui/MarcoDeLaApp';
import { PantallaCategorias } from '../src/categorias/PantallaCategorias';
import { PantallaDashboard } from '../src/dashboard/PantallaDashboard';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { relacionDeContraste } from '../src/ui/contraste';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import { RESUMEN } from './resumen.fixture';
import { desmontar, montar } from './montar';
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
 * US9, FR-017 a FR-020 — **el modo oscuro, medido en un navegador de verdad**.
 *
 * Es la única forma de verificar esta historia. El modo lo elige el navegador al evaluar
 * `prefers-color-scheme` (D-03), así que un DOM simulado no puede decir nada: no tiene preferencia
 * que emular ni resuelve una consulta de medios. Lo que acá se emula es la preferencia del
 * dispositivo —con el protocolo de Chromium, igual que lo haría el sistema operativo— y lo que se
 * mide es el color que el navegador terminó pintando.
 *
 * **Las expectativas no son colores escritos a mano.** Se leen de la hoja de estilos ya cargada en
 * la página: el modo claro de `:root`, el oscuro del bloque `@media (prefers-color-scheme: dark)`.
 * Así la prueba afirma lo que importa —que la pantalla use el valor del modo que corresponde— y no
 * se vuelve una copia de la paleta que hay que editar cada vez que un color cambia. Los umbrales de
 * contraste de esos valores son otra prueba: `tests/Paleta.test.ts`, que mide los dos modos.
 */

const ALTO = 900;

/** Lo que la persona tiene configurado en su dispositivo. */
type Preferencia = 'light' | 'dark';

/**
 * Emula la preferencia de esquema de color del dispositivo.
 *
 * Va por el protocolo de Chromium (`Emulation.setEmulatedMedia`) porque es lo que cambia la
 * preferencia **del navegador**, que es quien tiene que reaccionar. Pisar la consulta de medios
 * desde el CSS o desde JavaScript probaría otra cosa: que el CSS que escribimos se aplica cuando lo
 * forzamos, no que el navegador lo elija solo.
 */
async function preferir(preferencia: Preferencia): Promise<void> {
  await cdp().send('Emulation.setEmulatedMedia', {
    features: [{ name: 'prefers-color-scheme', value: preferencia }],
  });
}

/** Deja al navegador sin preferencia emulada, para que un archivo no le cambie el modo al siguiente. */
async function dejarDePreferir(): Promise<void> {
  await cdp().send('Emulation.setEmulatedMedia', { features: [] });
}

/** Los dos modos de Lila, con el nombre que usa la paleta y el que entiende el navegador. */
const MODOS = [
  { modo: 'claro', preferencia: 'light' },
  { modo: 'oscuro', preferencia: 'dark' },
] as const;

/**
 * Las hojas de estilo de la página cuyas reglas se pueden leer.
 *
 * Una hoja de otro origen lanza al tocar `cssRules`; acá no debería haber ninguna, pero filtrarlas
 * cuesta tres líneas y evita que la prueba falle por algo que no es su tema.
 */
function hojasLegibles(): CSSStyleSheet[] {
  return [...document.styleSheets].filter((hoja) => {
    try {
      return hoja.cssRules.length >= 0;
    } catch {
      return false;
    }
  });
}

/** Cada regla de la página, entrando a los bloques `@media` y compañía, con su condición al lado. */
function reglasDeLaPagina(): { regla: CSSStyleRule; condiciones: string[] }[] {
  const encontradas: { regla: CSSStyleRule; condiciones: string[] }[] = [];

  const recorrer = (reglas: CSSRuleList, condiciones: string[]): void => {
    for (const regla of reglas) {
      if (regla instanceof CSSStyleRule) {
        encontradas.push({ regla, condiciones });
      } else if (regla instanceof CSSGroupingRule) {
        // `conditionText` es de `CSSConditionRule` —`@media` y `@supports`— y no de todo grupo:
        // un `@layer` agrupa sin condicionar nada. Se entra igual a sus reglas, con la condición
        // vacía, que es lo que significa "este bloque no acota por nada".
        const condicion = regla instanceof CSSConditionRule ? regla.conditionText : '';

        recorrer(regla.cssRules, [...condiciones, condicion]);
      }
    }
  };

  for (const hoja of hojasLegibles()) {
    recorrer(hoja.cssRules, []);
  }

  return encontradas;
}

/** ¿Esta condición de `@media` es la que enciende el modo oscuro? */
const esCondicionOscura = (condicion: string): boolean =>
  /prefers-color-scheme\s*:\s*dark/.test(condicion);

/**
 * La paleta declarada de un modo, leída de la hoja de estilos de la página.
 *
 * El modo claro es el `:root` de nivel superior. El oscuro es ése **con el bloque oscuro encima**,
 * que es lo que el navegador aplica: el bloque oscuro sólo tiene que redefinir lo que cambia. Es la
 * misma resolución que hace `coloresPorModo` de `tests/fuentes.ts` leyendo el archivo; acá se lee de
 * la página porque es la página la que se mide.
 */
function paletaDeclarada(modo: 'claro' | 'oscuro'): Record<string, string> {
  const paleta: Record<string, string> = {};

  for (const { regla, condiciones } of reglasDeLaPagina()) {
    if (regla.selectorText !== ':root') {
      continue;
    }

    const oscura = condiciones.some(esCondicionOscura);

    // Una regla de otro modo no cuenta; una sin condición cuenta para los dos.
    if (oscura && modo === 'claro') {
      continue;
    }
    if (condiciones.length > 0 && !oscura) {
      continue;
    }
    if (oscura && modo !== 'oscuro') {
      continue;
    }

    for (const propiedad of regla.style) {
      if (propiedad.startsWith('--color-')) {
        paleta[propiedad] = regla.style.getPropertyValue(propiedad).trim();
      }
    }
  }

  return paleta;
}

/** El valor que un token tiene en un modo, normalizado a la forma en que el navegador lo informa. */
function token(modo: 'claro' | 'oscuro', nombre: string): string {
  const declarado = paletaDeclarada(modo)[nombre];

  if (!declarado) {
    throw new Error(`La paleta ${modo} no declara ${nombre}.`);
  }

  return normalizar(declarado);
}

/**
 * El mismo color, en la forma en que `getComputedStyle` lo devuelve.
 *
 * La hoja declara `#16141b` y el navegador informa `rgb(22, 20, 27)`. Comparar las dos formas pide
 * normalizar una, y la forma honesta de normalizar un color es dejar que lo haga el navegador: se le
 * pide pintar un elemento con el valor declarado y se lee qué entendió.
 */
function normalizar(color: string): string {
  const sonda = document.createElement('div');
  sonda.style.backgroundColor = color;
  document.body.appendChild(sonda);

  const normalizado = getComputedStyle(sonda).backgroundColor;
  sonda.remove();

  return normalizado;
}

const NEGRO = '#000000';

/**
 * Los canales de un color informado por el navegador, o `null` si es transparente.
 *
 * Lanza ante una forma que no conoce, en lugar de devolver un valor por defecto: un color que no se
 * puede leer es un color **sin verificar**, y saltearlo en silencio dejaría pasar justo lo que esta
 * prueba busca. Es la misma decisión que toma `ui/contraste.ts`.
 */
function opaco(color: string): string | null {
  const rgba = /^rgba?\(([^)]+)\)$/.exec(color);

  if (rgba) {
    const partes = rgba[1].split(/[,/]/).map((parte) => parte.trim());

    if (partes.length === 4 && Number(partes[3]) === 0) {
      return null;
    }

    return `rgb(${partes.slice(0, 3).join(', ')})`;
  }

  // `color-mix` computa a esta forma en Chromium: los canales van de 0 a 1.
  const srgb = /^color\(srgb\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)/.exec(color);

  if (srgb) {
    const canales = srgb.slice(1, 4).map((canal) => Math.round(Number(canal) * 255));

    return `rgb(${canales.join(', ')})`;
  }

  throw new Error(`No se puede interpretar el color "${color}" que informó el navegador.`);
}

/**
 * ¿`color` es más claro que `limite`?
 *
 * Se compara el contraste de cada uno contra el negro, que crece con la luminancia: así la
 * comparación reusa la cuenta de WCAG que ya existe en lugar de traer una segunda implementación de
 * la luminancia relativa, que es la clase de duplicado que se desincroniza.
 */
function masClaroQue(color: string, limite: string): boolean {
  return relacionDeContraste(color, NEGRO) > relacionDeContraste(limite, NEGRO);
}

/**
 * Las superficies claras que una pantalla pinta estando el navegador en oscuro.
 *
 * **El límite es el acento**, que es lo más claro que el modo oscuro pinta de fondo: encima de él no
 * hay ningún color de la paleta oscura, así que un fondo más claro que eso no salió de Lila. Es la
 * forma de detectar un `#fff` escrito a mano —la causa típica de que una pantalla se vea clara en
 * modo oscuro— sin tener que enumerar los colores permitidos, que sería una lista que alguien tiene
 * que acordarse de actualizar.
 */
function superficiesClaras(raiz: HTMLElement): string[] {
  const limite = token('oscuro', '--color-acento');

  return [...raiz.querySelectorAll<HTMLElement>('*'), raiz]
    .map((elemento) => ({ elemento, fondo: opaco(getComputedStyle(elemento).backgroundColor) }))
    .filter(({ fondo }) => fondo !== null && masClaroQue(fondo, limite))
    .map(
      ({ elemento, fondo }) =>
        `${elemento.tagName.toLowerCase()}${elemento.className ? `.${String(elemento.className).split(' ').join('.')}` : ''} con fondo ${fondo}`,
    );
}

/** Un movimiento cualquiera, para que el listado tenga una fila y su contenedor desplazable exista. */
const MOVIMIENTOS: Movimiento[] = [
  {
    id: 1,
    tipo: 'gasto',
    monto: 1234.5,
    categoriaId: CATEGORIAS[0].id,
    categoriaNombre: CATEGORIAS[0].nombre,
    monedaCodigo: MONEDAS[0].codigo,
    fecha: '2026-10-01',
    nota: 'Una nota',
  },
];

/** Las tres pantallas con sesión, cada una con lo mínimo para montarse. */
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

async function abrirAcceso(): Promise<HTMLElement> {
  await page.viewport(1440, ALTO);

  return montar(<FormularioAcceso onEntrar={() => {}} />);
}

async function abrirConSesion(seccion: Seccion, contenido: React.ReactNode): Promise<HTMLElement> {
  await page.viewport(1440, ALTO);

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

beforeEach(() => {
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue(MOVIMIENTOS);
});

afterEach(async () => {
  await desmontar();
  await dejarDePreferir();
});

describe('FR-017 · la pantalla de acceso sigue la preferencia del dispositivo', () => {
  it.each(MODOS)(
    'con la preferencia en $preferencia pinta el fondo del modo $modo (US9:AC1, US9:AC2)',
    async ({ modo, preferencia }) => {
      await preferir(preferencia);
      await abrirAcceso();

      expect(getComputedStyle(document.body).backgroundColor).toBe(token(modo, '--color-fondo'));
      expect(getComputedStyle(document.body).color).toBe(token(modo, '--color-texto'));
    },
  );

  it('no deja ninguna superficie clara en la pantalla de acceso (US9:AC1)', async () => {
    await preferir('dark');
    const raiz = await abrirAcceso();

    expect(superficiesClaras(raiz)).toEqual([]);
  });
});

/**
 * `AC4` — las otras tres pantallas.
 *
 * El fondo del `<body>` no depende de qué pantalla esté montada, así que comprobar sólo eso en cada
 * una sería repetir la prueba de arriba tres veces. Lo que cada pantalla puede romper por su cuenta
 * es **pintar una superficie propia**: un color fuera de token en una tarjeta, una fila o un riel se
 * ve claro en medio de la pantalla oscura, y es lo único que distingue a esta prueba de la anterior.
 */
describe('FR-017 · las pantallas con sesión también se muestran en oscuro (US9:AC4)', () => {
  for (const { seccion, nombre, contenido } of PANTALLAS) {
    it(`${nombre} se pinta entera con la paleta oscura (FR-017, US9:AC4)`, async () => {
      await preferir('dark');
      const raiz = await abrirConSesion(seccion, contenido());

      expect(getComputedStyle(document.body).backgroundColor).toBe(
        token('oscuro', '--color-fondo'),
      );
      expect(superficiesClaras(raiz)).toEqual([]);
    });
  }
});

/**
 * `FR-018`, `AC3` — **la prueba de que el cambio lo hace el navegador y no React**.
 *
 * Que los colores cambien no alcanza: también cambiarían si al cambiar la preferencia la app se
 * remontara con otro tema. Lo que se afirma acá es que **el mismo nodo** sigue en el documento con
 * el mismo valor escrito: con JavaScript eligiendo el tema, el componente se remontaría y el campo
 * quedaría vacío.
 */
describe('FR-018 · cambiar la preferencia con la app abierta', () => {
  it('cambia los colores sin perder lo escrito ni remontar el campo (US9:AC3)', async () => {
    await preferir('light');
    const raiz = await abrirAcceso();

    const email = raiz.querySelector<HTMLInputElement>('input[type="email"]')!;
    await userEvent.fill(email, 'ana@ejemplo.com');

    expect(getComputedStyle(document.body).backgroundColor).toBe(token('claro', '--color-fondo'));

    await preferir('dark');

    expect(getComputedStyle(document.body).backgroundColor).toBe(token('oscuro', '--color-fondo'));

    // El mismo nodo, todavía en el documento, con lo que la persona había escrito.
    expect(raiz.querySelector('input[type="email"]')).toBe(email);
    expect(email.isConnected).toBe(true);
    expect(email.value).toBe('ana@ejemplo.com');
  });
});

/**
 * `FR-020` — los controles que dibuja el navegador.
 *
 * Es el caso borde que la spec nombra: el selector de fecha blanco en medio de una pantalla oscura.
 * Lo que hace que no pase es `color-scheme`, y es el único requisito del modo oscuro que no se
 * cumple con declarar la paleta.
 */
describe('FR-020 · los controles del navegador siguen el modo oscuro', () => {
  it('el documento declara el esquema oscuro al navegador', async () => {
    await preferir('dark');
    await abrirAcceso();

    expect(getComputedStyle(document.documentElement).colorScheme).toContain('dark');
  });

  it('ninguna regla de la hoja pisa el esquema fuera de :root (FR-020)', async () => {
    await abrirAcceso();

    const pisan = reglasDeLaPagina()
      .filter(({ regla }) => regla.style.getPropertyValue('color-scheme') !== '')
      .filter(({ regla }) => regla.selectorText !== ':root')
      .map(({ regla }) => regla.selectorText);

    expect(pisan).toEqual([]);
  });

  it('el selector de fecha, las listas y el contenedor desplazable heredan el esquema', async () => {
    await preferir('dark');
    const raiz = await abrirConSesion('movimientos', PANTALLAS[0].contenido());

    const controles = [
      ...raiz.querySelectorAll<HTMLElement>(
        'input[type="date"], select, .c-listado-movimientos__desborde',
      ),
    ];

    // La premisa de la prueba: si la pantalla dejara de tener estos controles, la prueba estaría
    // pasando sin medir nada.
    expect(controles.length).toBeGreaterThanOrEqual(3);

    const claros = controles
      .map((control) => ({ control, estilo: getComputedStyle(control) }))
      .filter(
        ({ estilo }) =>
          !estilo.colorScheme.includes('dark') ||
          masClaroQue(
            opaco(estilo.backgroundColor) ?? token('oscuro', '--color-fondo'),
            token('oscuro', '--color-acento'),
          ),
      )
      .map(({ control, estilo }) => `${control.tagName.toLowerCase()} con ${estilo.colorScheme}`);

    expect(claros).toEqual([]);
  });
});

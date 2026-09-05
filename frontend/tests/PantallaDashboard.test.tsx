import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaDashboard } from '../src/dashboard/PantallaDashboard';
import { LA_INESPERADA, MONEDAS } from './monedas.fixture';
import { EN_PESOS, RESUMEN, SIN_MOVIMIENTOS } from './resumen.fixture';

vi.mock('../src/api/cliente', () => ({
  obtenerResumen: vi.fn(),
  ErrorDeSesion: class ErrorDeSesion extends Error {},
  ErrorDeValidacion: class ErrorDeValidacion extends Error {
    constructor(readonly errores: Record<string, string[]>) {
      super('rechazada');
    }
  },
}));

const cliente = await import('../src/api/cliente');

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
});

function renderizar(monedas = MONEDAS) {
  render(<PantallaDashboard monedas={monedas} onVolver={() => {}} onSesionVencida={() => {}} />);
}

describe('PantallaDashboard', () => {
  it('pide el resumen y lo pinta, con una sección por cada moneda que llegó', async () => {
    renderizar();

    await screen.findByRole('region', { name: /resumen del período/i });

    for (const moneda of RESUMEN.monedas) {
      expect(screen.getByRole('region', { name: new RegExp(moneda.monedaCodigo) })).toBeVisible();
    }
  });

  /**
   * El período se titula con lo que **vino del servidor**.
   *
   * `desde` y `hasta` viajan siempre desde la feature 006 justamente para esto, y ésta es la
   * primera pantalla que los usa: sin ellos habría que calcular el mes en curso en la zona horaria
   * del navegador, y volverían a existir dos criterios de "hoy".
   */
  it('titula el período con el desde y el hasta que devolvió el servidor', async () => {
    renderizar();

    expect(await screen.findByText(/2026-09-01/)).toBeVisible();
    expect(screen.getByText(/2026-09-30/)).toBeVisible();
  });

  /**
   * Sin rango elegido, el dashboard **no pide ningún período**: lo decide el servidor.
   *
   * Los dos extremos van vacíos, y `obtenerResumen` traduce eso a una URL sin query string — está
   * probado en `cliente.test.ts`, que es donde vive esa traducción. Acá se verifica el otro lado de
   * la costura: que la pantalla no invente un mes ni mande medio rango.
   */
  it('sin rango elegido no pide ningún período: lo decide el servidor', async () => {
    renderizar();

    await screen.findByRole('region', { name: /resumen del período/i });
    expect(vi.mocked(cliente.obtenerResumen).mock.calls[0]).toEqual(['', '']);
  });

  it('si el resumen falla lo dice, y no muestra ceros', async () => {
    vi.mocked(cliente.obtenerResumen).mockRejectedValue(new Error('sin red'));

    renderizar();

    expect(await screen.findByRole('alert')).toHaveTextContent(/no se pudo cargar/i);
    expect(screen.queryByRole('region', { name: /resumen del período/i })).not.toBeInTheDocument();
  });

  it('un 401 vuelve al acceso en vez de mostrar un error de carga', async () => {
    const alVencer = vi.fn();
    vi.mocked(cliente.obtenerResumen).mockRejectedValue(new cliente.ErrorDeSesion());

    render(<PantallaDashboard monedas={MONEDAS} onVolver={() => {}} onSesionVencida={alVencer} />);

    await vi.waitFor(() => expect(alVencer).toHaveBeenCalled());
    expect(screen.queryByText(/no se pudo cargar/i)).not.toBeInTheDocument();
  });
});

/**
 * FR-004, FR-005 y D-08/D-09: el período que la persona elige.
 *
 * Es lo que distingue al dashboard del resumen de la pantalla principal: aquél está clavado al mes
 * calendario por decisión de FEAT-001c, y éste es el lugar donde uno elige qué mirar.
 */
describe('PantallaDashboard — el período', () => {
  async function elegirRango(desde: string, hasta: string) {
    const usuario = userEvent.setup();

    await usuario.clear(screen.getByLabelText(/desde/i));
    await usuario.type(screen.getByLabelText(/desde/i), desde);
    await usuario.clear(screen.getByLabelText(/hasta/i));
    await usuario.type(screen.getByLabelText(/hasta/i), hasta);
    await usuario.click(screen.getByRole('button', { name: /aplicar/i }));
  }

  it('elegir un rango vuelve a pedir el resumen con ese desde y ese hasta FR-004', async () => {
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    await elegirRango('2026-08-01', '2026-08-31');

    await waitFor(() =>
      expect(cliente.obtenerResumen).toHaveBeenCalledWith('2026-08-01', '2026-08-31'),
    );
  });

  /**
   * FR-005: la pantalla NO valida el rango por su cuenta.
   *
   * `PeriodoPedido` lleva escrito que es "el único intérprete de desde y hasta", y que con dos
   * intérpretes la igualdad entre vistas depende de que nadie toque uno sin tocar el otro. Así que
   * el rango inválido se manda y el mensaje que se muestra es **el del servidor**.
   */
  it('un rango inválido se manda igual: el único intérprete es el servidor D-08', async () => {
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    vi.mocked(cliente.obtenerResumen).mockRejectedValueOnce(
      new cliente.ErrorDeValidacion({
        rango: ['La fecha de inicio no puede ser posterior a la de fin.'],
      }),
    );

    await elegirRango('2026-09-30', '2026-09-01');

    await waitFor(() =>
      expect(cliente.obtenerResumen).toHaveBeenCalledWith('2026-09-30', '2026-09-01'),
    );
  });

  it('el mensaje del rechazo se muestra junto al control del período FR-005', async () => {
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    vi.mocked(cliente.obtenerResumen).mockRejectedValueOnce(
      new cliente.ErrorDeValidacion({
        rango: ['La fecha de inicio no puede ser posterior a la de fin.'],
      }),
    );

    await elegirRango('2026-09-30', '2026-09-01');

    expect(await screen.findByText(/la fecha de inicio no puede ser posterior/i)).toBeVisible();
  });

  /**
   * **Los totales que estaban a la vista siguen ahí.**
   *
   * Un vacío se leería como "no hay nada" y escondería que la pregunta estaba mal formada — que es
   * el mismo motivo por el que el servidor rechaza un rango invertido en vez de devolver una lista
   * vacía.
   */
  it('un rango rechazado no borra los totales que estaban a la vista FR-005', async () => {
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    vi.mocked(cliente.obtenerResumen).mockRejectedValueOnce(
      new cliente.ErrorDeValidacion({ rango: ['Indicá las dos fechas del rango, o ninguna.'] }),
    );

    await elegirRango('2026-09-30', '2026-09-01');
    await screen.findByText(/indicá las dos fechas/i);

    expect(screen.getByRole('region', { name: /resumen del período/i })).toBeVisible();
  });

  /**
   * **La carrera**: dos cambios de rango seguidos, con las respuestas llegando al revés.
   *
   * Es la cicatriz `22e3e96` de la feature 009, y acá la ventana es más ancha: un rango de un año
   * sobre 10000 movimientos tarda más que un acotado del listado. Sin la guarda, el dashboard
   * termina mostrando el período que se pidió antes, con el control diciendo otra cosa — sin error
   * y sin nada en la consola.
   */
  it('la respuesta de un rango viejo no pisa a la del vigente D-09', async () => {
    const viejo = { ...RESUMEN, desde: '2026-01-01', hasta: '2026-01-31' };
    const nuevo = { ...RESUMEN, desde: '2026-07-01', hasta: '2026-07-31' };

    let resolverViejo: (r: typeof viejo) => void = () => {};
    const promesaVieja = new Promise<typeof viejo>((r) => {
      resolverViejo = r;
    });

    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    vi.mocked(cliente.obtenerResumen).mockReturnValueOnce(promesaVieja);
    await elegirRango('2026-01-01', '2026-01-31');

    vi.mocked(cliente.obtenerResumen).mockResolvedValueOnce(nuevo);
    await elegirRango('2026-07-01', '2026-07-31');
    await screen.findByText(/2026-07-31/);

    // Y AHORA llega la vieja, tarde.
    resolverViejo(viejo);

    await waitFor(() => expect(screen.getByText(/2026-07-31/)).toBeVisible());
    expect(screen.queryByText(/2026-01-31/)).not.toBeInTheDocument();
  });
});

/**
 * FR-006, FR-007 y D-05: mirar una sola moneda. Salda la deuda **D9-02**.
 *
 * **El filtro es de presentación**: elige cuál de los bloques que el servidor ya devuelve separados
 * se mira. `PRD:RF-29` prohíbe que un total mezcle monedas, así que ningún número depende de qué
 * monedas se pidan — filtrar es mostrar menos de lo que ya llegó, no calcular otra cosa.
 */
describe('PantallaDashboard — el acotado por moneda', () => {
  const idDelSelector = /ver sólo la moneda/i;

  it('ofrece las monedas del catálogo más la opción de no acotar FR-006', async () => {
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    const selector = screen.getByLabelText(idDelSelector);

    // Una opción por moneda del catálogo MÁS la de "todas". Se cuenta contra el catálogo y no
    // contra un número escrito: `verificar-monedas.sh` corre la suite con una moneda de más.
    expect(within(selector).getAllByRole('option')).toHaveLength(MONEDAS.length + 1);
  });

  /**
   * FR-007: **una moneda que ninguna línea de código conoce aparece igual.**
   *
   * Es el caso que se pone en rojo el día que alguien escriba `['ARS', 'USD']` en el frontend, y el
   * que sostiene de este lado la promesa que `verificar-monedas.sh` protege del otro: sumar una
   * moneda al catálogo cuesta 0 líneas.
   */
  it('una moneda agregada sólo como dato aparece en el selector FR-007', async () => {
    renderizar([...MONEDAS, LA_INESPERADA]);
    await screen.findByRole('region', { name: /resumen del período/i });

    expect(
      within(screen.getByLabelText(idDelSelector)).getByRole('option', {
        name: new RegExp(LA_INESPERADA.codigo),
      }),
    ).toBeInTheDocument();
  });

  it('sin elegir ninguna se ven todas las monedas FR-006', async () => {
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    for (const moneda of RESUMEN.monedas) {
      expect(screen.getByRole('region', { name: new RegExp(moneda.monedaCodigo) })).toBeVisible();
    }
  });

  it('elegir una moneda muestra sólo esa FR-006', async () => {
    const usuario = userEvent.setup();
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    await usuario.selectOptions(screen.getByLabelText(idDelSelector), String(EN_PESOS.monedaId));

    expect(screen.getByRole('region', { name: new RegExp(EN_PESOS.monedaCodigo) })).toBeVisible();
    expect(
      screen.queryByRole('region', { name: new RegExp(SIN_MOVIMIENTOS.monedaCodigo) }),
    ).not.toBeInTheDocument();
  });

  /**
   * **Filtrar recorta lo que se ve, nunca lo que se calcula** (FR-013).
   *
   * Los números de una moneda son idénticos esté o no aplicado el filtro, y no pueden no serlo: son
   * los mismos que ya habían llegado.
   */
  it('los totales de una moneda son los mismos con y sin filtro FR-013', async () => {
    const usuario = userEvent.setup();
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });

    const sinFiltro = screen.getByRole('region', {
      name: new RegExp(EN_PESOS.monedaCodigo),
    }).textContent;

    await usuario.selectOptions(screen.getByLabelText(idDelSelector), String(EN_PESOS.monedaId));

    expect(
      screen.getByRole('region', { name: new RegExp(EN_PESOS.monedaCodigo) }).textContent,
    ).toBe(sinFiltro);
  });

  /**
   * **Cambiar de moneda no dispara ninguna petición** (D-05).
   *
   * Es la mitad que prueba que el filtro es de presentación: si pidiera de nuevo, sería la otra
   * decisión — la que habría que discutir contra la garantía que la feature 009 blindó en el
   * servidor, y que dice que el resumen informa sobre TODAS las monedas del catálogo, siempre.
   */
  it('cambiar de moneda no vuelve a pedir nada al servidor D-05', async () => {
    const usuario = userEvent.setup();
    renderizar();
    await screen.findByRole('region', { name: /resumen del período/i });
    const pedidosAntes = vi.mocked(cliente.obtenerResumen).mock.calls.length;

    await usuario.selectOptions(screen.getByLabelText(idDelSelector), String(EN_PESOS.monedaId));
    await usuario.selectOptions(screen.getByLabelText(idDelSelector), '');

    expect(vi.mocked(cliente.obtenerResumen).mock.calls).toHaveLength(pedidosAntes);
  });
});

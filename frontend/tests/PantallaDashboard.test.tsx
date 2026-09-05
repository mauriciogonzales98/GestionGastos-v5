import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaDashboard } from '../src/dashboard/PantallaDashboard';
import { RESUMEN } from './resumen.fixture';

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

function renderizar() {
  render(<PantallaDashboard onVolver={() => {}} onSesionVencida={() => {}} />);
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

    render(<PantallaDashboard onVolver={() => {}} onSesionVencida={alVencer} />);

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

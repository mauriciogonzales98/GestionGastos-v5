import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaDashboard } from '../src/dashboard/PantallaDashboard';
import { RESUMEN } from './resumen.fixture';

vi.mock('../src/api/cliente', () => ({
  obtenerResumen: vi.fn(),
  ErrorDeSesion: class ErrorDeSesion extends Error {},
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

  it('sin rango elegido lo pide sin parámetros: el período por omisión lo decide el servidor', async () => {
    renderizar();

    await screen.findByRole('region', { name: /resumen del período/i });
    expect(vi.mocked(cliente.obtenerResumen).mock.calls[0]).toEqual([]);
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

import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { ErrorDeRed, ErrorDelServidor } from '../src/api/cliente';
import { CATEGORIAS } from './categorias.fixture';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');
  return {
    ...real,
    obtenerCategorias: vi.fn(),
    obtenerMovimientos: vi.fn(),
    crearMovimiento: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

beforeEach(() => {
  vi.clearAllMocks();
});

/**
 * Lo que pasa cuando la API no está. Hasta ahora: nada. Las dos promesas rechazaban sin `.catch`,
 * el indicador de carga se quedaba encendido para siempre y el selector de categorías vacío, así
 * que guardar era imposible y la única señal era un unhandled rejection en la consola.
 */
describe('carga inicial que falla', () => {
  it('con el backend caído muestra el error y deja de decir que está cargando', async () => {
    vi.mocked(cliente.obtenerCategorias).mockRejectedValue(new ErrorDeRed(new TypeError('nope')));
    vi.mocked(cliente.obtenerMovimientos).mockRejectedValue(new ErrorDeRed(new TypeError('nope')));

    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        email="ana@ejemplo.com"
        onCerrarSesion={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    const aviso = await screen.findByRole('alert');

    // Las dos cargas fallan y cuál gana depende de cuál rechace última, así que se afirma sobre lo
    // que ambos mensajes comparten. Afirmar sobre una redacción sola sería un test que pasa por
    // orden de llegada.
    expect(aviso).toHaveTextContent(/recargá la página/i);

    // Y sobre todo: el indicador de carga se apaga. Quedarse encendido es mentirle a la persona.
    await waitFor(() =>
      expect(screen.queryByText(/cargando movimientos/i)).not.toBeInTheDocument(),
    );
  });

  it('si falla el listado pero no el catálogo, el formulario sigue siendo usable', async () => {
    vi.mocked(cliente.obtenerCategorias).mockResolvedValue(CATEGORIAS);
    vi.mocked(cliente.obtenerMovimientos).mockRejectedValue(new ErrorDelServidor(500, 'boom'));

    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        email="ana@ejemplo.com"
        onCerrarSesion={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    await screen.findByRole('alert');

    // El catálogo llegó, así que se puede cargar un movimiento aunque el listado no esté.
    await waitFor(() =>
      expect(screen.getByLabelText('Categoría')).toContainHTML('<option value="1">Comida</option>'),
    );
    expect(screen.getByRole('button', { name: 'Registrar' })).toBeEnabled();
  });

  it('si falla el catálogo, lo dice en vez de ofrecer un selector vacío', async () => {
    vi.mocked(cliente.obtenerCategorias).mockRejectedValue(new ErrorDelServidor(500, 'boom'));
    vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([]);

    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        email="ana@ejemplo.com"
        onCerrarSesion={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/categor/i);
  });
});

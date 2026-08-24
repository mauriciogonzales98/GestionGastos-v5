import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';

vi.mock('../src/api/cliente', () => ({
  obtenerCategorias: vi.fn(),
  obtenerMovimientos: vi.fn(),
  crearMovimiento: vi.fn(),
}));

const cliente = await import('../src/api/cliente');

const HOY = '2026-08-23';

const DEL_20: Movimiento = {
  id: 5,
  tipo: 'gasto',
  monto: 300,
  categoriaId: 2,
  categoriaNombre: 'Transporte',
  monedaCodigo: 'ARS',
  fecha: '2026-08-20',
};

const DEL_10: Movimiento = {
  id: 3,
  tipo: 'gasto',
  monto: 100,
  categoriaId: 1,
  categoriaNombre: 'Comida',
  monedaCodigo: 'ARS',
  fecha: '2026-08-10',
};

beforeEach(() => {
  // clearAllMocks y no sólo reset del que interesa: los contadores de llamadas se acumulan entre
  // tests, y un "se llamó una vez" que en realidad cuenta las corridas anteriores no verifica nada.
  vi.clearAllMocks();
  vi.mocked(cliente.obtenerCategorias).mockResolvedValue(CATEGORIAS);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([DEL_20, DEL_10]);
});

async function renderizar() {
  render(<PantallaMovimientos hoy={HOY} />);
  await screen.findByRole('table');
}

function fechasDelListado() {
  return screen
    .getAllByRole('row')
    .slice(1)
    .map((f) => within(f).getAllByRole('cell')[0].textContent);
}

describe('PantallaMovimientos', () => {
  it('muestra formulario y listado en una sola pantalla FR-013', async () => {
    await renderizar();

    expect(screen.getByRole('heading', { level: 1, name: 'Mis movimientos' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Registrar' })).toBeInTheDocument();
    expect(screen.getByRole('table')).toBeInTheDocument();
  });

  // AC-15 + FR-014: el movimiento guardado aparece en el listado, en su posición.
  it('inserta el movimiento guardado en su posición del orden AC-15 FR-014', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({
      id: 9,
      tipo: 'gasto',
      monto: 1250.5,
      categoriaId: 1,
      categoriaNombre: 'Comida',
      monedaCodigo: 'ARS',
      fecha: '2026-08-15',
    });
    await renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '1250.50');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.clear(screen.getByLabelText('Fecha'));
    await usuario.type(screen.getByLabelText('Fecha'), '2026-08-15');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    // Entre el 20 y el 10, no al final ni recargando la lista entera.
    await waitFor(() =>
      expect(fechasDelListado()).toEqual(['2026-08-20', '2026-08-15', '2026-08-10']),
    );
    expect(cliente.obtenerMovimientos).toHaveBeenCalledTimes(1);
  });

  it('tras guardar vacía el formulario y devuelve el foco al primer campo FR-014', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...DEL_10, id: 9, fecha: '2026-08-23' });
    await renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    await waitFor(() => expect(screen.getByLabelText('Monto')).toHaveValue(null));
    expect(screen.getByLabelText('Categoría')).toHaveValue('');
    expect(screen.getByLabelText('Fecha')).toHaveValue(HOY);
    expect(screen.getByRole('radio', { name: 'Gasto' })).toBeChecked();

    // El foco vuelve al primer campo: es lo que permite encadenar cargas sin tocar el mouse.
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'Gasto' }));
  });

  it('confirma el guardado de un movimiento del mes', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...DEL_10, id: 9, fecha: '2026-08-15' });
    await renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const confirmacion = await screen.findByRole('status');
    expect(confirmacion).toHaveTextContent(/registrado/i);
  });

  it('un movimiento fuera del mes se guarda, no aparece en el listado y la confirmación lo dice', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...DEL_10, id: 9, fecha: '2026-05-04' });
    await renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.clear(screen.getByLabelText('Fecha'));
    await usuario.type(screen.getByLabelText('Fecha'), '2026-05-04');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const confirmacion = await screen.findByRole('status');

    // Se guardó: decir sólo "no aparece" haría creer que se perdió.
    expect(confirmacion).toHaveTextContent(/registrado/i);
    expect(confirmacion).toHaveTextContent(/no aparece en el listado/i);
    expect(confirmacion).toHaveTextContent(/2026-05-04/);

    // Y efectivamente no está en el listado del mes.
    expect(fechasDelListado()).toEqual(['2026-08-20', '2026-08-10']);
  });
});

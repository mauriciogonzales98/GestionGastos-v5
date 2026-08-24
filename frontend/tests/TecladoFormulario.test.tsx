import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { CATEGORIAS } from './categorias.fixture';

vi.mock('../src/api/cliente', () => ({
  obtenerCategorias: vi.fn(),
  obtenerMovimientos: vi.fn(),
  crearMovimiento: vi.fn(),
}));

const cliente = await import('../src/api/cliente');

beforeEach(() => {
  vi.mocked(cliente.obtenerCategorias).mockResolvedValue(CATEGORIAS);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([]);
  vi.mocked(cliente.crearMovimiento).mockResolvedValue({
    id: 1,
    tipo: 'gasto',
    monto: 800,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'ARS',
    fecha: '2026-08-23',
  });
});

/**
 * AC-55 (RF-15): el formulario se recorre, se completa y se envía íntegramente con el teclado.
 *
 * Sin mouse en ningún paso: ni un click. Si algún control quedara fuera del orden de tabulación o
 * el envío con Enter dejara de funcionar, este test es lo único que se entera.
 */
describe('AC-55 — el formulario se usa entero con el teclado', () => {
  it('se recorre con Tab y se envía con Enter, sin usar el mouse AC-55', async () => {
    const usuario = userEvent.setup();
    render(<PantallaMovimientos hoy="2026-08-23" />);
    await screen.findByRole('button', { name: 'Registrar' });

    // El orden del DOM es el orden de tabulación: no hay tabindex positivo que lo altere.
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'Gasto' }));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Monto'));
    await usuario.keyboard('800');

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Categoría'));
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Fecha'));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Registrar' }));

    // Enter sobre el botón enviado con el teclado.
    await usuario.keyboard('{Enter}');

    expect(cliente.crearMovimiento).toHaveBeenCalledWith(
      expect.objectContaining({ tipo: 'gasto', monto: 800, categoriaId: 1 }),
    );
  });
});

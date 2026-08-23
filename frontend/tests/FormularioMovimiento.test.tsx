import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { FormularioMovimiento } from '../src/movimientos/FormularioMovimiento';
import { CATEGORIAS } from './categorias.fixture';

const HOY = '2026-08-23';

function renderizar(onGuardar = vi.fn()) {
  render(<FormularioMovimiento categorias={CATEGORIAS} hoy={HOY} onGuardar={onGuardar} />);
  return onGuardar;
}

describe('FormularioMovimiento', () => {
  it('arranca en gasto y con la fecha de hoy puesta', () => {
    renderizar();

    // `gasto` viene marcado de entrada: es el caso mayoritario y el que el PRD nombra primero.
    expect(screen.getByRole('radio', { name: 'Gasto' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Ingreso' })).not.toBeChecked();
    expect(screen.getByLabelText('Fecha')).toHaveValue(HOY);
  });

  // AC-10: el selector de gasto no muestra ninguna categoría de ingreso, y viceversa.
  it('ofrece las 7 categorías de gasto y ninguna de ingreso AC-10', () => {
    renderizar();

    const selector = screen.getByLabelText('Categoría');
    const opciones = within(selector).getAllByRole('option');
    const nombres = opciones.map((o) => o.textContent);

    expect(nombres).toEqual([
      'Elegí una categoría',
      'Comida',
      'Transporte',
      'Vivienda',
      'Servicios',
      'Salud',
      'Ocio',
      'Otros',
    ]);
    expect(nombres).not.toContain('Sueldo');
    expect(nombres).not.toContain('Ingreso extra');
  });

  it('al pasar a ingreso repuebla el selector y limpia la selección anterior AC-10', async () => {
    const usuario = userEvent.setup();
    renderizar();

    const selector = screen.getByLabelText('Categoría');
    await usuario.selectOptions(selector, '1');
    expect(selector).toHaveValue('1');

    await usuario.click(screen.getByRole('radio', { name: 'Ingreso' }));

    // Dejar la selección puesta permitiría enviar un ingreso con categoría de gasto, que el
    // servidor rechaza por FR-011. Mejor que la combinación imposible no sea alcanzable.
    expect(selector).toHaveValue('');
    const nombres = within(selector)
      .getAllByRole('option')
      .map((o) => o.textContent);
    expect(nombres).toEqual(['Elegí una categoría', 'Sueldo', 'Ingreso extra', 'Otros']);
  });

  it('es un form real que se envía con un button type=submit', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '1250.50');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(onGuardar).toHaveBeenCalledWith({
      tipo: 'gasto',
      monto: 1250.5,
      categoriaId: 1,
      fecha: HOY,
    });
  });
});

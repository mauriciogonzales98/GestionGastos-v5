import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ListadoMovimientos } from '../src/movimientos/ListadoMovimientos';
import type { Movimiento } from '../src/api/tipos';

const MOVIMIENTOS: Movimiento[] = [
  {
    id: 2,
    tipo: 'ingreso',
    monto: 50000,
    categoriaId: 8,
    categoriaNombre: 'Sueldo',
    monedaCodigo: 'ARS',
    fecha: '2026-08-20',
  },
  {
    id: 1,
    tipo: 'gasto',
    monto: 1250.5,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'ARS',
    fecha: '2026-08-10',
  },
];

describe('ListadoMovimientos', () => {
  it('es una tabla con encabezados de columna, no una grilla de divs', () => {
    render(<ListadoMovimientos movimientos={MOVIMIENTOS} />);

    const tabla = screen.getByRole('table');
    const encabezados = within(tabla).getAllByRole('columnheader');

    expect(encabezados.map((e) => e.textContent)).toEqual(['Fecha', 'Tipo', 'Categoría', 'Monto']);
    // scope="col" es lo que permite a un lector de pantalla anunciar la columna de cada celda.
    encabezados.forEach((e) => expect(e).toHaveAttribute('scope', 'col'));
  });

  it('muestra el tipo como texto y no sólo por color', () => {
    render(<ListadoMovimientos movimientos={MOVIMIENTOS} />);

    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[0]).getByText('Ingreso')).toBeInTheDocument();
    expect(within(filas[1]).getByText('Gasto')).toBeInTheDocument();
  });

  it('muestra categoría y monto de cada movimiento', () => {
    render(<ListadoMovimientos movimientos={MOVIMIENTOS} />);

    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[1]).getByText('Comida')).toBeInTheDocument();
    expect(within(filas[1]).getByText(/1\.250,50/)).toBeInTheDocument();
  });

  it('sin movimientos muestra un mensaje explícito y ninguna tabla FR-012', () => {
    render(<ListadoMovimientos movimientos={[]} />);

    // Un mes sin movimientos no es un error: es un listado vacío con su mensaje.
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText(/no hay movimientos/i)).toBeInTheDocument();
  });
});

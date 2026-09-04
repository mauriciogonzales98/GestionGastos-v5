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

/**
 * Dos gastos del MISMO monto en dos monedas distintas. Es el caso que AC-05 nombra, y el único que
 * distingue "el listado muestra la moneda" de "el listado muestra el monto": con montos distintos,
 * un listado que ignorara la moneda igual se vería bien.
 */
const MISMO_MONTO_DOS_MONEDAS: Movimiento[] = [
  {
    id: 4,
    tipo: 'gasto',
    monto: 100,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'USD',
    fecha: '2026-09-04',
  },
  {
    id: 3,
    tipo: 'gasto',
    monto: 100,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'ARS',
    fecha: '2026-09-04',
  },
];

describe('ListadoMovimientos', () => {
  it('es una tabla con encabezados de columna, no una grilla de divs', () => {
    render(<ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} />);

    const tabla = screen.getByRole('table');
    const encabezados = within(tabla).getAllByRole('columnheader');

    // La última columna no nombra nada visible: aloja el botón de editar de cada fila, que ya se
    // anuncia solo. Su encabezado existe igual —y con `scope`— para que la tabla siga siendo
    // regular para un lector de pantalla.
    expect(encabezados.map((e) => e.textContent)).toEqual([
      'Fecha',
      'Tipo',
      'Categoría',
      'Monto',
      'Moneda',
      'Acciones',
    ]);
    // scope="col" es lo que permite a un lector de pantalla anunciar la columna de cada celda.
    encabezados.forEach((e) => expect(e).toHaveAttribute('scope', 'col'));
  });

  it('muestra el tipo como texto y no sólo por color', () => {
    render(<ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} />);

    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[0]).getByText('Ingreso')).toBeInTheDocument();
    expect(within(filas[1]).getByText('Gasto')).toBeInTheDocument();
  });

  it('muestra categoría y monto de cada movimiento', () => {
    render(<ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} />);

    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[1]).getByText('Comida')).toBeInTheDocument();
    expect(within(filas[1]).getByText(/1\.250,50/)).toBeInTheDocument();
  });

  it('sin movimientos muestra un mensaje explícito y ninguna tabla FR-012', () => {
    render(<ListadoMovimientos movimientos={[]} onEditar={() => {}} />);

    // Un mes sin movimientos no es un error: es un listado vacío con su mensaje.
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText(/no hay movimientos/i)).toBeInTheDocument();
  });

  /**
   * AC-05 y FR-007: cada fila muestra el **código** de su moneda, y dos monedas distintas se ven
   * distintas.
   *
   * **El código va explícito aunque el monto ya se formatee con el símbolo de su moneda**, que es
   * lo que el listado hace desde FEAT-001a. El símbolo lo elige `Intl` a partir del locale, y para
   * dos monedas cualesquiera puede repetirse: con el catálogo abierto a monedas agregadas como
   * dato, apoyar la distinción en el símbolo es apoyarla en algo que nadie controla.
   *
   * Por eso el test busca el CÓDIGO y no el símbolo. Uno que buscara "US$" pasaría hoy sin que la
   * columna existiera, que es exactamente el test que no sirve.
   */
  it('muestra el código de la moneda de cada fila AC-05', () => {
    render(<ListadoMovimientos movimientos={MISMO_MONTO_DOS_MONEDAS} onEditar={() => {}} />);

    const filas = screen.getAllByRole('row').slice(1);
    const codigos = filas.map((f) => within(f).getAllByRole('cell')[4].textContent);

    expect(codigos).toEqual(['USD', 'ARS']);
  });

  /**
   * AC-04 del lado del listado: el código sale del dato del movimiento, no de una tabla de
   * equivalencias escrita en el código.
   *
   * Una moneda agregada al catálogo sólo como dato tiene que verse igual de bien. Es la misma
   * promesa que el selector sostiene en el formulario y que `verificar-monedas.sh` protege en el
   * backend.
   */
  it('muestra el código de una moneda que ninguna constante conoce AC-04', () => {
    const enUnaMonedaNueva: Movimiento[] = [{ ...MISMO_MONTO_DOS_MONEDAS[0], monedaCodigo: 'XCT' }];

    render(<ListadoMovimientos movimientos={enUnaMonedaNueva} onEditar={() => {}} />);

    expect(screen.getByRole('cell', { name: 'XCT' })).toBeInTheDocument();
  });
});

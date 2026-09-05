import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ResumenDelPeriodo } from '../src/resumen/ResumenDelPeriodo';
import { construirMoneda, EN_PESOS, RESUMEN, SIN_MOVIMIENTOS } from './resumen.fixture';

/**
 * FR-002 y FR-009: lo ingresado, lo gastado y el balance de cada moneda.
 *
 * **Ningún caso de acá escribe cuántas monedas hay**: todo sale de `RESUMEN.monedas`. El resumen
 * devuelve una entrada por cada moneda del catálogo, así que un `toHaveLength(2)` se rompería el
 * día que `verificar-monedas.sh` corre la suite con una moneda de más.
 */
describe('ResumenDelPeriodo', () => {
  it('muestra una sección por cada moneda que llegó, con su código', () => {
    render(<ResumenDelPeriodo resumen={RESUMEN} />);

    for (const moneda of RESUMEN.monedas) {
      expect(screen.getByRole('region', { name: new RegExp(moneda.monedaCodigo) })).toBeVisible();
    }
  });

  it('muestra lo ingresado, lo gastado y el balance de una moneda', () => {
    render(<ResumenDelPeriodo resumen={RESUMEN} />);

    const pesos = screen.getByRole('region', { name: new RegExp(EN_PESOS.monedaCodigo) });

    expect(within(pesos).getByText(/100\.000/)).toBeVisible();
    expect(within(pesos).getByText(/140\.000/)).toBeVisible();
  });

  /**
   * Un mes en rojo es un resultado, no un error.
   *
   * Se afirma sobre el signo y no sólo sobre el número: mostrar `40.000` donde el balance es
   * `-40.000` es exactamente el bug que este caso existe para atrapar, y sin el signo el test lo
   * dejaría pasar.
   */
  it('un balance negativo se muestra negativo, no en cero ni como un error', () => {
    render(<ResumenDelPeriodo resumen={RESUMEN} />);

    const pesos = screen.getByRole('region', { name: new RegExp(EN_PESOS.monedaCodigo) });
    const balance = within(pesos).getByTestId('balance');

    expect(balance).toHaveTextContent('-');
    expect(balance).toHaveTextContent(/40\.000/);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  /**
   * FR-009: la moneda del catálogo sin movimientos aparece igual, en cero.
   *
   * El servidor la manda a propósito —compone desde el catálogo y no desde el agregado— y la
   * pantalla no puede esconderla: una moneda que desaparece del resumen se lee como si no
   * existiera en el catálogo.
   */
  it('una moneda sin movimientos aparece con sus totales en cero y sin ningún error', () => {
    render(<ResumenDelPeriodo resumen={RESUMEN} />);

    const sinMovimientos = screen.getByRole('region', {
      name: new RegExp(SIN_MOVIMIENTOS.monedaCodigo),
    });

    expect(within(sinMovimientos).getByTestId('balance')).toHaveTextContent('0');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('el período que muestra es el que vino del servidor, no uno calculado acá', () => {
    render(<ResumenDelPeriodo resumen={RESUMEN} />);

    // `desde` y `hasta` viajan siempre justamente para esto (D-06 de la feature 006): sin ellos, la
    // pantalla tendría que calcular el mes en curso en la zona horaria del navegador, y volverían a
    // existir dos criterios de "hoy".
    expect(screen.getByText(/2026-09-01/)).toBeVisible();
    expect(screen.getByText(/2026-09-30/)).toBeVisible();
  });

  it('un balance en cero con movimientos no se confunde con una moneda sin movimientos', () => {
    const empatada = construirMoneda(EN_PESOS, { balance: 0 });

    render(<ResumenDelPeriodo resumen={{ ...RESUMEN, monedas: [empatada] }} />);

    const region = screen.getByRole('region', { name: new RegExp(empatada.monedaCodigo) });

    expect(within(region).getByTestId('balance')).toHaveTextContent('0');
    // Y sus gastos por categoría siguen estando: el balance en cero no vacía el desglose.
    expect(within(region).getByText(/Vivienda/)).toBeVisible();
  });
});

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { GastosPorCategoria } from '../src/resumen/GastosPorCategoria';
import { GASTOS_EN_PESOS } from './resumen.fixture';

/**
 * FR-008, FR-014 y FR-016: el desglose, legible como texto.
 *
 * **La barra no se prueba acá y es a propósito**: llega en US2. Separarlas es lo que permite ver
 * que el dato se lee sin ella, que es lo que `PRD:RNF-06` pide y lo que hace verificables a los
 * demás criterios sin inspeccionar píxeles (D-03).
 */
describe('GastosPorCategoria', () => {
  it('muestra una fila por categoría, con su nombre y su total legibles', () => {
    render(<GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="ARS" />);

    for (const gasto of GASTOS_EN_PESOS) {
      const fila = screen.getByRole('row', { name: new RegExp(gasto.categoriaNombre) });
      expect(within(fila).getByText(new RegExp(gasto.total.toLocaleString('es-AR')))).toBeVisible();
    }
  });

  /**
   * FR-016: el orden es el que llegó.
   *
   * El servidor lo ordena de mayor a menor y **desempata por id**, con esa razón escrita: sin el
   * desempate, dos categorías con el mismo total salen en el orden que el motor elija ese día y las
   * barras se intercambian solas entre dos pedidos idénticos. Reordenar acá tiraría esa garantía a
   * la basura y volvería a abrir el mismo problema una capa más arriba.
   */
  it('respeta el orden en el que llegaron, sin reordenar', () => {
    render(<GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="ARS" />);

    const filas = screen.getAllByRole('row').slice(1); // la primera es el encabezado
    const nombres = filas.map((fila) => within(fila).getAllByRole('cell')[0].textContent);

    expect(nombres).toEqual(GASTOS_EN_PESOS.map((g) => g.categoriaNombre));
  });

  /**
   * FR-009: sin datos NO es un error.
   *
   * Es la diferencia que `FR-010` vuelve obligatoria: un período sin movimientos y un servidor
   * caído terminan en la misma pantalla por motivos opuestos, y confundirlos haría que alguien
   * creyera que no gastó nada.
   */
  it('sin gastos dice que no hay datos para graficar, y no muestra ningún error', () => {
    render(<GastosPorCategoria gastos={[]} monedaCodigo="USD" />);

    expect(screen.getByText(/no hay/i)).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('row')).not.toBeInTheDocument();
  });

  /**
   * FR-014: los totales que se muestran son los que llegaron.
   *
   * Ninguna suma, ningún porcentaje, ningún total general. Es el requisito más fácil de romper sin
   * que nadie lo note, porque romperlo da un número que igual parece correcto — y por eso el caso
   * compara contra el valor exacto del fixture y no contra "algo que parezca un número".
   */
  it('no calcula ningún total: muestra exactamente lo que recibió', () => {
    const { container } = render(
      <GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="ARS" />,
    );

    const sumaDeLosTres = GASTOS_EN_PESOS.reduce((suma, g) => suma + g.total, 0);

    // El total general NO aparece en ningún lado: nadie lo pidió y la pantalla no lo deriva.
    expect(container.textContent).not.toContain(sumaDeLosTres.toLocaleString('es-AR'));
    // Y ningún porcentaje, que sería la otra forma de derivar un número acá.
    expect(container.textContent).not.toMatch(/%/);
  });

  it('formatea cada total con el código de moneda que le pasan', () => {
    render(<GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="USD" />);

    const fila = screen.getByRole('row', { name: /Vivienda/ });

    expect(within(fila).getByText(/US\$|USD/)).toBeVisible();
  });
});

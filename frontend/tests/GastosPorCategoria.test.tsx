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

/**
 * FR-001 y NFR-003: la barra, que es el gráfico.
 *
 * **Es el único bloque de la feature que mira un ancho**, y con motivo: la proporción es lo único
 * que puede fallar en una barra cuyo dato ya está probado como texto. Todo lo demás se afirma sobre
 * el contenido de la fila (D-03).
 */
describe('GastosPorCategoria — la barra', () => {
  function barraDe(nombre: string): HTMLElement {
    const fila = screen.getByRole('row', { name: new RegExp(nombre) });
    const barra = within(fila).getByTestId('barra');
    return barra;
  }

  it('la barra más larga es la de la categoría con el total más alto, al 100 %', () => {
    render(<GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="ARS" />);

    // Vivienda es 80000 sobre un máximo de 80000.
    expect(barraDe('Vivienda')).toHaveStyle({ width: '100%' });
  });

  it('el ancho de cada barra es proporcional al mayor total de esa moneda', () => {
    render(<GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="ARS" />);

    // Comida es 40000 sobre 80000; Transporte, 20000 sobre 80000.
    expect(barraDe('Comida')).toHaveStyle({ width: '50%' });
    expect(barraDe('Transporte')).toHaveStyle({ width: '25%' });
  });

  /**
   * La barra es **decorativa**: no aporta ningún dato que la fila no tenga ya.
   *
   * Es lo que vuelve verdadera la decisión D-03. Si la barra llevara información propia, el
   * equivalente textual dejaría de ser el gráfico y pasaría a ser una segunda representación — dos
   * que pueden discrepar.
   */
  it('la barra es decorativa y no aporta texto propio', () => {
    render(<GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="ARS" />);

    const barra = barraDe('Vivienda');

    expect(barra).toHaveAttribute('aria-hidden', 'true');
    expect(barra).toHaveTextContent('');
  });

  /**
   * `PRD:AC-13` y `NFR-003`: ninguna categoría se distingue por su color.
   *
   * La lectura habitual de "distinguir por un atributo además del color" es agregarle un patrón al
   * color. Hay una lectura más fuerte: **no codificar por color en absoluto**. Si el color no lleva
   * información, no hay nada que un daltonismo pueda quitarle — y lo que distingue a cada categoría
   * es su nombre, escrito al lado de su barra (D-04).
   *
   * Un test que exigiera colores distintos estaría verificando lo contrario de la decisión.
   */
  it('todas las barras comparten el mismo relleno: ninguna categoría se codifica por color', () => {
    render(<GastosPorCategoria gastos={GASTOS_EN_PESOS} monedaCodigo="ARS" />);

    const clases = GASTOS_EN_PESOS.map((g) => barraDe(g.categoriaNombre).className);

    expect(new Set(clases).size).toBe(1);

    // Y ninguna trae un color propio puesto a mano, que sería la otra forma de codificar por color.
    for (const gasto of GASTOS_EN_PESOS) {
      expect(barraDe(gasto.categoriaNombre).style.backgroundColor).toBe('');
    }
  });

  it('una categoría en cero no rompe el cálculo del ancho', () => {
    render(
      <GastosPorCategoria
        gastos={[{ categoriaId: 1, categoriaNombre: 'Comida', total: 0 }]}
        monedaCodigo="ARS"
      />,
    );

    // Sin guarda, 0/0 da NaN y el ancho sale "NaN%": la fila se ve, la barra desaparece sin motivo.
    expect(barraDe('Comida')).toHaveStyle({ width: '0%' });
  });
});

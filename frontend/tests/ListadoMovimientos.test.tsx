import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ListadoMovimientos } from '../src/movimientos/ListadoMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { SIN_CENTAVOS } from './monedas.fixture';
import { IconoEditar, IconoEliminar } from '../src/ui/iconos';
import { formatearFecha } from '../src/ui/formatearFecha';

const MOVIMIENTOS: Movimiento[] = [
  {
    id: 2,
    tipo: 'ingreso',
    monto: 50000,
    categoriaId: 8,
    categoriaNombre: 'Sueldo',
    monedaCodigo: 'ARS',
    fecha: '2026-08-20',
    nota: '',
  },
  {
    id: 1,
    tipo: 'gasto',
    monto: 1250.5,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'ARS',
    fecha: '2026-08-10',
    nota: '',
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
    nota: '',
  },
  {
    id: 3,
    tipo: 'gasto',
    monto: 100,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'ARS',
    fecha: '2026-09-04',
    nota: '',
  },
];

describe('ListadoMovimientos', () => {
  it('es una tabla con encabezados de columna, no una grilla de divs', () => {
    render(
      <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} onEliminar={() => {}} />,
    );

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
      // La nota entra con la feature 012, entre la moneda y las acciones: es la columna más ancha y
      // la menos urgente de leer (`FR-006`).
      'Nota',
      'Acciones',
    ]);
    // scope="col" es lo que permite a un lector de pantalla anunciar la columna de cada celda.
    encabezados.forEach((e) => expect(e).toHaveAttribute('scope', 'col'));
  });

  it('muestra la fecha en dd/MM/yyyy y no en el ISO que manda la API', () => {
    render(
      <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} onEliminar={() => {}} />,
    );

    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[0]).getByText('20/08/2026')).toBeInTheDocument();
    expect(within(filas[1]).getByText('10/08/2026')).toBeInTheDocument();

    // Y el ISO ya no aparece: ni en la celda ni en ninguna otra parte de la fila, que es donde
    // seguiría si el nombre de sus botones lo dijera.
    const celdas = within(filas[0]).getAllByRole('cell');
    expect(celdas[0]).toHaveTextContent('20/08/2026');
    expect(filas[0]).not.toHaveTextContent('2026-08-20');
  });

  it('muestra el tipo como texto y no sólo por color', () => {
    render(
      <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} onEliminar={() => {}} />,
    );

    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[0]).getByText('Ingreso')).toBeInTheDocument();
    expect(within(filas[1]).getByText('Gasto')).toBeInTheDocument();
  });

  it('muestra categoría y monto de cada movimiento', () => {
    render(
      <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} onEliminar={() => {}} />,
    );

    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[1]).getByText('Comida')).toBeInTheDocument();
    expect(within(filas[1]).getByText(/1\.250,50/)).toBeInTheDocument();
  });

  it('sin movimientos muestra un mensaje explícito y ninguna tabla FR-012', () => {
    render(<ListadoMovimientos movimientos={[]} onEditar={() => {}} onEliminar={() => {}} />);

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
    render(
      <ListadoMovimientos
        movimientos={MISMO_MONTO_DOS_MONEDAS}
        onEditar={() => {}}
        onEliminar={() => {}}
      />,
    );

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

    render(
      <ListadoMovimientos
        movimientos={enUnaMonedaNueva}
        onEditar={() => {}}
        onEliminar={() => {}}
      />,
    );

    expect(screen.getByRole('cell', { name: 'XCT' })).toBeInTheDocument();
  });

  /**
   * **Un código que no sea tres letras no puede tumbar la pantalla.**
   *
   * `Intl.NumberFormat` con `style: 'currency'` exige tres letras ASCII y lanza `RangeError` con
   * cualquier otra cosa — comprobado: `'USD'` y `'XCT'` van, `'BT1'`, `'US'` y `'A-B'` lanzan.
   *
   * La columna `moneda.codigo` es `char(3)`, que garantiza **tres caracteres pero no tres letras**:
   * `'BT1'` es un dato perfectamente válido para el esquema. Y desde esta feature se puede registrar
   * un movimiento en cualquier moneda del catálogo, así que ese dato llega hasta acá. Sin este
   * guardarraíl, el `RangeError` sube por el render, React desmonta el árbol y la cuenta queda con
   * la pantalla en blanco hasta que alguien borre el movimiento por SQL.
   *
   * Es exactamente la promesa que la feature vende —agregar una moneda es sólo un dato— rompiéndose
   * por un dato que nadie declaró inválido en ninguna parte.
   */
  it('muestra el monto aunque el código no sea una moneda que Intl entienda', () => {
    const enUnCodigoRaro: Movimiento[] = [{ ...MISMO_MONTO_DOS_MONEDAS[0], monedaCodigo: 'BT1' }];

    render(
      <ListadoMovimientos movimientos={enUnCodigoRaro} onEditar={() => {}} onEliminar={() => {}} />,
    );

    // El monto se ve, y el código también: se degrada, no se cae.
    expect(screen.getByRole('cell', { name: 'BT1' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: /100/ })).toBeInTheDocument();
  });
});

/**
 * `FR-019` — el listado muestra cada monto en la escala de su moneda.
 *
 * Es una de las tres pantallas que muestran plata, y las tres tienen que tomar la escala del mismo
 * lado: del catálogo, y no de lo que `Intl` deduzca del código ISO (D-08).
 */
describe('ListadoMovimientos — la escala del monto FR-019', () => {
  const EN_YENES = {
    id: 40,
    tipo: 'gasto' as const,
    monto: 1250,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'JPY',
    fecha: '2026-09-01',
    nota: '',
  };

  it('una moneda sin centavos se muestra sin centavos FR-019', () => {
    render(
      <ListadoMovimientos
        movimientos={[EN_YENES]}
        monedas={[SIN_CENTAVOS]}
        onEditar={() => {}}
        onEliminar={() => {}}
      />,
    );

    const monto = screen.getByRole('cell', { name: /1\.250/ });

    expect(monto).toBeInTheDocument();
    expect(monto.textContent).not.toMatch(/1\.250,/);
  });

  it('sin catálogo se cae en lo que Intl deduzca, y la fila se sigue leyendo', () => {
    render(
      <ListadoMovimientos movimientos={[EN_YENES]} onEditar={() => {}} onEliminar={() => {}} />,
    );

    expect(screen.getByRole('cell', { name: /1\.250/ })).toBeInTheDocument();
  });
});

/**
 * FR-041, FR-047, US8:AC6 — **lápiz y tacho en el listado, y editar dice de qué movimiento**.
 *
 * El de eliminar ya decía sobre qué actuaba desde la feature 005; el de editar decía sólo "Editar",
 * y con seis movimientos en la tabla un lector de pantalla anunciaba seis botones indistinguibles.
 * `FR-047` lo arregla, y es uno de los cambios de nombre accesible que `SC-006` autoriza: por eso
 * `VentanaDeEdicion.test.tsx` sí se tocó y `EliminarMovimiento.test.tsx` no.
 */
describe('FR-041, FR-047 · los botones de fila son íconos y dicen sobre qué actúan', () => {
  function dibujoDe(Icono: () => React.JSX.Element): string {
    const { container, unmount } = render(<Icono />);
    const dibujo = container.querySelector('svg')!.innerHTML;
    unmount();

    return dibujo;
  }

  it('cada movimiento tiene el lápiz y el tacho (FR-041, US8:AC6)', () => {
    const lapiz = dibujoDe(IconoEditar);
    const tacho = dibujoDe(IconoEliminar);

    expect(lapiz).not.toBe(tacho);

    render(
      <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} onEliminar={() => {}} />,
    );

    const filas = screen.getAllByRole('row').slice(1);
    expect(filas).toHaveLength(MOVIMIENTOS.length);

    for (const fila of filas) {
      const botones = within(fila).getAllByRole('button');

      expect(botones).toHaveLength(2);
      expect(botones[0].querySelector('svg')?.innerHTML).toBe(lapiz);
      expect(botones[1].querySelector('svg')?.innerHTML).toBe(tacho);
    }
  });

  it('el nombre de editar dice de qué movimiento se trata (FR-047, US8:AC6)', () => {
    render(
      <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} onEliminar={() => {}} />,
    );

    // **Uno por movimiento y todos distintos**, que es el requisito: con el nombre viejo había
    // tantos "Editar" como filas y ninguno se podía elegir sin verlo.
    const nombres = screen
      .getAllByRole('button', { name: /^Editar/ })
      .map((boton) => boton.getAttribute('aria-label'));

    expect(nombres).toHaveLength(MOVIMIENTOS.length);
    expect(new Set(nombres).size).toBe(MOVIMIENTOS.length);

    // Y cada uno nombra su fila con la fecha y la categoría, igual que el de eliminar.
    for (const movimiento of MOVIMIENTOS) {
      expect(
        screen.getByRole('button', {
          name: new RegExp(
            `^Editar .*${formatearFecha(movimiento.fecha)}.*${movimiento.categoriaNombre}`,
          ),
        }),
      ).toBeInTheDocument();
    }
  });

  it('el nombre de eliminar es el mismo de antes (FR-042, SC-006)', () => {
    render(
      <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={() => {}} onEliminar={() => {}} />,
    );

    // Lo que `EliminarMovimiento.test.tsx` busca sigue existiendo con el mismo texto: el tacho
    // reemplaza la palabra visible, no el nombre accesible.
    for (const movimiento of MOVIMIENTOS) {
      expect(
        screen.getByRole('button', {
          name: new RegExp(
            `^Eliminar .*${formatearFecha(movimiento.fecha)}.*${movimiento.categoriaNombre}`,
          ),
        }),
      ).toBeInTheDocument();
    }
  });
});

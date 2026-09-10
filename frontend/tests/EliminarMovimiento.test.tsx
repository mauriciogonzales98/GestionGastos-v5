import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import { RESUMEN } from './resumen.fixture';

/**
 * FR-010 a FR-013, `PRD:RF-15`, `PRD:AC-21` — eliminar un movimiento desde el listado.
 *
 * `DELETE /api/movimientos/{id}` existe desde FEAT-001b, está probado y acota por cuenta. **Nunca
 * tuvo un cliente**: era el único endpoint de la API sin una línea de pantalla que lo llamara. Lo
 * que esta feature agrega es todo de este lado.
 */

/**
 * **Las clases de error son las de verdad, no unas de mentira.**
 *
 * La pantalla distingue por `instanceof`, así que una clase inventada en el mock no es la misma que
 * la que el componente importa: la comparación da siempre `false`, o directamente lanza un
 * `TypeError` si la clase ni siquiera está en el mock. Se descubrió acá — el primer intento
 * declaraba sólo dos y `ErrorDelServidor` llegaba como `undefined`.
 *
 * Se mockean las funciones y se conservan las clases.
 */
vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');

  return {
    ...real,
    obtenerMovimientos: vi.fn(),
    obtenerResumen: vi.fn(),
    eliminarMovimiento: vi.fn(),
    crearMovimiento: vi.fn(),
    editarMovimiento: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

const EL_GASTO: Movimiento = {
  id: 7,
  tipo: 'gasto',
  monto: 1500,
  categoriaId: 1,
  categoriaNombre: 'Comida',
  monedaCodigo: 'ARS',
  fecha: '2026-09-01',
  nota: '',
};

const EL_INGRESO: Movimiento = {
  id: 8,
  tipo: 'ingreso',
  monto: 90000,
  categoriaId: 11,
  categoriaNombre: 'Sueldo',
  monedaCodigo: 'ARS',
  fecha: '2026-09-02',
  nota: '',
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([EL_INGRESO, EL_GASTO]);
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
  vi.mocked(cliente.eliminarMovimiento).mockResolvedValue(undefined);
});

function renderizar() {
  render(
    <PantallaMovimientos
      hoy="2026-09-08"
      email="ana@ejemplo.com"
      categorias={CATEGORIAS}
      monedas={MONEDAS}
      errorDelCatalogo={null}
      errorDelCatalogoDeMonedas={null}
      onCerrarSesion={() => {}}
      onGestionarCategorias={() => {}}
      onVerDashboard={() => {}}
      onSesionVencida={() => {}}
    />,
  );
}

/**
 * La fila de un movimiento, por el nombre de su categoría.
 *
 * Se busca **dentro de la tabla del listado** y no en toda la pantalla: el resumen que está arriba
 * también tiene un desglose por categoría, así que "Comida" aparece dos veces y en dos tablas
 * distintas. Buscar suelto encontraba las dos.
 */
async function fila(categoria: string) {
  const tabla = await screen.findByRole('table', { name: 'Movimientos del mes' });
  const celda = within(tabla).getByRole('cell', { name: categoria });
  return celda.closest('tr') as HTMLElement;
}

describe('FR-011 · la eliminación pide confirmación antes de ejecutarse', () => {
  it('apretar Eliminar no borra nada: pide confirmación y dice la consecuencia entera (FR-011)', async () => {
    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));

    // No se llamó al servidor todavía. Es la mitad que hace que la confirmación sirva.
    expect(cliente.eliminarMovimiento).not.toHaveBeenCalled();

    // Dicho entero y no "¿Seguro?": lo que hay que saber antes de apretar es que no se puede
    // deshacer y que el total cambia.
    expect(within(laDelGasto).getByRole('alert')).toHaveTextContent(
      /se elimina para siempre.*deja de sumar/i,
    );
  });

  it('cancelar deja el movimiento donde estaba y no llama al servidor (FR-011, SC-005)', async () => {
    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^No eliminar/ }));

    expect(cliente.eliminarMovimiento).not.toHaveBeenCalled();
    expect(within(laDelGasto).getByRole('cell', { name: 'Comida' })).toBeInTheDocument();
  });
});

describe('FR-010, FR-012 · confirmar elimina y deja la pantalla coherente', () => {
  it('la fila desaparece y el resumen se vuelve a pedir, sin recargar (FR-010, FR-012, PRD:AC-21)', async () => {
    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));

    // El resumen ya se pidió una vez al montar: lo que se verifica es que se pida OTRA.
    const antes = vi.mocked(cliente.obtenerResumen).mock.calls.length;

    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    expect(cliente.eliminarMovimiento).toHaveBeenCalledWith(EL_GASTO.id);

    await waitFor(() => {
      const tabla = screen.queryByRole('table', { name: 'Movimientos del mes' });
      expect(tabla && within(tabla).queryByRole('cell', { name: 'Comida' })).toBeFalsy();
    });

    // Un total no se puede editar en la pantalla: hay que recalcularlo, y quien recalcula es el
    // servidor (D-11).
    await waitFor(() => {
      expect(vi.mocked(cliente.obtenerResumen).mock.calls.length).toBeGreaterThan(antes);
    });

    // Y NO se volvió a pedir el listado: el servidor no tiene nada más que decir sobre una fila que
    // ya no está.
    expect(vi.mocked(cliente.obtenerMovimientos).mock.calls.length).toBe(1);

    expect(await screen.findByRole('status')).toHaveTextContent(/eliminado/i);
  });

  it('eliminar la única fila deja el mensaje de vacío, no una tabla sin filas', async () => {
    vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([EL_GASTO]);
    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    expect(await screen.findByText(/No hay movimientos registrados este mes/i)).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: /Movimientos del mes/i })).not.toBeInTheDocument();
  });

  /**
   * D-09 — el foco no se queda en el aire.
   *
   * El botón que se apretó deja de existir en el mismo render y el navegador manda el foco al
   * `<body>`. Quien navega con teclado queda al principio de la página sin ningún anuncio de que la
   * acción salió bien. Es la variante de `FR-005` que sólo aparece cuando algo se borra.
   */
  it('el foco va a un destino estable después de que la fila desaparece (FR-005, D-09)', async () => {
    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    await waitFor(() => {
      expect(document.activeElement).toBe(
        screen.getByRole('heading', { name: 'Movimientos del mes' }),
      );
    });

    expect(document.activeElement).not.toBe(document.body);
  });
});

describe('FR-013 · lo que pasa cuando el servidor dice que no', () => {
  it('un 404 lo dice y deja de mostrar la fila (FR-013)', async () => {
    vi.mocked(cliente.eliminarMovimiento).mockRejectedValue(
      new cliente.ErrorDelServidor(404, 'El servidor respondió 404.'),
    );

    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    // El mensaje es uno solo: el servidor responde igual si no existe, si es de otra cuenta o si ya
    // se eliminó, y la pantalla no intenta distinguirlos.
    expect(await screen.findByText(/ya no está/i)).toBeInTheDocument();

    await waitFor(() => {
      const tabla = screen.queryByRole('table', { name: 'Movimientos del mes' });
      expect(tabla && within(tabla).queryByRole('cell', { name: 'Comida' })).toBeFalsy();
    });
  });

  /**
   * **Un fallo que no es un 404 no puede afirmar que el movimiento ya no existe** (hallazgo 2 de la
   * revisión del PR #28).
   *
   * Hasta el arreglo, el `catch` mandaba a la rama del 404 **todo** lo que no fuera `ErrorDeSesion`.
   * Con el backend caído, confirmar un borrado sacaba la fila del listado y la pantalla decía "Ese
   * movimiento ya no está" — mientras el movimiento seguía existiendo. Al recargar reaparecía.
   *
   * Es una pantalla afirmando algo falso, que es la clase de bug que este proyecto persigue desde
   * la feature 009. La fila se queda, y se dice que no se pudo.
   */
  it('un fallo de red no saca la fila ni dice que el movimiento ya no está (FR-013)', async () => {
    vi.mocked(cliente.eliminarMovimiento).mockRejectedValue(
      new cliente.ErrorDeRed(new Error('sin red')),
    );

    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    expect(await screen.findByText(/no se pudo eliminar/i)).toBeInTheDocument();

    // Y NO dice que ya no está, porque sigue estando.
    expect(screen.queryByText(/ya no está/i)).not.toBeInTheDocument();

    // La fila se queda: sacarla afirmaría que se borró algo que no se borró.
    expect(
      within(screen.getByRole('table', { name: 'Movimientos del mes' })).getByRole('cell', {
        name: 'Comida',
      }),
    ).toBeInTheDocument();
  });

  it('un 500 tampoco saca la fila (FR-013)', async () => {
    vi.mocked(cliente.eliminarMovimiento).mockRejectedValue(
      new cliente.ErrorDelServidor(500, 'El servidor respondió 500.'),
    );

    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    expect(await screen.findByText(/no se pudo eliminar/i)).toBeInTheDocument();
    expect(
      within(screen.getByRole('table', { name: 'Movimientos del mes' })).getByRole('cell', {
        name: 'Comida',
      }),
    ).toBeInTheDocument();
  });

  /**
   * **El aviso no le sobrevive a lo que venga después** (hallazgo 6 de la revisión del PR #28).
   *
   * Un cartel que se queda puesto mientras la persona sigue trabajando termina describiendo algo que
   * pasó hace rato. Es la misma regla que el listado ya sigue con su error de carga —se va cuando
   * una carga posterior sale bien— aplicada al resultado del borrado.
   */
  it('el aviso del borrado se va cuando se registra un movimiento (FR-013)', async () => {
    vi.mocked(cliente.eliminarMovimiento).mockRejectedValue(
      new cliente.ErrorDeRed(new Error('sin red')),
    );
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...EL_GASTO, id: 50 });

    const usuario = userEvent.setup();
    renderizar();

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    expect(await screen.findByText(/no se pudo eliminar/i)).toBeInTheDocument();

    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    await waitFor(() => {
      expect(screen.queryByText(/no se pudo eliminar/i)).not.toBeInTheDocument();
    });
  });

  it('un 401 vuelve al acceso diciendo qué pasó con ESE movimiento (FR-013)', async () => {
    vi.mocked(cliente.eliminarMovimiento).mockRejectedValue(new cliente.ErrorDeSesion());
    const alVencer = vi.fn();

    const usuario = userEvent.setup();
    render(
      <PantallaMovimientos
        hoy="2026-09-08"
        email="ana@ejemplo.com"
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        errorDelCatalogo={null}
        errorDelCatalogoDeMonedas={null}
        onCerrarSesion={() => {}}
        onGestionarCategorias={() => {}}
        onVerDashboard={() => {}}
        onSesionVencida={alVencer}
      />,
    );

    const laDelGasto = await fila('Comida');
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Eliminar el gasto/ }));
    await usuario.click(within(laDelGasto).getByRole('button', { name: /^Confirmar y eliminar/ }));

    // La pantalla está por desaparecer: el aviso tiene que decir qué pasó con este movimiento, o
    // quien lo pidió no sabe si se borró o no.
    await waitFor(() => {
      expect(alVencer).toHaveBeenCalledWith(expect.stringMatching(/no se eliminó/i));
    });
  });
});

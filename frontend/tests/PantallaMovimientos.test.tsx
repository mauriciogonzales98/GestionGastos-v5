import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { LA_INESPERADA, MONEDAS } from './monedas.fixture';

vi.mock('../src/api/cliente', () => ({
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
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([DEL_20, DEL_10]);
});

async function renderizar(monedas = MONEDAS) {
  render(
    <PantallaMovimientos
      hoy={HOY}
      email="ana@ejemplo.com"
      categorias={CATEGORIAS}
      monedas={monedas}
      errorDelCatalogo={null}
      onCerrarSesion={() => {}}
      onGestionarCategorias={() => {}}
      onSesionVencida={() => {}}
    />,
  );
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

  /**
   * FR-010 y AC-12: el control de acotado ofrece **las monedas del catálogo más la opción de no
   * acotar**, y sale de la misma lectura que alimenta el selector del formulario.
   *
   * Que salgan de la misma lectura es lo que hace imposible que discrepen (`PRD:NFR-02`). Este test
   * lo comprueba de la única forma en que se puede desde acá: el mismo array de props alimenta los
   * dos, así que se verifica que el acotado ofrezca exactamente lo mismo que el selector, más
   * "Todas".
   */
  it('ofrece las monedas del catálogo más "todas" para acotar FR-010', async () => {
    await renderizar();

    const acotado = screen.getByLabelText('Ver sólo la moneda');
    const opciones = within(acotado).getAllByRole('option');

    expect(opciones.map((o) => o.textContent)).toEqual([
      'Todas las monedas',
      'Peso argentino',
      'Dólar',
    ]);
    expect(acotado).toHaveValue('');
  });

  /** AC-04 del lado del acotado: una moneda agregada sólo como dato también se puede acotar. */
  it('ofrece para acotar una moneda agregada al catálogo sólo como dato AC-04', async () => {
    await renderizar([...MONEDAS, LA_INESPERADA]);

    const acotado = screen.getByLabelText('Ver sólo la moneda');

    expect(
      within(acotado)
        .getAllByRole('option')
        .map((o) => o.textContent),
    ).toContain(LA_INESPERADA.nombre);
  });

  /**
   * AC-06: elegir una moneda vuelve a pedir el listado **acotado a esa moneda**.
   *
   * Se comprueba lo que se le pide a la API, no lo que queda en la tabla: el acotado lo hace el
   * servidor, y un filtrado del lado del cliente sobre la lista que ya tenía se vería idéntico y
   * estaría mal — mostraría sólo lo que ya se había traído del mes en curso.
   */
  it('acota el listado pidiéndoselo al servidor AC-06', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.selectOptions(screen.getByLabelText('Ver sólo la moneda'), '2');

    await waitFor(() =>
      expect(vi.mocked(cliente.obtenerMovimientos)).toHaveBeenLastCalledWith({ monedaId: 2 }),
    );
  });

  /** AC-07: volver a "todas" pide el listado sin acotar. */
  it('vuelve a pedir todas las monedas al quitar el acotado AC-07', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    const acotado = screen.getByLabelText('Ver sólo la moneda');
    await usuario.selectOptions(acotado, '2');
    await usuario.selectOptions(acotado, '');

    await waitFor(() =>
      expect(vi.mocked(cliente.obtenerMovimientos)).toHaveBeenLastCalledWith({ monedaId: null }),
    );
  });

  /**
   * **La respuesta que llega tarde no puede pisar a la que llegó después.**
   *
   * Escenario: el acotado se cambia dos veces seguidas. Salen dos peticiones y la primera tarda
   * más, así que resuelve **última** y su `setMovimientos` se escribe encima del resultado correcto.
   * El listado termina mostrando dólares con el control diciendo "Todas las monedas": la pantalla
   * se contradice a sí misma, sin error y sin nada en la consola.
   *
   * No existía antes de esta feature porque el listado se pedía una sola vez, al montar. Lo trae
   * el acotado por moneda, que es lo que vuelve al efecto capaz de correr más de una vez.
   */
  it('descarta la respuesta de un acotado que ya no está vigente', async () => {
    const usuario = userEvent.setup();

    let resolverLenta: (movimientos: Movimiento[]) => void = () => {};

    vi.mocked(cliente.obtenerMovimientos).mockImplementation((acotado) => {
      // El acotado a dólares es el lento: se resuelve a mano, después del otro.
      if (acotado?.monedaId === 2) {
        return new Promise<Movimiento[]>((resolver) => {
          resolverLenta = resolver;
        });
      }

      return Promise.resolve([DEL_20, DEL_10]);
    });

    await renderizar();

    const acotado = screen.getByLabelText('Ver sólo la moneda');
    await usuario.selectOptions(acotado, '2');
    await usuario.selectOptions(acotado, '');

    // Ahora sí contesta la de dólares, tarde y fuera de tiempo. `act` deja que su `.then` corra y
    // que React pinte lo que sea que haya pasado: sin eso, la aserción se evalúa antes de que la
    // respuesta vieja tenga oportunidad de pisar nada, y el test pasa sin verificar nada.
    const soloDolares: Movimiento = { ...DEL_20, id: 99, monedaCodigo: 'USD' };
    await act(async () => {
      resolverLenta([soloDolares]);
    });

    expect(fechasDelListado()).toEqual(['2026-08-20', '2026-08-10']);
  });
});

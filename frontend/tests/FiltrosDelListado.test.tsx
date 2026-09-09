import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { LA_INESPERADA, MONEDAS } from './monedas.fixture';
import { RESUMEN } from './resumen.fixture';

/**
 * FR-014 a FR-018, `PRD:RF-17`, `PRD:RF-18` — la barra de acotado del listado.
 *
 * **Salda la deuda D9-01.** El servidor acota por categoría, rango de fechas y moneda desde
 * FEAT-001b; hasta esta feature sólo la moneda tenía control en la interfaz. Nada de lo que se
 * verifica acá es capacidad nueva del servidor: es la pantalla que faltaba.
 */

vi.mock('../src/api/cliente', () => ({
  obtenerMovimientos: vi.fn(),
  obtenerResumen: vi.fn(),
  crearMovimiento: vi.fn(),
  editarMovimiento: vi.fn(),
  eliminarMovimiento: vi.fn(),
  ErrorDeSesion: class ErrorDeSesion extends Error {},
  ErrorDeValidacion: class ErrorDeValidacion extends Error {
    constructor(readonly errores: Record<string, string[]>) {
      super('rechazada');
    }
  },
}));

const cliente = await import('../src/api/cliente');

const UN_GASTO: Movimiento = {
  id: 3,
  tipo: 'gasto',
  monto: 100,
  categoriaId: 1,
  categoriaNombre: 'Comida',
  monedaCodigo: 'ARS',
  fecha: '2026-09-10',
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([UN_GASTO]);
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
});

async function renderizar(monedas = MONEDAS) {
  render(
    <PantallaMovimientos
      hoy="2026-09-08"
      email="ana@ejemplo.com"
      categorias={CATEGORIAS}
      monedas={monedas}
      errorDelCatalogo={null}
      errorDelCatalogoDeMonedas={null}
      onCerrarSesion={() => {}}
      onGestionarCategorias={() => {}}
      onVerDashboard={() => {}}
      onSesionVencida={() => {}}
    />,
  );
  await screen.findByRole('table', { name: /movimientos del mes/i });
}

/** El último acotado con el que se pidió el listado. */
function ultimoAcotado() {
  const llamadas = vi.mocked(cliente.obtenerMovimientos).mock.calls;
  return llamadas[llamadas.length - 1][0];
}

describe('NFR-005, D-06 · los tres se aplican juntos, con un solo botón', () => {
  it('elegir un acotado no pide nada: pide el botón (NFR-005)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    const pedidos = vi.mocked(cliente.obtenerMovimientos).mock.calls.length;

    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '1');
    await usuario.selectOptions(screen.getByLabelText('Acotar por moneda'), '2');
    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01');
    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31');

    // Ni una petición mientras se elige. Aplicar en cada cambio serían hasta tres para expresar una
    // sola pregunta, y una fecha se escribe dígito a dígito: cada tecla dispararía un rechazo con su
    // cartel apareciendo y desapareciendo.
    expect(vi.mocked(cliente.obtenerMovimientos).mock.calls.length).toBe(pedidos);

    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    // Y una sola cuando se aplica.
    await waitFor(() => {
      expect(vi.mocked(cliente.obtenerMovimientos).mock.calls.length).toBe(pedidos + 1);
    });
  });

  /**
   * **La respuesta vieja no pisa a la vigente.**
   *
   * Es la cicatriz `22e3e96` de la feature 009. El botón único hace el caso menos frecuente que con
   * tres controles que aplicaran solos, no imposible: dos clics seguidos dejan dos peticiones en
   * vuelo, y si la primera tarda más resuelve última. El listado terminaría mostrando lo que se
   * pidió antes, con los controles diciendo otra cosa — sin error y sin nada en la consola.
   *
   * La guarda es la bandera `vigente` del efecto, y este test es lo que impide que un refactor se
   * la lleve puesta.
   */
  it('dos "Aplicar" seguidos: la primera respuesta no pisa a la segunda', async () => {
    const usuario = userEvent.setup();

    let resolverLaPrimera!: (m: Movimiento[]) => void;
    const laPrimera = new Promise<Movimiento[]>((resolver) => {
      resolverLaPrimera = resolver;
    });
    const DE_LA_PRIMERA: Movimiento = { ...UN_GASTO, id: 90, categoriaNombre: 'Transporte' };
    const DE_LA_SEGUNDA: Movimiento = { ...UN_GASTO, id: 91, categoriaNombre: 'Salud' };

    await renderizar();

    vi.mocked(cliente.obtenerMovimientos).mockReturnValueOnce(laPrimera);
    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    vi.mocked(cliente.obtenerMovimientos).mockResolvedValueOnce([DE_LA_SEGUNDA]);
    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '2');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await screen.findByRole('cell', { name: 'Salud' });

    // Ahora resuelve la que salió primero. No tiene que escribirse encima.
    resolverLaPrimera([DE_LA_PRIMERA]);
    await waitFor(() => {
      const tabla = screen.getByRole('table', { name: /movimientos del mes/i });
      expect(within(tabla).queryByRole('cell', { name: 'Transporte' })).not.toBeInTheDocument();
    });

    expect(
      within(screen.getByRole('table', { name: /movimientos del mes/i })).getByRole('cell', {
        name: 'Salud',
      }),
    ).toBeInTheDocument();
  });
});

describe('FR-014 · acotar por categoría', () => {
  it('elegir una categoría se lo pide al servidor (PRD:AC-23, FR-014)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() => expect(ultimoAcotado()).toMatchObject({ categoriaId: 1 }));
  });

  it('sin elegir ninguna, se ven las de todas (PRD:AC-24, FR-014)', async () => {
    await renderizar();

    const control = screen.getByLabelText('Acotar por categoría');
    expect(control).toHaveValue('');
    expect(within(control).getAllByRole('option')[0]).toHaveTextContent('Todas las categorías');

    // El primer pedido sale sin `categoriaId`: la ausencia es lo que el servidor entiende por
    // "todas".
    expect(ultimoAcotado()).toEqual({});
  });

  it('ofrece las categorías del catálogo, sin escribir ninguna a mano (PRD:AC-24)', async () => {
    await renderizar();

    const opciones = within(screen.getByLabelText('Acotar por categoría')).getAllByRole('option');

    // Todas las del catálogo más "Todas". Ningún número fijo sobre cuántas hay: se deriva del
    // fixture, que es lo que hace que agregar una categoría no rompa este test.
    expect(opciones).toHaveLength(CATEGORIAS.length + 1);
  });
});

describe('FR-015 · acotar por rango de fechas', () => {
  /**
   * `PRD:AC-25` y `FR-015` — el mes actual por omisión, **sin que la pantalla lo calcule**.
   *
   * Acá hay una trampa que conviene ver: `GET /api/movimientos` devuelve un arreglo pelado y **no
   * dice qué período aplicó**. El único que lo dice es `GET /api/resumen`, que lleva `desde` y
   * `hasta` puestos por el servidor — y la pantalla principal ya lo tiene cargado. El control se
   * prefija desde ahí (D-05): el dato lo sigue decidiendo el servidor y no aparece un segundo
   * criterio de "hoy".
   */
  it('el control muestra el mes que eligió el servidor, sin calcularlo (PRD:AC-25, FR-015)', async () => {
    await renderizar();

    await waitFor(() => {
      expect(screen.getByLabelText('Desde')).toHaveValue(RESUMEN.desde);
    });
    expect(screen.getByLabelText('Hasta')).toHaveValue(RESUMEN.hasta);

    // Y aun así el primer pedido del listado sale **sin período**: lo prefijado se muestra, no se
    // manda. La ausencia de los dos extremos es lo que el servidor entiende por "el mes en curso".
    expect(ultimoAcotado()).toEqual({});
  });

  /**
   * **El control no puede terminar mostrando un período distinto del aplicado** (hallazgo 4 de la
   * revisión del PR #28).
   *
   * `ControlesDelPeriodo` lee sus valores iniciales sólo al montarse, así que el período que llega
   * con el resumen entra por una `key` que lo remonta. El problema es que esa `key` cambiaba
   * **después** de que alguien ya hubiera aplicado un rango propio: si el resumen tarda o falla al
   * principio y llega recién tras el primer alta, el control se remontaba mostrando el mes en curso
   * mientras el listado seguía mostrando agosto. La pantalla contradiciéndose sola, que es el mismo
   * daño que la guarda `vigente` evita del otro lado.
   */
  it('el período aplicado no lo pisa un resumen que llega después', async () => {
    const usuario = userEvent.setup();

    // El resumen falla al principio: el control arranca sin período prefijado.
    vi.mocked(cliente.obtenerResumen).mockRejectedValueOnce(new Error('caído'));
    await renderizar();

    await usuario.clear(screen.getByLabelText('Desde'));
    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01');
    await usuario.clear(screen.getByLabelText('Hasta'));
    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() =>
      expect(ultimoAcotado()).toMatchObject({ desde: '2026-08-01', hasta: '2026-08-31' }),
    );

    // Ahora sí llega un resumen, disparado por un alta. El período del listado no cambió.
    vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...UN_GASTO, id: 99 });
    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    await waitFor(() => expect(cliente.obtenerResumen).toHaveBeenCalledTimes(2));

    // El control tiene que seguir diciendo agosto, que es lo que el listado está mostrando.
    expect(screen.getByLabelText('Desde')).toHaveValue('2026-08-01');
    expect(screen.getByLabelText('Hasta')).toHaveValue('2026-08-31');
  });

  it('un rango elegido se manda con sus dos extremos (PRD:AC-26, FR-015)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.clear(screen.getByLabelText('Desde'));
    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01');
    await usuario.clear(screen.getByLabelText('Hasta'));
    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    // Los extremos van incluidos: eso lo garantiza el servidor, y lo que se verifica acá es que se
    // le manden tal cual, sin recortarlos ni corregirlos.
    await waitFor(() =>
      expect(ultimoAcotado()).toMatchObject({ desde: '2026-08-01', hasta: '2026-08-31' }),
    );
  });
});

describe('FR-018 · el rechazo del período', () => {
  it('un rango invertido muestra el mensaje del servidor y deja el listado como estaba (FR-018)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    vi.mocked(cliente.obtenerMovimientos).mockRejectedValueOnce(
      new cliente.ErrorDeValidacion({
        rango: ['La fecha de inicio no puede ser posterior a la de fin.'],
      }),
    );

    await usuario.clear(screen.getByLabelText('Desde'));
    await usuario.type(screen.getByLabelText('Desde'), '2026-08-31');
    await usuario.clear(screen.getByLabelText('Hasta'));
    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-01');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    // El mensaje es el del servidor: `PeriodoPedido` es el único intérprete del período y la
    // pantalla no reimplementa su criterio (D-05).
    expect(await screen.findByText(/no puede ser posterior/i)).toBeInTheDocument();

    // Y el listado conserva lo que estaba mostrando: lo rechazado fue el pedido nuevo, y vaciarlo
    // diría que no hay movimientos, que es otra cosa.
    expect(
      within(screen.getByRole('table', { name: /movimientos del mes/i })).getByRole('cell', {
        name: 'Comida',
      }),
    ).toBeInTheDocument();
  });

  it('medio rango se manda igual, para que lo rechace el servidor (FR-018)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.clear(screen.getByLabelText('Hasta'));
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    // No se comprueba acá que falte un extremo: los dos van juntos o no va ninguno es una regla del
    // servidor, y comprobarla en la pantalla sería el segundo intérprete.
    await waitFor(() => expect(ultimoAcotado()).toMatchObject({ hasta: '' }));
  });
});

describe('FR-016, FR-017 · los tres juntos, y siempre contra el servidor', () => {
  it('los tres acotados viajan juntos en una sola petición (FR-016)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '1');
    await usuario.selectOptions(screen.getByLabelText('Acotar por moneda'), '2');
    await usuario.clear(screen.getByLabelText('Desde'));
    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01');
    await usuario.clear(screen.getByLabelText('Hasta'));
    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() =>
      expect(ultimoAcotado()).toEqual({
        categoriaId: 1,
        monedaId: 2,
        desde: '2026-08-01',
        hasta: '2026-08-31',
      }),
    );
  });

  it('sin resultados lo dice, y no como un error (FR-016)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    vi.mocked(cliente.obtenerMovimientos).mockResolvedValueOnce([]);
    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    expect(await screen.findByText(/No hay movimientos registrados este mes/i)).toBeInTheDocument();

    // Un acotado sin resultados y un servidor caído terminan en pantallas parecidas por motivos
    // opuestos: confundirlos haría creer que no se gastó nada.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  /**
   * `FR-017` — el acotado lo resuelve el servidor.
   *
   * Filtrar del lado del cliente la lista que ya se tenía se vería igual y estaría mal: mostraría
   * sólo lo que ya se había traído, que es el mes en curso. Un rango de agosto sobre una lista de
   * septiembre daría vacío en vez de los movimientos de agosto.
   */
  it('cambiar un acotado vuelve a pedir, no filtra lo que ya tenía (FR-017)', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    const pedidos = vi.mocked(cliente.obtenerMovimientos).mock.calls.length;

    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() => {
      expect(vi.mocked(cliente.obtenerMovimientos).mock.calls.length).toBe(pedidos + 1);
    });
  });

  /** `PRD:AC-04` del lado del acotado: una moneda agregada sólo como dato también se ofrece. */
  it('ofrece para acotar una moneda que ninguna línea de código conoce (PRD:AC-04)', async () => {
    await renderizar([...MONEDAS, LA_INESPERADA]);

    expect(
      within(screen.getByLabelText('Acotar por moneda')).getByRole('option', {
        name: LA_INESPERADA.nombre,
      }),
    ).toBeInTheDocument();
  });
});

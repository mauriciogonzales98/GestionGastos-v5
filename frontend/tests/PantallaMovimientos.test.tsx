import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { LA_INESPERADA, MONEDAS } from './monedas.fixture';
import { construirResumen, RESUMEN } from './resumen.fixture';

vi.mock('../src/api/cliente', () => ({
  obtenerMovimientos: vi.fn(),
  crearMovimiento: vi.fn(),
  obtenerResumen: vi.fn(),
  ErrorDeSesion: class ErrorDeSesion extends Error {},
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
  nota: '',
};

const DEL_10: Movimiento = {
  id: 3,
  tipo: 'gasto',
  monto: 100,
  categoriaId: 1,
  categoriaNombre: 'Comida',
  monedaCodigo: 'ARS',
  fecha: '2026-08-10',
  nota: '',
};

beforeEach(() => {
  // clearAllMocks y no sólo reset del que interesa: los contadores de llamadas se acumulan entre
  // tests, y un "se llamó una vez" que en realidad cuenta las corridas anteriores no verifica nada.
  vi.clearAllMocks();
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([DEL_20, DEL_10]);
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
});

async function renderizar(monedas = MONEDAS) {
  render(
    <PantallaMovimientos
      hoy={HOY}
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
  // Por nombre y no `getByRole('table')` a secas: desde la feature 010 la pantalla tiene más de
  // una tabla —el desglose del resumen es una— y la consulta sin nombre dejó de ser inequívoca.
  await screen.findByRole('table', { name: /movimientos del mes/i });
}

function fechasDelListado() {
  return within(screen.getByRole('table', { name: /movimientos del mes/i }))
    .getAllByRole('row')
    .slice(1)
    .map((f) => within(f).getAllByRole('cell')[0].textContent);
}

describe('PantallaMovimientos', () => {
  it('muestra formulario y listado en una sola pantalla FR-013', async () => {
    await renderizar();

    expect(screen.getByRole('heading', { level: 1, name: 'Mis movimientos' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Registrar' })).toBeInTheDocument();
    expect(screen.getByRole('table', { name: /movimientos del mes/i })).toBeInTheDocument();
  });

  // AC-15 + FR-014: el movimiento guardado aparece en el listado, en su posición.
  it('inserta el movimiento guardado en su posición del orden AC-15 FR-014', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({
      id: 9,
      nota: '',
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

    const acotado = screen.getByLabelText('Acotar por moneda');
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

    const acotado = screen.getByLabelText('Acotar por moneda');

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

    await usuario.selectOptions(screen.getByLabelText('Acotar por moneda'), '2');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() =>
      // `objectContaining`: desde la feature 011 el acotado lleva los cuatro campos y se aplican
      // juntos (D-06). Lo que este caso verifica es la moneda, no la forma entera del objeto.
      expect(vi.mocked(cliente.obtenerMovimientos)).toHaveBeenLastCalledWith(
        expect.objectContaining({ monedaId: 2 }),
      ),
    );
  });

  /** AC-07: volver a "todas" pide el listado sin acotar. */
  it('vuelve a pedir todas las monedas al quitar el acotado AC-07', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    const acotado = screen.getByLabelText('Acotar por moneda');
    await usuario.selectOptions(acotado, '2');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));
    await usuario.selectOptions(acotado, '');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() =>
      expect(vi.mocked(cliente.obtenerMovimientos)).toHaveBeenLastCalledWith(
        expect.objectContaining({ monedaId: null }),
      ),
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

    const acotado = screen.getByLabelText('Acotar por moneda');
    await usuario.selectOptions(acotado, '2');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));
    await usuario.selectOptions(acotado, '');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

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

/**
 * FR-011, FR-010 y FR-017: el resumen del mes en curso, arriba de todo.
 *
 * Es la deuda D9-06 saldándose. El servidor calcula estos números bien desde FEAT-001c y hasta hoy
 * no los mostraba nadie.
 */
describe('PantallaMovimientos — el resumen del mes en curso', () => {
  /**
   * Devuelve la posición de un elemento en el orden real del documento.
   *
   * Se compara el orden y no la mera presencia porque `FR-011` es una afirmación sobre **dónde**
   * está el resumen: "arriba del formulario y del listado" es el requisito, y un test que sólo
   * comprobara que existe lo daría por cumplido con el resumen al pie de la página.
   */
  function posicionEnElDocumento(elemento: Element): number {
    return Array.from(document.querySelectorAll('*')).indexOf(elemento);
  }

  it('muestra el resumen del mes ARRIBA del formulario y del listado FR-011', async () => {
    await renderizar();

    const resumen = await screen.findByRole('region', { name: /resumen del mes/i });
    const formulario = screen.getByRole('button', { name: 'Registrar' });
    const listado = screen.getByRole('table', { name: /movimientos del mes/i });

    expect(posicionEnElDocumento(resumen)).toBeLessThan(posicionEnElDocumento(formulario));
    expect(posicionEnElDocumento(resumen)).toBeLessThan(posicionEnElDocumento(listado));
  });

  it('lo pide sin período: el mes en curso lo decide el servidor FR-011b', async () => {
    await renderizar();

    await waitFor(() => expect(cliente.obtenerResumen).toHaveBeenCalled());
    expect(vi.mocked(cliente.obtenerResumen).mock.calls[0]).toEqual([]);
  });

  /**
   * `010:FR-011b` reformulado por la feature 011, y conviene ver por qué.
   *
   * Este test decía *"no ofrece ningún control de período"*, y desde que existe la barra de acotado
   * del listado eso dejó de ser cierto: `PRD:RF-18` pide un rango de fechas sobre el listado, y ahí
   * está. **Pero la garantía que el test protegía sigue en pie, y es otra**: el resumen de esta
   * pantalla está clavado al mes en curso, lo decide el servidor, y ningún control de acá lo mueve.
   *
   * La forma vieja verificaba la garantía por la ausencia del control, que era lo único disponible
   * mientras no hubiera ninguno. Ahora se la verifica de frente: se elige un rango, se aplica, y el
   * resumen **no se vuelve a pedir con período**. Es más fuerte que lo anterior, no más débil — la
   * ausencia de un control nunca dijo nada sobre qué pasaría si existiera.
   */
  it('el acotado del listado no mueve el resumen del mes 010:FR-011b FR-016', async () => {
    const usuario = userEvent.setup();
    await renderizar();
    await screen.findByRole('region', { name: /resumen del mes/i });
    await waitFor(() => expect(cliente.obtenerResumen).toHaveBeenCalled());

    const pedidosDelResumen = vi.mocked(cliente.obtenerResumen).mock.calls.length;

    // `clear` primero: los campos llegan prefijados con el mes que eligió el servidor (FR-015), así
    // que escribir encima sin vaciarlos no cambia el valor.
    await usuario.clear(screen.getByLabelText('Desde'));
    await usuario.type(screen.getByLabelText('Desde'), '2026-01-01');
    await usuario.clear(screen.getByLabelText('Hasta'));
    await usuario.type(screen.getByLabelText('Hasta'), '2026-01-31');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    // El listado sí se vuelve a pedir, con el rango.
    await waitFor(() =>
      expect(cliente.obtenerMovimientos).toHaveBeenCalledWith(
        expect.objectContaining({ desde: '2026-01-01', hasta: '2026-01-31' }),
      ),
    );

    // El resumen NO. Sigue siendo el del mes en curso, decidido por el servidor.
    expect(vi.mocked(cliente.obtenerResumen).mock.calls.length).toBe(pedidosDelResumen);
  });

  it('registrar un movimiento vuelve a pedir el resumen', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...DEL_10, id: 9, fecha: '2026-08-15' });
    await renderizar();
    await waitFor(() => expect(cliente.obtenerResumen).toHaveBeenCalledTimes(1));

    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    // El listado NO se vuelve a pedir —la fila se inserta— pero el resumen sí: un total no se puede
    // insertar, hay que recalcularlo, y recalcularlo acá sería sumar en el cliente (FR-014).
    await waitFor(() => expect(cliente.obtenerResumen).toHaveBeenCalledTimes(2));
    expect(cliente.obtenerMovimientos).toHaveBeenCalledTimes(1);
  });

  /**
   * FR-010: cargando, sin datos y no se pudo cargar son TRES estados distintos.
   *
   * Mostrar ceros ante un servidor caído sería la pantalla afirmando que no hubo movimientos.
   */
  it('si el resumen falla lo dice, y NO muestra ceros FR-010', async () => {
    vi.mocked(cliente.obtenerResumen).mockRejectedValue(new Error('sin red'));

    await renderizar();

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/no se pudo cargar/i);
    expect(screen.queryByRole('region', { name: /resumen del mes/i })).not.toBeInTheDocument();
  });

  /**
   * La cicatriz `10a2e6d` de la feature 009: un cartel de fallo que sobrevive a una carga que salió
   * bien miente, y miente justo al lado de los datos que lo desmienten.
   */
  it('el cartel del fallo desaparece cuando una carga posterior sale bien', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.obtenerResumen).mockRejectedValueOnce(new Error('sin red'));
    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...DEL_10, id: 9, fecha: '2026-08-15' });
    await renderizar();
    await screen.findByRole('alert');

    vi.mocked(cliente.obtenerResumen).mockResolvedValue(construirResumen());
    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    await screen.findByRole('region', { name: /resumen del mes/i });
    expect(screen.queryByText(/no se pudo cargar el resumen/i)).not.toBeInTheDocument();
  });

  /**
   * FR-017: un 401 no es "falló la carga", es que ya no hay sesión.
   *
   * La reacción es volver al acceso, no mostrar un error de carga sobre una pantalla protegida.
   */
  it('un 401 al pedir el resumen vuelve al acceso, no muestra un error de carga FR-017', async () => {
    const alVencer = vi.fn();
    vi.mocked(cliente.obtenerResumen).mockRejectedValue(new cliente.ErrorDeSesion());

    render(
      <PantallaMovimientos
        hoy={HOY}
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

    await waitFor(() => expect(alVencer).toHaveBeenCalled());
    expect(screen.queryByText(/no se pudo cargar el resumen/i)).not.toBeInTheDocument();
  });
});

/**
 * **La carrera del resumen de la pantalla principal.**
 *
 * El listado de esta pantalla tiene guarda contra la respuesta que llega tarde desde la feature 009
 * (`22e3e96`), y el dashboard la tiene desde la 010. El resumen de acá **no la tenía**: es el
 * hallazgo 1 de la revisión del PR #25, y es la misma cicatriz por tercera vez.
 *
 * Cuesta más provocarla que la del dashboard —hacen falta dos guardados seguidos— y el guardado
 * siguiente la corrige. Pero mientras dura, el total del mes muestra una suma que no incluye el
 * movimiento que sí se ve en el listado, dos centímetros más abajo. Sin error y sin nada en la
 * consola.
 */
describe('PantallaMovimientos — la carrera del resumen', () => {
  /** Una promesa que el test resuelve cuando quiere, para poder ordenar las respuestas a mano. */
  function promesaControlada<T>() {
    let cumplir: (valor: T) => void = () => {};
    const promesa = new Promise<T>((resolver) => {
      cumplir = resolver;
    });
    return { promesa, cumplir };
  }

  it('la respuesta de una recarga vieja no pisa a la de la vigente', async () => {
    const usuario = userEvent.setup();
    const VIEJO = construirResumen({ desde: '2020-01-01', hasta: '2020-01-31' });
    const NUEVO = construirResumen({ desde: '2030-12-01', hasta: '2030-12-31' });

    vi.mocked(cliente.crearMovimiento).mockResolvedValue({ ...DEL_10, id: 9, fecha: '2026-08-15' });
    await renderizar();
    await screen.findByRole('region', { name: /resumen del mes/i });

    // Primer guardado: su recarga queda EN VUELO, sin resolver.
    const primera = promesaControlada<typeof VIEJO>();
    vi.mocked(cliente.obtenerResumen).mockReturnValueOnce(primera.promesa);
    await usuario.type(screen.getByLabelText('Monto'), '500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    // Segundo guardado: su recarga resuelve enseguida y es la que vale.
    vi.mocked(cliente.obtenerResumen).mockResolvedValueOnce(NUEVO);
    await usuario.type(screen.getByLabelText('Monto'), '700');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));
    await screen.findByText(/2030-12-31/);

    // Y AHORA llega la primera, tarde.
    primera.cumplir(VIEJO);

    await waitFor(() => expect(screen.getByText(/2030-12-31/)).toBeVisible());
    expect(screen.queryByText(/2020-01-31/)).not.toBeInTheDocument();
  });
});

/**
 * D6-06 · el resumen y el listado pueden estar hablando de cosas distintas, y la pantalla lo dice.
 *
 * El resumen es del **mes completo** por decisión declarada en
 * `specs/006-resumen-del-mes/contracts/resumen.md`: `GET /api/resumen` no acepta `categoriaId` y el
 * de esta pantalla nunca pide un período. El listado sí se acota. Sin aviso, la misma pantalla
 * muestra dos cifras que se contradicen y quien mira no tiene cómo saber cuál es cuál.
 *
 * **El aviso mira lo aplicado contra lo que el resumen cubre, y no si alguien tocó el botón.** Es la
 * diferencia que decide todo: la barra viene sembrada con el mes en curso, así que "Aplicar" sin
 * cambiar nada manda un rango que coincide con el del resumen. Un aviso atado al botón mentiría ahí.
 */
describe('PantallaMovimientos — el aviso de que el listado está acotado D6-06', () => {
  const AVISO = /este resumen es de todo el mes/i;

  it('al entrar, sin nada acotado, no avisa nada', async () => {
    await renderizar();

    expect(screen.queryByText(AVISO)).not.toBeInTheDocument();
  });

  it('acotar por categoría hace aparecer el aviso', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.selectOptions(screen.getByLabelText('Acotar por categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    expect(await screen.findByText(AVISO)).toBeVisible();
  });

  it('acotar por moneda hace aparecer el aviso', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.selectOptions(screen.getByLabelText('Acotar por moneda'), '2');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    expect(await screen.findByText(AVISO)).toBeVisible();
  });

  /**
   * El caso que separa "se aplicó" de "acota de verdad". La barra arranca con el período del
   * resumen, así que aplicar sin tocar nada pide exactamente el mes que el resumen ya cuenta: las
   * dos vistas coinciden y no hay nada que aclarar.
   */
  it('aplicar sin cambiar nada NO avisa: el listado y el resumen cuentan lo mismo', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() => expect(vi.mocked(cliente.obtenerMovimientos)).toHaveBeenCalledTimes(2));
    expect(screen.queryByText(AVISO)).not.toBeInTheDocument();
  });

  it('un rango distinto del que cuenta el resumen hace aparecer el aviso', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    // Un día después del `desde` del resumen: alcanza con que el período no sea el mismo.
    await usuario.clear(screen.getByLabelText('Desde'));
    await usuario.type(screen.getByLabelText('Desde'), '2026-09-02');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    expect(await screen.findByText(AVISO)).toBeVisible();
  });

  it('volver a "todas" hace desaparecer el aviso', async () => {
    const usuario = userEvent.setup();
    await renderizar();

    const categoria = screen.getByLabelText('Acotar por categoría');
    await usuario.selectOptions(categoria, '1');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));
    expect(await screen.findByText(AVISO)).toBeVisible();

    await usuario.selectOptions(categoria, '');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar' }));

    await waitFor(() => expect(screen.queryByText(AVISO)).not.toBeInTheDocument());
  });
});

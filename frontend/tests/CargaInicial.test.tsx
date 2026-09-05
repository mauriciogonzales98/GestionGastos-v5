import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { ErrorDeRed, ErrorDelServidor } from '../src/api/cliente';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');
  return {
    ...real,
    obtenerMovimientos: vi.fn(),
    crearMovimiento: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

/** Estable a propósito: va en las dependencias del efecto que pide el listado. */
const NO_OP = () => {};

beforeEach(() => {
  vi.clearAllMocks();
});

/**
 * Lo que pasa cuando la API no está. Antes de FEAT-001: nada. Las promesas rechazaban sin `.catch`,
 * el indicador de carga se quedaba encendido para siempre y el selector de categorías vacío, así
 * que guardar era imposible y la única señal era un unhandled rejection en la consola.
 *
 * **Desde la feature 007 esta pantalla ya no pide el catálogo** (D-08): lo carga la raíz y baja por
 * props, junto con el aviso de que no se pudo. Así que acá se ejercita el listado, que sigue
 * cargándose acá, y el aviso del catálogo entra como prop. Que la petición del catálogo falle lo
 * cubre `App.test.tsx`, que es donde ahora ocurre.
 */
const ERROR_DEL_CATALOGO =
  'No se pudieron cargar las categorías. Revisá la conexión y recargá la página.';

describe('carga inicial que falla', () => {
  it('con el backend caído muestra el error y deja de decir que está cargando', async () => {
    vi.mocked(cliente.obtenerMovimientos).mockRejectedValue(new ErrorDeRed(new TypeError('nope')));

    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        email="ana@ejemplo.com"
        categorias={[]}
        monedas={MONEDAS}
        errorDelCatalogo={ERROR_DEL_CATALOGO}
        errorDelCatalogoDeMonedas={null}
        onCerrarSesion={() => {}}
        onGestionarCategorias={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    // Los dos fallos se muestran. Se espera por los dos con `waitFor` y no con un `findAll` a
    // secas: el del catálogo llega por prop en el primer render y el del listado después de que la
    // promesa rechace, así que un `findAll` puede leer justo el instante en que hay uno solo.
    await waitFor(() => expect(screen.getAllByRole('alert')).toHaveLength(2));

    // **Cada aviso dice lo suyo, y ya no comparten la salida.** Hasta la feature 009 los dos
    // pedían recargar la página y el test afirmaba sobre esa frase común. Dejaron de compartirla y
    // la divergencia es correcta: sin categorías no se puede registrar nada y recargar es el único
    // recurso; el listado, en cambio, se vuelve a pedir solo al cambiar el acotado, así que pedir
    // una recarga sugeriría un callejón que no existe.
    //
    // Se afirma sobre los dos textos completos y no sobre una frase compartida: una frase común es
    // una coincidencia de redacción, y afirmar sobre ella hace que arreglar un mensaje rompa un
    // test que no habla de él.
    const textos = screen.getAllByRole('alert').map((a) => a.textContent);
    expect(textos).toContain(ERROR_DEL_CATALOGO);
    expect(textos).toContain('No se pudo cargar el listado de movimientos. Volvé a intentarlo.');

    // Y sobre todo: el indicador de carga se apaga. Quedarse encendido es mentirle a la persona.
    await waitFor(() =>
      expect(screen.queryByText(/cargando movimientos/i)).not.toBeInTheDocument(),
    );
  });

  it('si falla el listado pero no el catálogo, el formulario sigue siendo usable', async () => {
    vi.mocked(cliente.obtenerMovimientos).mockRejectedValue(new ErrorDelServidor(500, 'boom'));

    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        email="ana@ejemplo.com"
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        errorDelCatalogo={null}
        errorDelCatalogoDeMonedas={null}
        onCerrarSesion={() => {}}
        onGestionarCategorias={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    await screen.findByRole('alert');

    // El catálogo llegó, así que se puede cargar un movimiento aunque el listado no esté.
    await waitFor(() =>
      expect(screen.getByLabelText('Categoría')).toContainHTML('<option value="1">Comida</option>'),
    );
    expect(screen.getByRole('button', { name: 'Registrar' })).toBeEnabled();
  });

  it('si falla el catálogo, lo dice en vez de ofrecer un selector vacío', async () => {
    vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([]);

    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        email="ana@ejemplo.com"
        categorias={[]}
        monedas={MONEDAS}
        errorDelCatalogo={ERROR_DEL_CATALOGO}
        errorDelCatalogoDeMonedas={null}
        onCerrarSesion={() => {}}
        onGestionarCategorias={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/categor/i);
  });

  /**
   * **El cartel de un fallo viejo no puede sobrevivir a una carga que salió bien.**
   *
   * Escenario: falla la carga inicial y aparece "No se pudo cargar…". La persona acota el listado a
   * una moneda, esa petición sale bien y el listado se llena — y el cartel sigue ahí, diciendo que
   * no se pudo cargar exactamente lo que está mirando.
   *
   * No podía pasar antes de esta feature: el listado se pedía una sola vez y del error no se salía
   * sin recargar. El acotado por moneda creó ese camino de recuperación y el cartel quedó atrás.
   *
   * `NO_OP` y no una flecha inline: `onSesionVencida` está en las dependencias del efecto que pide
   * el listado, así que una función nueva en cada render lo vuelve a disparar sola. `App` la
   * memoriza con `useCallback` justamente por eso.
   */
  it('limpia el error de carga cuando una carga posterior sale bien', async () => {
    const usuario = userEvent.setup();

    vi.mocked(cliente.obtenerMovimientos)
      .mockRejectedValueOnce(new ErrorDelServidor(500, 'boom'))
      .mockResolvedValue([]);

    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        email="ana@ejemplo.com"
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        errorDelCatalogo={null}
        errorDelCatalogoDeMonedas={null}
        onCerrarSesion={NO_OP}
        onGestionarCategorias={NO_OP}
        onSesionVencida={NO_OP}
      />,
    );

    await screen.findByText('No se pudo cargar el listado de movimientos. Volvé a intentarlo.');

    await usuario.selectOptions(screen.getByLabelText('Ver sólo la moneda'), '2');

    await waitFor(() =>
      expect(
        screen.queryByText('No se pudo cargar el listado de movimientos. Volvé a intentarlo.'),
      ).not.toBeInTheDocument(),
    );
  });
});

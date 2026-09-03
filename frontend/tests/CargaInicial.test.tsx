import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { ErrorDeRed, ErrorDelServidor } from '../src/api/cliente';
import { CATEGORIAS } from './categorias.fixture';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');
  return {
    ...real,
    obtenerMovimientos: vi.fn(),
    crearMovimiento: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

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
        errorDelCatalogo={ERROR_DEL_CATALOGO}
        onCerrarSesion={() => {}}
        onGestionarCategorias={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    // Los dos fallos se muestran. Se espera por los dos con `waitFor` y no con un `findAll` a
    // secas: el del catálogo llega por prop en el primer render y el del listado después de que la
    // promesa rechace, así que un `findAll` puede leer justo el instante en que hay uno solo.
    await waitFor(() => expect(screen.getAllByRole('alert')).toHaveLength(2));

    // Se afirma sobre lo que ambos mensajes comparten: afirmar sobre una redacción sola sería un
    // test que pasa por orden de llegada.
    for (const aviso of screen.getAllByRole('alert')) {
      expect(aviso).toHaveTextContent(/recargá la página/i);
    }

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
        errorDelCatalogo={null}
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
        errorDelCatalogo={ERROR_DEL_CATALOGO}
        onCerrarSesion={() => {}}
        onGestionarCategorias={() => {}}
        onSesionVencida={() => {}}
      />,
    );

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/categor/i);
  });
});

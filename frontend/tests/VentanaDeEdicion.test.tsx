import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');
  return {
    ...real,
    obtenerMovimientos: vi.fn(),
    crearMovimiento: vi.fn(),
    editarMovimiento: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

const HOY = '2026-09-04';

const EN_PESOS: Movimiento = {
  id: 1,
  tipo: 'gasto',
  monto: 1250.5,
  categoriaId: 1,
  categoriaNombre: 'Comida',
  monedaCodigo: 'ARS',
  fecha: '2026-09-02',
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([EN_PESOS]);
});

async function renderizar() {
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
      onSesionVencida={() => {}}
    />,
  );
  await screen.findByRole('table');
}

/** Abre la ventana desde la fila del listado y devuelve el `userEvent` en uso. */
async function abrir() {
  const usuario = userEvent.setup();
  await renderizar();

  const fila = screen.getAllByRole('row')[1];
  await usuario.click(within(fila).getByRole('button', { name: 'Editar' }));

  return usuario;
}

/**
 * La ventana emergente de edición (FR-011, `PRD:FR-07`).
 *
 * Es un `<dialog>` nativo con `showModal()`, y no un `<div role="dialog">` con el foco manejado a
 * mano: la plataforma ya atrapa el foco adentro, cierra con `Escape`, vuelve inerte el fondo y
 * pinta el `::backdrop`. Reimplementarlo sería más código y peor accesibilidad, y el defecto
 * —el foco que se escapa al fondo— sólo aparece para quien navega con teclado, o sea nunca durante
 * el desarrollo.
 *
 * El entorno de tests es happy-dom, y que implemente `showModal()` se verificó **antes** de elegir
 * el enfoque, no a mitad de la implementación (research.md D-07).
 */
describe('VentanaDeEdicion', () => {
  /**
   * AC-10 del lado de la pantalla: la ventana se abre con **todo ya cargado**.
   *
   * Es la mitad de `PRD:FR-07` que hace que corregir sea corregir y no volver a cargar. Si un campo
   * llegara vacío, quien corrige la moneda tendría que reescribir el monto, y un dígito de menos
   * convierte una corrección en un dato falso.
   */
  it('se abre con el monto, la categoría, la moneda y la fecha del movimiento AC-10', async () => {
    await abrir();

    const ventana = screen.getByRole('dialog');

    expect(within(ventana).getByLabelText('Monto')).toHaveValue(1250.5);
    expect(within(ventana).getByLabelText('Categoría')).toHaveValue('1');
    expect(within(ventana).getByLabelText('Moneda')).toHaveValue('1');
    expect(within(ventana).getByLabelText('Fecha')).toHaveValue('2026-09-02');
  });

  /**
   * Cerrar la ventana avisa a la pantalla, que la desmonta.
   *
   * **Este caso cierra con "Cancelar" y no con `Escape`, y hay que saber por qué.** Las dos vías
   * son la misma: `Escape` sobre un `<dialog>` abierto con `showModal()` lo cierra y emite `close`,
   * que es el evento que esta ventana escucha — por eso se escucha `close` y no un `keydown`.
   *
   * Lo que pasa es que **happy-dom no simula esa conducta del navegador**: se comprobó
   * ejecutándolo, y tras un `Escape` el `<dialog>` sigue con `open === true` y no emite nada.
   * `close()` sí funciona y sí emite. Así que un test que "verifique Escape" acá no verificaría
   * `Escape`: verificaría un `keydown` que nadie escucha, y pasaría en verde el día que el manejo
   * del cierre se rompiera de verdad.
   *
   * Lo que sí se puede afirmar desde acá es que **el camino del cierre está bien cableado**, y eso
   * es lo que se afirma. La conducta de `Escape` la aporta la plataforma y se comprueba a mano en
   * el paso 8 del quickstart. Queda anotado como D9-07.
   */
  it('avisa a la pantalla cuando se cierra', async () => {
    const usuario = await abrir();

    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  /**
   * AC-09 y FR-011: cambiar la moneda y guardar manda la edición **con el resto sin tocar**, y la
   * fila del listado queda actualizada.
   *
   * Lo que se comprueba es el cuerpo que sale, no lo que se ve en los controles: una ventana que
   * muestra bien y manda mal se ve perfecta y corrompe el dato.
   */
  it('cambia la moneda y deja el resto igual FR-011', async () => {
    vi.mocked(cliente.editarMovimiento).mockResolvedValue({ ...EN_PESOS, monedaCodigo: 'USD' });

    const usuario = await abrir();
    const ventana = screen.getByRole('dialog');

    await usuario.selectOptions(within(ventana).getByLabelText('Moneda'), '2');
    await usuario.click(within(ventana).getByRole('button', { name: 'Guardar cambios' }));

    await waitFor(() =>
      expect(vi.mocked(cliente.editarMovimiento)).toHaveBeenCalledWith(1, {
        tipo: 'gasto',
        monto: 1250.5,
        categoriaId: 1,
        monedaId: 2,
        fecha: '2026-09-02',
      }),
    );

    // La fila se actualiza con lo que devolvió el servidor, sin volver a pedir el listado.
    await waitFor(() => expect(screen.getByRole('cell', { name: 'USD' })).toBeInTheDocument());
  });

  /** Guardado con éxito, la ventana se va sola: dejarla abierta obliga a un clic que nada aporta. */
  it('se cierra al guardar con éxito', async () => {
    vi.mocked(cliente.editarMovimiento).mockResolvedValue(EN_PESOS);

    const usuario = await abrir();
    const ventana = screen.getByRole('dialog');

    await usuario.click(within(ventana).getByRole('button', { name: 'Guardar cambios' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  /**
   * **Una fila que deja de cumplir el acotado no puede quedarse en el listado.**
   *
   * Escenario, y es el caso de uso central de esta historia: el listado está acotado a Dólar, la
   * persona abre una fila y le corrige la moneda a Pesos — que es exactamente para lo que la
   * ventana existe. Al guardar, la fila se reemplaza en su lugar y queda visible **bajo un control
   * que dice "Ver sólo la moneda: Dólar"**. El listado muestra algo que él mismo declara estar
   * filtrando.
   *
   * Sacarla en silencio tampoco alcanza: la persona corrigió algo y necesita saber que salió bien.
   * Por eso la confirmación lo dice, con el mismo criterio con el que el alta avisa cuando el
   * movimiento cae fuera del mes del listado.
   */
  it('saca del listado la fila que dejó de cumplir el acotado, y lo dice', async () => {
    vi.mocked(cliente.editarMovimiento).mockResolvedValue({ ...EN_PESOS, monedaCodigo: 'ARS' });

    const usuario = userEvent.setup();
    await renderizar();

    // El listado, acotado a dólares, trae el movimiento que está en dólares.
    vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([{ ...EN_PESOS, monedaCodigo: 'USD' }]);
    await usuario.selectOptions(screen.getByLabelText('Ver sólo la moneda'), '2');
    await waitFor(() => expect(screen.getByRole('cell', { name: 'USD' })).toBeInTheDocument());

    const fila = screen.getAllByRole('row')[1];
    await usuario.click(within(fila).getByRole('button', { name: 'Editar' }));

    const ventana = screen.getByRole('dialog');
    await usuario.selectOptions(within(ventana).getByLabelText('Moneda'), '1');
    await usuario.click(within(ventana).getByRole('button', { name: 'Guardar cambios' }));

    await screen.findByText(
      'Movimiento actualizado. Como ya no es de la moneda que estás viendo, salió del listado.',
    );

    expect(screen.queryByRole('cell', { name: 'ARS' })).not.toBeInTheDocument();
  });
});

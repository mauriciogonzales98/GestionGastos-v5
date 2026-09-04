import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { FormularioMovimiento } from '../src/movimientos/FormularioMovimiento';
import type { Categoria } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { LA_INESPERADA, MONEDAS } from './monedas.fixture';

const HOY = '2026-08-23';

function renderizar(onGuardar = vi.fn(), monedas = MONEDAS) {
  render(
    <FormularioMovimiento
      categorias={CATEGORIAS}
      monedas={monedas}
      hoy={HOY}
      onGuardar={onGuardar}
    />,
  );
  return onGuardar;
}

describe('FormularioMovimiento', () => {
  it('arranca en gasto y con la fecha de hoy puesta', () => {
    renderizar();

    // `gasto` viene marcado de entrada: es el caso mayoritario y el que el PRD nombra primero.
    expect(screen.getByRole('radio', { name: 'Gasto' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Ingreso' })).not.toBeChecked();
    expect(screen.getByLabelText('Fecha')).toHaveValue(HOY);
  });

  /**
   * AC-03 y FR-005: el selector ofrece **exactamente las monedas que bajan por props**, en su
   * orden, y con la predeterminada ya elegida.
   *
   * No hay opción vacía como en categorías: la moneda siempre tiene un valor: `PRD:NFR-01` exige
   * que se pueda guardar sin tocar el campo, y un "elegí una moneda" obligaría a tocarlo.
   */
  it('ofrece las monedas del catálogo con la predeterminada elegida AC-03', () => {
    renderizar();

    const selector = screen.getByLabelText('Moneda');
    const opciones = within(selector).getAllByRole('option');

    expect(opciones.map((o) => o.textContent)).toEqual(['Peso argentino', 'Dólar']);
    expect(selector).toHaveValue('1');
  });

  /**
   * AC-04 y FR-005 — **el caso que se pone en rojo el día que alguien escriba `['ARS', 'USD']` en
   * el frontend**.
   *
   * La feature 008 verificó, con una barrera entera, que sumar una moneda al catálogo cuesta 0
   * líneas de código. Esa barrera mira el backend. Desde que existe este selector, el frontend
   * también puede romper la promesa, y del único lado que el usuario mira. Este test es lo que lo
   * impide: la moneda no la conoce ninguna constante y tiene que aparecer igual.
   */
  it('ofrece una moneda agregada al catálogo sólo como dato AC-04', () => {
    renderizar(vi.fn(), [...MONEDAS, LA_INESPERADA]);

    const selector = screen.getByLabelText('Moneda');
    const opciones = within(selector).getAllByRole('option');

    expect(opciones.map((o) => o.textContent)).toContain(LA_INESPERADA.nombre);
  });

  /**
   * FR-001: la moneda elegida viaja en el alta.
   *
   * Se comprueba el `monedaId` que sale, no lo que se ve en el control: un `<select>` que muestra
   * la moneda correcta y manda otra cosa se ve perfecto y guarda mal.
   */
  it('manda la moneda elegida al registrar FR-001', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '100');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.selectOptions(screen.getByLabelText('Moneda'), '2');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(onGuardar).toHaveBeenCalledWith(expect.objectContaining({ monedaId: 2 }));
  });

  /**
   * AC-02, FR-002 y `PRD:NFR-01` — **el criterio que más fácil se rompe sin que nadie lo note,
   * porque romperlo no produce ningún error.**
   *
   * Quien opera en una sola moneda tiene que poder registrar **con exactamente las mismas
   * interacciones que antes de esta feature: cero adicionales**. Así que este test carga monto y
   * categoría, no toca la moneda, y comprueba dos cosas: que el guardado sale con la
   * predeterminada, y que el `<select>` de moneda **nunca recibió una interacción**.
   *
   * La segunda mitad es la que hace al test sobre NFR-01 y no sobre otra cosa: sin ella, un
   * formulario que exija elegir moneda pasaría igual con sólo agregar el clic al test.
   */
  it('registra sin tocar la moneda y manda la predeterminada AC-02 NFR-01', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '100');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const predeterminada = MONEDAS.find((m) => m.esPredeterminada)!;
    expect(onGuardar).toHaveBeenCalledWith(
      expect.objectContaining({ monedaId: predeterminada.id }),
    );
  });

  // AC-10: el selector de gasto no muestra ninguna categoría de ingreso, y viceversa.
  it('ofrece las 7 categorías de gasto y ninguna de ingreso AC-10', () => {
    renderizar();

    const selector = screen.getByLabelText('Categoría');
    const opciones = within(selector).getAllByRole('option');
    const nombres = opciones.map((o) => o.textContent);

    expect(nombres).toEqual([
      'Elegí una categoría',
      'Comida',
      'Transporte',
      'Vivienda',
      'Servicios',
      'Salud',
      'Ocio',
      'Otros',
    ]);
    expect(nombres).not.toContain('Sueldo');
    expect(nombres).not.toContain('Ingreso extra');
  });

  it('al pasar a ingreso repuebla el selector y limpia la selección anterior AC-10', async () => {
    const usuario = userEvent.setup();
    renderizar();

    const selector = screen.getByLabelText('Categoría');
    await usuario.selectOptions(selector, '1');
    expect(selector).toHaveValue('1');

    await usuario.click(screen.getByRole('radio', { name: 'Ingreso' }));

    // Dejar la selección puesta permitiría enviar un ingreso con categoría de gasto, que el
    // servidor rechaza por FR-011. Mejor que la combinación imposible no sea alcanzable.
    expect(selector).toHaveValue('');
    const nombres = within(selector)
      .getAllByRole('option')
      .map((o) => o.textContent);
    expect(nombres).toEqual(['Elegí una categoría', 'Sueldo', 'Ingreso extra', 'Otros']);
  });

  it('es un form real que se envía con un button type=submit', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '1250.50');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    // La forma COMPLETA de lo que sale, no un `objectContaining`: es lo que atrapa un campo de más
    // que nadie decidió mandar. `monedaId` entra con la feature 009 y viene de la predeterminada,
    // porque este caso no toca el selector.
    expect(onGuardar).toHaveBeenCalledWith({
      tipo: 'gasto',
      monto: 1250.5,
      categoriaId: 1,
      monedaId: 1,
      fecha: HOY,
    });
  });
});

/**
 * El edge case de la spec (FR-022): la categoría elegida en el formulario se da de baja desde la
 * otra pantalla mientras el movimiento está a medio cargar.
 *
 * Sin esto, el `<select>` se queda con un valor que ya no tiene `<option>`: el control se ve vacío
 * pero el estado sigue guardando el id viejo, así que "Registrar" manda una categoría que el
 * servidor rechaza con un mensaje que la persona no puede entender —eligió algo y le dicen que no
 * eligió nada—. La salida es sacarla de la selección, que es lo que el control ya está mostrando.
 */
describe('FormularioMovimiento — la categoría elegida deja de estar disponible FR-022', () => {
  const CON_GIMNASIO: Categoria[] = [
    ...CATEGORIAS,
    { id: 43, nombre: 'Gimnasio', tipo: 'gasto', esPropia: true },
  ];

  it('saca de la selección la categoría que desapareció del catálogo', async () => {
    const usuario = userEvent.setup();
    const onGuardar = vi.fn();

    const { rerender } = render(
      <FormularioMovimiento
        categorias={CON_GIMNASIO}
        monedas={MONEDAS}
        hoy={HOY}
        onGuardar={onGuardar}
      />,
    );

    await usuario.selectOptions(screen.getByLabelText('Categoría'), '43');
    expect(screen.getByLabelText('Categoría')).toHaveValue('43');

    // La dan de baja desde la pantalla de gestión: el catálogo baja sin ella.
    rerender(
      <FormularioMovimiento
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        hoy={HOY}
        onGuardar={onGuardar}
      />,
    );

    expect(screen.getByLabelText('Categoría')).toHaveValue('');

    // Y al enviar se pide elegir una, en vez de mandar el id que ya no vale.
    await usuario.type(screen.getByLabelText('Monto'), '800');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(onGuardar).not.toHaveBeenCalled();
    expect(screen.getByText('Elegí una categoría.')).toBeInTheDocument();
  });

  it('no toca la selección si la categoría sigue estando', async () => {
    const usuario = userEvent.setup();
    const onGuardar = vi.fn();

    const { rerender } = render(
      <FormularioMovimiento
        categorias={CON_GIMNASIO}
        monedas={MONEDAS}
        hoy={HOY}
        onGuardar={onGuardar}
      />,
    );

    await usuario.selectOptions(screen.getByLabelText('Categoría'), '43');

    // Otra categoría se da de baja, no la elegida.
    rerender(
      <FormularioMovimiento
        categorias={CON_GIMNASIO.filter((c) => c.id !== 6)}
        monedas={MONEDAS}
        hoy={HOY}
        onGuardar={onGuardar}
      />,
    );

    expect(screen.getByLabelText('Categoría')).toHaveValue('43');
  });
});

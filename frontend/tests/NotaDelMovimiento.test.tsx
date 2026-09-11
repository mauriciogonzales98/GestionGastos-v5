import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ErrorDeValidacion } from '../src/api/cliente';
import { FormularioMovimiento } from '../src/movimientos/FormularioMovimiento';
import { ListadoMovimientos } from '../src/movimientos/ListadoMovimientos';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';

const HOY = '2026-09-10';

function renderizarFormulario(onGuardar = vi.fn()) {
  render(
    <FormularioMovimiento
      categorias={CATEGORIAS}
      monedas={MONEDAS}
      hoy={HOY}
      onGuardar={onGuardar}
    />,
  );
  return onGuardar;
}

function movimiento(nota: string, id = 1): Movimiento {
  return {
    id,
    tipo: 'gasto',
    monto: 8500,
    categoriaId: 1,
    categoriaNombre: 'Transporte',
    monedaCodigo: 'ARS',
    fecha: HOY,
    nota,
  };
}

function renderizarListado(movimientos: Movimiento[]) {
  render(
    <ListadoMovimientos
      movimientos={movimientos}
      monedas={MONEDAS}
      onEditar={vi.fn()}
      onEliminar={vi.fn()}
    />,
  );
}

describe('La nota al registrar (RF-33)', () => {
  /**
   * `PRD:AC-09` y `FR-001`: el campo se presenta como **opcional** y se puede guardar sin haberlo
   * tocado.
   *
   * Es la mitad del valor del ticket que se pierde más fácil. El producto entero combate la fricción
   * del camino de carga, y la nota es el único campo libre del formulario: si obligara a algo, cada
   * movimiento costaría una decisión más.
   */
  it('se puede guardar sin tocar la nota, y queda vacía AC-09', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizarFormulario();

    await usuario.type(screen.getByLabelText('Monto'), '1200');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(onGuardar).toHaveBeenCalledWith(expect.objectContaining({ nota: '' }));
  });

  it('manda la nota que se escribió AC-01', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizarFormulario();

    await usuario.type(screen.getByLabelText('Monto'), '8500');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.type(screen.getByLabelText('Nota'), 'viaje al aeropuerto');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(onGuardar).toHaveBeenCalledWith(
      expect.objectContaining({ nota: 'viaje al aeropuerto' }),
    );
  });

  /**
   * `FR-008`: el error del largo aparece **al lado del campo**, con la tripleta que `CampoConError`
   * arma en un solo lugar — no como un cartel suelto al pie del formulario.
   *
   * El mensaje lo produce el servidor con la clave `nota`, y lo que hace que llegue a su lugar es que
   * `nota` esté en la lista de campos con lugar propio. Una clave que no está en esa lista cae en la
   * región general: el mensaje se muestra, pero lejos del control que hay que corregir.
   */
  it('el error del largo queda asociado al campo, no suelto FR-008', async () => {
    const usuario = userEvent.setup();
    renderizarFormulario(
      // La clase real y no un objeto con la misma forma: el reparto de errores del formulario usa
      // `instanceof`, así que un doble parecido caería en la región general y el test pasaría por el
      // camino equivocado.
      vi
        .fn()
        .mockRejectedValue(
          new ErrorDeValidacion({ nota: ['La nota no puede superar los 120 caracteres.'] }),
        ),
    );

    await usuario.type(screen.getByLabelText('Monto'), '1200');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const campo = screen.getByLabelText('Nota');
    const error = await screen.findByText('La nota no puede superar los 120 caracteres.');

    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(campo).toHaveAttribute('aria-describedby', error.id);
  });

  /**
   * `FR-003`, adelantado en la pantalla: el límite se cuenta en **caracteres Unicode**.
   *
   * 120 emoji son 120 caracteres y 240 unidades UTF-16. Contando con `.length` —el largo "natural" de
   * una cadena en JavaScript— se rechazarían por superar 120 cuando la persona escribió exactamente
   * 120, con un mensaje que no se puede entender ni corregir. El servidor cuenta igual: las dos
   * validaciones tienen que aceptar y rechazar el mismo conjunto de notas.
   */
  it('120 emoji se aceptan porque el límite se cuenta en caracteres FR-013', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizarFormulario();
    const nota = '😀'.repeat(120);

    expect(nota.length).toBe(240);
    expect([...nota].length).toBe(120);

    await usuario.type(screen.getByLabelText('Monto'), '1200');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.type(screen.getByLabelText('Nota'), nota);
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(onGuardar).toHaveBeenCalledWith(expect.objectContaining({ nota }));
  });

  it('121 caracteres se rechazan sin llamar al servidor FR-003', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizarFormulario();

    await usuario.type(screen.getByLabelText('Monto'), '1200');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.type(screen.getByLabelText('Nota'), 'a'.repeat(121));
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(onGuardar).not.toHaveBeenCalled();
    expect(await screen.findByText(/120 caracteres/)).toBeInTheDocument();
  });
});

describe('La nota en el listado (RF-33)', () => {
  /**
   * `NFR-001` y `PRD:AC-08`: la nota se muestra como **texto plano**.
   *
   * El framework escapa por omisión, así que este test no verifica que haya una protección: verifica
   * que **desactivarla rompa en rojo**. El riesgo real de la única entrada de texto libre de la
   * aplicación no es que falte el escape, es que alguien lo saque a propósito "para que se vea mejor"
   * — y eso, sin este test, pasaría la suite entera en verde.
   */
  it('una nota con forma de marcado se muestra con sus caracteres AC-08', () => {
    const nota = '<b>hola</b> -- DROP TABLE movimiento;';
    renderizarListado([movimiento(nota)]);

    expect(screen.getByText(nota)).toBeInTheDocument();
    // Y no se interpretó: no hay un elemento en negrita que el texto haya creado.
    expect(document.querySelector('td b')).toBeNull();
  });

  it('muestra la nota junto a su movimiento FR-006', () => {
    renderizarListado([movimiento('viaje al aeropuerto')]);

    const fila = screen.getByRole('row', { name: /viaje al aeropuerto/ });
    expect(within(fila).getByText('viaje al aeropuerto')).toBeInTheDocument();
  });

  /**
   * `FR-005`: un movimiento sin nota se ve **sin texto de relleno**.
   *
   * Ni un guion, ni "sin nota", ni `null`. La celda queda vacía, que es lo que corresponde a un estado
   * normal: la mayoría de los movimientos no va a tener nota, y los que existían antes de esta feature
   * tampoco. Un relleno convertiría lo habitual en algo que parece faltar.
   */
  it('un movimiento sin nota no muestra relleno FR-005', () => {
    renderizarListado([movimiento('')]);

    expect(screen.queryByText('—')).not.toBeInTheDocument();
    expect(screen.queryByText(/sin nota/i)).not.toBeInTheDocument();
    expect(screen.queryByText('null')).not.toBeInTheDocument();
  });

  /**
   * `FR-012`: los saltos de línea se conservan en el dato y **no significan nada en la presentación**.
   *
   * Es la única de las tres combinaciones posibles que no miente. Transformarlos al guardar cambiaría
   * en silencio lo que la persona escribió; darles significado sería construir el formato que el PRD
   * deja fuera de alcance. Conservarlos sin significado deja el dato intacto y la pantalla simple.
   */
  it('una nota de varias líneas se lee en una sola línea visual FR-012', () => {
    renderizarListado([movimiento('primera\nsegunda')]);

    // El texto está completo en el DOM —un lector de pantalla lo lee entero— y se presenta en una
    // línea: el salto no agrega ni quita nada.
    const celda = screen.getByText(/primera/);
    expect(celda.textContent).toBe('primera\nsegunda');
  });
});

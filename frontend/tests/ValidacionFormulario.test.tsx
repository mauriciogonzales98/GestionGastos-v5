import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { FormularioMovimiento } from '../src/movimientos/FormularioMovimiento';
import { ErrorDelServidor, ErrorDeValidacion } from '../src/api/cliente';
import { CATEGORIAS } from './categorias.fixture';

const HOY = '2026-08-23';

function renderizar(onGuardar = vi.fn()) {
  render(<FormularioMovimiento categorias={CATEGORIAS} hoy={HOY} onGuardar={onGuardar} />);
  return onGuardar;
}

describe('validación en el formulario', () => {
  // AC-18: monto inválido rechazado con motivo visible, junto a su campo.
  it('muestra el error de monto junto al campo, con aria-invalid y aria-describedby AC-18', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '0');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const control = screen.getByLabelText('Monto');
    expect(control).toHaveAttribute('aria-invalid', 'true');
    expect(control).toHaveAttribute('aria-describedby', 'monto-error');
    expect(document.getElementById('monto-error')).toHaveTextContent(/mayor a cero/i);

    // Ni siquiera se intentó la petición: el error es del cliente.
    expect(onGuardar).not.toHaveBeenCalled();
  });

  // AC-40: sin categoría, el motivo aparece junto al selector.
  it('muestra el error de categoría junto a su selector AC-40', async () => {
    const usuario = userEvent.setup();
    const onGuardar = renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '100');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const selector = screen.getByLabelText('Categoría');
    expect(selector).toHaveAttribute('aria-invalid', 'true');
    expect(selector).toHaveAttribute('aria-describedby', 'categoriaId-error');
    expect(document.getElementById('categoriaId-error')).toHaveTextContent(/categoría/i);
    expect(onGuardar).not.toHaveBeenCalled();
  });

  it('conserva lo cargado cuando el formulario se rechaza AC-18 AC-40', async () => {
    const usuario = userEvent.setup();
    renderizar();

    await usuario.type(screen.getByLabelText('Monto'), '10.999');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '3');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    // Nada se pierde: volver a escribir todo es la peor respuesta a un error.
    expect(screen.getByLabelText('Monto')).toHaveValue(10.999);
    expect(screen.getByLabelText('Categoría')).toHaveValue('3');
  });

  it('no usa alert ni un bloque de errores agrupado arriba del formulario', async () => {
    const usuario = userEvent.setup();
    // happy-dom no implementa window.alert, así que vi.spyOn no tiene qué espiar. Se define el
    // stub a mano. jsdom sí lo implementa: es una de las diferencias de fidelidad que costó
    // adoptar happy-dom, anotada en research.md D-10.
    const original = window.alert;
    const alerta = vi.fn();
    window.alert = alerta;

    renderizar();
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    expect(alerta).not.toHaveBeenCalled();

    // Tampoco un bloque de errores agrupado arriba: cada mensaje va al lado de su campo.
    const formulario = screen.getByRole('button', { name: 'Registrar' }).closest('form');
    expect(formulario?.firstElementChild?.tagName).toBe('FIELDSET');

    window.alert = original;
  });

  // T064: el origen del error no cambia dónde se muestra.
  it('enruta un error del servidor al mismo lugar que uno del cliente', async () => {
    const usuario = userEvent.setup();
    const onGuardar = vi.fn().mockRejectedValue(
      new ErrorDeValidacion({
        monto: ['El monto no puede superar 999.999.999,99.'],
        categoriaId: ['La categoría elegida no existe.'],
      }),
    );
    renderizar(onGuardar);

    await usuario.type(screen.getByLabelText('Monto'), '100');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    await waitFor(() =>
      expect(document.getElementById('monto-error')).toHaveTextContent(/999\.999\.999,99/),
    );
    expect(screen.getByLabelText('Monto')).toHaveAttribute('aria-describedby', 'monto-error');
    expect(document.getElementById('categoriaId-error')).toHaveTextContent(/no existe/i);

    // Y lo cargado sigue ahí.
    expect(screen.getByLabelText('Monto')).toHaveValue(100);
  });

  // T066: un error sin campo va a la región de error del formulario.
  it('un fallo al persistir se muestra en la región de error, conservando lo cargado', async () => {
    const usuario = userEvent.setup();
    const onGuardar = vi
      .fn()
      .mockRejectedValue(new ErrorDelServidor(500, 'El servidor respondió 500.'));
    renderizar(onGuardar);

    await usuario.type(screen.getByLabelText('Monto'), '100');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const region = await screen.findByRole('alert');
    expect(region).toHaveTextContent(/no se pudo registrar/i);
    expect(screen.getByLabelText('Monto')).toHaveValue(100);
    expect(screen.getByLabelText('Categoría')).toHaveValue('1');
  });

  // T067: sin esto, dos clicks rápidos registran dos movimientos.
  it('deshabilita el botón mientras se envía, para evitar el doble envío', async () => {
    const usuario = userEvent.setup();
    let resolver: (() => void) | undefined;
    const onGuardar = vi.fn().mockImplementation(
      () =>
        new Promise<void>((resuelve) => {
          resolver = resuelve;
        }),
    );
    renderizar(onGuardar);

    await usuario.type(screen.getByLabelText('Monto'), '100');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: /registrar|enviando/i }));

    await waitFor(() => expect(screen.getByRole('button')).toBeDisabled());

    resolver?.();
    await waitFor(() => expect(screen.getByRole('button')).toBeEnabled());
    expect(onGuardar).toHaveBeenCalledTimes(1);
  });
});

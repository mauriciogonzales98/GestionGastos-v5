import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { CampoConError } from '../src/ui/CampoConError';

// El componente único de campo del punto 2 del Contrato de marcado. Ninguna pantalla arma la
// tripleta label + control + error a mano: si cada una la arma a su manera, el ticket 6 tiene que
// cambiar la presentación en tantos lugares como formularios haya, y alguno queda atrás.
describe('CampoConError', () => {
  it('asocia el label al control por for/id', () => {
    render(
      <CampoConError campo="monto" etiqueta="Monto">
        {(props) => <input type="number" {...props} />}
      </CampoConError>,
    );

    const control = screen.getByLabelText('Monto');
    expect(control).toBeInTheDocument();
    expect(control.id).toBe('monto');
  });

  it('sin error no pone aria-invalid ni aria-describedby', () => {
    render(
      <CampoConError campo="monto" etiqueta="Monto">
        {(props) => <input type="number" {...props} />}
      </CampoConError>,
    );

    const control = screen.getByLabelText('Monto');
    expect(control).not.toHaveAttribute('aria-invalid');
    expect(control).not.toHaveAttribute('aria-describedby');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('con error pone aria-invalid, aria-describedby y un contenedor con role=alert', () => {
    render(
      <CampoConError campo="monto" etiqueta="Monto" error="El monto debe ser mayor a cero.">
        {(props) => <input type="number" {...props} />}
      </CampoConError>,
    );

    const control = screen.getByLabelText('Monto');
    expect(control).toHaveAttribute('aria-invalid', 'true');
    expect(control).toHaveAttribute('aria-describedby', 'monto-error');

    // role="alert" para que el error se anuncie al aparecer, sin mover el foco.
    const alerta = screen.getByRole('alert');
    expect(alerta).toHaveAttribute('id', 'monto-error');
    expect(alerta).toHaveTextContent('El monto debe ser mayor a cero.');
  });

  it('pone el mensaje inmediatamente después del control', () => {
    render(
      <CampoConError campo="monto" etiqueta="Monto" error="Roto.">
        {(props) => <input type="number" {...props} />}
      </CampoConError>,
    );

    const control = screen.getByLabelText('Monto');
    expect(control.nextElementSibling).toBe(screen.getByRole('alert'));
  });
});

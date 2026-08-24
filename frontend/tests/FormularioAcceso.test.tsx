import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');
  return {
    ...real,
    crearCuenta: vi.fn(),
    iniciarSesion: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

beforeEach(() => {
  vi.mocked(cliente.crearCuenta).mockResolvedValue(undefined);
  vi.mocked(cliente.iniciarSesion).mockResolvedValue({ email: 'ana@ejemplo.com' });
});

/**
 * El contrato de marcado de la pantalla de acceso (contracts/ui-pantalla.md).
 *
 * Lo que se verifica no es la apariencia sino lo que un lector de pantalla y un gestor de
 * contraseñas necesitan para funcionar: el vínculo entre campo y error, el tipo del control, y los
 * `autocomplete`. Nada de eso se ve mirando la pantalla, y por eso es lo que se rompe sin que
 * nadie se entere.
 */
describe('FormularioAcceso — el marcado', () => {
  it('arma cada campo con CampoConError y no a mano', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.iniciarSesion).mockRejectedValue(
      new cliente.ErrorDeValidacion({ email: ['Ese email no parece válido.'] }),
    );

    render(<FormularioAcceso onEntrar={vi.fn()} />);

    await usuario.type(screen.getByLabelText('Email'), 'no-es-un-email');
    await usuario.type(screen.getByLabelText('Contraseña'), 'una frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    const mensaje = await screen.findByRole('alert');
    const campo = screen.getByLabelText('Email');

    // La firma observable de `CampoConError`: el control queda inválido y apunta al `id` del
    // mensaje. Armar la tripleta a mano dentro de esta pantalla la dejaría fuera del único lugar
    // donde el ticket 6 va a poder cambiarla.
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(campo).toHaveAttribute('aria-describedby', mensaje.id);
    expect(mensaje).toHaveAttribute('id', 'email-error');
    expect(mensaje).toHaveTextContent('Ese email no parece válido.');
  });

  it('el campo de contraseña es type=password, así el navegador no la muestra en claro', () => {
    render(<FormularioAcceso onEntrar={vi.fn()} />);

    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'password');
  });

  /**
   * Los `autocomplete` no son decoración: son lo que permite a un gestor de contraseñas guardar y
   * ofrecer la credencial correcta, y un gestor de contraseñas es la mejor defensa que tiene quien
   * usa la aplicación. `current-password` en el login y `new-password` en el alta — invertirlos
   * hace que el gestor ofrezca la vieja donde se pide una nueva.
   */
  it('lleva los autocomplete que corresponden a cada modo', async () => {
    const usuario = userEvent.setup();
    render(<FormularioAcceso onEntrar={vi.fn()} />);

    expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'username');
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('autocomplete', 'current-password');

    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));

    expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'username');
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('autocomplete', 'new-password');
  });
});

describe('FormularioAcceso — el teclado', () => {
  /**
   * El mismo criterio que AC-55 de FEAT-001a: se recorre y se envía sin tocar el mouse. Un
   * formulario de acceso que exija el mouse deja afuera a quien no puede usarlo, y es el primer
   * formulario de la aplicación: si falla acá, no se llega a ninguna otra pantalla.
   */
  it('se completa y se envía entero con el teclado, sin un solo click', async () => {
    const usuario = userEvent.setup();
    const onEntrar = vi.fn();

    render(<FormularioAcceso onEntrar={onEntrar} />);

    await usuario.tab();
    await usuario.tab();
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Email'));
    await usuario.keyboard('ana@ejemplo.com');

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Contraseña'));
    await usuario.keyboard('una frase larga');

    // Enter desde el campo de contraseña: lo envía el navegador porque es un `<form>` real con un
    // `<button type="submit">`. Si alguien lo reimplementara con handlers de tecla, esto se rompe.
    await usuario.keyboard('{Enter}');

    expect(vi.mocked(cliente.iniciarSesion)).toHaveBeenCalledWith({
      email: 'ana@ejemplo.com',
      contrasena: 'una frase larga',
    });
    expect(onEntrar).toHaveBeenCalledWith({ email: 'ana@ejemplo.com' });
  });
});

describe('FormularioAcceso — los estados', () => {
  it('el error de credenciales va a la región del formulario y no al lado de un campo', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.iniciarSesion).mockRejectedValue(new cliente.ErrorDeCredenciales());

    render(<FormularioAcceso onEntrar={vi.fn()} />);

    await usuario.type(screen.getByLabelText('Email'), 'ana@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'la que no era');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Email o contraseña incorrectos.');

    // Ninguno de los dos campos queda señalado: señalar uno diría que el otro estaba bien, que es
    // justo lo que NFR-03 no quiere decir.
    expect(screen.getByLabelText('Email')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByLabelText('Contraseña')).not.toHaveAttribute('aria-invalid');
  });

  it('el botón queda deshabilitado mientras se envía, así no se manda dos veces', async () => {
    const usuario = userEvent.setup();
    let resolver: (sesion: { email: string }) => void = () => {};
    vi.mocked(cliente.iniciarSesion).mockReturnValue(
      new Promise((cumplir) => {
        resolver = cumplir;
      }),
    );

    render(<FormularioAcceso onEntrar={vi.fn()} />);

    await usuario.type(screen.getByLabelText('Email'), 'ana@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'una frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(screen.getByRole('button', { name: 'Entrando…' })).toBeDisabled();

    resolver({ email: 'ana@ejemplo.com' });
  });

  /**
   * NFR-03 del lado de la pantalla: el alta muestra el mismo mensaje exista o no la cuenta, porque
   * el servidor responde lo mismo en los dos casos y la pantalla no tiene con qué distinguirlos —
   * ni debe.
   */
  it('tras el alta muestra el mensaje y vuelve a Iniciar sesión', async () => {
    const usuario = userEvent.setup();
    render(<FormularioAcceso onEntrar={vi.fn()} />);

    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    await usuario.type(screen.getByLabelText('Email'), 'ana@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'una frase bien larga');
    await usuario.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));

    expect(vi.mocked(cliente.crearCuenta)).toHaveBeenCalledWith({
      email: 'ana@ejemplo.com',
      contrasena: 'una frase bien larga',
    });

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Si el email no estaba registrado, la cuenta fue creada. Ya podés iniciar sesión.',
    );

    // Vuelve al login con el email puesto: quien acaba de darse de alta lo que quiere es entrar.
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveValue('ana@ejemplo.com');
  });

  it('los errores de validación del alta van cada uno a su campo', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearCuenta).mockRejectedValue(
      new cliente.ErrorDeValidacion({
        contrasena: ['La contraseña tiene que tener al menos 12 caracteres.'],
      }),
    );

    render(<FormularioAcceso onEntrar={vi.fn()} />);

    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    await usuario.type(screen.getByLabelText('Email'), 'ana@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'corta');
    await usuario.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'La contraseña tiene que tener al menos 12 caracteres.',
    );
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('aria-invalid', 'true');
  });
});

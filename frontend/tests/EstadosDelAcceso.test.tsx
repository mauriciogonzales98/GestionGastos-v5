import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');
  return { ...real, crearCuenta: vi.fn(), iniciarSesion: vi.fn() };
});

const cliente = await import('../src/api/cliente');

beforeEach(() => {
  vi.mocked(cliente.crearCuenta).mockResolvedValue(undefined);
  vi.mocked(cliente.iniciarSesion).mockResolvedValue({ email: 'ana@ejemplo.com' });
});

/**
 * US3, FR-004, FR-005, SC-004 — los cuatro estados de la pantalla de acceso se reconocen de un
 * vistazo, y **se distinguen entre sí también sin color**.
 *
 * Los cuatro son: error de un campo, error general del formulario, confirmación del alta y envío en
 * curso. El que motiva la historia es la confirmación: hasta esta feature era texto plano, así que
 * después de crear la cuenta la persona no tenía ninguna señal de que salió bien.
 *
 * **Qué verifica esta prueba y qué no.** No verifica que se vean lindos: eso se mira con los ojos y
 * está en el paso 2 del quickstart. Verifica que cada estado tenga **al menos una distinción que no
 * sea el color** —un ícono propio, un borde más grueso, un atributo— que es lo que `SC-004` pide
 * cuando dice "vistos en escala de grises", y es lo único de eso que se puede automatizar.
 */

async function enviarLogin(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.type(screen.getByLabelText('Email'), 'ana@ejemplo.com');
  await usuario.type(screen.getByLabelText('Contraseña'), 'una frase larga');
  await usuario.click(screen.getByRole('button', { name: 'Entrar' }));
}

describe('FR-004 · el error de un campo se marca en el campo', () => {
  it('marca el campo y pone el motivo pegado a él (FR-004, US3:AC1)', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.iniciarSesion).mockRejectedValue(
      new cliente.ErrorDeValidacion({ email: ['Ese email no parece válido.'] }),
    );

    render(<FormularioAcceso onEntrar={vi.fn()} />);
    await enviarLogin(usuario);

    const campo = await screen.findByLabelText('Email');

    // `aria-invalid` es la distinción sin color: de ahí cuelga el borde grueso de la hoja, y es lo
    // mismo que anuncia el lector de pantalla. Un dato, dos usos (`FR-004`).
    expect(campo).toHaveAttribute('aria-invalid', 'true');

    // Y el motivo está asociado al campo, no suelto en la pantalla.
    expect(campo).toHaveAccessibleDescription('Ese email no parece válido.');
  });
});

describe('FR-013 · el rechazo de credenciales no marca ningún campo', () => {
  /**
   * Es una decisión de seguridad que esta feature **conserva** (`FR-013`).
   *
   * Marcar el campo del email diría que el email existe y la contraseña estaba mal; marcar el de la
   * contraseña, lo mismo al revés. Las dos cosas le dicen a quien prueba direcciones cuáles tienen
   * cuenta. El mensaje va al formulario y no dice cuál de los dos estaba mal.
   */
  it('muestra el error como mensaje del formulario, sin marcar campos (FR-013, US3:AC2)', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.iniciarSesion).mockRejectedValue(new cliente.ErrorDeCredenciales());

    render(<FormularioAcceso onEntrar={vi.fn()} />);
    await enviarLogin(usuario);

    expect(await screen.findByRole('alert')).toBeInTheDocument();

    for (const etiqueta of ['Email', 'Contraseña']) {
      expect(screen.getByLabelText(etiqueta)).not.toHaveAttribute('aria-invalid', 'true');
    }
  });
});

describe('FR-005, SC-004 · el error y la confirmación no se confunden', () => {
  it('la confirmación del alta tiene aspecto de resultado positivo (FR-005, US3:AC3)', async () => {
    const usuario = userEvent.setup();

    render(<FormularioAcceso onEntrar={vi.fn()} />);

    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    await usuario.type(screen.getByLabelText('Email'), 'nueva@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'una frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));

    const confirmacion = await screen.findByRole('status');

    // Bloque de mensaje con la variante de éxito, no texto plano. Era lo que faltaba.
    expect(confirmacion).toHaveClass('c-mensaje', 'c-mensaje--exito');

    // Y el formulario vuelve a "Iniciar sesión" con el email puesto, como hoy (`FR-013`).
    expect(screen.getByRole('button', { name: 'Iniciar sesión' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByLabelText('Email')).toHaveValue('nueva@ejemplo.com');
  });

  /**
   * **La distinción que no es el color**: cada uno lleva su propio ícono.
   *
   * `SC-004` pide que los cuatro estados se distingan vistos en escala de grises. El color del
   * borde y del texto no sirven para eso; un ícono de exclamación y un tilde sí. Lo que se verifica
   * es que los dos bloques **no tengan el mismo dibujo**: si los dos llevaran el mismo ícono, la
   * única diferencia volvería a ser el color.
   */
  it('el error y la confirmación llevan íconos distintos (FR-005, SC-004, US3:AC3)', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.iniciarSesion).mockRejectedValue(new cliente.ErrorDeCredenciales());

    const { container } = render(<FormularioAcceso onEntrar={vi.fn()} />);
    await enviarLogin(usuario);

    const error = await screen.findByRole('alert');
    expect(error).toHaveClass('c-mensaje', 'c-mensaje--error');

    const dibujoDelError = error.querySelector('svg')?.innerHTML;
    expect(dibujoDelError, 'el mensaje de error no tiene ícono').toBeTruthy();

    // Ahora el otro estado, en la misma pantalla.
    vi.mocked(cliente.crearCuenta).mockResolvedValue(undefined);
    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    await usuario.type(screen.getByLabelText('Email'), 'nueva@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'una frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Crear mi cuenta' }));

    const confirmacion = await screen.findByRole('status');
    const dibujoDelExito = confirmacion.querySelector('svg')?.innerHTML;

    expect(dibujoDelExito, 'la confirmación no tiene ícono').toBeTruthy();
    expect(dibujoDelExito).not.toBe(dibujoDelError);

    // Y los íconos no se anuncian: el nombre lo da el texto del mensaje. Dos nombres para lo mismo
    // es ruido para un lector de pantalla.
    for (const svg of container.querySelectorAll('svg')) {
      expect(svg).toHaveAttribute('aria-hidden', 'true');
    }
  });
});

describe('US3:AC4 · el envío en curso se ve y no se puede repetir', () => {
  it('deja el botón ocupado mientras espera la respuesta', async () => {
    const usuario = userEvent.setup();

    let liberar: () => void = () => {};
    vi.mocked(cliente.iniciarSesion).mockReturnValue(
      new Promise((resolve) => {
        liberar = () => resolve({ email: 'ana@ejemplo.com' });
      }),
    );

    render(<FormularioAcceso onEntrar={vi.fn()} />);
    await enviarLogin(usuario);

    const boton = screen.getByRole('button', { name: 'Entrando…' });

    // Deshabilitado: no se puede volver a enviar. Y el texto cambió, que es la señal visible de
    // que algo está pasando — no depende del color.
    expect(boton).toBeDisabled();

    // Y sigue siendo el botón principal: si al deshabilitarse perdiera el relleno de acento, el
    // aspecto cambiaría por completo justo cuando la persona está esperando, y eso se lee como que
    // algo salió mal (`FR-012`).
    expect(boton).toHaveClass('c-boton--principal');

    liberar();
  });
});

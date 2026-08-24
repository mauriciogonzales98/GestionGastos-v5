import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';
import { CATEGORIAS } from './categorias.fixture';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');
  return {
    ...real,
    consultarSesion: vi.fn(),
    cerrarSesion: vi.fn(),
    crearCuenta: vi.fn(),
    iniciarSesion: vi.fn(),
    obtenerCategorias: vi.fn(),
    obtenerMovimientos: vi.fn(),
    crearMovimiento: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

/** Una promesa que este test resuelve cuando quiere, para poder mirar el estado intermedio. */
function promesaControlada<T>() {
  let cumplir: (valor: T) => void = () => {};
  const promesa = new Promise<T>((resolver) => {
    cumplir = resolver;
  });
  return { promesa, cumplir };
}

beforeEach(() => {
  vi.mocked(cliente.consultarSesion).mockResolvedValue({ email: 'ana@ejemplo.com' });
  vi.mocked(cliente.cerrarSesion).mockResolvedValue(undefined);
  vi.mocked(cliente.obtenerCategorias).mockResolvedValue(CATEGORIAS);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([]);
});

describe('App — qué pantalla se muestra', () => {
  /**
   * Mientras se averigua si hay sesión NO se muestra la pantalla de acceso.
   *
   * Mostrarla "por defecto" haría parpadear el login en cada recarga de alguien que sí tiene
   * sesión, y peor: le diría que se desconectó cuando no pasó nada. La consulta a `GET /api/sesion`
   * es la única que sabe, y hasta que responda no hay nada que afirmar.
   */
  it('mientras averigua muestra un indicador y NO la pantalla de acceso', async () => {
    const { promesa, cumplir } = promesaControlada<{ email: string }>();
    vi.mocked(cliente.consultarSesion).mockReturnValue(promesa);

    render(<App hoy="2026-08-24" />);

    expect(screen.getByText('Cargando…')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Entrar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Mis movimientos' })).not.toBeInTheDocument();

    cumplir({ email: 'ana@ejemplo.com' });
    await screen.findByRole('heading', { name: 'Mis movimientos' });
  });

  it('sin sesión muestra la pantalla de acceso', async () => {
    vi.mocked(cliente.consultarSesion).mockRejectedValue(new cliente.ErrorDeSesion());

    render(<App hoy="2026-08-24" />);

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Mis movimientos' })).not.toBeInTheDocument();
  });

  /**
   * AC-03 (FR-003): con sesión iniciada se ve la pantalla de movimientos. Es la mitad del criterio
   * que vive en la pantalla; la otra mitad —que el servidor emita y reconozca la cookie— la
   * verifica `InicioDeSesionTests`.
   */
  it('con sesión muestra la pantalla de movimientos AC-03', async () => {
    render(<App hoy="2026-08-24" />);

    expect(await screen.findByRole('heading', { name: 'Mis movimientos' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Entrar' })).not.toBeInTheDocument();
  });

  it('tras entrar por el formulario pasa a movimientos AC-03', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.consultarSesion).mockRejectedValue(new cliente.ErrorDeSesion());
    vi.mocked(cliente.iniciarSesion).mockResolvedValue({ email: 'ana@ejemplo.com' });

    render(<App hoy="2026-08-24" />);

    await usuario.type(await screen.findByLabelText('Email'), 'ana@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'una frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('heading', { name: 'Mis movimientos' })).toBeInTheDocument();
  });

  /**
   * Un fallo de red al arrancar no es lo mismo que "no hay sesión", aunque las dos cosas terminen
   * en la pantalla de acceso: si no se dijera, alguien con el backend caído vería el login y
   * creería que lo desconectaron, y probaría su contraseña una y otra vez contra nada.
   */
  it('si la consulta inicial falla por red lo dice, en vez de fingir que no hay sesión', async () => {
    vi.mocked(cliente.consultarSesion).mockRejectedValue(
      new cliente.ErrorDeRed(new TypeError('Failed to fetch')),
    );

    render(<App hoy="2026-08-24" />);

    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo contactar al servidor.');
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeInTheDocument();
  });
});

describe('App — cualquier 401 vuelve a la pantalla de acceso', () => {
  /**
   * AC-12: la sesión expira en el servidor y el cliente se entera por el `401` de la petición
   * siguiente. No hay temporizador en el cliente (D-09).
   */
  it('un 401 al cargar el listado devuelve al acceso con el aviso de sesión vencida AC-12', async () => {
    vi.mocked(cliente.obtenerMovimientos).mockRejectedValue(new cliente.ErrorDeSesion());
    vi.mocked(cliente.obtenerCategorias).mockRejectedValue(new cliente.ErrorDeSesion());

    render(<App hoy="2026-08-24" />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Tu sesión venció');
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Mis movimientos' })).not.toBeInTheDocument();
  });

  /**
   * Lo que se estaba haciendo no desaparece en silencio.
   *
   * Si el `401` llega al enviar un movimiento, no alcanza con volver al login: hay que decir que
   * ESE movimiento no se registró. Vaciar la pantalla sin explicación es la peor versión de una
   * sesión vencida — quien la sufre no sabe si guardó o no, y termina cargándolo dos veces o
   * ninguna.
   */
  it('un 401 al registrar un movimiento avisa que ese movimiento no se guardó AC-12', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearMovimiento).mockRejectedValue(new cliente.ErrorDeSesion());

    render(<App hoy="2026-08-24" />);

    await screen.findByRole('button', { name: 'Registrar' });
    await usuario.type(screen.getByLabelText('Monto'), '800');
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('Tu sesión venció');
    expect(aviso).toHaveTextContent('no se registró');
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeInTheDocument();
  });
});

describe('App — cierre de sesión', () => {
  /**
   * AC-06 (FR-005): cerrar la sesión devuelve a la pantalla de acceso. No lleva aviso de sesión
   * vencida: no venció, se cerró a propósito, y decir lo contrario sería mentirle a quien acaba de
   * apretar el botón.
   */
  it('cerrar sesión vuelve al acceso, sin el aviso de sesión vencida AC-06', async () => {
    const usuario = userEvent.setup();

    render(<App hoy="2026-08-24" />);

    await usuario.click(await screen.findByRole('button', { name: 'Cerrar sesión' }));

    expect(vi.mocked(cliente.cerrarSesion)).toHaveBeenCalled();
    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  /**
   * Si el `DELETE` falla, la sesión se cierra igual del lado del cliente.
   *
   * Quien apretó "Cerrar sesión" quiso irse. Dejarlo dentro porque el servidor no contestó es lo
   * contrario de lo que pidió, y en una máquina compartida es un problema de verdad. La cookie
   * sigue viva del otro lado, pero eso lo resuelve la expiración de 24 h.
   */
  it('si el cierre falla igual saca de la pantalla, porque irse es lo que se pidió', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.cerrarSesion).mockRejectedValue(
      new cliente.ErrorDeRed(new TypeError('Failed to fetch')),
    );

    render(<App hoy="2026-08-24" />);

    await usuario.click(await screen.findByRole('button', { name: 'Cerrar sesión' }));

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument();
  });
});

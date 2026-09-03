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
    crearCategoria: vi.fn(),
    renombrarCategoria: vi.fn(),
    darDeBajaCategoria: vi.fn(),
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
  // Los contadores de llamadas se limpian entre tests: sin esto, un test que cuenta cuántas veces
  // se pidió el catálogo mide lo que hicieron todos los anteriores del archivo.
  vi.clearAllMocks();

  vi.mocked(cliente.consultarSesion).mockResolvedValue({ email: 'ana@ejemplo.com' });
  vi.mocked(cliente.cerrarSesion).mockResolvedValue(undefined);
  vi.mocked(cliente.obtenerCategorias).mockResolvedValue(CATEGORIAS);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([]);
  vi.mocked(cliente.darDeBajaCategoria).mockResolvedValue(undefined);
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

describe('App — el catálogo de categorías', () => {
  /**
   * AC-12 (FR-018): al cargar la pantalla, el catálogo se pide **exactamente una vez**.
   *
   * Es lo que fija D-08. El catálogo lo necesitan dos pantallas —el selector del formulario y la
   * gestión— y la salida fácil es que cada una lo pida por su cuenta: dos peticiones al arrancar, y
   * peor, dos copias que se desincronizan en cuanto una crea una categoría y la otra no se entera.
   *
   * El test cuenta llamadas y no mira la pantalla a propósito: la desincronización no se ve, se
   * cuenta.
   */
  it('pide el catálogo exactamente una vez al cargar AC-12', async () => {
    render(<App hoy="2026-08-24" />);

    await screen.findByRole('heading', { name: 'Mis movimientos' });

    expect(vi.mocked(cliente.obtenerCategorias)).toHaveBeenCalledTimes(1);
  });

  /**
   * Y sigue siendo una sola después de ir a la gestión de categorías y volver.
   *
   * Es la mitad que un contador tomado sólo al arrancar no ve: el catálogo puede pedirse una vez y
   * volver a pedirse en cada cambio de vista, que es exactamente lo que pasa si vive en la pantalla
   * en vez de arriba.
   */
  it('no vuelve a pedir el catálogo al ir a categorías y volver AC-12', async () => {
    const usuario = userEvent.setup();

    render(<App hoy="2026-08-24" />);

    await usuario.click(await screen.findByRole('button', { name: 'Categorías' }));
    await screen.findByRole('heading', { name: 'Mis categorías' });

    await usuario.click(screen.getByRole('button', { name: 'Volver a movimientos' }));
    await screen.findByRole('heading', { name: 'Mis movimientos' });

    expect(vi.mocked(cliente.obtenerCategorias)).toHaveBeenCalledTimes(1);
  });
});

describe('App — el catálogo se comparte entre las dos pantallas', () => {
  /**
   * AC-13 y FR-019: lo que se crea en la gestión aparece en el selector del formulario **al
   * volver**, sin recargar y sin una segunda petición del catálogo.
   *
   * Es la razón entera por la que el catálogo subió a la raíz (D-08). Con una copia por pantalla,
   * esto pasaría igual la primera vez que se probara a mano —porque la pantalla se remonta y vuelve
   * a pedir— y el costo sería una petición de más en cada ida y vuelta. Por eso el test cuenta
   * llamadas además de mirar la pantalla.
   */
  it('una categoría creada en la gestión aparece en el selector al volver AC-13', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.crearCategoria).mockResolvedValue({
      id: 43,
      nombre: 'Mascotas',
      tipo: 'gasto',
      esPropia: true,
    });

    render(<App hoy="2026-08-24" />);

    await usuario.click(await screen.findByRole('button', { name: 'Categorías' }));
    await usuario.type(await screen.findByLabelText('Nombre'), 'Mascotas');
    await usuario.click(screen.getByRole('button', { name: 'Crear categoría' }));

    expect(vi.mocked(cliente.crearCategoria)).toHaveBeenCalledWith({
      nombre: 'Mascotas',
      tipo: 'gasto',
    });

    await usuario.click(screen.getByRole('button', { name: 'Volver a movimientos' }));

    const selector = await screen.findByLabelText('Categoría');
    expect(selector).toContainHTML('<option value="43">Mascotas</option>');

    // Y el catálogo no se volvió a pedir: la lista se actualizó con lo que devolvió el alta.
    expect(vi.mocked(cliente.obtenerCategorias)).toHaveBeenCalledTimes(1);
  });

  it('un renombre en la gestión se ve en el selector al volver AC-13', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.obtenerCategorias).mockResolvedValue([
      ...CATEGORIAS,
      { id: 43, nombre: 'Gimnasio', tipo: 'gasto', esPropia: true },
    ]);
    vi.mocked(cliente.renombrarCategoria).mockResolvedValue({
      id: 43,
      nombre: 'Gimnasio y pileta',
      tipo: 'gasto',
      esPropia: true,
    });

    render(<App hoy="2026-08-24" />);

    await usuario.click(await screen.findByRole('button', { name: 'Categorías' }));
    await usuario.click(await screen.findByRole('button', { name: 'Renombrar Gimnasio' }));

    const campo = screen.getByLabelText('Nombre nuevo');
    await usuario.clear(campo);
    await usuario.type(campo, 'Gimnasio y pileta');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await usuario.click(screen.getByRole('button', { name: 'Volver a movimientos' }));

    const selector = await screen.findByLabelText('Categoría');
    expect(selector).toContainHTML('<option value="43">Gimnasio y pileta</option>');
    expect(selector).not.toContainHTML('>Gimnasio<');
    expect(vi.mocked(cliente.obtenerCategorias)).toHaveBeenCalledTimes(1);
  });

  /**
   * Si el catálogo no se puede cargar, se dice. La petición vive en la raíz desde la feature 007,
   * así que el aviso nace acá — pero se muestra en la pantalla de movimientos, que es la que queda
   * inservible sin categorías.
   */
  /**
   * Y el aviso se va con la sesión que lo produjo.
   *
   * `errorDelCatalogo` vive en la raíz igual que el catálogo, así que le sobrevivía a la cuenta que
   * falló: la siguiente entraba con la red ya sana, veía su selector completo **y** el cartel de
   * "no se pudieron cargar las categorías" al lado, contradiciéndolo.
   */
  it('el aviso de fallo no le sobrevive a la sesión que lo produjo', async () => {
    const usuario = userEvent.setup();
    vi.mocked(cliente.obtenerCategorias).mockRejectedValueOnce(
      new cliente.ErrorDelServidor(500, 'boom'),
    );
    vi.mocked(cliente.iniciarSesion).mockResolvedValue({ email: 'bruno@ejemplo.com' });

    render(<App hoy="2026-08-24" />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/categor/i);

    await usuario.click(screen.getByRole('button', { name: 'Cerrar sesión' }));
    await screen.findByRole('button', { name: 'Entrar' });

    await usuario.type(screen.getByLabelText('Email'), 'bruno@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'otra frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    // El catálogo de Bruno carga bien: no hay nada que avisar.
    expect(await screen.findByRole('option', { name: 'Comida' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('si falla la carga del catálogo lo dice en vez de ofrecer un selector vacío', async () => {
    vi.mocked(cliente.obtenerCategorias).mockRejectedValue(
      new cliente.ErrorDelServidor(500, 'boom'),
    );

    render(<App hoy="2026-08-24" />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/categor/i);
  });
});

describe('App — lo que la sesión se lleva al cerrarse', () => {
  /**
   * FR-002 y FR-012 del lado del cliente: **el catálogo no sobrevive a la cuenta que lo pidió.**
   *
   * El backend nunca le manda a una cuenta las categorías propias de otra, y hasta acá eso alcanzaba
   * porque el catálogo vivía dentro de la pantalla y moría con ella. Desde D-08 vive en la raíz, que
   * no se desmonta al cerrar sesión: si nadie lo limpia, la lista sigue en memoria y la cuenta
   * siguiente la ve mientras su propia carga viaja.
   *
   * El escenario es la máquina compartida, sin nada que forzar: Ana tiene una categoría propia con
   * un nombre que no quiere que nadie lea, cierra sesión, y entra Bruno en la misma pestaña.
   *
   * La segunda carga se deja **sin resolver** a propósito. Ahí está el agujero: si se la dejara
   * responder, taparía la lista vieja y el test pasaría con o sin arreglo.
   */
  it('el catálogo de una cuenta no se le muestra a la siguiente FR-002', async () => {
    const usuario = userEvent.setup();
    const deAna = [...CATEGORIAS, { id: 40, nombre: 'Psicólogo', tipo: 'gasto', esPropia: true }];
    vi.mocked(cliente.obtenerCategorias).mockResolvedValueOnce(deAna);
    vi.mocked(cliente.iniciarSesion).mockResolvedValue({ email: 'bruno@ejemplo.com' });

    render(<App hoy="2026-08-24" />);

    // Ana entra, ve lo suyo y se va.
    expect(await screen.findByRole('option', { name: 'Psicólogo' })).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Cerrar sesión' }));
    await screen.findByRole('button', { name: 'Entrar' });

    // La carga de Bruno queda en el aire: es la ventana en la que se ve lo que quedó.
    const { promesa } = promesaControlada<typeof CATEGORIAS>();
    vi.mocked(cliente.obtenerCategorias).mockReturnValue(promesa);

    await usuario.type(screen.getByLabelText('Email'), 'bruno@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'otra frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    await screen.findByRole('heading', { name: 'Mis movimientos' });
    expect(screen.queryByRole('option', { name: 'Psicólogo' })).not.toBeInTheDocument();
  });
});

describe('App — la vista también se va con la sesión', () => {
  /**
   * Se cierra sesión estando en la gestión de categorías, y la cuenta siguiente entra a
   * **movimientos**, no a "Mis categorías".
   *
   * `vista` es estado de la raíz igual que el catálogo (D-09, FR-018), y por el mismo motivo que
   * aquél tampoco se desmonta al terminar la sesión. Dejarlo puesto hace que quien entra caiga en
   * una pantalla que no pidió, y —antes de que el catálogo se vaciara— en la lista de otro con los
   * botones de renombrar y dar de baja encima.
   */
  it('entrar de nuevo lleva a movimientos aunque se haya salido desde la gestión FR-018', async () => {
    const usuario = userEvent.setup();
    const propia = { id: 41, nombre: 'Gimnasio', tipo: 'gasto', esPropia: true } as const;
    vi.mocked(cliente.obtenerCategorias).mockResolvedValue([...CATEGORIAS, propia]);
    vi.mocked(cliente.iniciarSesion).mockResolvedValue({ email: 'bruno@ejemplo.com' });

    render(<App hoy="2026-08-24" />);

    await usuario.click(await screen.findByRole('button', { name: 'Categorías' }));
    await screen.findByRole('heading', { name: 'Mis categorías' });

    // La gestión no tiene botón de salir: la sesión se termina sola, que es como se llega acá.
    vi.mocked(cliente.consultarSesion).mockRejectedValue(new cliente.ErrorDeSesion());
    vi.mocked(cliente.darDeBajaCategoria).mockRejectedValue(new cliente.ErrorDeSesion());
    await usuario.click(screen.getByRole('button', { name: 'Dar de baja Gimnasio' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar la baja' }));
    await screen.findByRole('button', { name: 'Entrar' });

    await usuario.type(screen.getByLabelText('Email'), 'bruno@ejemplo.com');
    await usuario.type(screen.getByLabelText('Contraseña'), 'otra frase larga');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('heading', { name: 'Mis movimientos' })).toBeInTheDocument();
  });
});

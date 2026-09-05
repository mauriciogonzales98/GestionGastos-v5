import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  cerrarSesion,
  consultarSesion,
  ErrorDeCredenciales,
  ErrorDeRed,
  ErrorDelServidor,
  ErrorDeSesion,
  ErrorDeValidacion,
  iniciarSesion,
  obtenerCategorias,
  obtenerResumen,
} from '../src/api/cliente';

function responderCon(cuerpo: string, init: ResponseInit) {
  // Un Response nuevo por llamada: el cuerpo se consume una sola vez, así que reutilizar la misma
  // instancia hace fallar la segunda petición por una razón que no es la que se está probando.
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation(() => Promise.resolve(new Response(cuerpo, init))),
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('cliente HTTP', () => {
  it('devuelve el JSON cuando la respuesta es 200', async () => {
    responderCon('[{"id":1,"nombre":"Comida","tipo":"gasto"}]', { status: 200 });

    await expect(obtenerCategorias()).resolves.toHaveLength(1);
  });

  // El caso que dejaba la pantalla cargando para siempre: sin proxy, Vite respondía su index.html
  // con 200 y `respuesta.ok` era true.
  it('un 200 con cuerpo que no es JSON sale como ErrorDelServidor y no como SyntaxError', async () => {
    responderCon('<!doctype html><html><body>Vite</body></html>', { status: 200 });

    await expect(obtenerCategorias()).rejects.toBeInstanceOf(ErrorDelServidor);
  });

  it('un 400 con cuerpo que no es JSON tampoco escapa sin tipar', async () => {
    responderCon('<html>error del proxy</html>', { status: 400 });

    await expect(obtenerCategorias()).rejects.toBeInstanceOf(ErrorDelServidor);
  });

  it('un 400 con ProblemDetails sale como ErrorDeValidacion con sus mensajes por campo', async () => {
    responderCon(JSON.stringify({ status: 400, errors: { monto: ['Ingresá un monto.'] } }), {
      status: 400,
    });

    await expect(obtenerCategorias()).rejects.toMatchObject({
      name: 'ErrorDeValidacion',
      errores: { monto: ['Ingresá un monto.'] },
    });
    await expect(obtenerCategorias()).rejects.toBeInstanceOf(ErrorDeValidacion);
  });

  it('un 500 sale como ErrorDelServidor con su código', async () => {
    responderCon('', { status: 500 });

    await expect(obtenerCategorias()).rejects.toMatchObject({ estado: 500 });
  });

  it('si no se llega al servidor sale como ErrorDeRed, con la causa adentro', async () => {
    const causa = new TypeError('Failed to fetch');
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(causa));

    await expect(obtenerCategorias()).rejects.toBeInstanceOf(ErrorDeRed);
    await expect(obtenerCategorias()).rejects.toMatchObject({ causa });
  });
});

describe('cliente HTTP — la sesión', () => {
  /**
   * Cualquier `401`, de cualquier petición, sale como `ErrorDeSesion` y no como un
   * `ErrorDelServidor` con estado 401 (D-09). Es lo que permite que la aplicación entera reaccione
   * igual sin que cada pantalla tenga que leer un número.
   */
  it('un 401 de cualquier petición sale como ErrorDeSesion', async () => {
    responderCon('', { status: 401 });

    await expect(obtenerCategorias()).rejects.toBeInstanceOf(ErrorDeSesion);
    await expect(consultarSesion()).rejects.toBeInstanceOf(ErrorDeSesion);
  });

  /**
   * Salvo en el login: ahí el 401 significa "esas credenciales no son", no "tu sesión venció".
   * Confundirlos mostraría el aviso de sesión vencida a alguien que nunca tuvo una.
   */
  it('el 401 del login sale como ErrorDeCredenciales y no como ErrorDeSesion', async () => {
    responderCon(JSON.stringify({ status: 401, title: 'Email o contraseña incorrectos.' }), {
      status: 401,
    });

    const fallo = iniciarSesion({ email: 'ana@ejemplo.com', contrasena: 'la que no era' });

    await expect(fallo).rejects.toBeInstanceOf(ErrorDeCredenciales);
    await expect(fallo).rejects.not.toBeInstanceOf(ErrorDeSesion);
  });

  /**
   * El `DELETE` responde `204`, sin cuerpo. Leerlo como JSON fallaría con un cuerpo vacío y el
   * cierre de sesión saldría como `ErrorDelServidor` habiendo funcionado perfectamente.
   */
  it('el cierre de sesión no intenta leer el cuerpo vacío de un 204', async () => {
    responderCon('', { status: 204 });

    await expect(cerrarSesion()).resolves.toBeUndefined();
  });

  /**
   * Sin `credentials: 'include'` el navegador no manda la cookie de sesión y TODAS las peticiones
   * vuelven 401, incluso recién iniciada la sesión. Es la clase de detalle que no se ve en ninguna
   * pantalla hasta que nada funciona.
   */
  it('manda la cookie de sesión en cada petición', async () => {
    responderCon('[]', { status: 200 });

    await obtenerCategorias();

    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      '/api/categorias',
      expect.objectContaining({ credentials: 'include' }),
    );
  });
});

describe('cliente HTTP — el resumen del período', () => {
  /**
   * Devuelve la URL con la que se llamó a `fetch`, para poder afirmar sobre la query string.
   *
   * Se mira la URL y no sólo el resultado porque lo que este bloque verifica es **qué se le pidió
   * al servidor**, que es de donde sale el período: el mes por omisión lo decide él, y lo decide
   * ante la AUSENCIA de los dos parámetros.
   */
  function urlPedida(): string {
    const llamada = vi.mocked(fetch).mock.calls[0];
    return String(llamada[0]);
  }

  const RESPUESTA = JSON.stringify({ desde: '2026-09-01', hasta: '2026-09-30', monedas: [] });

  it('sin argumentos pide /api/resumen sin ninguna query string', async () => {
    responderCon(RESPUESTA, { status: 200 });

    await obtenerResumen();

    // Sin `?`, y no con `?desde=&hasta=`. La diferencia importa: el servidor entiende la ausencia
    // de los dos como "el mes en curso, que decido yo", y dos parámetros vacíos lo obligarían a
    // interpretar una cadena vacía como si fuera una fecha.
    expect(urlPedida()).toBe('/api/resumen');
  });

  it('con un rango manda desde y hasta', async () => {
    responderCon(RESPUESTA, { status: 200 });

    await obtenerResumen('2026-08-01', '2026-08-31');

    expect(urlPedida()).toBe('/api/resumen?desde=2026-08-01&hasta=2026-08-31');
  });

  it('un 401 sale como ErrorDeSesion, igual que el resto del cliente', async () => {
    responderCon('', { status: 401 });

    await expect(obtenerResumen()).rejects.toBeInstanceOf(ErrorDeSesion);
  });
});

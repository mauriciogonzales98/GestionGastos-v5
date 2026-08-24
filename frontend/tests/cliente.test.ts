import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ErrorDeRed,
  ErrorDelServidor,
  ErrorDeValidacion,
  obtenerCategorias,
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

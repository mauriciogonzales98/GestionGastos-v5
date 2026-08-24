import type { Categoria, Movimiento, NuevoMovimiento, ProblemDetails } from './tipos';

/**
 * Errores tipados, como exige `AGENTS.md`: quien llama distingue por el tipo qué pasó y qué hacer,
 * en vez de leer un mensaje. Y nunca hay un catch silencioso — lo que no se sabe manejar se
 * propaga.
 */

/** El servidor rechazó la petición por validación (400). Trae los mensajes por campo. */
export class ErrorDeValidacion extends Error {
  constructor(readonly errores: Record<string, string[]>) {
    super('La petición fue rechazada por validación.');
    this.name = 'ErrorDeValidacion';
  }
}

/** El servidor falló (5xx) o devolvió algo que no se puede interpretar. */
export class ErrorDelServidor extends Error {
  constructor(
    readonly estado: number,
    mensaje: string,
  ) {
    super(mensaje);
    this.name = 'ErrorDelServidor';
  }
}

/** No se pudo llegar al servidor: sin red, servidor caído, petición abortada. */
export class ErrorDeRed extends Error {
  constructor(readonly causa: unknown) {
    super('No se pudo contactar al servidor.');
    this.name = 'ErrorDeRed';
  }
}

/**
 * Lee el cuerpo como JSON sin dejar escapar un error sin tipar.
 *
 * `respuesta.json()` lanza un SyntaxError crudo cuando el cuerpo no es JSON, y ese error no es
 * ninguno de los tres tipos que este módulo promete. Pasa más seguido de lo que parece: un proxy
 * mal configurado, un portal cautivo o un servidor de desarrollo que responde su index.html con
 * 200 devuelven HTML donde el cliente espera JSON, y `respuesta.ok` es true igual.
 */
async function leerJson<T>(respuesta: Response): Promise<T> {
  let texto: string;

  try {
    texto = await respuesta.text();
  } catch (causa) {
    // La conexión se cortó con la respuesta a medio leer.
    throw new ErrorDeRed(causa);
  }

  try {
    return JSON.parse(texto) as T;
  } catch {
    throw new ErrorDelServidor(
      respuesta.status,
      `El servidor respondió ${respuesta.status} con un cuerpo que no es JSON.`,
    );
  }
}

async function pedir<T>(url: string, opciones?: RequestInit): Promise<T> {
  let respuesta: Response;

  try {
    respuesta = await fetch(url, opciones);
  } catch (causa) {
    // Se envuelve, no se traga: quien llama necesita distinguir "no llegué" de "me rechazaron".
    throw new ErrorDeRed(causa);
  }

  if (respuesta.ok) {
    return leerJson<T>(respuesta);
  }

  if (respuesta.status === 400) {
    const problema = await leerJson<ProblemDetails>(respuesta);
    // Un 400 sin `errors` es un rechazo sin campo señalado: se enruta a la región de error del
    // formulario, no a un control.
    throw new ErrorDeValidacion(problema.errors ?? {});
  }

  throw new ErrorDelServidor(respuesta.status, `El servidor respondió ${respuesta.status}.`);
}

export function obtenerCategorias(): Promise<Categoria[]> {
  return pedir<Categoria[]>('/api/categorias');
}

export function obtenerMovimientos(): Promise<Movimiento[]> {
  return pedir<Movimiento[]>('/api/movimientos');
}

export function crearMovimiento(nuevo: NuevoMovimiento): Promise<Movimiento> {
  return pedir<Movimiento>('/api/movimientos', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(nuevo),
  });
}

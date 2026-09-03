import type {
  Categoria,
  CategoriaEditada,
  Credenciales,
  Movimiento,
  NuevaCategoria,
  NuevaCuenta,
  NuevoMovimiento,
  ProblemDetails,
  SesionActual,
} from './tipos';

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

/**
 * No hay sesión, o la que había venció (AC-12).
 *
 * Es un tipo aparte y no un `ErrorDelServidor` con estado 401 porque la aplicación entera reacciona
 * a esto de una sola manera —volver a la pantalla de acceso— y esa reacción no se decide leyendo un
 * número. La cookie es `HttpOnly`, así que el cliente no puede saber que venció hasta que el
 * servidor se lo dice: este error es ese aviso (D-09).
 */
export class ErrorDeSesion extends Error {
  constructor() {
    super('La sesión no está iniciada o venció.');
    this.name = 'ErrorDeSesion';
  }
}

/**
 * El inicio de sesión rechazó las credenciales.
 *
 * Nace del mismo 401 que `ErrorDeSesion`, y aun así es otro error: en `POST /api/sesion` un 401 no
 * significa "tu sesión venció" sino "esas credenciales no son". Confundirlos haría que un login
 * fallido mostrara el aviso de sesión vencida a alguien que nunca tuvo una.
 *
 * No dice cuál de los dos campos estaba mal porque el servidor tampoco lo dice (NFR-03).
 */
export class ErrorDeCredenciales extends Error {
  constructor() {
    super('Email o contraseña incorrectos.');
    this.name = 'ErrorDeCredenciales';
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

/**
 * Manda la petición y traduce el rechazo a uno de los tipos de este módulo. Devuelve la respuesta
 * sin leerla: quién la interpreta —y si tiene cuerpo— lo decide quien llama.
 */
async function enviar(url: string, opciones?: RequestInit): Promise<Response> {
  let respuesta: Response;

  try {
    // `credentials: 'include'` va DESPUÉS del spread y no antes: así ninguna llamada puede
    // desactivarlo sin querer. Sin esto el navegador no manda la cookie de sesión y todas las
    // peticiones vuelven 401, incluso recién iniciada la sesión.
    respuesta = await fetch(url, { ...opciones, credentials: 'include' });
  } catch (causa) {
    // Se envuelve, no se traga: quien llama necesita distinguir "no llegué" de "me rechazaron".
    throw new ErrorDeRed(causa);
  }

  if (respuesta.ok) {
    return respuesta;
  }

  // Antes que el 400 y que el caso general: un 401 tiene una reacción propia en toda la aplicación
  // y no se parece en nada a "el servidor falló" (D-09).
  if (respuesta.status === 401) {
    throw new ErrorDeSesion();
  }

  if (respuesta.status === 400) {
    const problema = await leerJson<ProblemDetails>(respuesta);
    // Un 400 sin `errors` sigue siendo un rechazo de validación, así que se propaga como tal con
    // el diccionario vacío. Quién lo muestra y dónde lo decide el formulario: `repartir()` manda a
    // la región general todo lo que no tenga un campo donde ir.
    throw new ErrorDeValidacion(problema.errors ?? {});
  }

  throw new ErrorDelServidor(respuesta.status, `El servidor respondió ${respuesta.status}.`);
}

async function pedir<T>(url: string, opciones?: RequestInit): Promise<T> {
  return leerJson<T>(await enviar(url, opciones));
}

/**
 * Para las respuestas cuyo cuerpo no se usa: un `204` no tiene ninguno, y leerlo como JSON
 * fallaría con un cuerpo vacío.
 */
async function pedirSinCuerpo(url: string, opciones?: RequestInit): Promise<void> {
  await enviar(url, opciones);
}

/** Las cabeceras de un cuerpo JSON. Se repiten en cada POST y se olvidan de a una. */
const JSON_ENVIADO = { 'Content-Type': 'application/json' };

export function obtenerCategorias(): Promise<Categoria[]> {
  return pedir<Categoria[]>('/api/categorias');
}

/**
 * Crea una categoría propia (FR-004). Devuelve la creada, en la misma forma que el catálogo.
 *
 * Que devuelva la categoría y no `void` es lo que permite insertarla en el catálogo que la pantalla
 * ya tiene, sin volver a pedirlo (FR-019, AC-13).
 */
export function crearCategoria(nueva: NuevaCategoria): Promise<Categoria> {
  return pedir<Categoria>('/api/categorias', {
    method: 'POST',
    headers: JSON_ENVIADO,
    body: JSON.stringify(nueva),
  });
}

/** Renombra una categoría propia (FR-007). Devuelve la categoría ya modificada. */
export function renombrarCategoria(id: number, cambio: CategoriaEditada): Promise<Categoria> {
  return pedir<Categoria>(`/api/categorias/${id}`, {
    method: 'PUT',
    headers: JSON_ENVIADO,
    body: JSON.stringify(cambio),
  });
}

/**
 * Da de baja una categoría propia (FR-010). La fila no se borra: deja de ofrecerse y sigue
 * nombrando los movimientos que ya la usan.
 *
 * Es idempotente del lado del servidor: darle de baja a algo ya dado de baja también responde
 * `204`, así que esto no falla por llegar tarde.
 */
export function darDeBajaCategoria(id: number): Promise<void> {
  return pedirSinCuerpo(`/api/categorias/${id}`, { method: 'DELETE' });
}

export function obtenerMovimientos(): Promise<Movimiento[]> {
  return pedir<Movimiento[]>('/api/movimientos');
}

export function crearMovimiento(nuevo: NuevoMovimiento): Promise<Movimiento> {
  return pedir<Movimiento>('/api/movimientos', {
    method: 'POST',
    headers: JSON_ENVIADO,
    body: JSON.stringify(nuevo),
  });
}

/**
 * Crea una cuenta (FR-001).
 *
 * El `201` trae un mensaje y esta función lo descarta: el texto que ve la persona lo pone la
 * pantalla. No es un descuido — NFR-03 exige que el mensaje sea el mismo exista o no la cuenta, y
 * una constante del cliente lo garantiza más fuerte que confiar en que el servidor no divida ese
 * texto en dos algún día.
 */
export function crearCuenta(nueva: NuevaCuenta): Promise<void> {
  return pedirSinCuerpo('/api/cuentas', {
    method: 'POST',
    headers: JSON_ENVIADO,
    body: JSON.stringify(nueva),
  });
}

/** Inicia sesión (FR-003). La cookie la pone el servidor; acá no se guarda nada. */
export async function iniciarSesion(credenciales: Credenciales): Promise<SesionActual> {
  try {
    return await pedir<SesionActual>('/api/sesion', {
      method: 'POST',
      headers: JSON_ENVIADO,
      body: JSON.stringify(credenciales),
    });
  } catch (error) {
    // Acá el 401 no es una sesión vencida: es un rechazo de credenciales. Se traduce en el único
    // lugar donde se sabe qué endpoint respondió.
    if (error instanceof ErrorDeSesion) {
      throw new ErrorDeCredenciales();
    }
    throw error;
  }
}

/** La cuenta en sesión, o `ErrorDeSesion` si no hay. Es lo que la aplicación pregunta al arrancar. */
export function consultarSesion(): Promise<SesionActual> {
  return pedir<SesionActual>('/api/sesion');
}

/**
 * Cierra la sesión (FR-005). Es idempotente del lado del servidor: cerrar una que ya no existe
 * también responde `204`, así que esto no falla por llegar tarde.
 */
export function cerrarSesion(): Promise<void> {
  return pedirSinCuerpo('/api/sesion', { method: 'DELETE' });
}

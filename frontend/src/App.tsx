import { useCallback, useEffect, useState } from 'react';
import { FormularioAcceso } from './acceso/FormularioAcceso';
import {
  ErrorDeSesion,
  cerrarSesion,
  consultarSesion,
  crearCategoria,
  darDeBajaCategoria,
  obtenerCategorias,
  renombrarCategoria,
} from './api/cliente';
import { PantallaCategorias } from './categorias/PantallaCategorias';
import type { Categoria, NuevaCategoria, SesionActual } from './api/tipos';
import { PantallaMovimientos } from './movimientos/PantallaMovimientos';

export interface PropsApp {
  /** El día de hoy en `YYYY-MM-DD`. Entra por prop para que los tests sean deterministas. */
  hoy: string;
}

/**
 * Averiguando · sin sesión · con sesión. El estado arranca en `averiguando` y no en `sin-sesion`
 * porque todavía no se sabe: afirmar que no hay sesión antes de preguntar haría parpadear el login
 * en cada recarga de quien sí la tiene.
 */
type Estado = 'averiguando' | 'sin-sesion' | 'con-sesion';

/**
 * Qué se está mirando con la sesión abierta. Igual que `Estado`, no son dos rutas sino un estado
 * con dos valores: no hay router y no hace falta uno (D-09, FR-018).
 */
type Vista = 'movimientos' | 'categorias';

/**
 * Con qué vista arranca una sesión. **Siempre movimientos**, y por eso es una constante y no un
 * literal suelto en tres lugares: es el valor inicial y también al que se vuelve cuando la sesión
 * termina, y esas dos cosas tienen que ser la misma o la próxima cuenta entra donde salió la
 * anterior.
 */
const VISTA_INICIAL: Vista = 'movimientos';

const SESION_VENCIDA = 'Tu sesión venció. Volvé a entrar.';

/**
 * Inserta una categoría en el catálogo respetando el orden del contrato: por tipo y después por
 * identificador.
 *
 * Se inserta en lugar de recargar el catálogo entero porque el servidor ya devolvió la categoría
 * completa, justamente para no tener que volver a pedirla (FR-019, AC-13). Es la misma decisión que
 * `insertarEnOrden` para movimientos.
 *
 * El orden se replica acá y no se hereda del servidor porque esta lista deja de ser la que el
 * servidor mandó en cuanto se le agrega algo. Si el contrato cambiara de orden, esto tiene que
 * cambiar con él.
 */
export function insertarCategoriaEnOrden(catalogo: Categoria[], nueva: Categoria): Categoria[] {
  const rango = (c: Categoria) => (c.tipo === 'gasto' ? 0 : 1);

  const posicion = catalogo.findIndex(
    (c) => rango(c) > rango(nueva) || (rango(c) === rango(nueva) && c.id > nueva.id),
  );

  if (posicion === -1) {
    return [...catalogo, nueva];
  }

  return [...catalogo.slice(0, posicion), nueva, ...catalogo.slice(posicion)];
}

/**
 * La raíz: decide qué pantalla se muestra según haya sesión o no (D-08).
 *
 * No hay router. No son dos rutas que enrutar sino un estado con dos valores: nadie navega a
 * `/login`, se llega ahí por no tener sesión. Quien gobierna es la respuesta del servidor, no la
 * URL — y así no existe la URL que alguien pueda escribir a mano para ver la pantalla protegida
 * antes de que el servidor diga que no.
 */
export function App({ hoy }: PropsApp) {
  const [estado, setEstado] = useState<Estado>('averiguando');
  const [sesion, setSesion] = useState<SesionActual | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [vista, setVista] = useState<Vista>(VISTA_INICIAL);

  /**
   * **El catálogo vive acá y en ningún otro lado** (D-08).
   *
   * Lo necesitan dos pantallas —el selector del formulario y la gestión— y la salida fácil sería
   * que cada una lo pidiera por su cuenta. Serían dos peticiones al arrancar (AC-12 pide una) y,
   * peor, dos copias que se desincronizan en cuanto una crea una categoría: la persona la crea en
   * la gestión, vuelve al formulario y no está.
   *
   * **Y por eso mismo hay que vaciarlo al cerrar la sesión.** La raíz no se desmonta cuando la
   * sesión termina, así que un catálogo que vive acá le sobrevive a la cuenta que lo pidió: la
   * siguiente entra en la misma pestaña y ve las categorías propias de la anterior durante todo lo
   * que tarde su propia carga — justo lo que el servidor se cuida de no mandarle nunca (FR-002,
   * FR-012). Se vacía en las dos salidas, el cierre a propósito y el 401.
   *
   * `errorDelCatalogo` se va con él y por lo mismo: un fallo de carga de una cuenta le quedaba
   * puesto a la siguiente, que veía su selector completo y el cartel de que no se pudo cargar al
   * lado, diciendo lo contrario de lo que la pantalla mostraba.
   */
  const [categorias, setCategorias] = useState<Categoria[]>([]);
  const [errorDelCatalogo, setErrorDelCatalogo] = useState<string | null>(null);

  useEffect(() => {
    void consultarSesion()
      .then((actual) => {
        setSesion(actual);
        setEstado('con-sesion');
      })
      .catch((error: unknown) => {
        setEstado('sin-sesion');

        // Sin sesión es el caso normal al arrancar y no lleva aviso: nadie necesita que le digan
        // que no inició sesión todavía. Cualquier OTRO error sí, porque termina en la misma
        // pantalla por un motivo completamente distinto —el servidor no contestó— y confundirlos
        // haría que alguien probara su contraseña una y otra vez contra nada.
        if (!(error instanceof ErrorDeSesion)) {
          setAviso('No se pudo contactar al servidor. Revisá la conexión y volvé a intentar.');
        }
      });
  }, []);

  /**
   * La reacción única a cualquier `401`, venga de donde venga (D-09).
   *
   * `useCallback` no es una optimización: `PantallaMovimientos` la usa dentro de un `useEffect`, y
   * una función nueva en cada render volvería a disparar la carga inicial en bucle.
   */
  const alVencerLaSesion = useCallback((motivo: string) => {
    setSesion(null);
    setEstado('sin-sesion');
    setAviso(motivo);
    setCategorias([]);
    setErrorDelCatalogo(null);
    setVista(VISTA_INICIAL);
  }, []);

  /**
   * El catálogo se pide una sola vez, cuando aparece la sesión (AC-12).
   *
   * Depende de `estado` y no de `sesion`: `sesion` es un objeto nuevo en cada respuesta y volvería
   * a disparar la carga. `estado` es una cadena y sólo cambia cuando cambia de verdad.
   */
  useEffect(() => {
    if (estado !== 'con-sesion') {
      return;
    }

    void obtenerCategorias()
      .then(setCategorias)
      .catch((error: unknown) => {
        // Un 401 no es "falló la carga": es que ya no hay sesión. La reacción es volver al acceso,
        // no mostrar un error de carga sobre una pantalla protegida.
        if (error instanceof ErrorDeSesion) {
          alVencerLaSesion(SESION_VENCIDA);
          return;
        }

        setErrorDelCatalogo(
          'No se pudieron cargar las categorías. Revisá la conexión y recargá la página.',
        );
      });
  }, [estado, alVencerLaSesion]);

  /**
   * Las tres operaciones que modifican el catálogo. Viven acá porque acá vive el estado: la
   * pantalla de gestión las invoca y la lista se actualiza para las DOS pantallas a la vez, sin
   * recargar y sin una segunda petición (FR-019, AC-13).
   *
   * **Ninguna traga su error.** Un `401` se traduce a la reacción de siempre y se vuelve a lanzar;
   * cualquier otro sube tal cual, para que la pantalla lo muestre al lado del campo que
   * corresponda. Tragarlo dejaría a la persona creyendo que guardó.
   */
  async function crear(nueva: NuevaCategoria) {
    try {
      const creada = await crearCategoria(nueva);
      setCategorias((previas) => insertarCategoriaEnOrden(previas, creada));
    } catch (error) {
      if (error instanceof ErrorDeSesion) {
        alVencerLaSesion('Tu sesión venció y la categoría no se creó. Volvé a entrar.');
      }
      throw error;
    }
  }

  async function renombrar(id: number, nombre: string) {
    try {
      const renombrada = await renombrarCategoria(id, { nombre });
      setCategorias((previas) => previas.map((c) => (c.id === id ? renombrada : c)));
    } catch (error) {
      if (error instanceof ErrorDeSesion) {
        alVencerLaSesion('Tu sesión venció y el cambio no se guardó. Volvé a entrar.');
      }
      throw error;
    }
  }

  async function darDeBaja(id: number) {
    try {
      await darDeBajaCategoria(id);
      // Sale del catálogo, que es lo único que esta capa sabe de la baja: la fila sigue existiendo
      // del otro lado y sigue nombrando los movimientos que la usan (FR-010, FR-011).
      setCategorias((previas) => previas.filter((c) => c.id !== id));
    } catch (error) {
      if (error instanceof ErrorDeSesion) {
        alVencerLaSesion('Tu sesión venció y la baja no se guardó. Volvé a entrar.');
      }
      throw error;
    }
  }

  async function salir() {
    try {
      await cerrarSesion();
    } catch {
      // No es un catch silencioso: la salida ocurre igual, y eso es la decisión. Quien apretó
      // "Cerrar sesión" quiso irse; dejarlo dentro porque el servidor no contestó es lo contrario
      // de lo que pidió, y en una máquina compartida es un problema de verdad. La cookie sigue
      // viva del otro lado hasta que expire, y eso ya está acotado a 24 h.
    }

    setSesion(null);
    setEstado('sin-sesion');
    // Sin aviso: no venció, se cerró a propósito. Decir "tu sesión venció" acá sería mentirle a
    // quien acaba de apretar el botón.
    setAviso(null);
    setCategorias([]);
    setErrorDelCatalogo(null);
    setVista(VISTA_INICIAL);
  }

  if (estado === 'averiguando') {
    return (
      <main className="l-pila">
        <p>Cargando…</p>
      </main>
    );
  }

  if (estado === 'con-sesion' && sesion) {
    if (vista === 'categorias') {
      return (
        <PantallaCategorias
          categorias={categorias}
          onCrear={crear}
          onRenombrar={renombrar}
          onDarDeBaja={darDeBaja}
          onVolver={() => setVista('movimientos')}
        />
      );
    }

    return (
      <PantallaMovimientos
        hoy={hoy}
        email={sesion.email}
        categorias={categorias}
        errorDelCatalogo={errorDelCatalogo}
        onCerrarSesion={() => void salir()}
        onSesionVencida={alVencerLaSesion}
        onGestionarCategorias={() => setVista('categorias')}
      />
    );
  }

  return (
    <>
      {/* Antes del formulario y con role="alert": es lo que explica por qué se está viendo esta
          pantalla en lugar de la otra. */}
      {aviso ? <p role="alert">{aviso}</p> : null}
      <FormularioAcceso
        onEntrar={(actual) => {
          setSesion(actual);
          setEstado('con-sesion');
          setAviso(null);
        }}
      />
    </>
  );
}

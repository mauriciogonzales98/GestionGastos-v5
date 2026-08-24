import { useEffect, useState } from 'react';
import {
  ErrorDeSesion,
  crearMovimiento,
  obtenerCategorias,
  obtenerMovimientos,
} from '../api/cliente';
import type { Categoria, Movimiento, NuevoMovimiento } from '../api/tipos';
import { FormularioMovimiento } from './FormularioMovimiento';
import { ListadoMovimientos } from './ListadoMovimientos';

export interface PropsPantallaMovimientos {
  /** El día de hoy en `YYYY-MM-DD`. Entra por prop para que los tests sean deterministas. */
  hoy: string;
  /** El email de la cuenta en sesión, para que se vea con cuál se está trabajando (FR-004). */
  email: string;
  onCerrarSesion: () => void;
  /**
   * Se llama cuando una petición vuelve `401`, con el aviso que explica qué pasó y qué se perdió.
   *
   * Es obligatoria y no tiene valor por defecto a propósito: un no-op implícito se tragaría el
   * 401 y dejaría la pantalla mostrando datos de una sesión que ya no existe.
   */
  onSesionVencida: (aviso: string) => void;
}

const SESION_VENCIDA = 'Tu sesión venció. Volvé a entrar.';

/**
 * Inserta el movimiento en su posición según `fecha DESC, id DESC`.
 *
 * No se agrega al final ni se recarga la lista entera: el servidor ya devolvió el movimiento
 * completo justamente para no tener que volver a pedirlo (FR-014).
 */
export function insertarEnOrden(movimientos: Movimiento[], nuevo: Movimiento): Movimiento[] {
  const posicion = movimientos.findIndex(
    (m) => m.fecha < nuevo.fecha || (m.fecha === nuevo.fecha && m.id < nuevo.id),
  );

  if (posicion === -1) {
    return [...movimientos, nuevo];
  }

  return [...movimientos.slice(0, posicion), nuevo, ...movimientos.slice(posicion)];
}

/** `true` si la fecha `YYYY-MM-DD` cae en el mismo mes calendario que `hoy`. */
export function esDelMesDe(fecha: string, hoy: string): boolean {
  return fecha.slice(0, 7) === hoy.slice(0, 7);
}

/**
 * Formulario y listado en una sola pantalla (FR-013). No hay navegación intermedia ni una segunda
 * ruta: no hay adónde ir todavía.
 */
export function PantallaMovimientos({
  hoy,
  email,
  onCerrarSesion,
  onSesionVencida,
}: PropsPantallaMovimientos) {
  const [categorias, setCategorias] = useState<Categoria[]>([]);
  const [movimientos, setMovimientos] = useState<Movimiento[]>([]);
  const [cargandoListado, setCargandoListado] = useState(true);
  const [confirmacion, setConfirmacion] = useState<string | null>(null);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);

  useEffect(() => {
    // Las dos cargas van por separado a propósito: el formulario ya es usable mientras el listado
    // carga, y si una falla la otra puede seguir sirviendo. Un Promise.all las ataría.
    //
    // Cada una tiene su `catch`. Sin ellos, un backend caído dejaba el indicador de carga
    // encendido para siempre y el selector de categorías vacío: guardar era imposible y la única
    // señal era un unhandled rejection en la consola, que nadie mira.
    void obtenerCategorias()
      .then(setCategorias)
      .catch((error: unknown) => {
        // Un 401 no es "falló la carga": es que ya no hay sesión, y quedarse acá mostrando un
        // error de carga dejaría la pantalla protegida a la vista con los datos de una sesión
        // muerta. La reacción la decide la raíz (D-09).
        if (error instanceof ErrorDeSesion) {
          onSesionVencida(SESION_VENCIDA);
          return;
        }

        setErrorDeCarga(
          'No se pudieron cargar las categorías. Revisá la conexión y recargá la página.',
        );
      });

    void obtenerMovimientos()
      .then(setMovimientos)
      .catch((error: unknown) => {
        if (error instanceof ErrorDeSesion) {
          onSesionVencida(SESION_VENCIDA);
          return;
        }

        setErrorDeCarga('No se pudo cargar el listado de movimientos. Recargá la página.');
      })
      // El indicador se apaga pase lo que pase. Dejarlo encendido tras un fallo es decirle a la
      // persona que espere algo que no va a llegar.
      .finally(() => setCargandoListado(false));
  }, [onSesionVencida]);

  async function guardar(nuevo: NuevoMovimiento) {
    let creado: Movimiento;

    try {
      creado = await crearMovimiento(nuevo);
    } catch (error) {
      if (!(error instanceof ErrorDeSesion)) {
        // Los demás errores los muestra el formulario, que además conserva lo cargado.
        throw error;
      }

      // La pantalla está por desaparecer, así que el aviso tiene que decir qué pasó con ESTE
      // movimiento. Volver al login sin explicar deja a la persona sin saber si guardó o no, y
      // termina cargándolo dos veces o ninguna.
      onSesionVencida(
        'Tu sesión venció y el movimiento no se registró. Volvé a entrar y cargalo de nuevo.',
      );
      return;
    }

    if (esDelMesDe(creado.fecha, hoy)) {
      setMovimientos((previos) => insertarEnOrden(previos, creado));
      setConfirmacion('Movimiento registrado.');
      return;
    }

    // Se guardó igual, pero el listado sólo muestra el mes actual. Si la confirmación no lo
    // dijera, la persona vería que no aparece y creería que se perdió.
    setConfirmacion(
      `Movimiento registrado con fecha ${creado.fecha}. Como no es de este mes, no aparece en el listado.`,
    );
  }

  return (
    <main className="l-pila">
      <div className="l-fila l-cabecera">
        <h1>Mis movimientos</h1>
        {/* Se ve con qué cuenta se está trabajando (FR-004): sin esto, dos cuentas en el mismo
            navegador son indistinguibles hasta que alguien carga un gasto en la equivocada. */}
        <p>{email}</p>
        {/* Un `<button>` y no un enlace: cambia estado del servidor, y los enlaces son para
            navegar. Un enlace acá además sería seguible por un prefetch del navegador. */}
        <button type="button" onClick={onCerrarSesion}>
          Cerrar sesión
        </button>
      </div>

      <FormularioMovimiento categorias={categorias} hoy={hoy} onGuardar={guardar} />

      {/* role="status" y no "alert": es una confirmación, no un error, y se anuncia sin
          interrumpir lo que la persona esté haciendo. */}
      {confirmacion ? <p role="status">{confirmacion}</p> : null}

      {/* role="alert": la carga falló y no hay nada que la persona pueda hacer desde el formulario
          para enterarse sola. */}
      {errorDeCarga ? <p role="alert">{errorDeCarga}</p> : null}

      {cargandoListado ? (
        <p>Cargando movimientos…</p>
      ) : (
        <ListadoMovimientos movimientos={movimientos} />
      )}
    </main>
  );
}

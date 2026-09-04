import { useEffect, useState } from 'react';
import { ErrorDeSesion, crearMovimiento, obtenerMovimientos } from '../api/cliente';
import type { Categoria, Moneda, Movimiento, NuevoMovimiento } from '../api/tipos';
import { FormularioMovimiento } from './FormularioMovimiento';
import { ListadoMovimientos } from './ListadoMovimientos';

export interface PropsPantallaMovimientos {
  /** El día de hoy en `YYYY-MM-DD`. Entra por prop para que los tests sean deterministas. */
  hoy: string;
  /** El email de la cuenta en sesión, para que se vea con cuál se está trabajando (FR-004). */
  email: string;
  /**
   * El catálogo que alimenta el selector. **Baja por props y ya no se pide acá** (D-08): vive en la
   * raíz para que esta pantalla y la de gestión miren la misma lista, y para que se pida una sola
   * vez (AC-12).
   */
  categorias: Categoria[];
  /** El catálogo de monedas, también desde la raíz y por el mismo motivo (AC-12). */
  monedas: Moneda[];
  /** El aviso de que el catálogo no se pudo cargar, si pasó. Lo produce la raíz, que es quien pide. */
  errorDelCatalogo: string | null;
  onCerrarSesion: () => void;
  /** Lleva a la pantalla de gestión del catálogo (FR-017). */
  onGestionarCategorias: () => void;
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
  categorias,
  monedas,
  errorDelCatalogo,
  onCerrarSesion,
  onGestionarCategorias,
  onSesionVencida,
}: PropsPantallaMovimientos) {
  const [movimientos, setMovimientos] = useState<Movimiento[]>([]);
  const [cargandoListado, setCargandoListado] = useState(true);
  const [confirmacion, setConfirmacion] = useState<string | null>(null);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);

  useEffect(() => {
    // El catálogo ya no se pide acá: lo carga la raíz una sola vez y baja por props (D-08, AC-12).
    // Queda el listado, con su propio `catch`: sin él, un backend caído dejaba el indicador de
    // carga encendido para siempre y la única señal era un unhandled rejection en la consola, que
    // nadie mira.
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
        <button type="button" onClick={onGestionarCategorias}>
          Categorías
        </button>
        {/* Un `<button>` y no un enlace: cambia estado del servidor, y los enlaces son para
            navegar. Un enlace acá además sería seguible por un prefetch del navegador. */}
        <button type="button" onClick={onCerrarSesion}>
          Cerrar sesión
        </button>
      </div>

      <FormularioMovimiento
        categorias={categorias}
        monedas={monedas}
        hoy={hoy}
        onGuardar={guardar}
      />

      {/* role="status" y no "alert": es una confirmación, no un error, y se anuncia sin
          interrumpir lo que la persona esté haciendo. */}
      {confirmacion ? <p role="status">{confirmacion}</p> : null}

      {/* role="alert": la carga falló y no hay nada que la persona pueda hacer desde el formulario
          para enterarse sola. */}
      {errorDeCarga ? <p role="alert">{errorDeCarga}</p> : null}

      {/* El fallo de carga del catálogo lo produce la raíz, que es quien lo pide, pero se muestra
          acá: es esta pantalla la que queda inservible sin él —sin categorías no se puede
          registrar nada— y es donde la persona lo va a notar. */}
      {errorDelCatalogo ? <p role="alert">{errorDelCatalogo}</p> : null}

      {cargandoListado ? (
        <p>Cargando movimientos…</p>
      ) : (
        <ListadoMovimientos movimientos={movimientos} />
      )}
    </main>
  );
}

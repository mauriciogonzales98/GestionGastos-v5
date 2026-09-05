import { useEffect, useId, useState } from 'react';
import {
  ErrorDeSesion,
  crearMovimiento,
  editarMovimiento,
  obtenerMovimientos,
} from '../api/cliente';
import type {
  Categoria,
  Moneda,
  Movimiento,
  MovimientoEditado,
  NuevoMovimiento,
} from '../api/tipos';
import { FormularioMovimiento } from './FormularioMovimiento';
import { ListadoMovimientos } from './ListadoMovimientos';
import { VentanaDeEdicion } from './VentanaDeEdicion';

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
  /**
   * Lo mismo para el catálogo de monedas.
   *
   * Es una prop aparte y no el mismo texto porque las consecuencias son distintas: sin categorías
   * no se puede registrar nada, sin monedas sí — se registra en la predeterminada. Un solo aviso
   * tendría que mentir en uno de los dos casos.
   */
  errorDelCatalogoDeMonedas: string | null;
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
  errorDelCatalogoDeMonedas,
  onCerrarSesion,
  onGestionarCategorias,
  onSesionVencida,
}: PropsPantallaMovimientos) {
  const [movimientos, setMovimientos] = useState<Movimiento[]>([]);

  /**
   * La moneda a la que está acotado el listado. `''` es "todas" (FR-008).
   *
   * Cadena y no `number | null` porque es el valor de un `<select>`, y convertirlo de ida y vuelta
   * en cada render abre la posibilidad de que el control muestre una cosa y el estado guarde otra.
   */
  const [monedaAcotada, setMonedaAcotada] = useState('');

  /**
   * El movimiento que se está corrigiendo, o `null` si la ventana está cerrada (FR-011).
   *
   * Se guarda el movimiento entero y no su id: la ventana necesita todos sus valores para abrir con
   * los campos cargados, y buscarlo por id en la lista sería el mismo dato con un paso de más que
   * puede fallar si la lista cambió abajo.
   */
  const [enEdicion, setEnEdicion] = useState<Movimiento | null>(null);
  const [cargandoListado, setCargandoListado] = useState(true);
  const idAcotado = useId();
  const [confirmacion, setConfirmacion] = useState<string | null>(null);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);

  useEffect(() => {
    // El catálogo ya no se pide acá: lo carga la raíz una sola vez y baja por props (D-08, AC-12).
    // Queda el listado, con su propio `catch`: sin él, un backend caído dejaba el indicador de
    // carga encendido para siempre y la única señal era un unhandled rejection en la consola, que
    // nadie mira.
    /**
     * **La guarda contra la respuesta que llega tarde.**
     *
     * Desde que el acotado existe, este efecto corre más de una vez y puede haber dos peticiones en
     * vuelo. Si la primera tarda más, resuelve última y su `setMovimientos` se escribe encima del
     * resultado correcto: el listado termina mostrando lo que se pidió antes, con el control
     * diciendo otra cosa. Sin error y sin nada en la consola — la pantalla contradiciéndose sola.
     *
     * El `cleanup` de React corre **antes** de volver a lanzar el efecto, así que apagar esta
     * bandera es exactamente "lo que pedí dejó de importar". No se cancela la petición: cancelarla
     * sería mejor y exige un `AbortController` hasta el `fetch`; descartar la respuesta arregla lo
     * que se ve, que es el daño.
     */
    let vigente = true;

    void obtenerMovimientos({ monedaId: monedaAcotada === '' ? null : Number(monedaAcotada) })
      .then((traidos) => {
        if (vigente) {
          setMovimientos(traidos);

          // El error de la carga ANTERIOR se va **cuando ésta sale bien**, no cuando empieza. Sin
          // esto, un fallo inicial dejaba el cartel puesto sobre un listado que después cargó bien:
          // decía que no se pudo cargar exactamente lo que la persona estaba mirando. No podía
          // pasar antes del acotado, porque el listado se pedía una sola vez y del error no se
          // salía sin recargar.
          //
          // Va acá y no al principio del efecto por dos razones que apuntan al mismo lado: el
          // linter prohíbe `setState` sincrónico dentro de un efecto —cuesta un render extra—, y
          // limpiarlo al empezar borraría el cartel también cuando la carga nueva vuelve a fallar,
          // dejando un instante sin ninguna señal y después el mismo error de vuelta.
          setErrorDeCarga(null);
        }
      })
      .catch((error: unknown) => {
        if (error instanceof ErrorDeSesion) {
          onSesionVencida(SESION_VENCIDA);
          return;
        }

        if (vigente) {
          setErrorDeCarga('No se pudo cargar el listado de movimientos. Recargá la página.');
        }
      })
      // El indicador se apaga pase lo que pase. Dejarlo encendido tras un fallo es decirle a la
      // persona que espere algo que no va a llegar.
      .finally(() => {
        if (vigente) {
          setCargandoListado(false);
        }
      });

    return () => {
      vigente = false;
    };
    // `monedaAcotada` en las dependencias: el acotado lo hace el SERVIDOR, así que cambiarlo tiene
    // que volver a pedir. Filtrar del lado del cliente la lista que ya se tenía se vería igual y
    // estaría mal — mostraría sólo lo que ya se había traído.
  }, [onSesionVencida, monedaAcotada]);

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

  /**
   * Guarda la corrección y **reemplaza la fila con lo que devolvió el servidor**, sin volver a
   * pedir el listado (FR-011).
   *
   * No se reordena: la edición puede cambiar la fecha, y con ella el lugar que le toca. Reordenar
   * acá duplicaría la regla que `insertarEnOrden` ya tiene; recargar la traería del servidor. Se
   * elige reemplazar en el lugar y dejar el reordenamiento para la próxima carga — que es lo que la
   * lista ya hace hoy con cualquier otro cambio.
   */
  async function guardarEdicion(id: number, cambio: MovimientoEditado) {
    let editado: Movimiento;

    try {
      editado = await editarMovimiento(id, cambio);
    } catch (error) {
      if (!(error instanceof ErrorDeSesion)) {
        // Los demás errores los muestra la ventana, que además conserva lo cargado.
        throw error;
      }

      onSesionVencida(
        'Tu sesión venció y el cambio no se guardó. Volvé a entrar e intentá de nuevo.',
      );
      return;
    }

    setMovimientos((previos) => previos.map((m) => (m.id === editado.id ? editado : m)));
    setEnEdicion(null);
    setConfirmacion('Movimiento actualizado.');
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

      {/* `role="status"` y no `alert`: no impide trabajar, así que se anuncia sin interrumpir. */}
      {errorDelCatalogoDeMonedas ? <p role="status">{errorDelCatalogoDeMonedas}</p> : null}

      {/* El acotado por moneda (FR-008, FR-010). Es el ÚNICO control de acotado de esta pantalla:
          el servidor también acota por categoría y por rango de fechas desde FEAT-001b, y esos dos
          nunca tuvieron interfaz. Es la deuda D9-01, y acá es donde la barra va a crecer. */}
      <div className="l-fila">
        <label htmlFor={idAcotado}>Ver sólo la moneda</label>
        <select
          id={idAcotado}
          value={monedaAcotada}
          onChange={(e) => setMonedaAcotada(e.target.value)}
        >
          <option value="">Todas las monedas</option>
          {monedas.map((m) => (
            <option key={m.id} value={m.id}>
              {m.nombre}
            </option>
          ))}
        </select>
      </div>

      {cargandoListado ? (
        <p>Cargando movimientos…</p>
      ) : (
        <ListadoMovimientos movimientos={movimientos} onEditar={setEnEdicion} />
      )}

      {/* La ventana se monta sólo cuando hay algo que editar, y se desmonta al cerrarse. Montarla
          siempre y esconderla dejaría sus campos con los valores del movimiento anterior la próxima
          vez que se abriera. */}
      {enEdicion ? (
        <VentanaDeEdicion
          movimiento={enEdicion}
          categorias={categorias}
          monedas={monedas}
          hoy={hoy}
          onGuardar={(cambio) => guardarEdicion(enEdicion.id, cambio)}
          onCerrar={() => setEnEdicion(null)}
        />
      ) : null}
    </main>
  );
}

import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ErrorDeSesion,
  ErrorDeValidacion,
  crearMovimiento,
  editarMovimiento,
  eliminarMovimiento,
  obtenerMovimientos,
  obtenerResumen,
} from '../api/cliente';
import type { AcotadoDelListado } from '../api/cliente';
import type {
  Categoria,
  Moneda,
  Movimiento,
  MovimientoEditado,
  NuevoMovimiento,
  Resumen,
} from '../api/tipos';
import { ResumenDelPeriodo } from '../resumen/ResumenDelPeriodo';
import { FormularioMovimiento } from './FormularioMovimiento';
import { FiltrosDelListado } from './FiltrosDelListado';
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
  /** Lleva al dashboard, donde se elige el período y la moneda que se miran (FR-011b). */
  onVerDashboard: () => void;
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
  onVerDashboard,
  onSesionVencida,
}: PropsPantallaMovimientos) {
  const [movimientos, setMovimientos] = useState<Movimiento[]>([]);

  /**
   * Los acotados **aplicados**, que no son los elegidos: los elegidos viven en la barra y sólo
   * llegan acá cuando alguien aprieta "Aplicar" (D-06). Un objeto vacío es "todo".
   *
   * Desde la feature 011 son los cuatro que el servidor entiende desde FEAT-001b, y no sólo la
   * moneda: es la deuda D9-01 saldada.
   */
  const [acotado, setAcotado] = useState<AcotadoDelListado>({});

  /** El mensaje con el que el servidor rechazó el período, si lo rechazó (FR-018). */
  const [errorDelPeriodo, setErrorDelPeriodo] = useState<string | null>(null);

  /**
   * El movimiento que se está corrigiendo, o `null` si la ventana está cerrada (FR-011).
   *
   * Se guarda el movimiento entero y no su id: la ventana necesita todos sus valores para abrir con
   * los campos cargados, y buscarlo por id en la lista sería el mismo dato con un paso de más que
   * puede fallar si la lista cambió abajo.
   */
  const [enEdicion, setEnEdicion] = useState<Movimiento | null>(null);
  const [cargandoListado, setCargandoListado] = useState(true);
  const [confirmacion, setConfirmacion] = useState<string | null>(null);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);

  /**
   * El resumen del mes en curso (FR-011). **Su estado vive acá y no en `App.tsx`** (D-06).
   *
   * Izarlo a la raíz es el atajo que cualquiera tomaría por analogía con los catálogos, que sí
   * viven allá desde la feature 007. Pero aquéllos se izaron porque las dos pantallas necesitan
   * **el mismo dato**, y acá pasa lo contrario: esta pantalla y el dashboard necesitan **dos
   * períodos distintos** del mismo cálculo. Con un único `resumen` en la raíz, elegir un trimestre
   * en el dashboard cambiaría estos números — que es exactamente lo que FR-012 prohíbe, y sería
   * invisible en la pantalla donde se produce.
   *
   * La regla, en sus dos mitades: se iza lo que es el mismo dato para todos; no se iza lo que cada
   * pantalla parametriza distinto.
   */
  const [resumen, setResumen] = useState<Resumen | null>(null);
  const [errorDelResumen, setErrorDelResumen] = useState<string | null>(null);

  /**
   * **La guarda contra la respuesta que llega tarde**, para el resumen.
   *
   * El listado de esta misma pantalla la tiene desde la feature 009 y el dashboard desde la 010;
   * acá faltaba. Dos guardados seguidos disparan dos recargas, y si la primera tarda más resuelve
   * última: el total del mes queda mostrando una suma que **no incluye** el movimiento que sí se ve
   * en el listado, dos centímetros más abajo. Sin error y sin nada en la consola.
   *
   * Es un contador y no la bandera `vigente` del listado porque estas recargas no las lanza un
   * efecto —las lanzan el alta y la edición—, así que no hay `cleanup` donde apagar nada. Cada
   * llamada se lleva su número y sólo escribe si sigue siendo la última que salió.
   */
  const ultimaRecarga = useRef(0);

  /**
   * Adónde va el foco cuando la fila que lo tenía deja de existir (D-09).
   *
   * El botón que se apretó desaparece en el mismo render, y el navegador manda el foco al `<body>`:
   * quien navega con teclado queda al principio de la página sin ningún anuncio de que la acción
   * salió bien, y se entera apretando Tab hasta volver a encontrar el listado. Es la variante de
   * `FR-005` que sólo aparece cuando algo se borra, y por eso ninguna pantalla anterior se la
   * encontró.
   *
   * El destino es el encabezado del listado y no la fila siguiente: la fila siguiente falla en los
   * dos bordes —eliminar la última, eliminar la única— con un caso especial cada uno, y el
   * encabezado siempre existe.
   */
  const encabezadoDelListado = useRef<HTMLHeadingElement>(null);

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

    void obtenerMovimientos(acotado)
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
          setErrorDelPeriodo(null);
        }
      })
      .catch((error: unknown) => {
        if (error instanceof ErrorDeSesion) {
          onSesionVencida(SESION_VENCIDA);
          return;
        }

        if (!vigente) {
          return;
        }

        /**
         * **El rechazo del período va al lado de los campos que lo produjeron** (FR-018).
         *
         * `PeriodoPedido` rechaza un rango invertido y un rango con un solo extremo, bajo la clave
         * `rango` — una clave que existe, según su propio comentario, "porque el frontend la usa
         * para poner el mensaje al lado del control". La pantalla no reimplementa esas reglas:
         * muestra la que volvió.
         *
         * Y el listado **conserva lo que estaba mostrando**: no se vacía. Lo que se rechazó fue el
         * pedido nuevo; lo que estaba en pantalla sigue siendo el resultado válido del anterior, y
         * borrarlo diría que no hay movimientos, que es otra cosa.
         */
        if (error instanceof ErrorDeValidacion) {
          const motivos = Object.values(error.errores).flat();
          setErrorDelPeriodo(
            motivos[0] ?? 'El período pedido no es válido. Revisá las dos fechas.',
          );
          return;
        }

        // No dice "recargá la página": desde que existe el acotado hay un camino de recuperación
        // sin recargar —cambiarlo vuelve a pedir— y pedir una recarga sugeriría que no lo hay.
        setErrorDeCarga('No se pudo cargar el listado de movimientos. Volvé a intentarlo.');
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
    // `acotado` en las dependencias: el acotado lo hace el SERVIDOR, así que cambiarlo tiene que
    // volver a pedir. Filtrar del lado del cliente la lista que ya se tenía se vería igual y estaría
    // mal — mostraría sólo lo que ya se había traído (FR-017).
  }, [onSesionVencida, acotado]);

  /**
   * Vuelve a pedir el resumen del mes.
   *
   * **Se pide sin período**: el mes en curso lo decide el servidor, y que el filtro exista en el
   * dashboard no convierte a este valor por omisión en algo que el cliente elija (FR-011b).
   *
   * Se llama después de cada alta y de cada edición, y **siempre**, sin averiguar antes si el
   * movimiento cae o no en el mes: averiguarlo obligaría a decidir acá si un total cambió, que es
   * la clase de cuenta que FR-014 saca de la pantalla. Un total no se puede insertar como se
   * inserta una fila — hay que recalcularlo, y el que recalcula es el servidor.
   *
   * `useCallback` no es una optimización: la usa un `useEffect`, y una función nueva en cada render
   * volvería a disparar la carga en bucle. Es el mismo motivo por el que `alVencerLaSesion` lo es
   * en la raíz.
   */
  const recargarResumen = useCallback(() => {
    const mia = ++ultimaRecarga.current;

    return obtenerResumen()
      .then((traido) => {
        // Salió otra después: lo que ésta trae ya no es lo que hay que mostrar.
        if (mia !== ultimaRecarga.current) {
          return;
        }

        setResumen(traido);

        // El error de la carga anterior se va **cuando ésta sale bien**, no cuando empieza. Es la
        // misma regla que el listado, y por la misma cicatriz: un cartel que sobrevive a una carga
        // buena dice que no se pudo cargar justo lo que la persona está mirando.
        setErrorDelResumen(null);
      })
      .catch((error: unknown) => {
        // Un 401 no es "falló la carga": es que ya no hay sesión, y la reacción es volver al acceso
        // en vez de mostrar un error de carga sobre una pantalla protegida (FR-017).
        //
        // Va **antes** de la guarda y no después: que la respuesta haya quedado vieja no cambia que
        // la sesión se terminó, y callarlo dejaría la pantalla protegida a la vista sin sesión.
        if (error instanceof ErrorDeSesion) {
          onSesionVencida(SESION_VENCIDA);
          return;
        }

        if (mia !== ultimaRecarga.current) {
          return;
        }

        // Se dice, y se dice **como fallo**. Mostrar ceros acá sería la pantalla afirmando que no
        // hubo movimientos, que es lo contrario de lo que pasó (FR-010).
        setErrorDelResumen('No se pudo cargar el resumen del mes. Volvé a intentarlo.');
      });
  }, [onSesionVencida]);

  useEffect(() => {
    void recargarResumen();
  }, [recargarResumen]);

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

    void recargarResumen();

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

    setEnEdicion(null);
    void recargarResumen();

    // **Si dejó de cumplir el acotado, sale del listado.** Es el caso de uso central de la ventana
    // —corregir la moneda— y sin esto la fila corregida queda visible bajo un control que dice
    // estar mostrando otra moneda: el listado contradiciendo lo que él mismo declara filtrar.
    //
    // La comparación es por CÓDIGO contra el catálogo, porque el movimiento trae el código y el
    // acotado guarda el identificador. Si el catálogo todavía no llegó, no se saca nada: preferir
    // dejarla de más antes que hacerla desaparecer por no poder comprobarlo.
    //
    // **Sólo la moneda, y no los otros dos acotados.** Una edición también puede sacar la fila del
    // rango de fechas o de la categoría acotada, y eso no se comprueba: haría falta reimplementar
    // acá las reglas del período, que es lo que `PeriodoPedido` existe para impedir. La fila queda
    // de más hasta la próxima carga, que es lo mismo que ya pasaba con el reordenamiento.
    const codigoAcotado = monedas.find((m) => m.id === acotado.monedaId)?.codigo;

    if (codigoAcotado !== undefined && editado.monedaCodigo !== codigoAcotado) {
      setMovimientos((previos) => previos.filter((m) => m.id !== editado.id));

      // Se dice, no se hace en silencio: la persona corrigió algo y necesita saber que salió bien
      // aunque la fila desaparezca. Mismo criterio con el que el alta avisa cuando el movimiento
      // queda fuera del mes del listado.
      setConfirmacion(
        'Movimiento actualizado. Como ya no es de la moneda que estás viendo, salió del listado.',
      );
      return;
    }

    setMovimientos((previos) => previos.map((m) => (m.id === editado.id ? editado : m)));
    setConfirmacion('Movimiento actualizado.');
  }

  /**
   * Elimina un movimiento ya confirmado y deja la pantalla coherente (FR-010, FR-012, `PRD:AC-21`).
   *
   * **No se vuelve a pedir el listado**: la fila se saca del estado, que es la misma regla que el
   * alta y la edición ya siguen — el servidor no tiene nada más que decir sobre una fila que ya no
   * está, y traer la lista entera para quitar una es traer todo para tirar uno.
   *
   * **El resumen sí se recalcula**, y siempre, sin averiguar antes si el movimiento caía en el mes
   * en curso: un total no se puede editar en la pantalla, hay que recalcularlo, y quien recalcula
   * es el servidor. Averiguarlo acá sería la clase de cuenta que `FR-014` de la feature 009 sacó de
   * la pantalla.
   *
   * **Eliminar con la ventana de edición abierta sobre esa misma fila no es alcanzable**: el
   * `<dialog>` es modal y `showModal()` vuelve inerte el fondo, así que no se puede llegar al botón.
   * Queda dicho para que sea una decisión y no un caso olvidado.
   */
  async function eliminar(movimiento: Movimiento) {
    try {
      await eliminarMovimiento(movimiento.id);
    } catch (error) {
      if (error instanceof ErrorDeSesion) {
        onSesionVencida(
          'Tu sesión venció y el movimiento no se eliminó. Volvé a entrar e intentá de nuevo.',
        );
        return;
      }

      // Un 404 significa que ya no está: no existe, no es de esta cuenta, o alguien lo eliminó
      // antes. El servidor responde lo mismo en los tres casos a propósito, y la pantalla no
      // intenta distinguirlos — la reacción es la misma. La fila se saca igual, porque dejarla
      // visible afirmaría que existe algo que no existe.
      setMovimientos((previos) => previos.filter((m) => m.id !== movimiento.id));
      setConfirmacion(null);
      setErrorDeCarga('Ese movimiento ya no está. Se quitó del listado.');
      void recargarResumen();
      encabezadoDelListado.current?.focus();
      return;
    }

    setMovimientos((previos) => previos.filter((m) => m.id !== movimiento.id));
    void recargarResumen();
    setConfirmacion('Movimiento eliminado.');

    // El foco, antes de que el render se lleve el botón que se apretó (D-09).
    encabezadoDelListado.current?.focus();
  }

  return (
    <main className="l-pila">
      <div className="l-fila l-cabecera">
        <h1>Mis movimientos</h1>
        {/* Se ve con qué cuenta se está trabajando (FR-004): sin esto, dos cuentas en el mismo
            navegador son indistinguibles hasta que alguien carga un gasto en la equivocada. */}
        <p>{email}</p>
        <button type="button" onClick={onVerDashboard}>
          Dashboard
        </button>
        <button type="button" onClick={onGestionarCategorias}>
          Categorías
        </button>
        {/* Un `<button>` y no un enlace: cambia estado del servidor, y los enlaces son para
            navegar. Un enlace acá además sería seguible por un prefetch del navegador. */}
        <button type="button" onClick={onCerrarSesion}>
          Cerrar sesión
        </button>
      </div>

      {/* El resumen del mes, ARRIBA de todo (FR-011). Es lo primero que alguien quiere saber al
          entrar —cómo viene el mes— y ponerlo debajo del formulario lo dejaría fuera de la
          pantalla en cuanto el listado crezca.

          Sin control de período y a propósito: el de acá es siempre el mes en curso, y elegir qué
          mirar es del dashboard (FR-011b). */}
      {errorDelResumen ? <p role="alert">{errorDelResumen}</p> : null}
      {resumen ? (
        <ResumenDelPeriodo resumen={resumen} titulo="Resumen del mes" monedas={monedas} />
      ) : null}

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

      {/* La barra de acotado, con los TRES controles (FR-014 a FR-016). Hasta la feature 011 acá
          había un `<select>` de moneda suelto y el comentario decía que era "donde la barra va a
          crecer": creció. Los tres se aplican juntos con un solo botón (D-06).

          El período arranca con el que el servidor eligió, que llega en el `Resumen` del mes en
          curso: `GET /api/movimientos` no dice qué período aplicó, y calcularlo acá sería un segundo
          intérprete de "hoy" (FR-015, D-05). Mientras el resumen no haya llegado, los campos
          arrancan vacíos — que es lo mismo que el servidor entiende por "el mes en curso". */}
      <FiltrosDelListado
        categorias={categorias}
        monedas={monedas}
        desdeInicial={resumen?.desde}
        hastaInicial={resumen?.hasta}
        errorDelPeriodo={errorDelPeriodo}
        onAplicar={setAcotado}
      />

      {cargandoListado ? (
        <p>Cargando movimientos…</p>
      ) : (
        <ListadoMovimientos
          monedas={monedas}
          movimientos={movimientos}
          onEditar={setEnEdicion}
          onEliminar={(m) => void eliminar(m)}
          refDelEncabezado={encabezadoDelListado}
        />
      )}

      {/* La ventana se monta sólo cuando hay algo que editar, y se desmonta al cerrarse. Montarla
          siempre y esconderla dejaría sus campos con los valores del movimiento anterior la próxima
          vez que se abriera. */}
      {enEdicion ? (
        <VentanaDeEdicion
          // **`key` con el id, para que cambiar de fila remonte la ventana.**
          // `CamposDelMovimiento` lee sus valores iniciales sólo al montarse; sin `key`, una
          // ventana que pasara del movimiento A al B mostraría los valores de A y guardaría sobre
          // el id de B. Hoy no es alcanzable —`showModal()` vuelve inerte el fondo, así que no se
          // puede clicar "Editar" en otra fila con la ventana abierta—, y eso es exactamente el
          // motivo de la `key`: que no dependa de una propiedad del `<dialog>` sino de la
          // estructura.
          key={enEdicion.id}
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

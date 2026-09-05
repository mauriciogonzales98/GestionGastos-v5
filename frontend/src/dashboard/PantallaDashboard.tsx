import { useEffect, useId, useState } from 'react';
import { ErrorDeSesion, ErrorDeValidacion, obtenerResumen } from '../api/cliente';
import type { Moneda, Resumen } from '../api/tipos';
import { ResumenDelPeriodo } from '../resumen/ResumenDelPeriodo';
import { ControlesDelPeriodo } from './ControlesDelPeriodo';

export interface PropsPantallaDashboard {
  /**
   * El catálogo que alimenta el acotado. Baja por props desde la raíz, que lo pide una sola vez por
   * sesión (AC-12 de la feature 009), y **es lo único que puede llenar ese selector**: una lista
   * escrita a mano acá rompería la promesa de que sumar una moneda cuesta 0 líneas, del único lado
   * que el usuario mira (FR-007).
   */
  monedas: Moneda[];
  /** Vuelve a la pantalla principal. */
  onVolver: () => void;
  onSesionVencida: (motivo: string) => void;
}

const SESION_VENCIDA = 'Tu sesión venció. Volvé a entrar.';

/**
 * El dashboard: los mismos totales que la pantalla principal, pero del período que uno elija
 * (RF-19, RF-21, RF-30).
 *
 * **Su diferencia con el resumen de la pantalla principal no es el gráfico: es el período.** Aquél
 * está clavado al mes calendario en curso por decisión de FEAT-001c; éste es el lugar donde la
 * persona elige qué mirar. Los dos salen del **mismo** endpoint, pedido de dos maneras — que es la
 * decisión que `ResumenEndpoints` documenta como "es un endpoint y no dos".
 *
 * **Su estado es suyo y no se comparte con la pantalla principal** (D-06). Un único `resumen` en la
 * raíz haría que elegir un trimestre acá cambiara los números de allá, que es lo que `FR-012`
 * prohíbe y sería invisible en la pantalla donde se produce.
 */
export function PantallaDashboard({ monedas, onVolver, onSesionVencida }: PropsPantallaDashboard) {
  const [resumen, setResumen] = useState<Resumen | null>(null);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);

  /**
   * El período aplicado. `['', '']` es *sin período pedido*, o sea el mes que el servidor elija.
   *
   * Es una tupla y no dos estados sueltos porque los dos extremos **van juntos o no va ninguno**:
   * es la regla del servidor, y con dos estados separados existiría un render intermedio con medio
   * rango puesto que dispararía una petición que el servidor va a rechazar.
   */
  const [periodo, setPeriodo] = useState<[string, string]>(['', '']);
  const [errorDelRango, setErrorDelRango] = useState<string | null>(null);

  /**
   * La moneda que se está mirando. `''` es "todas" (FR-006).
   *
   * **Es un acotado de presentación y NO viaja al servidor** (D-05). `PRD:RF-29` ya obliga a que
   * nada se sume a través de monedas, así que el resumen llega con los universos separados —una
   * entrada por moneda, con sus totales y su desglose— y ningún número depende de qué monedas se
   * pidan. Elegir una es elegir cuál de esos bloques se mira, no calcular otra cosa.
   *
   * Por eso **no está en las dependencias del efecto**: cambiarlo no vuelve a pedir nada. Y por eso
   * mismo la garantía que la feature 009 blindó en el servidor —el resumen informa sobre TODAS las
   * monedas del catálogo, siempre— queda intacta y sin reabrirse.
   *
   * Cadena y no `number | null` porque es el valor de un `<select>`: convertirlo de ida y vuelta en
   * cada render abre la posibilidad de que el control muestre una cosa y el estado guarde otra. Es
   * el mismo criterio que el acotado del listado.
   */
  const [monedaAcotada, setMonedaAcotada] = useState('');
  const idAcotado = useId();

  /**
   * Pide el resumen. Sin período por ahora: el rango llega en la historia siguiente.
   *
   * `useCallback` no es una optimización: lo usa un `useEffect`, y una función nueva en cada render
   * volvería a disparar la carga en bucle.
   */
  const [desde, hasta] = periodo;

  useEffect(() => {
    /**
     * **La guarda contra la respuesta que llega tarde** (D-09).
     *
     * Cada cambio de período dispara una petición, así que puede haber dos en vuelo. Si la primera
     * tarda más, resuelve última y se escribe encima del resultado correcto: el dashboard termina
     * mostrando el período que se pidió antes, con los controles diciendo otra cosa. Sin error y
     * sin nada en la consola — la pantalla contradiciéndose sola.
     *
     * Es la cicatriz `22e3e96` de la feature 009, y acá la ventana es más ancha: un rango de un año
     * sobre 10000 movimientos tarda más que un acotado del listado.
     *
     * El `cleanup` corre **antes** de volver a lanzar el efecto, así que apagar esta bandera es
     * exactamente "lo que pedí dejó de importar".
     */
    let vigente = true;

    void obtenerResumen(desde, hasta)
      .then((traido) => {
        if (!vigente) {
          return;
        }

        setResumen(traido);
        // Los dos errores se van **cuando la carga sale bien**, no cuando empieza: un cartel que
        // sobrevive a una carga buena dice que no se pudo cargar justo lo que se está mirando.
        setErrorDeCarga(null);
        setErrorDelRango(null);
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
         * **El rango rechazado no borra lo que estaba a la vista.**
         *
         * `resumen` no se toca: un vacío se leería como "no hay nada" y escondería que la pregunta
         * estaba mal formada, que es el mismo motivo por el que el servidor rechaza un rango
         * invertido en vez de devolver una lista vacía.
         *
         * El mensaje es **el del servidor**, bajo la clave `rango`. No se reescribe ni se traduce:
         * el único intérprete del período es `PeriodoPedido` (D-08).
         */
        if (error instanceof ErrorDeValidacion) {
          setErrorDelRango(
            error.errores.rango?.join(' ') ?? 'No se pudo interpretar el período pedido.',
          );
          return;
        }

        setErrorDeCarga('No se pudo cargar el dashboard. Volvé a intentarlo.');
      });

    return () => {
      vigente = false;
    };
  }, [desde, hasta, onSesionVencida]);

  return (
    <main className="l-pila">
      <div className="l-fila l-cabecera">
        <h1>Dashboard</h1>
        <button type="button" onClick={onVolver}>
          Volver a movimientos
        </button>
      </div>

      <ControlesDelPeriodo
        onAplicar={(nuevoDesde, nuevoHasta) => setPeriodo([nuevoDesde, nuevoHasta])}
        error={errorDelRango}
      />

      {errorDeCarga ? <p role="alert">{errorDeCarga}</p> : null}

      {/* El acotado por moneda (FR-006). Sale del CATÁLOGO, nunca de una lista escrita acá: es lo
          que hace que una moneda agregada como dato aparezca sin tocar código, y lo que
          `verificar-monedas.sh` vigila en `frontend/src/` desde la feature 009. */}
      <div className="l-fila">
        <label htmlFor={idAcotado}>Ver sólo la moneda</label>
        <select
          id={idAcotado}
          value={monedaAcotada}
          onChange={(evento) => setMonedaAcotada(evento.target.value)}
        >
          <option value="">Todas las monedas</option>
          {monedas.map((moneda) => (
            <option key={moneda.id} value={moneda.id}>
              {moneda.codigo} — {moneda.nombre}
            </option>
          ))}
        </select>
      </div>

      {resumen ? (
        <ResumenDelPeriodo
          resumen={
            monedaAcotada === ''
              ? resumen
              : // Un recorte sobre lo que ya llegó. Ninguna suma, ninguna petición: los totales que
                // se muestran son exactamente los que el servidor calculó (FR-013, FR-014).
                {
                  ...resumen,
                  monedas: resumen.monedas.filter(
                    (moneda) => String(moneda.monedaId) === monedaAcotada,
                  ),
                }
          }
          titulo="Resumen del período"
        />
      ) : null}
    </main>
  );
}

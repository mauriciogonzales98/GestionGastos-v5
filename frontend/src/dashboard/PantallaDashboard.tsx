import { useCallback, useEffect, useState } from 'react';
import { ErrorDeSesion, obtenerResumen } from '../api/cliente';
import type { Resumen } from '../api/tipos';
import { ResumenDelPeriodo } from '../resumen/ResumenDelPeriodo';

export interface PropsPantallaDashboard {
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
export function PantallaDashboard({ onVolver, onSesionVencida }: PropsPantallaDashboard) {
  const [resumen, setResumen] = useState<Resumen | null>(null);
  const [errorDeCarga, setErrorDeCarga] = useState<string | null>(null);

  /**
   * Pide el resumen. Sin período por ahora: el rango llega en la historia siguiente.
   *
   * `useCallback` no es una optimización: lo usa un `useEffect`, y una función nueva en cada render
   * volvería a disparar la carga en bucle.
   */
  const cargar = useCallback(
    () =>
      obtenerResumen()
        .then((traido) => {
          setResumen(traido);
          // El error de la carga anterior se va cuando ésta sale bien, no cuando empieza: es la
          // misma regla del listado y del resumen de la principal, y la misma cicatriz.
          setErrorDeCarga(null);
        })
        .catch((error: unknown) => {
          if (error instanceof ErrorDeSesion) {
            onSesionVencida(SESION_VENCIDA);
            return;
          }

          // Se dice como fallo. Mostrar ceros acá sería el dashboard afirmando que no hubo
          // movimientos en el período, que es lo contrario de lo que pasó (FR-010).
          setErrorDeCarga('No se pudo cargar el dashboard. Volvé a intentarlo.');
        }),
    [onSesionVencida],
  );

  useEffect(() => {
    void cargar();
  }, [cargar]);

  return (
    <main className="l-pila">
      <div className="l-fila l-cabecera">
        <h1>Dashboard</h1>
        <button type="button" onClick={onVolver}>
          Volver a movimientos
        </button>
      </div>

      {errorDeCarga ? <p role="alert">{errorDeCarga}</p> : null}

      {resumen ? <ResumenDelPeriodo resumen={resumen} titulo="Resumen del período" /> : null}
    </main>
  );
}

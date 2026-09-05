import type { Resumen } from '../api/tipos';
import { TotalesDeUnaMoneda } from './TotalesDeUnaMoneda';

export interface PropsResumenDelPeriodo {
  resumen: Resumen;
  /**
   * Cómo se titula. La pantalla principal muestra el mes en curso; el dashboard, el período que la
   * persona eligió — son el mismo cálculo pedido de dos maneras, no dos cosas distintas.
   */
  titulo?: string;
}

/**
 * El resumen de un período, pintado (RF-19, RF-20, RF-22).
 *
 * **Lo usan las dos pantallas y por eso vive acá y no dentro de ninguna de ellas.** Lo que cada una
 * decide es *qué* período pedir, no *cómo* mostrarlo: esa frontera es la misma que separa
 * `resumen/` de `dashboard/`.
 *
 * El período que se muestra es el que **vino del servidor**. `desde` y `hasta` viajan siempre
 * justamente para esto: sin ellos habría que calcular el mes en curso en la zona horaria del
 * navegador, y volverían a existir dos criterios de "hoy".
 */
export function ResumenDelPeriodo({ resumen, titulo = 'Resumen del mes' }: PropsResumenDelPeriodo) {
  return (
    <section className="l-pila c-resumen" aria-label={titulo}>
      <h2>{titulo}</h2>
      <p>
        Del {resumen.desde} al {resumen.hasta}
      </p>

      {resumen.monedas.map((moneda) => (
        <TotalesDeUnaMoneda key={moneda.monedaId} moneda={moneda} />
      ))}
    </section>
  );
}

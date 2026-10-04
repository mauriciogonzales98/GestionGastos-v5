import type { Resumen } from '../api/tipos';
import type { Moneda } from '../api/tipos';
import { TotalesDeUnaMoneda } from './TotalesDeUnaMoneda';
import { formatearFecha } from '../ui/formatearFecha';

export interface PropsResumenDelPeriodo {
  resumen: Resumen;
  /**
   * Cómo se titula. La pantalla principal muestra el mes en curso; el dashboard, el período que la
   * persona eligió — son el mismo cálculo pedido de dos maneras, no dos cosas distintas.
   */
  titulo?: string;
  /**
   * El catálogo, que baja hasta los montos para que cada uno se muestre en la escala de su moneda
   * (`FR-019`). Opcional: sin él se cae en lo que `Intl` deduzca del código ISO.
   */
  monedas?: Moneda[];
  /**
   * Lo que hay que aclarar sobre de qué habla este resumen, o nada.
   *
   * **Llega como texto y no se arma acá adentro** (D6-06). El caso que lo motiva es el acotado del
   * listado de la pantalla principal, y este componente lo usa también el dashboard, que no tiene
   * ningún listado debajo: decidir *qué* aclarar es de quien tiene el acotado, y lo único que se
   * decide acá es dónde va.
   */
  aviso?: string;
  /**
   * Si se muestra el desglose por categoría de cada moneda.
   *
   * **`false` en la pantalla de movimientos** (`FR-035`): ahí el resumen muestra ingresado, gastado
   * y balance, que es lo que `PRD:RF-22` pide para la pantalla principal. El desglose es
   * `PRD:RF-19`, que el PRD ubica en el dashboard, y ocupaba tanto alto que empujaba el formulario
   * y el listado —lo que se usa todos los días— fuera de la pantalla.
   *
   * Por defecto `true`, así que **el dashboard no cambia su llamada**. El servidor tampoco cambia:
   * sigue mandando el desglose en las dos respuestas (`FR-040`).
   */
  conDesglose?: boolean;
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
export function ResumenDelPeriodo({
  resumen,
  titulo = 'Resumen del mes',
  monedas,
  aviso,
  conDesglose = true,
}: PropsResumenDelPeriodo) {
  return (
    <section className="l-pila c-resumen" aria-label={titulo}>
      {/* Encabezado en **un** renglón: el título y el período al lado, en texto secundario
          (`FR-039`). Antes eran dos renglones completos arriba de todo, y el período es contexto,
          no el dato. Se envuelve si no entra, en vez de desbordar. */}
      <div className="l-fila c-resumen__encabezado">
        <h2>{titulo}</h2>
        <p className="c-resumen__periodo">
          Del {formatearFecha(resumen.desde)} al {formatearFecha(resumen.hasta)}
        </p>
      </div>

      {/* `role="status"` y no `alert`: nada está roto y no hay nada que reparar. Aparece en
          respuesta a algo que la persona acaba de hacer —acotar el listado— y se anuncia sin
          interrumpirla, que es el mismo criterio que usa la confirmación del alta. */}
      {aviso ? (
        <p role="status" className="c-resumen__aviso">
          {aviso}
        </p>
      ) : null}

      {/* **Sin ninguna moneda que mostrar, se dice.**
          
          El servidor devuelve una entrada por cada moneda del catálogo, así que esta lista no llega
          vacía desde la API: llega vacía desde la PANTALLA, cuando el dashboard la recorta por una
          moneda que el resumen no trae — el catálogo del selector se pidió al abrir la sesión y el
          resumen es de recién, así que pueden discrepar.
          
          Sin esto se veía el título y el período y nada más: ni totales, ni "no hay datos", ni
          error. Es el hallazgo 4 de la revisión del PR #25, y el silencio es justo lo que este
          proyecto no se permite en ningún otro lado. Sin `role="alert"`, porque no hay datos y un
          fallo son cosas distintas (FR-009, FR-010). */}
      {resumen.monedas.length === 0 ? (
        <p>No hay ninguna moneda para mostrar en este período.</p>
      ) : (
        resumen.monedas.map((moneda) => (
          <TotalesDeUnaMoneda
            key={moneda.monedaId}
            moneda={moneda}
            monedas={monedas}
            conDesglose={conDesglose}
          />
        ))
      )}
    </section>
  );
}

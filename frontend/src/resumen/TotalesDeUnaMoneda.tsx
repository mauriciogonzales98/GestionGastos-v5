import type { ResumenPorMoneda } from '../api/tipos';
import type { Moneda } from '../api/tipos';
import { decimalesDe, formatearMonto } from '../ui/formatearMonto';
import { GastosPorCategoria } from './GastosPorCategoria';

export interface PropsTotalesDeUnaMoneda {
  moneda: ResumenPorMoneda;
  /**
   * El catálogo, para la escala de cada monto (`FR-019`). Opcional: sin él se cae en lo que `Intl`
   * deduzca del código ISO, que es lo que se hacía hasta la feature 011.
   */
  monedas?: Moneda[];
  /** Si se muestra el desglose por categoría. `false` en movimientos (`FR-035`). */
  conDesglose?: boolean;
}

/**
 * Lo que pasó en una moneda durante el período (RF-20, RF-29).
 *
 * Es la unidad indivisible: **nada se suma nunca a través de dos de éstas** y no hay conversión en
 * ningún lado. Que sea un componente y no tres campos sueltos es lo que hace que esa separación se
 * vea en la pantalla y no sólo en el contrato.
 *
 * Aparece **tenga o no movimientos**: el servidor compone las monedas desde el catálogo justamente
 * para que un período vacío devuelva ceros, y esconder acá la que está en cero se leería como si
 * esa moneda no existiera en el catálogo (FR-009).
 */
export function TotalesDeUnaMoneda({
  moneda,
  monedas,
  conDesglose = true,
}: PropsTotalesDeUnaMoneda) {
  /**
   * **Una moneda sin ningún movimiento en el período** (`FR-038`).
   *
   * Se deduce de los dos totales y no del balance: `PRD:RF-13` exige montos mayores a cero, así que
   * con un solo movimiento alguno de los dos deja de ser cero. Mirar el balance sería distinto —una
   * moneda con movimientos cuyo ingreso y gasto se empatan da balance cero— y ésa tiene que
   * mostrarse entera.
   */
  const sinMovimientos = moneda.totalIngresado === 0 && moneda.totalGastado === 0;

  if (sinMovimientos) {
    return (
      /*
       * Una línea, no un bloque de tres ceros.
       *
       * **Sigue apareciendo**, que es lo que la feature 006 quería proteger con `FR-009` y lo que
       * `PRD:AC-31` describe: esconderla se leería como si la moneda no existiera en el catálogo.
       * Lo que cambia es el espacio que ocupa — tres ceros ocupaban lo mismo que una moneda con
       * datos y empujaban el formulario fuera de la pantalla.
       */
      <section
        className="c-totales-moneda c-totales-moneda--vacia"
        aria-label={`Totales en ${moneda.monedaCodigo}`}
      >
        <h3>{moneda.monedaCodigo}</h3>
        <p>sin movimientos en el período</p>
      </section>
    );
  }

  return (
    <section className="l-pila c-totales-moneda" aria-label={`Totales en ${moneda.monedaCodigo}`}>
      <h3>{moneda.monedaCodigo}</h3>

      {/* Las tres cifras en **un** renglón, cada una con su nombre arriba (`FR-037`). Sigue siendo
          un `<dl>`, así que lo que un lector de pantalla anuncia no cambia. */}
      <dl className="c-cifras">
        <div>
          <dt>Ingresado</dt>
          <dd>
            {formatearMonto(
              moneda.totalIngresado,
              moneda.monedaCodigo,
              decimalesDe(monedas, moneda.monedaCodigo),
            )}
          </dd>
        </div>
        <div>
          <dt>Gastado</dt>
          <dd>
            {formatearMonto(
              moneda.totalGastado,
              moneda.monedaCodigo,
              decimalesDe(monedas, moneda.monedaCodigo),
            )}
          </dd>
        </div>
        <div>
          <dt>Balance</dt>
          {/* Un balance negativo se muestra negativo. Un mes en rojo es exactamente la información
              que alguien necesita ver, así que no se recorta a cero ni se presenta como un error. */}
          <dd data-testid="balance">
            {formatearMonto(
              moneda.balance,
              moneda.monedaCodigo,
              decimalesDe(monedas, moneda.monedaCodigo),
            )}
          </dd>
        </div>
      </dl>

      {conDesglose ? (
        <GastosPorCategoria
          gastos={moneda.gastosPorCategoria}
          monedaCodigo={moneda.monedaCodigo}
          monedas={monedas}
        />
      ) : null}
    </section>
  );
}

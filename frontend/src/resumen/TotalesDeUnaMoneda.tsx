import type { ResumenPorMoneda } from '../api/tipos';
import { formatearMonto } from '../ui/formatearMonto';
import { GastosPorCategoria } from './GastosPorCategoria';

export interface PropsTotalesDeUnaMoneda {
  moneda: ResumenPorMoneda;
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
export function TotalesDeUnaMoneda({ moneda }: PropsTotalesDeUnaMoneda) {
  return (
    <section className="l-pila c-totales-moneda" aria-label={`Totales en ${moneda.monedaCodigo}`}>
      <h3>{moneda.monedaCodigo}</h3>

      <dl className="l-fila">
        <div>
          <dt>Ingresado</dt>
          <dd>{formatearMonto(moneda.totalIngresado, moneda.monedaCodigo)}</dd>
        </div>
        <div>
          <dt>Gastado</dt>
          <dd>{formatearMonto(moneda.totalGastado, moneda.monedaCodigo)}</dd>
        </div>
        <div>
          <dt>Balance</dt>
          {/* Un balance negativo se muestra negativo. Un mes en rojo es exactamente la información
              que alguien necesita ver, así que no se recorta a cero ni se presenta como un error. */}
          <dd data-testid="balance">{formatearMonto(moneda.balance, moneda.monedaCodigo)}</dd>
        </div>
      </dl>

      <GastosPorCategoria gastos={moneda.gastosPorCategoria} monedaCodigo={moneda.monedaCodigo} />
    </section>
  );
}

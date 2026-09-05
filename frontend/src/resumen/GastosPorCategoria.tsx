import type { TotalPorCategoria } from '../api/tipos';
import { formatearMonto } from '../ui/formatearMonto';

export interface PropsGastosPorCategoria {
  /** El desglose de UNA moneda, ya ordenado por el servidor. */
  gastos: TotalPorCategoria[];
  /** El código de esa moneda, para formatear cada total con su símbolo. */
  monedaCodigo: string;
}

/**
 * El desglose de gastos por categoría de una moneda (RF-19).
 *
 * **Es una tabla, y la tabla es el gráfico.** No hay un dibujo por un lado y estos números por
 * otro: la barra que llega en US2 es un ancho puesto sobre estas mismas filas. Dos
 * representaciones del mismo dato son dos que pueden discrepar, y es el mismo criterio con el que
 * el servidor calcula los cuatro totales de una moneda a partir de las mismas filas (D-03).
 *
 * De ahí sale que `RNF-06` no cueste nada: el nombre y el total están en el DOM porque son el
 * contenido de la fila, no porque alguien se acordó de agregar una versión accesible.
 *
 * **Nada se calcula acá.** Ni el total general, ni porcentajes, ni un reordenamiento: el orden lo
 * fija el servidor —de mayor a menor, desempatando por id— y replicarlo acá volvería a abrir el
 * problema que ese desempate cerró, que es que dos categorías con el mismo total se intercambien
 * solas entre dos pedidos idénticos (FR-014, FR-016).
 */
export function GastosPorCategoria({ gastos, monedaCodigo }: PropsGastosPorCategoria) {
  if (gastos.length === 0) {
    // Sin datos NO es un error, y por eso no lleva `role="alert"`. Un período sin movimientos y un
    // servidor caído terminan en pantallas parecidas por motivos opuestos: confundirlos haría que
    // alguien creyera que no gastó nada (FR-009, FR-010).
    return <p>No hay gastos para graficar en este período.</p>;
  }

  return (
    <table aria-label={`Gastos por categoría en ${monedaCodigo}`}>
      <thead>
        <tr>
          <th scope="col">Categoría</th>
          <th scope="col">Total</th>
        </tr>
      </thead>
      <tbody>
        {gastos.map((gasto) => (
          <tr key={gasto.categoriaId}>
            {/* Un `td` y no un `th scope="row"`: el nombre es un dato del desglose, y como
                encabezado de fila cambiaría su rol y con él la forma de leerlo. */}
            <td>{gasto.categoriaNombre}</td>
            <td>{formatearMonto(gasto.total, monedaCodigo)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

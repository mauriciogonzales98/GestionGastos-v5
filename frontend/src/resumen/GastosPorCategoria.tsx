import type { CSSProperties } from 'react';
import type { TotalPorCategoria } from '../api/tipos';
import { COLORES_DEL_DASHBOARD } from '../ui/contraste';
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
 *
 * La única cuenta que sí se hace es el **ancho** de cada barra, `total / mayor`. No es un dato: es
 * cómo se dibuja el dato que ya está escrito al lado.
 */
export function GastosPorCategoria({ gastos, monedaCodigo }: PropsGastosPorCategoria) {
  if (gastos.length === 0) {
    // Sin datos NO es un error, y por eso no lleva `role="alert"`. Un período sin movimientos y un
    // servidor caído terminan en pantallas parecidas por motivos opuestos: confundirlos haría que
    // alguien creyera que no gastó nada (FR-009, FR-010).
    return <p>No hay gastos para graficar en este período.</p>;
  }

  // El mayor total de esta moneda, que es el 100 % de la barra más larga. Sale de las filas que ya
  // llegaron —no de otro pedido— así que la proporción no puede quedar desfasada del dato.
  const mayor = Math.max(...gastos.map((gasto) => gasto.total));

  return (
    <table
      aria-label={`Gastos por categoría en ${monedaCodigo}`}
      // Los colores bajan desde su única fuente en `ui/contraste.ts`, que es el archivo que el test
      // de `PRD:AC-13` mide. Declararlos también en la hoja de estilos daría dos copias, y el día
      // que una cambie el test seguiría midiendo la otra.
      style={
        {
          '--c-barra': COLORES_DEL_DASHBOARD.barra,
          '--c-riel': COLORES_DEL_DASHBOARD.rielDeLaBarra,
        } as CSSProperties
      }
    >
      <thead>
        <tr>
          <th scope="col">Categoría</th>
          <th scope="col">Total</th>
          {/* La columna de la barra no necesita título a la vista: la barra no dice nada que las
              dos columnas anteriores no digan. Pero la tabla sí necesita ser regular, así que el
              encabezado existe y sólo lo leen los lectores de pantalla. */}
          <th scope="col" className="u-solo-lectores">
            Proporción
          </th>
        </tr>
      </thead>
      <tbody>
        {gastos.map((gasto) => (
          <tr key={gasto.categoriaId}>
            {/* Un `td` y no un `th scope="row"`: el nombre es un dato del desglose, y como
                encabezado de fila cambiaría su rol y con él la forma de leerlo. */}
            <td>{gasto.categoriaNombre}</td>
            <td>{formatearMonto(gasto.total, monedaCodigo)}</td>
            <td className="c-desglose__riel">
              {/* **La barra: el gráfico** (FR-001, D-03).

                  Es decorativa —`aria-hidden`— y no lleva ningún dato que la fila no tenga ya. Ésa
                  es la mitad que hace verdadera la decisión: si la barra informara algo propio, el
                  texto dejaría de ser el gráfico y pasaría a ser una segunda representación, o sea
                  dos que pueden discrepar.

                  Todas comparten clase y relleno: las categorías no se codifican por color, se
                  distinguen por el nombre que está a su izquierda (D-04, NFR-003). */}
              <div
                data-testid="barra"
                aria-hidden="true"
                className="c-desglose__barra"
                // `mayor > 0` y no `mayor` a secas: con todos los totales en cero, `0 / 0` da NaN y
                // el ancho sale `NaN%` — la fila se ve y la barra desaparece sin motivo.
                style={{ width: `${mayor > 0 ? (gasto.total / mayor) * 100 : 0}%` }}
              />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

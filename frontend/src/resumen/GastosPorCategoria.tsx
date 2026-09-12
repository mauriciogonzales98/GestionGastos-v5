import type { Moneda, TotalPorCategoria } from '../api/tipos';
import { decimalesDe, formatearMonto } from '../ui/formatearMonto';

export interface PropsGastosPorCategoria {
  /** El desglose de UNA moneda, ya ordenado por el servidor. */
  gastos: TotalPorCategoria[];
  /** El código de esa moneda, para formatear cada total con su símbolo. */
  monedaCodigo: string;
  /**
   * El catálogo, para la escala de cada monto (`FR-019`). Opcional: sin él se cae en lo que `Intl`
   * deduzca del código ISO, que es lo que se hacía hasta la feature 011.
   */
  monedas?: Moneda[];
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
export function GastosPorCategoria({ gastos, monedaCodigo, monedas }: PropsGastosPorCategoria) {
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
    // Sin `style` con los colores: desde la feature 011 la paleta la declara `estilos/base.css` y
    // la hoja los toma de ahí (D-01). El componente no inyecta nada.
    <table aria-label={`Gastos por categoría en ${monedaCodigo}`}>
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
            <td>{formatearMonto(gasto.total, monedaCodigo, decimalesDe(monedas, monedaCodigo))}</td>
            <td className="c-desglose__riel">
              {/* **La barra: el gráfico** (FR-001, D-03).

                  Es decorativa —`aria-hidden`— y no lleva ningún dato que la fila no tenga ya. Ésa
                  es la mitad que hace verdadera la decisión: si la barra informara algo propio, el
                  texto dejaría de ser el gráfico y pasaría a ser una segunda representación, o sea
                  dos que pueden discrepar.

                  Todas comparten clase y relleno: las categorías no se codifican por color, se
                  distinguen por el nombre que está a su izquierda (D-04, NFR-003).

                  **Y esto dejó de ser una pendiente**: D12-03 preguntaba si las barras debían
                  llevar color ahora que existe la paleta, y se cerró que no. Una serie va de un
                  solo color: el nombre ya está en la celda de al lado y la magnitud en el largo,
                  así que un color por categoría gastaría el único canal libre en información que
                  el gráfico ya muestra. Y las categorías las crea el usuario y no tienen tope, así
                  que los tonos habría que ciclarlos — dos categorías distintas del mismo color, con
                  el color pareciendo decir algo. */}
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

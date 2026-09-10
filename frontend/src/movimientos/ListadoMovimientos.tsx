import { useState, type RefObject } from 'react';
import type { Moneda, Movimiento } from '../api/tipos';
import { decimalesDe, formatearMonto } from '../ui/formatearMonto';

export interface PropsListadoMovimientos {
  movimientos: Movimiento[];
  /** Abre la ventana de edición sobre ese movimiento (FR-011). */
  onEditar: (movimiento: Movimiento) => void;
  /** Elimina ese movimiento, ya confirmado por quien lo pidió (FR-010, `PRD:RF-15`). */
  onEliminar: (movimiento: Movimiento) => void;
  /**
   * El encabezado del listado, para que la pantalla pueda mandarle el foco cuando una fila
   * desaparece (D-09). Va con `tabIndex={-1}`: es alcanzable por código pero no entra en el
   * recorrido con Tab, que es lo que corresponde a un destino de foco que no es un control.
   */
  refDelEncabezado?: RefObject<HTMLHeadingElement | null>;
  /**
   * El catálogo, para mostrar cada monto en la escala de su moneda (`FR-019`).
   *
   * Opcional: sin él se cae en lo que `Intl` deduzca del código ISO, que es lo que este listado
   * hacía hasta la feature 011. Es la degradación correcta mientras el catálogo no haya llegado.
   */
  monedas?: Moneda[];
}

/**
 * Cómo se nombra un movimiento cuando hay que decir sobre cuál se está actuando.
 *
 * Los botones de una fila dicen sobre qué actúan y no sólo "Editar" o "Eliminar" (FR-005): quien
 * recorre la tabla con un lector de pantalla escucha el nombre del botón fuera del contexto de su
 * fila, y seis botones llamados "Eliminar" son seis botones indistinguibles. Es la misma forma que
 * la gestión de categorías usa desde la feature 007 con `Renombrar {nombre}`.
 */
function describir(m: Movimiento): string {
  // **Sin el monto, a propósito.** Sería más preciso, y hace que el nombre del botón contenga el
  // mismo texto que la celda del monto: dos aserciones del listado que buscaban ese número pasaban
  // a encontrar dos elementos. La fecha y la categoría alcanzan para distinguir una fila de otra, y
  // no ensucian lo que ya estaba (FR-020).
  return `${m.tipo === 'gasto' ? 'el gasto' : 'el ingreso'} del ${m.fecha} en ${m.categoriaNombre}`;
}

/**
 * El listado del mes (FR-007, FR-008, FR-012).
 *
 * Es una `<table>` y no una grilla de `<div>`: son datos tabulares, y la tabla es lo que los
 * lectores de pantalla saben recorrer.
 */
export function ListadoMovimientos({
  movimientos,
  onEditar,
  onEliminar,
  refDelEncabezado,
  monedas,
}: PropsListadoMovimientos) {
  /**
   * Qué fila está pidiendo confirmación para eliminarse. `null` = ninguna.
   *
   * **El borrado no se puede deshacer**, así que no puede dispararse con un clic pelado. Es el mismo
   * patrón de dos botones que usa la baja de una categoría desde la feature 007, y **no un
   * `window.confirm`**, por la razón que aquél dejó escrita: ése no se puede maquetar —"el ticket 6
   * no podría tocarlo"— ni se comporta igual en todos los navegadores. Ese comentario se escribió
   * para este ticket (D-07).
   */
  const [confirmando, setConfirmando] = useState<number | null>(null);

  if (movimientos.length === 0) {
    return (
      <section className="l-pila c-listado-movimientos">
        <h2 ref={refDelEncabezado} tabIndex={-1}>
          Movimientos del mes
        </h2>
        <p>No hay movimientos registrados este mes.</p>
      </section>
    );
  }

  return (
    <section className="l-pila c-listado-movimientos">
      <h2 ref={refDelEncabezado} tabIndex={-1}>
        Movimientos del mes
      </h2>
      {/* El envoltorio que se desplaza: seis columnas no entran en 360 px, y lo que no puede pasar
          es que desborde la página (FR-004). La regla vive en `componentes.css`. */}
      <div className="c-listado-movimientos__desborde">
        <table aria-label="Movimientos del mes">
          <thead>
            <tr>
              <th scope="col">Fecha</th>
              <th scope="col">Tipo</th>
              <th scope="col">Categoría</th>
              <th scope="col">Monto</th>
              {/* El CÓDIGO, además del símbolo que ya lleva el monto (FR-007).
                El símbolo lo elige `Intl` según el locale y puede repetirse entre dos monedas; con
                el catálogo abierto a monedas agregadas como dato, apoyar la distinción en él es
                apoyarla en algo que nadie controla. El código viene en el movimiento. */}
              <th scope="col">Moneda</th>
              {/* Sin texto visible: la columna de acciones no nombra nada, y el botón de cada fila ya
                se anuncia solo. `scope="col"` igual, para que la tabla siga siendo regular. */}
              <th scope="col">
                <span className="u-solo-lectores">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {movimientos.map((m) => (
              <tr key={m.id}>
                <td>{m.fecha}</td>
                {/* Como texto y no sólo por color: el color solo no es accesible. */}
                <td>{m.tipo === 'gasto' ? 'Gasto' : 'Ingreso'}</td>
                <td>{m.categoriaNombre}</td>
                <td>
                  {formatearMonto(m.monto, m.monedaCodigo, decimalesDe(monedas, m.monedaCodigo))}
                </td>
                <td>{m.monedaCodigo}</td>
                <td>
                  {confirmando === m.id ? (
                    <>
                      {/* Dicho entero y no "¿Seguro?": lo que hay que saber antes de apretar es que
                          se borra de verdad y que su monto sale de los totales, y ése es el dato
                          que una pregunta genérica se guarda. */}
                      <span role="alert">
                        Se elimina para siempre y su monto deja de sumar en el resumen.
                      </span>
                      <button type="button" onClick={() => onEliminar(m)}>
                        Confirmar y eliminar {describir(m)}
                      </button>
                      <button type="button" onClick={() => setConfirmando(null)}>
                        No eliminar {describir(m)}
                      </button>
                    </>
                  ) : (
                    <>
                      {/* "Editar" se queda como estaba: renombrarlo tocaría tests que están fuera
                          del presupuesto de D-12, y FR-020 sólo habilita lo que esta feature
                          agrega. El botón nuevo sí nace con el nombre completo. */}
                      <button type="button" onClick={() => onEditar(m)}>
                        Editar
                      </button>
                      <button type="button" onClick={() => setConfirmando(m.id)}>
                        Eliminar {describir(m)}
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

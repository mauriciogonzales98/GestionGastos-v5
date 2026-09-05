import type { Movimiento } from '../api/tipos';
import { formatearMonto } from '../ui/formatearMonto';

export interface PropsListadoMovimientos {
  movimientos: Movimiento[];
  /** Abre la ventana de edición sobre ese movimiento (FR-011). */
  onEditar: (movimiento: Movimiento) => void;
}

/**
 * El listado del mes (FR-007, FR-008, FR-012).
 *
 * Es una `<table>` y no una grilla de `<div>`: son datos tabulares, y la tabla es lo que los
 * lectores de pantalla saben recorrer.
 */
export function ListadoMovimientos({ movimientos, onEditar }: PropsListadoMovimientos) {
  if (movimientos.length === 0) {
    return (
      <section className="l-pila c-listado-movimientos">
        <h2>Movimientos del mes</h2>
        <p>No hay movimientos registrados este mes.</p>
      </section>
    );
  }

  return (
    <section className="l-pila c-listado-movimientos">
      <h2>Movimientos del mes</h2>
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
              <td>{formatearMonto(m.monto, m.monedaCodigo)}</td>
              <td>{m.monedaCodigo}</td>
              <td>
                <button type="button" onClick={() => onEditar(m)}>
                  Editar
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

import type { Movimiento } from '../api/tipos';

export interface PropsListadoMovimientos {
  movimientos: Movimiento[];
}

/** El monto con el símbolo de su moneda. `Intl` sabe el símbolo de cada código ISO 4217. */
function formatearMonto(monto: number, monedaCodigo: string) {
  return new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency: monedaCodigo,
  }).format(monto);
}

/**
 * El listado del mes (FR-007, FR-008, FR-012).
 *
 * Es una `<table>` y no una grilla de `<div>`: son datos tabulares, y la tabla es lo que los
 * lectores de pantalla saben recorrer.
 */
export function ListadoMovimientos({ movimientos }: PropsListadoMovimientos) {
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
      <table>
        <thead>
          <tr>
            <th scope="col">Fecha</th>
            <th scope="col">Tipo</th>
            <th scope="col">Categoría</th>
            <th scope="col">Monto</th>
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
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

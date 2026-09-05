import type { Movimiento } from '../api/tipos';

export interface PropsListadoMovimientos {
  movimientos: Movimiento[];
  /** Abre la ventana de edición sobre ese movimiento (FR-011). */
  onEditar: (movimiento: Movimiento) => void;
}

/**
 * El monto con el símbolo de su moneda. `Intl` sabe el símbolo de cada código ISO 4217.
 *
 * **El `try` no es defensivo por las dudas: es por un dato que el esquema admite.**
 * `Intl.NumberFormat` con `style: 'currency'` exige **tres letras ASCII** y lanza `RangeError` con
 * cualquier otra cosa. La columna `moneda.codigo` es `char(3)`, que garantiza tres caracteres pero
 * no tres letras: `'BT1'` entra sin que nada lo rechace.
 *
 * Hasta la feature 009 eso era inalcanzable —se podían agregar monedas al catálogo pero no
 * registrar movimientos en ellas—; desde que la moneda se elige, ese dato llega hasta acá. Y un
 * `RangeError` en un render no ensucia una fila: sube, React desmonta el árbol, y la cuenta se
 * queda con la pantalla en blanco hasta que alguien borre el movimiento por SQL.
 *
 * Se degrada al número con su código al lado, que es información suficiente y nunca falla. Lo que
 * el esquema debería decir —que un código es tres letras— es un CHECK que le falta a la columna, y
 * queda anotado como deuda: este guardarraíl protege además a los datos que ya estén cargados.
 */
function formatearMonto(monto: number, monedaCodigo: string) {
  try {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: monedaCodigo,
    }).format(monto);
  } catch {
    // No es un catch silencioso: el fallo se muestra, en la forma de un monto sin símbolo. No hay
    // nada que reportarle a la persona —no puede hacer nada con esto— y el dato se sigue leyendo.
    return `${new Intl.NumberFormat('es-AR').format(monto)} ${monedaCodigo}`;
  }
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
      <table>
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

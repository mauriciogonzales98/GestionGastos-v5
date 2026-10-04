/**
 * Los íconos de Lila, dibujados dentro del proyecto.
 *
 * **Por qué propios y no una librería** (`NFR-002`, research D-06): son siete, y una librería de
 * íconos sería una dependencia de estilo que la spec no permite. Es la misma razón del ADR-002 para
 * el gráfico del dashboard: lo que entra al proyecto como dependencia tiene que pagar su costo, y
 * siete trazos de SVG no lo pagan.
 *
 * Todos comparten tres decisiones:
 *
 *   - **`stroke="currentColor"`**: toman el color del texto que los rodea, así que siguen a los dos
 *     modos de Lila sin declarar ni un color propio. Un ícono con su color escrito sería un color
 *     fuera de la paleta, y `Paleta.test.ts` no lo vería porque no está en el CSS.
 *   - **`aria-hidden="true"` y `focusable="false"`**: el nombre accesible lo da el botón que los
 *     contiene, nunca el ícono. Un SVG anunciable sumaría un segundo nombre al mismo control.
 *   - **`viewBox="0 0 24 24"` y tamaño en `em`**: escalan con la letra, así que crecen cuando la
 *     persona agranda el texto del navegador.
 */

/** Lo común a los siete, para que ninguno pueda olvidarse una de las tres decisiones. */
function Icono({ children }: { children: React.ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="1.25em"
      height="1.25em"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  );
}

/** Movimientos: dos flechas, la que entra y la que sale. */
export function IconoMovimientos() {
  return (
    <Icono>
      <path d="M7 17V7m0 0L4 10m3-3 3 3" />
      <path d="M17 7v10m0 0 3-3m-3 3-3-3" />
    </Icono>
  );
}

/** Dashboard: las barras del desglose, que es literalmente lo que esa pantalla muestra. */
export function IconoDashboard() {
  return (
    <Icono>
      <path d="M4 20V10M10 20V4M16 20v-7M22 20H2" />
    </Icono>
  );
}

/** Categorías: una etiqueta, que es lo que una categoría es. */
export function IconoCategorias() {
  return (
    <Icono>
      <path d="M3 12V5a2 2 0 0 1 2-2h7l9 9-9 9z" />
      <circle cx="7.5" cy="7.5" r="1.5" />
    </Icono>
  );
}

/** Editar: un lápiz. Inequívoco en una fila (`FR-046`). */
export function IconoEditar() {
  return (
    <Icono>
      <path d="M4 20h4L20 8l-4-4L4 16z" />
      <path d="M14 6l4 4" />
    </Icono>
  );
}

/**
 * Eliminar: un tacho de basura.
 *
 * **Tacho y no cruz**, y es una decisión de la spec: en la misma fila, al renombrar, aparece
 * "Cancelar", y una cruz se lee como cerrar o cancelar, no como eliminar.
 */
export function IconoEliminar() {
  return (
    <Icono>
      <path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13" />
      <path d="M10 11v6M14 11v6" />
    </Icono>
  );
}

/** El ícono del mensaje de error: un signo de exclamación en un círculo. */
export function IconoError() {
  return (
    <Icono>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v6M12 16.5v.5" />
    </Icono>
  );
}

/** El ícono de la confirmación: un tilde en un círculo. */
export function IconoExito() {
  return (
    <Icono>
      <circle cx="12" cy="12" r="9" />
      <path d="M8 12.5l2.5 2.5L16 9.5" />
    </Icono>
  );
}

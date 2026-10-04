/**
 * El setup del proyecto `navegador`.
 *
 * **Sin los matchers de jest-dom, a propósito.** El proyecto `dom` importa
 * `@testing-library/jest-dom/vitest`, que parchea `expect` esperando el DOM que inyecta el entorno
 * de pruebas. Acá el DOM es el de Chromium y los matchers los trae el modo navegador de Vitest, así
 * que importar el otro paquete sumaría dos implementaciones del mismo `toHaveTextContent`
 * compitiendo — que es exactamente el choque que obligó a partir el `tsconfig` en dos (D-12).
 */

/**
 * Le dice a React que esto es un entorno de pruebas y que puede vaciar los efectos dentro de `act`.
 *
 * Sin esta bandera, React avisa por `stderr` —*"The current testing environment is not configured to
 * support act(...)"*— y `act` **no vacía nada**: el render queda a medio aplicar y lo que se mide es
 * un árbol incompleto. Con las alturas de hoy daba lo mismo porque los componentes no tienen efectos
 * que cambien la disposición, pero en una feature cuyo contenido entero es medir, un render a medio
 * aplicar es un número inventado que parece medido.
 *
 * Normalmente la pone `@testing-library/react`; acá no está, por la razón de arriba, así que la pone
 * el setup.
 */
declare global {
  var IS_REACT_ACT_ENVIRONMENT: boolean;
}

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

export {};

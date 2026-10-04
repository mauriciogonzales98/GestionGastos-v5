import { act, type ReactNode } from 'react';
import { createRoot, type Root } from 'react-dom/client';

/**
 * Montar y desmontar un componente en el navegador, sin dependencias nuevas.
 *
 * **Por qué no `vitest-browser-react`**, que es lo que la documentación del modo navegador sugiere:
 * sería una **tercera** dependencia, y ni research D-01 ni la justificación de `NFR-003` la
 * previeron. La spec contabilizó dos —el proveedor y Playwright— y `SC-007` afirma que son las
 * únicas. Montar un componente de React son doce líneas con `react-dom/client`, que ya es
 * dependencia de la app, así que la cuenta de la spec se mantiene verdadera por construcción.
 *
 * Hace falta un ayudante propio y no se puede usar el de `@testing-library/react` porque ése trae
 * los matchers de jest-dom, que no conviven con los del modo navegador en un mismo programa de
 * TypeScript (D-12).
 */

let raiz: Root | null = null;
let contenedor: HTMLElement | null = null;

/**
 * Monta `nodo` en un contenedor limpio y devuelve ese contenedor.
 *
 * El contenedor **no lleva estilos propios**: lo que se mide es cómo maqueta la hoja de la app, así
 * que cualquier ancho o relleno puesto acá falsearía la medición. Lo único que se toca del documento
 * es vaciar lo que dejó el montaje anterior.
 */
export async function montar(nodo: ReactNode): Promise<HTMLElement> {
  await desmontar();

  contenedor = document.createElement('div');
  document.body.appendChild(contenedor);

  const nueva = createRoot(contenedor);
  raiz = nueva;

  // `act` envuelve el render para que los efectos corran antes de medir. Sin él se mide un árbol a
  // medio montar, que es la clase de medición que da un número distinto cada vez.
  await act(async () => {
    nueva.render(nodo);
  });

  return contenedor;
}

/** Desmonta lo que haya montado y deja el documento como estaba. */
export async function desmontar(): Promise<void> {
  if (raiz) {
    const anterior = raiz;
    await act(async () => {
      anterior.unmount();
    });
    raiz = null;
  }

  contenedor?.remove();
  contenedor = null;
}

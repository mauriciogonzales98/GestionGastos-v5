import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { MarcoDeLaApp } from '../src/ui/MarcoDeLaApp';

/**
 * US5, FR-022 a FR-025, FR-028 — el marco que comparten las pantallas con sesión.
 *
 * Lo que se verifica acá es la **estructura**: que haya un solo punto de referencia de navegación,
 * que la sección actual se anuncie, que desde cualquiera se llegue a cualquier otra con un clic y
 * que el recorrido con teclado tenga el orden que se decidió. Dónde se **ve** la barra —al costado
 * o abajo— lo decide CSS y lo mide `Navegacion.navegador.test.tsx`: acá no se puede, porque un DOM
 * simulado no maqueta.
 */

/** Las tres secciones, en el orden en que la barra las muestra. */
const SECCIONES = ['Movimientos', 'Dashboard', 'Categorías'] as const;

function montarMarco(seccion: 'movimientos' | 'dashboard' | 'categorias' = 'movimientos') {
  const onIrA = vi.fn();
  const onCerrarSesion = vi.fn();

  const utilidades = render(
    <MarcoDeLaApp
      seccion={seccion}
      email="mauri@ejemplo.com"
      onIrA={onIrA}
      onCerrarSesion={onCerrarSesion}
    >
      <h1>La pantalla</h1>
    </MarcoDeLaApp>,
  );

  return { ...utilidades, onIrA, onCerrarSesion };
}

describe('FR-022 · un solo punto de referencia de navegación', () => {
  /**
   * **Un `<nav>` y no dos** (research D-05).
   *
   * La salida fácil sería un `<nav>` lateral y otro inferior, mostrando uno y ocultando el otro
   * según el ancho. Serían dos puntos de referencia iguales para un lector de pantalla —que los
   * anuncia los dos, aunque uno esté oculto visualmente— y dos lugares donde marcar la sección
   * actual, que pueden quedar desincronizados. Uno solo, reubicado por CSS, no tiene ese problema.
   */
  it('declara exactamente un nav, con las tres secciones (FR-022, D-05)', () => {
    const { container } = montarMarco();

    expect(container.querySelectorAll('nav')).toHaveLength(1);

    for (const seccion of SECCIONES) {
      expect(screen.getByRole('button', { name: seccion })).toBeInTheDocument();
    }
  });

  it('muestra la cuenta y el cierre de sesión (FR-022, FR-027, US5:AC1)', () => {
    montarMarco();

    // Por email, como hoy. La feature 015 lo reemplaza por el nombre; esta feature deja el lugar.
    expect(screen.getByText('mauri@ejemplo.com')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cerrar sesión' })).toBeInTheDocument();
  });

  it('renderiza la pantalla dentro de un main (FR-022)', () => {
    montarMarco();

    expect(screen.getByRole('main')).toContainElement(screen.getByRole('heading', { level: 1 }));
  });
});

describe('FR-024 · la sección actual se distingue y se anuncia', () => {
  it.each([
    ['movimientos', 'Movimientos'],
    ['dashboard', 'Dashboard'],
    ['categorias', 'Categorías'],
  ] as const)('marca %s con aria-current="page" (FR-024, US5:AC3)', (seccion, rotulo) => {
    montarMarco(seccion);

    expect(screen.getByRole('button', { name: rotulo })).toHaveAttribute('aria-current', 'page');

    // Y es la única: dos secciones marcadas es peor que ninguna.
    for (const otra of SECCIONES.filter((s) => s !== rotulo)) {
      expect(screen.getByRole('button', { name: otra })).not.toHaveAttribute('aria-current');
    }
  });

  /**
   * La contracara: el estilo cuelga de `aria-current`, no de una clase aparte.
   *
   * Es la misma decisión que el conmutador del acceso con `aria-pressed` (`FR-009`), por la misma
   * razón: lo que se ve y lo que se anuncia tienen que ser el mismo dato. La regla en la hoja la
   * verifica `ClasesConRegla.test.ts`, que es el que puede leer del disco.
   */
  it('no usa una clase propia para marcar la actual (FR-024)', () => {
    montarMarco('dashboard');

    expect(screen.getByRole('button', { name: 'Dashboard' }).className).not.toMatch(
      /actual|activ|current/i,
    );
  });
});

describe('SC-009 · desde cualquier sección se llega a cualquier otra con un clic', () => {
  it.each([
    ['dashboard', 'Categorías', 'categorias'],
    ['categorias', 'Dashboard', 'dashboard'],
    ['dashboard', 'Movimientos', 'movimientos'],
    ['categorias', 'Movimientos', 'movimientos'],
    ['movimientos', 'Dashboard', 'dashboard'],
    ['movimientos', 'Categorías', 'categorias'],
  ] as const)('de %s a %s, directo (SC-009, US5:AC4)', async (desde, rotulo, esperada) => {
    const { onIrA } = montarMarco(desde);

    await userEvent.click(screen.getByRole('button', { name: rotulo }));

    // Un solo clic y la sección pedida es la que llega. Hoy, para ir del dashboard a categorías
    // había que pasar por movimientos: eran dos.
    expect(onIrA).toHaveBeenCalledExactlyOnceWith(esperada);
  });
});

describe('FR-025 · la barra se recorre y se activa con el teclado', () => {
  /**
   * El orden es marca, secciones, cuenta, contenido (research D-05).
   *
   * **En el teléfono las secciones se ven abajo pero se recorren antes del contenido**, y queda
   * anotado para que no se lea como un descuido: es el patrón de las barras inferiores de las apps
   * nativas, y ponerlas al final del recorrido obligaría a atravesar un formulario entero para
   * cambiar de sección.
   */
  it('recorre las tres secciones y después el cierre de sesión (FR-025, US5:AC5)', async () => {
    montarMarco();

    const esperado = [...SECCIONES, 'Cerrar sesión'];
    const recorrido: string[] = [];

    for (let i = 0; i < esperado.length; i += 1) {
      await userEvent.tab();
      recorrido.push(document.activeElement?.textContent?.trim() ?? '(nada)');
    }

    expect(recorrido).toEqual(esperado);
  });

  it('el contenido se recorre después de la barra (FR-025, D-05)', async () => {
    render(
      <MarcoDeLaApp seccion="movimientos" email="a@b.com" onIrA={vi.fn()} onCerrarSesion={vi.fn()}>
        <button type="button">Un control de la pantalla</button>
      </MarcoDeLaApp>,
    );

    // Cuatro tabulaciones agotan la barra; la quinta tiene que caer en el contenido.
    for (let i = 0; i < 4; i += 1) {
      await userEvent.tab();
    }
    await userEvent.tab();

    expect(document.activeElement).toHaveTextContent('Un control de la pantalla');
  });

  it('activa el cierre de sesión con el teclado (FR-025, US5:AC5)', async () => {
    const { onCerrarSesion } = montarMarco();

    screen.getByRole('button', { name: 'Cerrar sesión' }).focus();
    await userEvent.keyboard('{Enter}');

    expect(onCerrarSesion).toHaveBeenCalledOnce();
  });
});

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';

vi.mock('../src/api/cliente', () => ({
  obtenerMovimientos: vi.fn(),
  crearMovimiento: vi.fn(),
  obtenerResumen: vi.fn().mockResolvedValue({
    desde: '2026-08-01',
    hasta: '2026-08-31',
    monedas: [],
  }),
  ErrorDeSesion: class ErrorDeSesion extends Error {},
}));

const cliente = await import('../src/api/cliente');

beforeEach(() => {
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue([]);
  vi.mocked(cliente.crearMovimiento).mockResolvedValue({
    id: 1,
    nota: '',
    tipo: 'gasto',
    monto: 800,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'ARS',
    fecha: '2026-08-23',
  });
});

/**
 * AC-55 (RF-15): el formulario se recorre, se completa y se envía íntegramente con el teclado.
 *
 * Sin mouse en ningún paso: ni un click. Si algún control quedara fuera del orden de tabulación o
 * el envío con Enter dejara de funcionar, este test es lo único que se entera.
 */
describe('AC-55 — el formulario se usa entero con el teclado', () => {
  it('se recorre con Tab y se envía con Enter, sin usar el mouse AC-55', async () => {
    const usuario = userEvent.setup();
    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        errorDelCatalogo={null}
        errorDelCatalogoDeMonedas={null}
        onSesionVencida={() => {}}
      />,
    );
    await screen.findByRole('button', { name: 'Registrar' });

    // El orden del DOM es el orden de tabulación: no hay tabindex positivo que lo altere.
    //
    // **Los tres botones de la cabecera ya no están acá** (`FR-022`, feature 014). Hasta entonces
    // esta pantalla empezaba con "Dashboard", "Categorías" y "Cerrar sesión", y este test los
    // recorría antes del formulario. Esos controles se fueron al marco de la app, que es donde
    // vive la navegación, y su recorrido con teclado lo verifica `MarcoDeLaApp.test.tsx` — que
    // además comprueba lo que acá no se podía: que la barra se recorra **antes** del contenido,
    // aunque en el teléfono se vea abajo (research D-05).
    //
    // Lo que este test sigue siendo es lo que dice su nombre: que el **formulario** se recorra
    // entero. Con la pantalla montada sola, el primer control es el primer campo.
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'Gasto' }));

    // **La fecha pasa a ir segunda** con el reordenamiento de la feature 014: comparte el renglón
    // de arriba con el tipo, uno en cada punta. Primero lo que encuadra el movimiento —de qué tipo
    // es y cuándo fue—, después en qué se gastó, y al final cuánto.
    //
    // El campo se movió **en el DOM** y no con `order` de CSS, igual que la moneda en su momento,
    // y es por eso que este recorrido sigue coincidiendo con lo que se ve (`FR-031`). Con `order`,
    // el orden visual y el de tabulación serían dos cosas distintas y este test habría seguido en
    // verde mientras la pantalla decía otra cosa.
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Fecha'));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Categoría'));
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Monto'));
    await usuario.keyboard('800');

    // La moneda va pegada al monto, y ese par no se separa: se leen juntos —"1500 ARS"— y por eso
    // comparten renglón en todo ancho (`FR-029`, `FR-030`). Entró al formulario con la feature 009,
    // cuando iba entre categoría y fecha.
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Moneda'));

    // La nota entra acá con la feature 012, ÚLTIMA del formulario y antes del botón. `AC-55` no
    // cambió de exigencia —el formulario se recorre entero con Tab y se envía con Enter sobre el
    // botón— y ahora tiene un control más que recorrer.
    //
    // Es un control de VARIAS LÍNEAS, así que Enter dentro de él inserta un salto en vez de enviar.
    // Eso no rompe `AC-55`: el envío con Enter se verifica sobre el botón, unas líneas más abajo, que
    // es el camino que este test siempre usó. Lo que sí dejó de ser cierto es el comentario de
    // `CamposDelMovimiento` que decía que el envío salía "desde cualquier campo", y se corrigió ahí.
    //
    // Que ponerlo al final sea deliberado se lee justo acá: quien no usa la nota paga exactamente un
    // Tab más y ningún dato más, que es lo que mantiene intacto el camino rápido de carga.
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Nota'));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Registrar' }));

    // Enter sobre el botón enviado con el teclado.
    await usuario.keyboard('{Enter}');

    expect(cliente.crearMovimiento).toHaveBeenCalledWith(
      expect.objectContaining({ tipo: 'gasto', monto: 800, categoriaId: 1 }),
    );
  });

  /**
   * `FR-007` y `PRD-06:AC-09` (feature 011): **el foco sale del formulario y no queda atrapado.**
   *
   * Es la otra mitad de `AC-55`, la que el test de arriba no cubre: recorrerlo entero verifica que
   * se pueda entrar y llegar al final, no que se pueda salir. Una trampa de foco —un manejador que
   * devuelve el foco al primer campo cuando llega al último— deja a quien navega con teclado
   * girando en el formulario sin poder alcanzar el resto de la página, y desde el mouse es
   * invisible.
   *
   * Se verifica **por la ausencia de trampa**: después del último control del formulario, el foco
   * está en algo que no pertenece al formulario. Cuál sea ese algo es del orden del documento y va
   * a cambiar con cada control que se agregue debajo; que ya no esté adentro, no.
   */
  it('el foco sale del formulario sin quedar atrapado FR-007 PRD-06:AC-09', async () => {
    const usuario = userEvent.setup();
    render(
      <PantallaMovimientos
        hoy="2026-08-23"
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        errorDelCatalogo={null}
        errorDelCatalogoDeMonedas={null}
        onSesionVencida={() => {}}
      />,
    );

    const registrar = await screen.findByRole('button', { name: 'Registrar' });
    const formulario = registrar.closest('form');
    expect(formulario).not.toBeNull();

    registrar.focus();
    expect(document.activeElement).toBe(registrar);

    await usuario.tab();

    expect(formulario!.contains(document.activeElement)).toBe(false);
  });
});

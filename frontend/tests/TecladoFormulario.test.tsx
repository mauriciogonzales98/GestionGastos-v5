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
        email="ana@ejemplo.com"
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        errorDelCatalogo={null}
        errorDelCatalogoDeMonedas={null}
        onCerrarSesion={() => {}}
        onGestionarCategorias={() => {}}
        onVerDashboard={() => {}}
        onSesionVencida={() => {}}
      />,
    );
    await screen.findByRole('button', { name: 'Registrar' });

    // El orden del DOM es el orden de tabulación: no hay tabindex positivo que lo altere.
    //
    // Los primeros controles de la pantalla son los tres de la cabecera —"Dashboard",
    // "Categorías" y "Cerrar sesión"—, que van antes del formulario. Se verifican en vez de
    // saltearlos: si alguno apareciera con `tabindex` para sacarlo del camino, quien navega con
    // teclado no podría alcanzarlo. "Categorías" llega con la feature 007 y es la puerta a la
    // pantalla de gestión; "Dashboard" llega con la 010 y es la puerta al análisis del período.
    //
    // Que este test se haya puesto en rojo al agregar el botón es la señal de que sirve: un control
    // nuevo en la cabecera no pasa sin que alguien confirme que se llega a él con el teclado.
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Dashboard' }));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Categorías' }));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Cerrar sesión' }));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'Gasto' }));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Monto'));
    await usuario.keyboard('800');

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Categoría'));
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');

    // La moneda entra acá con la feature 009, entre la categoría y la fecha. AC-55 no cambió de
    // exigencia —el formulario se recorre entero con Tab— y ahora tiene un control más que
    // recorrer: un campo nuevo que quedara fuera del orden de tabulación sería inalcanzable para
    // quien no usa mouse, y eso es justo lo que este test existe para impedir.
    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Moneda'));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByLabelText('Fecha'));

    await usuario.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Registrar' }));

    // Enter sobre el botón enviado con el teclado.
    await usuario.keyboard('{Enter}');

    expect(cliente.crearMovimiento).toHaveBeenCalledWith(
      expect.objectContaining({ tipo: 'gasto', monto: 800, categoriaId: 1 }),
    );
  });
});

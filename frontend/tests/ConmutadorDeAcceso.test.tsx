import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';

/**
 * FR-009, FR-003 — el conmutador se ve como dos opciones con una elegida, y hay un solo botón
 * principal.
 *
 * Lo que esta prueba cuida no es el aspecto —eso se mira con los ojos— sino **de dónde sale** el
 * aspecto. Hasta la feature 011 el modo activo se marcaba con negrita y subrayado, así que se leía
 * como un enlace; el problema de fondo es otro: si el estilo colgara de una clase que el componente
 * agrega aparte, lo que se **ve** y lo que se **anuncia** serían dos datos distintos y podrían
 * desincronizarse. `FR-009` exige que salgan del mismo atributo, y eso sí se puede verificar.
 */
describe('FR-009 · el modo elegido se ve y se anuncia desde el mismo dato', () => {
  it('marca el modo activo con aria-pressed y no con una clase aparte (FR-009, US1:AC2)', () => {
    render(<FormularioAcceso onEntrar={() => {}} />);

    const iniciar = screen.getByRole('button', { name: 'Iniciar sesión' });
    const crear = screen.getByRole('button', { name: 'Crear cuenta' });

    expect(iniciar).toHaveAttribute('aria-pressed', 'true');
    expect(crear).toHaveAttribute('aria-pressed', 'false');

    // Y ninguno lleva una clase que diga cuál está elegido: el dato es `aria-pressed` y uno solo.
    // Una clase `--activo` acá sería la segunda fuente que `FR-009` prohíbe.
    for (const boton of [iniciar, crear]) {
      expect(boton.className).not.toMatch(/activ|elegid|select/i);
    }
  });

  // La contracara —que el atributo tenga su regla en la hoja— la verifica
  // `ClasesConRegla.test.ts`, que corre en entorno `node` porque lee del disco. Acá no se puede:
  // una prueba de componente no tiene acceso al sistema de archivos.
});

describe('FR-003 · un solo botón con aspecto de acción principal', () => {
  it('el de envío es el único principal en la pantalla (FR-003, US1:AC3)', () => {
    const { container } = render(<FormularioAcceso onEntrar={() => {}} />);

    const principales = container.querySelectorAll('.c-boton--principal');

    expect(principales).toHaveLength(1);
    expect(principales[0]).toHaveAttribute('type', 'submit');
  });

  it('los del conmutador no son principales (FR-003)', () => {
    render(<FormularioAcceso onEntrar={() => {}} />);

    for (const nombre of ['Iniciar sesión', 'Crear cuenta']) {
      expect(screen.getByRole('button', { name: nombre })).not.toHaveClass('c-boton--principal');
    }
  });
});

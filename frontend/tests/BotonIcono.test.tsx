import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { BotonIcono } from '../src/ui/BotonIcono';
import { IconoEditar } from '../src/ui/iconos';

/**
 * FR-045, FR-042, US8:AC3 — **el botón de sólo ícono, y las tres reglas que lo hacen usable**.
 *
 * Un botón sin palabra no es accesible por el hecho de llevar un ícono bonito: hace falta que tenga
 * nombre accesible, que ese nombre diga sobre qué actúa, y que la palabra aparezca cuando alguien
 * la busca —apoyando el puntero o llegando con el teclado—. Las tres viven en un solo componente a
 * propósito (research D-06): escritas a mano en cada fila, la cuarta se olvida.
 *
 * **Por qué no `title`**, que sería una línea en lugar de un componente: el `title` del navegador no
 * aparece al llegar con el teclado, que es exactamente lo que `FR-042` exige, y en un teléfono no
 * aparece nunca. Un atributo que sólo funciona con mouse no cumple el requisito.
 */
describe('FR-045 · el nombre accesible es el completo y el ícono no lo repite', () => {
  it('pone el nombre completo en aria-label (FR-045, FR-042)', () => {
    render(
      <BotonIcono
        nombre="Renombrar Supermercado"
        accion="Renombrar"
        icono={IconoEditar}
        onClick={() => {}}
      />,
    );

    // Se lo busca por su nombre accesible, que es como lo encuentra quien usa un lector de
    // pantalla: si el `aria-label` faltara, el botón no tendría nombre y esto no lo encontraría.
    expect(screen.getByRole('button', { name: 'Renombrar Supermercado' })).toBeInTheDocument();
  });

  it('el ícono y el rótulo corto quedan fuera del árbol de accesibilidad (FR-045)', () => {
    const { container } = render(
      <BotonIcono
        nombre="Renombrar Supermercado"
        accion="Renombrar"
        icono={IconoEditar}
        onClick={() => {}}
      />,
    );

    const boton = screen.getByRole('button', { name: 'Renombrar Supermercado' });

    // El SVG no se anuncia: el nombre lo da el botón, y un ícono anunciable sumaría un segundo
    // nombre al mismo control.
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');

    // El rótulo corto tampoco: ya está dicho —mejor— en el `aria-label`, así que anunciarlo otra
    // vez sería "Renombrar Supermercado Renombrar".
    const rotulo = boton.querySelector('span');
    expect(rotulo).toHaveTextContent('Renombrar');
    expect(rotulo).toHaveAttribute('aria-hidden', 'true');

    // Y el nombre accesible sigue siendo UNO: el rótulo no se le suma.
    expect(boton).toHaveAccessibleName('Renombrar Supermercado');
  });

  it('no usa title, que no aparece con el teclado ni en el teléfono (FR-042, research D-06)', () => {
    const { container } = render(
      <BotonIcono
        nombre="Dar de baja Gimnasio"
        accion="Dar de baja"
        icono={IconoEditar}
        onClick={() => {}}
      />,
    );

    expect(container.querySelectorAll('[title]')).toHaveLength(0);
  });

  it('avisa al apretarse, una sola vez', async () => {
    const onClick = vi.fn();
    render(
      <BotonIcono
        nombre="Renombrar Supermercado"
        accion="Renombrar"
        icono={IconoEditar}
        onClick={onClick}
      />,
    );

    screen.getByRole('button', { name: 'Renombrar Supermercado' }).click();

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('es un botón de tipo button y no envía ningún formulario', () => {
    render(
      <BotonIcono
        nombre="Renombrar Supermercado"
        accion="Renombrar"
        icono={IconoEditar}
        onClick={() => {}}
      />,
    );

    // Vive dentro de filas que están dentro de pantallas con formularios: sin `type="button"`, un
    // `<button>` por defecto envía el formulario que lo contenga.
    expect(screen.getByRole('button', { name: 'Renombrar Supermercado' })).toHaveAttribute(
      'type',
      'button',
    );
  });
});

/*
 * **Las otras dos mitades de `FR-042` viven en otros dos archivos, y no por prolijidad.**
 *
 * Que la hoja declare las reglas de `:hover` y `:focus-visible` lo verifica
 * `ClasesConRegla.test.ts`, que corre en entorno `node` porque lee del disco — acá no se puede:
 * una prueba de componente no tiene acceso al sistema de archivos. Que el rótulo **se vea** de
 * verdad lo mide `FilasConIconos.navegador.test.tsx`, que es el único lugar donde `:hover` existe.
 */

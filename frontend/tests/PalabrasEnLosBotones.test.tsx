import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';
import { ListadoMovimientos } from '../src/movimientos/ListadoMovimientos';
import { PantallaCategorias } from '../src/categorias/PantallaCategorias';
import { MarcoDeLaApp } from '../src/ui/MarcoDeLaApp';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';

/**
 * FR-045, FR-046, US8:AC7, AC8 — **dónde la palabra se queda, y por qué**.
 *
 * US8 reemplaza botones por íconos, así que la pregunta que esta prueba contesta es la contraria:
 * cuáles **no** se reemplazan. `FR-046` lo decide caso por caso y `FR-045` da el criterio para los
 * que vengan después: lleva palabra el botón que envía un formulario, el que decide algo
 * irreversible, y el que tiene un ícono que se lee de dos maneras.
 *
 * **Se verifica el texto VISIBLE y no el nombre accesible**, y ahí está todo el punto: un
 * `aria-label` correcto no arregla nada para quien mira la pantalla y tiene que decidir si aprieta
 * "Confirmar y eliminar" o "No eliminar". Por eso cada aserción mira `textContent`, que es lo que la
 * persona lee, y no `toHaveAccessibleName`, que un `BotonIcono` también cumpliría.
 *
 * Sin esta prueba, la dirección de US8 —menos palabras, más íconos— no tiene freno: el próximo que
 * quiera "limpiar" la pantalla de confirmación cambia dos botones por un tilde y una cruz, y el
 * daño recién se ve cuando alguien borra un movimiento que no quería borrar.
 */

/**
 * Lo que la persona **lee** en el botón.
 *
 * **No es `textContent`, y la diferencia es la prueba entera.** `BotonIcono` lleva adentro un
 * `<span aria-hidden="true">` con el rótulo corto —"Eliminar"— que la hoja esconde hasta que alguien
 * apoya el puntero. Ese span está en `textContent`, así que con `textContent` pelado un botón de
 * sólo ícono "lleva palabra" y la guardia no guarda nada: se comprobó convirtiendo "No dar de baja"
 * en un `BotonIcono` y viendo que la prueba seguía en verde.
 *
 * Se descuentan dos clases de texto, que son las dos formas que tiene esta app de que un texto esté
 * en el DOM sin estar a la vista: lo que lleva `aria-hidden` —el rótulo del ícono— y lo que lleva
 * `.u-solo-lectores`, que es el caso simétrico, visible para un lector de pantalla y no para el ojo.
 */
function textoVisible(boton: HTMLElement): string {
  const copia = boton.cloneNode(true) as HTMLElement;

  copia.querySelectorAll('[aria-hidden="true"], .u-solo-lectores').forEach((oculto) => {
    oculto.remove();
  });

  return (copia.textContent ?? '').trim();
}

/**
 * Un botón lleva palabra cuando se lee una palabra en él, y además **no es un botón de sólo ícono**.
 *
 * Las dos cosas y no una: la primera cubre un ícono armado a mano, la segunda deja la intención
 * escrita donde se entiende sin razonar la cascada. Un botón de esta lista que aparezca como
 * `c-boton-icono` es, por definición de `FR-045`, un botón mal clasificado.
 */
function llevaPalabra(boton: HTMLElement): boolean {
  return /\p{L}/u.test(textoVisible(boton)) && !boton.classList.contains('c-boton-icono');
}

const MOVIMIENTO: Movimiento = {
  id: 1,
  tipo: 'gasto',
  monto: 1500,
  categoriaId: 1,
  categoriaNombre: 'Comida',
  monedaCodigo: 'ARS',
  fecha: '2026-10-01',
  nota: '',
};

describe('FR-046 · los botones que envían un formulario conservan la palabra', () => {
  it('el acceso: "Entrar", "Crear mi cuenta" y los dos modos del conmutador (FR-046, US8:AC7)', async () => {
    render(<FormularioAcceso onEntrar={() => {}} />);

    for (const nombre of ['Entrar', 'Iniciar sesión', 'Crear cuenta']) {
      expect(llevaPalabra(screen.getByRole('button', { name: nombre })), nombre).toBe(true);
    }

    // El alta: el envío cambia de texto pero sigue siendo una palabra.
    await userEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    expect(llevaPalabra(screen.getByRole('button', { name: 'Crear mi cuenta' }))).toBe(true);
  });

  it('las categorías: "Crear categoría", "Guardar" y "Cancelar" (FR-046, FR-044, US8:AC7)', async () => {
    render(
      <PantallaCategorias
        categorias={CATEGORIAS}
        onCrear={async () => {}}
        onRenombrar={async () => {}}
        onDarDeBaja={async () => {}}
      />,
    );

    expect(llevaPalabra(screen.getByRole('button', { name: 'Crear categoría' }))).toBe(true);

    // En modo renombre: `FR-044` dice que "Guardar" y "Cancelar" conservan el texto. "Cancelar" es
    // el caso del ícono ambiguo — una cruz se lee también como borrar.
    await userEvent.click(screen.getByRole('button', { name: 'Renombrar Comida' }));

    for (const nombre of ['Guardar', 'Cancelar']) {
      expect(llevaPalabra(screen.getByRole('button', { name: nombre })), nombre).toBe(true);
    }
  });

  it('el marco: "Cerrar sesión" con palabra, y las secciones con ícono Y palabra (FR-046, US8:AC8)', () => {
    render(
      <MarcoDeLaApp
        seccion="movimientos"
        email="ana@ejemplo.com"
        onIrA={() => {}}
        onCerrarSesion={() => {}}
      >
        <p>Contenido</p>
      </MarcoDeLaApp>,
    );

    // Un ícono de "salir" se confunde con volver o cerrar la ventana, así que éste lleva palabra.
    expect(llevaPalabra(screen.getByRole('button', { name: 'Cerrar sesión' }))).toBe(true);

    // Las secciones son lugares y no acciones: llevan las dos cosas (`FR-022`).
    for (const nombre of ['Movimientos', 'Dashboard', 'Categorías']) {
      const seccion = screen.getByRole('button', { name: nombre });

      expect(llevaPalabra(seccion), nombre).toBe(true);
      expect(seccion.querySelector('svg'), `${nombre} sin ícono`).not.toBeNull();
    }
  });
});

/**
 * **Lo irreversible se confirma con palabras.** Es el grupo que más importa de `FR-046`: los cuatro
 * botones que deciden un borrado, donde la palabra dice exactamente qué pasa.
 */
describe('FR-046 · las confirmaciones irreversibles conservan la palabra', () => {
  it('eliminar un movimiento: "Confirmar y eliminar" y "No eliminar" (FR-046, US8:AC8)', async () => {
    render(
      <ListadoMovimientos movimientos={[MOVIMIENTO]} onEditar={() => {}} onEliminar={() => {}} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /^Eliminar/ }));

    const confirmar = screen.getByRole('button', { name: /^Confirmar y eliminar/ });
    const descartar = screen.getByRole('button', { name: /^No eliminar/ });

    expect(llevaPalabra(confirmar)).toBe(true);
    expect(llevaPalabra(descartar)).toBe(true);

    // Y la advertencia sigue dicha entera, que es el dato que una pregunta genérica se guarda.
    expect(screen.getByRole('alert')).toHaveTextContent(/no se puede|para siempre/i);
  });

  it('dar de baja una categoría: "Confirmar la baja" y "No dar de baja" (FR-044, FR-046)', async () => {
    render(
      <PantallaCategorias
        categorias={CATEGORIAS}
        onCrear={async () => {}}
        onRenombrar={async () => {}}
        onDarDeBaja={async () => {}}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Dar de baja Comida' }));

    for (const nombre of ['Confirmar la baja', 'No dar de baja']) {
      expect(llevaPalabra(screen.getByRole('button', { name: nombre })), nombre).toBe(true);
    }

    // `FR-044`: la baja sigue pidiendo confirmación con la misma advertencia.
    expect(screen.getByRole('alert')).toHaveTextContent(/no se puede reactivar/i);
  });
});

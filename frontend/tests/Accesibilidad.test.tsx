import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';
import { PantallaCategorias } from '../src/categorias/PantallaCategorias';
import { PantallaDashboard } from '../src/dashboard/PantallaDashboard';
import { FormularioMovimiento } from '../src/movimientos/FormularioMovimiento';
import { ListadoMovimientos } from '../src/movimientos/ListadoMovimientos';
import { PantallaMovimientos } from '../src/movimientos/PantallaMovimientos';
import { VentanaDeEdicion } from '../src/movimientos/VentanaDeEdicion';
import type { Movimiento } from '../src/api/tipos';
import { CATEGORIAS } from './categorias.fixture';
import { MONEDAS } from './monedas.fixture';
import { RESUMEN } from './resumen.fixture';

// El dashboard pide su resumen al montarse. Se lo intercepta igual que en su propio test: acá no se
// verifica qué pide sino qué se puede recorrer con el teclado una vez que llegó.
// El dashboard y la pantalla principal piden al montarse. Se los intercepta conservando las clases
// de error reales: la aplicación distingue por `instanceof`, y una clase inventada en un mock nunca
// es la que el componente importa.
vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');

  return {
    ...real,
    obtenerResumen: vi.fn(),
    obtenerMovimientos: vi.fn(),
    crearMovimiento: vi.fn(),
    editarMovimiento: vi.fn(),
    eliminarMovimiento: vi.fn(),
  };
});

const cliente = await import('../src/api/cliente');

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(cliente.obtenerResumen).mockResolvedValue(RESUMEN);
  vi.mocked(cliente.obtenerMovimientos).mockResolvedValue(MOVIMIENTOS);
});

/**
 * FR-005, FR-006, FR-008, NFR-002 — el piso de `PRD:RNF-06` sobre **todas** las pantallas.
 *
 * Hasta esta feature la accesibilidad se cumplía por partes, ticket por ticket, sin que nadie la
 * comprobara sobre la aplicación completa. Es lo que dice el PRD del ticket 6 y es un hecho: cada
 * feature verificó lo suyo y ninguna verificó el conjunto.
 *
 * **La lista de controles se deriva del árbol montado, no se escribe.** Un control agregado después
 * —la nota descriptiva del ticket 2, por ejemplo— queda cubierto solo. Es el mismo criterio que
 * `NFR-003` le impone al verificador de clases.
 */

const HOY = '2026-09-08';

const MOVIMIENTOS: Movimiento[] = [
  {
    id: 1,
    tipo: 'gasto',
    monto: 1500,
    categoriaId: 1,
    categoriaNombre: 'Comida',
    monedaCodigo: 'ARS',
    fecha: '2026-09-01',
    nota: '',
  },
];

/**
 * Las cinco superficies de la aplicación. Es lo único enumerado a mano de este archivo, y tiene que
 * serlo: nadie puede montar una pantalla sin saber qué props necesita. Lo que **no** se enumera es
 * qué controles tiene cada una — eso sale del árbol.
 */
const SUPERFICIES: { nombre: string; montar: () => void }[] = [
  {
    nombre: 'el acceso',
    montar: () => {
      render(<FormularioAcceso onEntrar={vi.fn()} />);
    },
  },
  {
    nombre: 'el formulario de registro',
    montar: () => {
      render(
        <FormularioMovimiento
          categorias={CATEGORIAS}
          monedas={MONEDAS}
          hoy={HOY}
          onGuardar={vi.fn()}
        />,
      );
    },
  },
  {
    nombre: 'el listado de movimientos',
    montar: () => {
      render(
        <ListadoMovimientos movimientos={MOVIMIENTOS} onEditar={vi.fn()} onEliminar={vi.fn()} />,
      );
    },
  },
  {
    nombre: 'la ventana de edición',
    montar: () => {
      render(
        <VentanaDeEdicion
          movimiento={MOVIMIENTOS[0]}
          categorias={CATEGORIAS}
          monedas={MONEDAS}
          hoy={HOY}
          onGuardar={vi.fn()}
          onCerrar={vi.fn()}
        />,
      );
    },
  },
  {
    nombre: 'la gestión de categorías',
    montar: () => {
      render(
        <PantallaCategorias
          categorias={CATEGORIAS}
          onCrear={vi.fn()}
          onRenombrar={vi.fn()}
          onDarDeBaja={vi.fn()}
          onVolver={vi.fn()}
        />,
      );
    },
  },
  {
    /**
     * **La pantalla principal entera, y no sólo sus componentes por separado.**
     *
     * Es la superficie con más controles de la aplicación —los tres botones de la cabecera, el
     * formulario, la barra de acotado con sus cuatro controles y su botón, y el listado— y quedaba
     * fuera de la barrida: se montaban `FormularioMovimiento` y `ListadoMovimientos` sueltos, que
     * son dos de sus partes. La barra de filtros, que esta misma feature agrega, no la miraba nadie
     * (hallazgo 3 de la revisión del PR #28).
     */
    nombre: 'la pantalla principal entera',
    montar: () => {
      render(
        <PantallaMovimientos
          hoy={HOY}
          email="ana@ejemplo.com"
          categorias={CATEGORIAS}
          monedas={MONEDAS}
          errorDelCatalogo={null}
          errorDelCatalogoDeMonedas={null}
          onCerrarSesion={vi.fn()}
          onGestionarCategorias={vi.fn()}
          onVerDashboard={vi.fn()}
          onSesionVencida={vi.fn()}
        />,
      );
    },
  },
  {
    nombre: 'el dashboard',
    montar: () => {
      render(<PantallaDashboard monedas={MONEDAS} onVolver={vi.fn()} onSesionVencida={vi.fn()} />);
    },
  },
];

/**
 * Todo lo que puede recibir el foco, tomado del DOM montado.
 *
 * No usa `getAllByRole` con una lista de roles: un control con un rol que la lista no previera
 * quedaría sin verificar, y el punto es no depender de que alguien se acuerde.
 */
function controlesEnfocables(): HTMLElement[] {
  return [
    ...document.querySelectorAll<HTMLElement>(
      'a[href], button, input:not([type="hidden"]), select, textarea, [tabindex]:not([tabindex="-1"])',
    ),
  ].filter((elemento) => !elemento.hasAttribute('disabled'));
}

/**
 * El nombre accesible de un control, en las cuatro formas que este proyecto usa.
 *
 * **Escrito a mano y no traído de una librería**, porque `NFR-004` no admite dependencias nuevas y
 * porque el algoritmo completo de *accname* es mucho más de lo que hace falta acá: la aplicación
 * etiqueta con `aria-label`, con `aria-labelledby`, con un `<label for>` y —los botones— con su
 * propio texto. Un control que se etiquetara de otra forma quedaría informado como sin nombre, que
 * es el lado correcto en el que equivocarse: obliga a mirarlo en vez de darlo por bueno.
 */
function nombreAccesible(elemento: HTMLElement): string {
  const propio = elemento.getAttribute('aria-label');
  if (propio) {
    return propio.trim();
  }

  const apuntado = elemento.getAttribute('aria-labelledby');
  if (apuntado) {
    return apuntado
      .split(/\s+/)
      .map((id) => document.getElementById(id)?.textContent ?? '')
      .join(' ')
      .trim();
  }

  if (elemento.id) {
    const etiqueta = document.querySelector(`label[for="${CSS.escape(elemento.id)}"]`);
    if (etiqueta?.textContent) {
      return etiqueta.textContent.trim();
    }
  }

  // Un `<label>` que envuelve al control.
  const envolvente = elemento.closest('label');
  if (envolvente?.textContent) {
    return envolvente.textContent.trim();
  }

  /**
   * **El texto propio nombra a los botones y a los enlaces, y a nadie más.**
   *
   * Son los roles que ARIA llama "name from content". Un control de formulario **no** lo es, y la
   * diferencia importa: un `<select>` sin etiqueta tiene `textContent` —el texto de sus `<option>`—
   * y con el `textContent` como último recurso para todo, el verificador le encontraba nombre y lo
   * daba por etiquetado. Es el control más común de esta aplicación, así que el agujero se lo
   * tragaba casi entero.
   */
  if (elemento.matches('button, a[href], [role="button"], [role="link"]')) {
    return (elemento.textContent ?? '').trim();
  }

  return '';
}

describe('FR-005, NFR-002 · todo control interactivo tiene una etiqueta accesible', () => {
  it.each(SUPERFICIES)(
    '$nombre no deja ningún control sin nombre (FR-005, PRD:RNF-06, PRD-06:AC-02)',
    ({ montar }) => {
      montar();

      const controles = controlesEnfocables();

      // Que haya encontrado algo: un recorrido que no encuentra controles informa verde igual que
      // uno que los encontró todos etiquetados. Es el mismo fallo silencioso que D-03 persigue.
      expect(controles.length).toBeGreaterThan(0);

      const sinNombre = controles
        .filter((control) => nombreAccesible(control) === '')
        // Se informa QUÉ control quedó sin nombre, no cuántos: un fallo que dice "esperaba 0,
        // recibí 2" obliga a averiguar a mano lo que el test ya sabe.
        .map((control) => `${control.tagName.toLowerCase()}#${control.id || '(sin id)'}`);

      expect(sinNombre).toEqual([]);
    },
  );
});

describe('FR-006 · el orden del foco sigue el orden de lectura', () => {
  it.each(SUPERFICIES)('$nombre no reordena el foco (FR-006, PRD-06:AC-08)', ({ montar }) => {
    montar();

    // Ningún `tabindex` positivo. Es la única forma de sacar el foco del orden del documento, y por
    // eso verificar su ausencia es verificar que el orden del foco ES el orden de lectura, sin
    // tener que simular el recorrido pantalla por pantalla.
    const reordenados = controlesEnfocables().filter(
      (control) => Number(control.getAttribute('tabindex') ?? '0') > 0,
    );

    expect(reordenados).toEqual([]);
  });
});

describe('FR-008 · el motivo del rechazo llega asociado a su campo', () => {
  it('el formulario de registro asocia cada error a su control (FR-008, PRD-06:AC-03)', async () => {
    const usuario = userEvent.setup();
    render(
      <FormularioMovimiento
        categorias={CATEGORIAS}
        monedas={MONEDAS}
        hoy={HOY}
        onGuardar={vi.fn()}
      />,
    );

    // Un monto vacío es el rechazo más barato de provocar y el que `PRD:AC-18` ya cubre.
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    const monto = screen.getByLabelText(/Monto/i);

    expect(monto).toHaveAttribute('aria-invalid', 'true');

    // El mensaje no está "en la pantalla": está APUNTADO desde el control, que es lo que hace que
    // quien recorre con teclado lo reciba al llegar al campo y no antes ni después.
    const idDelMensaje = monto.getAttribute('aria-describedby');
    expect(idDelMensaje).toBeTruthy();
    expect(document.getElementById(idDelMensaje!)?.textContent ?? '').not.toBe('');
  });
});

/**
 * D-03 · el verificador se ve fallar.
 *
 * El modo de fallo concreto: `controlesEnfocables` deja de encontrar controles —porque el selector
 * se quedó corto, o porque una pantalla cambió de forma— y entonces `sinNombre` sale vacío y todo
 * pasa en verde sin haber mirado nada. La guarda de `length > 0` de arriba lo ataja; estos casos
 * comprueban que el detector detecta.
 */
describe('D-03 · el verificador de accesibilidad sabe fallar', () => {
  it('detecta un input sin etiqueta', () => {
    render(
      <form>
        <input type="text" />
      </form>,
    );

    const sinNombre = controlesEnfocables().filter((control) => nombreAccesible(control) === '');

    expect(sinNombre).toHaveLength(1);
  });

  /**
   * **El caso que faltaba, y que dejaba pasar al control más común de esta aplicación.**
   *
   * Un `<select>` sin etiqueta **tiene** `textContent`: el texto de sus `<option>`. Con el
   * `textContent` como último recurso para todo, el verificador le encontraba nombre —"Todas las
   * monedas Peso argentino Dólar"— y lo daba por etiquetado. Se descubrió quitándole a propósito la
   * etiqueta al acotado por moneda: la barrida siguió en verde (hallazgo 3 de la revisión del
   * PR #28).
   *
   * El `textContent` sólo nombra a los roles que toman su nombre del contenido —botones y
   * enlaces—, y nunca a un control de formulario.
   */
  it('detecta un select sin etiqueta, aunque sus opciones tengan texto', () => {
    render(
      <form>
        <select>
          <option value="">Todas las monedas</option>
          <option value="1">Peso argentino</option>
        </select>
      </form>,
    );

    const sinNombre = controlesEnfocables().filter((control) => nombreAccesible(control) === '');

    expect(sinNombre).toHaveLength(1);
  });

  it('detecta un textarea sin etiqueta, por el mismo motivo', () => {
    render(
      <form>
        <textarea defaultValue="algo escrito" />
      </form>,
    );

    expect(controlesEnfocables().filter((control) => nombreAccesible(control) === '')).toHaveLength(
      1,
    );
  });

  it('acepta un control etiquetado por su label', () => {
    render(
      <form>
        <label htmlFor="algo">Un campo</label>
        <input id="algo" type="text" />
      </form>,
    );

    expect(nombreAccesible(controlesEnfocables()[0])).toBe('Un campo');
  });

  it('detecta un tabindex positivo, que es lo que rompe el orden de lectura', () => {
    render(
      <form>
        <label htmlFor="uno">Uno</label>
        <input id="uno" tabIndex={3} />
      </form>,
    );

    const reordenados = controlesEnfocables().filter(
      (control) => Number(control.getAttribute('tabindex') ?? '0') > 0,
    );

    expect(reordenados).toHaveLength(1);
  });
});

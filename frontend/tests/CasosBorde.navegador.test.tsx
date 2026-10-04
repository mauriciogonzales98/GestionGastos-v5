import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { page, userEvent } from 'vitest/browser';
import { FormularioAcceso } from '../src/acceso/FormularioAcceso';
import { desmontar, montar } from './montar';
import { desbordaLaPagina } from './anchos';
import '../src/estilos/index.css';

vi.mock('../src/api/cliente', async () => {
  const real = await vi.importActual<typeof import('../src/api/cliente')>('../src/api/cliente');

  return { ...real, crearCuenta: vi.fn(), iniciarSesion: vi.fn() };
});

const cliente = await import('../src/api/cliente');

/**
 * **Los casos borde de la spec que ninguna historia midió.**
 *
 * La lista de casos borde de una spec es la parte que se lee al final y se verifica nunca: cada
 * historia prueba lo suyo y los bordes quedan en tierra de nadie. Los cinco de esta feature son
 * afirmaciones sobre píxeles —"no ensancha el bloque", "se desplaza dentro del campo", "crece en
 * alto y no en ancho"—, así que o se miden en un navegador o se dan por buenas.
 *
 * Dos de los cinco ya tenían prueba cuando se escribió este archivo y por eso no están acá: el
 * botón "Creando…" que no cambia de ancho lo mide `PantallaDeAcceso.navegador.test.tsx` (`FR-012`),
 * y la preferencia de movimiento reducido la cubre `MovimientoReducido.test.ts` leyendo la hoja.
 */

const ALTO = 900;

/** El ancho objetivo: el teléfono donde todo esto duele (`FR-011`). */
const ANGOSTO = 360;

/** El bloque visual de la pantalla de acceso. Lo que estos casos no pueden hacer es ensancharlo. */
function tarjeta(raiz: HTMLElement): HTMLElement {
  return raiz.querySelector<HTMLElement>('.c-tarjeta')!;
}

async function abrirAcceso(ancho = ANGOSTO): Promise<HTMLElement> {
  await page.viewport(ancho, ALTO);

  return montar(<FormularioAcceso onEntrar={() => {}} />);
}

/** Entra con credenciales cualesquiera, para llegar al estado que cada caso necesita. */
async function enviar(raiz: HTMLElement): Promise<void> {
  await userEvent.fill(raiz.querySelector<HTMLInputElement>('input[type="email"]')!, 'a@b.com');
  await userEvent.fill(
    raiz.querySelector<HTMLInputElement>('input[type="password"]')!,
    'una frase',
  );
  await userEvent.click(raiz.querySelector<HTMLElement>('button[type="submit"]')!);
}

beforeEach(() => {
  vi.mocked(cliente.iniciarSesion).mockResolvedValue({ email: 'a@b.com' });
  vi.mocked(cliente.crearCuenta).mockResolvedValue(undefined);
});

afterEach(desmontar);

/**
 * **Varios motivos del servidor juntos crecen en alto, no en ancho.**
 *
 * El bloque de mensaje declara `overflow-wrap: break-word` justamente para esto, y hasta acá eso
 * era una regla escrita y no una medición.
 */
describe('caso borde · un mensaje de error largo no ensancha el bloque', () => {
  it('el error de varias líneas deja la tarjeta del mismo ancho (FR-005)', async () => {
    const raiz = await abrirAcceso();
    const anchoLimpio = tarjeta(raiz).getBoundingClientRect().width;

    // **Varios motivos juntos**, que es exactamente el caso borde: el formulario los une en un solo
    // mensaje. Van bajo una clave que no es de ningún campo, que es lo que los manda al bloque
    // general en lugar de al lado de un control.
    vi.mocked(cliente.iniciarSesion).mockRejectedValue(
      new cliente.ErrorDeValidacion({
        general: [
          'El servidor rechazó la petición por varios motivos a la vez.',
          'La cuenta indicada no admite este tipo de acceso en este momento.',
          'Volvé a intentarlo más tarde o revisá los datos ingresados.',
        ],
      }),
    );

    await enviar(raiz);

    const mensaje = raiz.querySelector<HTMLElement>('.c-mensaje--error');

    // Premisa: el mensaje está a la vista. Sin esto la prueba mediría una pantalla sin error.
    expect(mensaje, 'el error no apareció').not.toBeNull();

    expect(tarjeta(raiz).getBoundingClientRect().width).toBe(anchoLimpio);
    expect(desbordaLaPagina(ANGOSTO)).toBe(false);

    // Y creció en alto: es la contracara, y sin ella "no ensanchó" podría significar "no apareció".
    expect(mensaje!.getBoundingClientRect().height).toBeGreaterThan(20);
  });
});

/**
 * **Un email largo se desplaza dentro del campo.**
 *
 * Es el comportamiento nativo de un `<input>`, y lo que hay que verificar es que nada lo haya
 * rompido: un `width: auto` o un `min-width` de contenido lo convertirían en un campo que se
 * estira.
 */
describe('caso borde · un email muy largo no ensancha el bloque', () => {
  it('el texto se desplaza adentro del campo y la tarjeta no se mueve (FR-004)', async () => {
    const raiz = await abrirAcceso();
    const anchoLimpio = tarjeta(raiz).getBoundingClientRect().width;

    const email = raiz.querySelector<HTMLInputElement>('input[type="email"]')!;
    const anchoDelCampo = email.getBoundingClientRect().width;

    await userEvent.fill(
      email,
      `${'nombre.muy.largo.de.persona'.repeat(4)}@ejemplo-larguisimo.com`,
    );

    expect(tarjeta(raiz).getBoundingClientRect().width).toBe(anchoLimpio);
    expect(email.getBoundingClientRect().width).toBe(anchoDelCampo);
    // El texto mide más que la caja: o sea que se desplaza adentro, que es lo que se pide.
    expect(email.scrollWidth).toBeGreaterThan(email.clientWidth);
    expect(desbordaLaPagina(ANGOSTO)).toBe(false);
  });
});

/**
 * **El texto del navegador al 200 %.**
 *
 * Se emula subiendo el tamaño de letra de la raíz, que es de lo que cuelga toda la escala de Lila:
 * la tipografía, el espaciado y los radios están en `rem`, así que duplicar el `font-size` del
 * documento es exactamente lo que hace la preferencia del navegador. Un zoom de página no serviría
 * para probar esto: escala también la ventana, así que nada cambia de proporción.
 */
describe('caso borde · el texto del navegador al 200 %', () => {
  afterEach(() => {
    document.documentElement.style.fontSize = '';
  });

  it('el formulario sigue usable, sin desborde y sin superposiciones (FR-004, FR-007)', async () => {
    const raiz = await abrirAcceso();
    document.documentElement.style.fontSize = '32px';

    // La premisa: la escala efectivamente se duplicó.
    expect(parseFloat(getComputedStyle(raiz.querySelector('h1')!).fontSize)).toBeGreaterThan(40);

    expect(desbordaLaPagina(ANGOSTO)).toBe(false);

    // Nada se superpone: cada control de la pila empieza por debajo del que tiene arriba.
    const controles = [...raiz.querySelectorAll<HTMLElement>('input, button[type="submit"]')].map(
      (control) => control.getBoundingClientRect(),
    );

    for (let i = 1; i < controles.length; i += 1) {
      expect(
        controles[i].top,
        `el control ${i} se superpone con el anterior al 200 %`,
      ).toBeGreaterThanOrEqual(controles[i - 1].bottom - 1);
    }
  });
});

/**
 * **Una pantalla apaisada y baja.**
 *
 * Es el caso que decidió que `l-centrado` ancle el bloque arriba en lugar de centrarlo en vertical:
 * centrado, con poca altura el título queda por encima del borde y no hay forma de desplazarse
 * hasta él. 740 × 360 es un teléfono dado vuelta.
 */
describe('caso borde · pantalla apaisada y baja', () => {
  it('la marca queda alcanzable y nada se corta por arriba (FR-008)', async () => {
    await page.viewport(740, 360);
    const raiz = await montar(<FormularioAcceso onEntrar={() => {}} />);

    const marca = raiz.querySelector<HTMLElement>('h1')!.getBoundingClientRect();

    // Dentro de la ventana por arriba: es lo que un centrado vertical rompía.
    expect(marca.top).toBeGreaterThanOrEqual(0);

    // Y si el bloque no entra, se puede desplazar hasta el final: crece hacia abajo.
    const tarjetaCaja = tarjeta(raiz).getBoundingClientRect();
    expect(tarjetaCaja.top).toBeGreaterThanOrEqual(0);
    expect(desbordaLaPagina(740)).toBe(false);
  });
});

import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * La lectura del código y de la hoja de estilos que comparten los verificadores de la feature 011.
 *
 * **Recorre el árbol, nunca una lista escrita a mano** (`NFR-003`). Es la misma promesa que
 * `verificar-monedas.sh` protege del lado del catálogo: una comprobación que se sostiene en que
 * alguien se acuerde de agregar una fila a una lista no es una comprobación, es un recordatorio.
 * Una pantalla nueva —la nota descriptiva del ticket 2, por ejemplo— queda cubierta sola.
 *
 * Vive en `tests/` y no en `src/` porque no lo consume la aplicación: sólo lo consumen los tests
 * que corren en entorno `node`, que son los únicos que pueden tocar el disco.
 */

const RAIZ = join(fileURLToPath(new URL('.', import.meta.url)), '..', 'src');

/** Todos los archivos bajo `dir` cuyo nombre termina en alguno de `extensiones`, recursivo. */
function archivosBajo(dir: string, extensiones: readonly string[]): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entrada) => {
    const ruta = join(dir, entrada.name);

    if (entrada.isDirectory()) {
      return archivosBajo(ruta, extensiones);
    }

    return extensiones.some((extension) => entrada.name.endsWith(extension)) ? [ruta] : [];
  });
}

/** El contenido de cada `.tsx` de `frontend/src/`, concatenado. Es donde se escriben las clases. */
export function codigoDeLasPantallas(): string {
  return archivosBajo(RAIZ, ['.tsx'])
    .map((ruta) => readFileSync(ruta, 'utf8'))
    .join('\n');
}

/** El contenido de cada `.css` de `frontend/src/estilos/`, concatenado. */
export function hojaDeEstilos(): string {
  return archivosBajo(join(RAIZ, 'estilos'), ['.css'])
    .map((ruta) => readFileSync(ruta, 'utf8'))
    .join('\n');
}

/**
 * Las clases `l-`, `c-` y `u-` que el código referencia.
 *
 * **Se buscan sólo dentro de un `className`**, y no en cualquier parte del archivo: `--c-riel` es
 * una variable CSS y `c-riel` no es una clase. Buscar el prefijo suelto las confundía.
 *
 * El proyecto escribe los nombres como literales —`disposicion.css` lo declara: *"nada de clases
 * utilitarias sueltas"*—, así que esto alcanza. El día que haga falta una clase armada en tiempo de
 * ejecución, este verificador deja de servir y hay que decirlo, no taparlo.
 */
export function clasesReferenciadas(codigo: string): string[] {
  const enClassName = /className=(?:"([^"]*)"|\{`([^`]*)`\}|\{'([^']*)'\})/g;
  const encontradas = new Set<string>();

  for (const coincidencia of codigo.matchAll(enClassName)) {
    const valor = coincidencia[1] ?? coincidencia[2] ?? coincidencia[3] ?? '';

    for (const nombre of valor.split(/\s+/)) {
      if (/^[lcu]-[a-zA-Z0-9_-]+$/.test(nombre)) {
        encontradas.add(nombre);
      }
    }
  }

  return [...encontradas].sort();
}

/**
 * Las clases que la hoja de estilos declara: todo `.x` que aparezca en un selector.
 *
 * **Se toma cada trozo de texto que precede a una llave de apertura**, y no lo que hay antes de la
 * primera llave de cada bloque. La diferencia importa para los selectores anidados dentro de un
 * at-rule: con la forma ingenua, `@media (...) { .l-cabecera { ... } }` reportaba `@media (...)`
 * como selector y perdía la clase de adentro — que fue exactamente lo que pasó al maquetar la
 * cabecera, y el test lo detectó.
 *
 * El prefacio de un at-rule no aporta falsos positivos: `@media (width >= 40rem)` no tiene ningún
 * `.` seguido de letra, y un `.5rem` tampoco lo tiene.
 */
export function clasesDeclaradas(css: string): string[] {
  const declaradas = new Set<string>();

  for (const prefacio of css.matchAll(/([^{}]+)\{/g)) {
    for (const coincidencia of prefacio[1].matchAll(/\.([a-zA-Z][a-zA-Z0-9_-]*)/g)) {
      declaradas.add(coincidencia[1]);
    }
  }

  return [...declaradas].sort();
}

/**
 * Las propiedades personalizadas de color declaradas en `:root`, como `{ '--color-texto': '#111' }`.
 *
 * Se limita a las que empiezan con `--color-` para no arrastrar espaciados ni tipografía: lo que
 * `Paleta.test.ts` mide son colores, y una relación de contraste sobre `1.5rem` no significa nada.
 */
export function coloresDeclarados(css: string): Record<string, string> {
  const paleta: Record<string, string> = {};

  for (const coincidencia of css.matchAll(/(--color-[a-zA-Z0-9_-]+)\s*:\s*([^;]+);/g)) {
    paleta[coincidencia[1]] = coincidencia[2].trim();
  }

  return paleta;
}

/**
 * Los anchos fijos declarados en píxeles: `width`, `min-width` y `max-width`.
 *
 * **Sirve para las dos fuentes**: la hoja de estilos y los `style` en línea de un `.tsx`. De ahí las
 * dos formas que reconoce — `min-width: 400px` en CSS y `minWidth: '400px'` en JSX, con la comilla
 * que el valor lleva ahí. Sin la variante en línea, un ancho fijo escrito en una pantalla pasaba sin
 * que nadie lo viera (hallazgo 8 de la revisión del PR #28).
 *
 * `max-width` entra porque un `max-width: 400px` no desborda pero sí deja contenido inalcanzable si
 * lo que hay adentro no se adapta; se informa igual y el test decide qué hacer con cada uno.
 */
export function anchosFijosEnPx(fuente: string): { propiedad: string; valor: number }[] {
  const declaracion = /\b(min-?|max-?)?[wW]idth\s*:\s*['"`]?(\d+(?:\.\d+)?)px/g;

  return [...fuente.matchAll(declaracion)].map((coincidencia) => ({
    // Se normaliza a la forma del CSS para que el filtro de `max-width` valga en los dos lados.
    propiedad: `${(coincidencia[1] ?? '').replace(/-?$/, coincidencia[1] ? '-' : '')}width`,
    valor: Number(coincidencia[2]),
  }));
}

/**
 * Las clases referenciadas que la hoja no declara. **Es el verificador de `FR-001`**, y está acá y
 * no dentro del test para que el caso sintético que tiene que dar rojo ejercite exactamente el
 * mismo código que la comprobación real (D-03). Dos implementaciones divergen, y entonces lo que se
 * vio fallar no es lo que corre.
 */
export function clasesSinRegla(codigo: string, css: string): string[] {
  const declaradas = new Set(clasesDeclaradas(css));

  return clasesReferenciadas(codigo).filter((clase) => !declaradas.has(clase));
}

/** Los anchos declarados por encima del objetivo. El verificador de `FR-003`, por la misma razón. */
export function anchosPorEncimaDe(css: string, objetivoPx: number) {
  return anchosFijosEnPx(css).filter(({ propiedad, valor }) => {
    // `max-width` acota hacia arriba: un `max-width: 800px` no produce desborde en una ventana de
    // 360 px, porque el elemento igual se encoge. Los otros dos sí lo producen.
    if (propiedad === 'max-width') {
      return false;
    }

    return valor > objetivoPx;
  });
}

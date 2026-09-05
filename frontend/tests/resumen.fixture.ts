import type { Resumen, ResumenPorMoneda, TotalPorCategoria } from '../src/api/tipos';
import { MONEDAS } from './monedas.fixture';

/**
 * Un `Resumen` de ejemplo, con las dos formas que importan: una moneda con movimientos y otra
 * **sin ninguno, en cero**.
 *
 * Esa segunda no es relleno. El servidor compone las monedas desde el **catálogo** y no desde el
 * agregado (D-05 de la feature 006), justamente para que un período sin movimientos devuelva ceros
 * en lugar de una lista vacía. Un fixture que sólo trajera monedas con datos dejaría sin ejercitar
 * la mitad del contrato que la pantalla tiene que saber pintar (FR-009).
 *
 * **Ningún test escribe cuántas monedas hay.** Ni `toHaveLength(2)`, ni "la segunda es USD", ni un
 * `monedaId` a mano: todo sale de `RESUMEN.monedas` o de este archivo. Es la regla D-10 de la
 * feature 009 y acá pesa más que en ninguna otra, porque el resumen devuelve **una entrada por cada
 * moneda del catálogo** — así que "trae dos monedas" es la aserción más natural del mundo y se
 * rompe el día que `verificar-monedas.sh` corre la suite con una moneda de más. Ya pasó una vez.
 */
const PESOS = MONEDAS[0];
const DOLARES = MONEDAS[1];

/** El desglose de la moneda con movimientos, ya ordenado de mayor a menor como llega del servidor. */
export const GASTOS_EN_PESOS: TotalPorCategoria[] = [
  { categoriaId: 3, categoriaNombre: 'Vivienda', total: 80000 },
  { categoriaId: 1, categoriaNombre: 'Comida', total: 40000 },
  { categoriaId: 2, categoriaNombre: 'Transporte', total: 20000 },
];

/**
 * La moneda con movimientos. El balance es negativo a propósito: un mes en rojo es un resultado que
 * la pantalla tiene que saber mostrar, no un error (FR-002).
 */
export const EN_PESOS: ResumenPorMoneda = {
  monedaId: PESOS.id,
  monedaCodigo: PESOS.codigo,
  totalIngresado: 100000,
  totalGastado: 140000,
  balance: -40000,
  gastosPorCategoria: GASTOS_EN_PESOS,
};

/** La moneda del catálogo que no tuvo movimientos en el período. Ceros, y desglose vacío. */
export const SIN_MOVIMIENTOS: ResumenPorMoneda = {
  monedaId: DOLARES.id,
  monedaCodigo: DOLARES.codigo,
  totalIngresado: 0,
  totalGastado: 0,
  balance: 0,
  gastosPorCategoria: [],
};

/** El mes en curso tal como lo devuelve el servidor: `desde` y `hasta` viajan siempre. */
export const RESUMEN: Resumen = {
  desde: '2026-09-01',
  hasta: '2026-09-30',
  monedas: [EN_PESOS, SIN_MOVIMIENTOS],
};

/**
 * Una variante del resumen, para no tener que rearmarlo entero cuando cambia una sola cosa.
 *
 * Se pasa lo que cambia y el resto queda como está: `construirResumen({ desde: '2026-08-01' })`.
 */
export function construirResumen(cambios: Partial<Resumen> = {}): Resumen {
  return { ...RESUMEN, ...cambios };
}

/** Lo mismo para una moneda: `construirMoneda(EN_PESOS, { balance: 0 })`. */
export function construirMoneda(
  base: ResumenPorMoneda,
  cambios: Partial<ResumenPorMoneda> = {},
): ResumenPorMoneda {
  return { ...base, ...cambios };
}

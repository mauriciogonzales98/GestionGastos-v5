import type { Moneda } from '../src/api/tipos';

/**
 * El catálogo sembrado por la migración: pesos —la predeterminada— y dólares.
 *
 * **Ningún test puede suponer que el catálogo tiene exactamente estas dos**, y por eso está
 * `LA_INESPERADA` acá al lado: es la moneda que ninguna constante del código conoce, la que
 * verifica que el selector y el acotado salgan del catálogo y no de una lista escrita a mano
 * (`PRD:AC-04`, D-11). El día que alguien escriba `['ARS', 'USD']` en el frontend, los tests que la
 * usan se ponen en rojo — que es lo único que sostiene, de este lado, la promesa que
 * `verificar-monedas.sh` protege del otro.
 */
export const MONEDAS: Moneda[] = [
  { id: 1, codigo: 'ARS', nombre: 'Peso argentino', simbolo: '$', esPredeterminada: true },
  { id: 2, codigo: 'USD', nombre: 'Dólar', simbolo: 'US$', esPredeterminada: false },
];

/** Una moneda agregada al catálogo **sólo como dato**: no la conoce ninguna línea de código. */
export const LA_INESPERADA: Moneda = {
  id: 77,
  codigo: 'XCT',
  nombre: 'Moneda de prueba',
  simbolo: 'XCT',
  esPredeterminada: false,
};

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
  {
    id: 1,
    codigo: 'ARS',
    nombre: 'Peso argentino',
    simbolo: '$',
    esPredeterminada: true,
    decimales: 2,
  },
  { id: 2, codigo: 'USD', nombre: 'Dólar', simbolo: 'US$', esPredeterminada: false, decimales: 2 },
];

/**
 * Una moneda **sin centavos**, para verificar que la escala sale del catálogo y no de `Intl`
 * (`FR-019`, D-08).
 *
 * El yen es el caso real: `Intl` también le asigna 0 decimales por su código ISO, así que por sí
 * solo no distingue de dónde salió la escala. Por eso `LA_DE_TRES` está al lado, con una escala que
 * `Intl` **no** deduciría de su código.
 */
export const SIN_CENTAVOS: Moneda = {
  id: 3,
  codigo: 'JPY',
  nombre: 'Yen japonés',
  simbolo: '¥',
  esPredeterminada: false,
  decimales: 0,
};

/**
 * La moneda que separa las dos fuentes posibles de la escala.
 *
 * `XCT` no es un código que `Intl` conozca, así que cae en la rama degradada; y el catálogo le pone
 * **tres** decimales, que es lo que ninguna deducción por código habría dado. Si un test la muestra
 * con dos, la escala no salió del catálogo.
 */
export const LA_DE_TRES: Moneda = {
  id: 78,
  codigo: 'XCT',
  nombre: 'Moneda de tres decimales',
  simbolo: 'XCT',
  esPredeterminada: false,
  decimales: 3,
};

/** Una moneda agregada al catálogo **sólo como dato**: no la conoce ninguna línea de código. */
export const LA_INESPERADA: Moneda = {
  id: 77,
  codigo: 'XCT',
  nombre: 'Moneda de prueba',
  simbolo: 'XCT',
  esPredeterminada: false,
  decimales: 2,
};

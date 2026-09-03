import type { Categoria } from '../src/api/tipos';

/**
 * El catálogo real de FR-006: 7 de gasto y 3 de ingreso, con "Otros" en los dos tipos.
 *
 * Las diez son predefinidas del sistema, así que van con `esPropia: false`. Las propias de una
 * cuenta las arma cada test que las necesita: no son parte del catálogo que toda cuenta ve.
 */
export const CATEGORIAS: Categoria[] = [
  { id: 1, nombre: 'Comida', tipo: 'gasto', esPropia: false },
  { id: 2, nombre: 'Transporte', tipo: 'gasto', esPropia: false },
  { id: 3, nombre: 'Vivienda', tipo: 'gasto', esPropia: false },
  { id: 4, nombre: 'Servicios', tipo: 'gasto', esPropia: false },
  { id: 5, nombre: 'Salud', tipo: 'gasto', esPropia: false },
  { id: 6, nombre: 'Ocio', tipo: 'gasto', esPropia: false },
  { id: 7, nombre: 'Otros', tipo: 'gasto', esPropia: false },
  { id: 8, nombre: 'Sueldo', tipo: 'ingreso', esPropia: false },
  { id: 9, nombre: 'Ingreso extra', tipo: 'ingreso', esPropia: false },
  { id: 10, nombre: 'Otros', tipo: 'ingreso', esPropia: false },
];

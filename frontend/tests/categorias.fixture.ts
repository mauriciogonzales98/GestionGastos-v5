import type { Categoria } from '../src/api/tipos';

/** El catálogo real de FR-006: 7 de gasto y 3 de ingreso, con "Otros" en los dos tipos. */
export const CATEGORIAS: Categoria[] = [
  { id: 1, nombre: 'Comida', tipo: 'gasto' },
  { id: 2, nombre: 'Transporte', tipo: 'gasto' },
  { id: 3, nombre: 'Vivienda', tipo: 'gasto' },
  { id: 4, nombre: 'Servicios', tipo: 'gasto' },
  { id: 5, nombre: 'Salud', tipo: 'gasto' },
  { id: 6, nombre: 'Ocio', tipo: 'gasto' },
  { id: 7, nombre: 'Otros', tipo: 'gasto' },
  { id: 8, nombre: 'Sueldo', tipo: 'ingreso' },
  { id: 9, nombre: 'Ingreso extra', tipo: 'ingreso' },
  { id: 10, nombre: 'Otros', tipo: 'ingreso' },
];

/**
 * La fuente de verdad del contrato HTTP entre esta pantalla y la API.
 *
 * Estos tipos están escritos a mano y NO se derivan del backend, así que nada los mantiene
 * alineados por construcción: un rename coherente del backend deja en verde el build, `tsc`,
 * ESLint y toda la suite, y hace llegar `undefined` a la pantalla. `tsc` verifica que el frontend
 * sea coherente consigo mismo, no que coincida con el backend.
 *
 * Lo que cierra esa brecha son los tests de `backend/GestionGastos.Api.Tests/Contrato/`, que LEEN
 * este archivo y lo comparan contra el JSON que la API emite de verdad, en las dos direcciones
 * (research.md D-09). Es la única excepción de estructura del proyecto, declarada en `AGENTS.md`:
 * lectura en una sola dirección, el frontend no lee nada del backend.
 *
 * Si cambiás algo acá, el contrato cambió. Esa es la idea.
 */

/**
 * Las dos mitades del dominio. Viaja como cadena y no como número: el `tinyint` de la base
 * obligaría a esta capa a conocer el mapeo del esquema.
 */
export type TipoMovimiento = 'gasto' | 'ingreso';

/** Una categoría del catálogo que alimenta el selector del formulario (FR-006). */
export interface Categoria {
  id: number;
  nombre: string;
  tipo: TipoMovimiento;
}

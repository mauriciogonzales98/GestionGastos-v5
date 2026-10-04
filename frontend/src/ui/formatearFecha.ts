/** Lo que la API manda: `yyyy-MM-dd`, la fecha de un movimiento sin hora ni zona. */
const ISO_SIN_HORA = /^(\d{4})-(\d{2})-(\d{2})$/;

/**
 * La fecha de un movimiento como se lee en Argentina: `dd/MM/yyyy`.
 *
 * **Parte la cadena y no construye un `Date`**, que es la decisión de todo esto. `new Date('2026-10-01')`
 * se interpreta como medianoche UTC, y en cualquier zona al oeste de Greenwich —la de acá entre
 * ellas— `toLocaleDateString` devuelve el día anterior. Un movimiento cargado el 1 se mostraría como
 * del 30, en silencio y sólo para algunas fechas. La API manda un día calendario, no un instante:
 * reordenar sus tres partes es la operación que corresponde, y además no puede fallar.
 *
 * Lo que no reconoce vuelve tal como llegó: es información suficiente y nunca peor que no mostrar
 * nada. Es la misma degradación que `formatearMonto` hace con un código ilegible.
 */
export function formatearFecha(fecha: string): string {
  const partes = ISO_SIN_HORA.exec(fecha);
  if (partes === null) return fecha;

  const [, anio, mes, dia] = partes;
  return `${dia}/${mes}/${anio}`;
}

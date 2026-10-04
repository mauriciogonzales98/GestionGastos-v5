import { describe, expect, it } from 'vitest';
import { formatearFecha } from '../src/ui/formatearFecha';

describe('formatearFecha', () => {
  it('da vuelta el `yyyy-MM-dd` de la API a `dd/MM/yyyy`', () => {
    expect(formatearFecha('2026-10-04')).toBe('04/10/2026');
  });

  it('no corre el día un lugar para atrás', () => {
    // El caso que motivó no usar `Date`: `new Date('2026-10-01')` es medianoche UTC, y en la zona
    // de acá `toLocaleDateString` devolvería el 30 de septiembre. Un movimiento cargado el primero
    // del mes se mostraría como del mes anterior, y sólo para algunas fechas.
    expect(formatearFecha('2026-10-01')).toBe('01/10/2026');
    expect(formatearFecha('2026-01-01')).toBe('01/01/2026');
  });

  it('devuelve tal cual lo que no sea una fecha sin hora', () => {
    // La degradación: información suficiente antes que nada. No hay nada que la persona pueda hacer
    // con un formato inesperado, y esconder el dato es peor que mostrarlo crudo.
    expect(formatearFecha('')).toBe('');
    expect(formatearFecha('2026-10-04T12:00:00Z')).toBe('2026-10-04T12:00:00Z');
    expect(formatearFecha('ayer')).toBe('ayer');
  });
});

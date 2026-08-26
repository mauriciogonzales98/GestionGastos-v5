# Plan de implementación — copia consolidada

Copia de solo lectura, tomada el 2026-08-22, de todo lo que hace falta para construir este proyecto
**desde cero**: el PRD del producto, el plan con el orden y sus razones, y el PRD de cada ticket —
tanto los que ya están en `main` como los que faltan. Las fuentes vivas siguen siendo
`docs/daw/prd/` y `docs/daw/discovery/`; esta carpeta es una foto para leer de corrido.

## Contenido

| Archivo | Qué es | Origen |
|---------|--------|--------|
| `PRD.md` | PRD de referencia del producto, con todas las modificaciones aplicadas hasta hoy | `docs/daw/prd/PRD.md` |
| `plan-DISC-001.md` | El plan: concepto, decisiones, mapa de dependencias y orden de los tickets | `docs/daw/discovery/concept-DISC-001.md` |
| `prds/implementados/` | Los 10 tickets ya mergeados a `main`, en el orden en que se hicieron | `docs/daw/prd/` |
| `prds/pendientes/` | Los 7 tickets que faltan | `docs/daw/prd/prd-DISC-001-*.md` |

## Recorrido completo, en orden

### Ya implementado

| Orden | Ticket | Título | PRD |
|-------|--------|--------|-----|
| 1 | FEAT-001a | Alta de movimientos y listado simple | `prds/implementados/prd-FEAT-001a.md` |
| 2 | FEAT-001b | Filtros del listado, edición y eliminación | `prds/implementados/prd-FEAT-001b.md` |
| 3 | FEAT-001c | Resumen del mes con desglose por categoría | `prds/implementados/prd-FEAT-001c.md` |
| 4 | FIX-001 | Linter del backend .NET (D-1) | `prds/implementados/prd-FIX-001.md` |
| 5 | FIX-002 | Bit de ejecución de `verificar-linter.sh` (QUICK-FIX) | `prds/implementados/fix-FIX-002.md` |
| 6 | FIX-003 | Comparación por subcadena en `validate_prd.py` (D-4, QUICK-FIX) | `prds/implementados/fix-FIX-003.md` |
| 7 | FEAT-003 | Alineación verificada del contrato frontend↔backend (D-2) | `prds/implementados/prd-FEAT-003.md` |
| 8 | FIX-004 | El sembrado de rendimiento vence el 2027-01-01 (D-3) | `prds/implementados/prd-FIX-004.md` |
| 9 | DISC-001-01a | Identidad y sesión | `prds/implementados/prd-DISC-001-01a.md` |
| 10 | DISC-001-01b | Límite de intentos fallidos | `prds/implementados/prd-DISC-001-01b.md` |

`prds/implementados/prd-FEAT-001.md` es el PRD padre de FEAT-001: no es un ticket implementable,
es el índice del corte en `a`/`b`/`c` y explica por qué se partió. Los cuatro FIX/FEAT de
infraestructura (D-1 a D-4) salieron intercalados porque el usuario decidió cerrar la deuda de
infraestructura antes de seguir con features de producto; el mapa de `plan-DISC-001.md` los
muestra como prerequisitos de todo lo demás.

Empezando de cero, el orden 4→8 puede ir antes del 1: son barreras de calidad (linter, contrato,
fixture, validador) que cuanto antes estén, menos trabajo tapan.

### Pendiente

| Orden | # | Título | PRD |
|-------|---|--------|-----|
| 11 | 1c | Aislamiento entre cuentas verificado | `prds/pendientes/prd-DISC-001-01c.md` |
| 12 | 3 | Categorías propias del usuario | `prds/pendientes/prd-DISC-001-03.md` |
| 13 | 4a | Catálogo de monedas y totales por moneda | `prds/pendientes/prd-DISC-001-04a.md` |
| 14 | 4b | Registrar y filtrar en varias monedas | `prds/pendientes/prd-DISC-001-04b.md` |
| 15 | 5 | Dashboard con gráficos | `prds/pendientes/prd-DISC-001-05.md` |
| 16 | 6 | Maquetación y accesibilidad | `prds/pendientes/prd-DISC-001-06.md` |
| — | 2 | Nota descriptiva del movimiento | `prds/pendientes/prd-DISC-001-02.md` |

`2` no tiene dependencias ni bloquea a nadie: entra en cualquier hueco. El resto es secuencial por
las razones que están en `plan-DISC-001.md`. Los siete están en estado `validated`.

> **Actualizado el 2026-08-26.** `1a` se mergeó en el PR #3 y `1b` sale con este ticket; los dos
> pasaron a la tabla de arriba. La foto original es del 2026-08-22, cuando los nueve estaban
> pendientes.

## Lo que no está acá

Los informes de validación (`*.validation.md`) que acompañan a cada PRD, las specs y fix-plans, los
RCA, los ADR, los modelos de amenazas y los informes de verificación y SAST. Siguen en `docs/daw/`
y en `docs/adr/`. Tampoco hay nada de FEAT-002: ese ticket se abandonó sin llegar a tener PRD.

# Plan de implementación — copia consolidada

Copia de solo lectura de lo que hace falta para construir este proyecto **desde cero**: el PRD del
producto, el plan con el orden y sus razones, y los PRD de los tickets.

> **Es una copia parcial, y conviene saberlo antes de leerla.** La carpeta llegó con el commit de
> arranque de v5 (`3a863ff`) trayendo el PRD del producto, el plan y **dos** PRD de ticket. Los
> demás PRD que este README nombraba —los de FEAT-001a/b/c, FIX-001 a FIX-004 y FEAT-003— nunca
> entraron a este repositorio, y la carpeta `docs/daw/` que figuraba como fuente viva tampoco
> existe acá. La columna *PRD* de las tablas dice, para cada ticket, si su archivo está o no.
>
> El estado de cada ticket de las tablas de abajo está **verificado contra el código**, no contra
> lo que decía la foto original.

## Contenido

| Archivo | Qué es |
|---------|--------|
| `PRD.md` | PRD de referencia del producto, con todas las modificaciones aplicadas |
| `plan-DISC-001.md` | El plan: concepto, decisiones, mapa de dependencias y orden de los tickets |
| `prds/implementados/` | Los PRD de tickets ya mergeados a `main` que están en este repositorio |
| `prds/pendientes/` | Los PRD de los tickets que faltan |

## Recorrido completo, en orden

### Ya implementado

Verificado contra el código el 2026-08-31.

| Orden | Ticket | Título | Qué lo demuestra en el código | PRD |
|-------|--------|--------|-------------------------------|-----|
| 1 | FEAT-001a | Alta de movimientos y listado simple | `POST` y `GET /api/movimientos` | no está en el repo |
| 2 | FIX-001 | Linter del backend .NET (D-1) | `backend/.editorconfig`, `verificar-linter.sh` | no está en el repo |
| 3 | FIX-002 | Bit de ejecución de `verificar-linter.sh` | los cuatro scripts en `100755` | no está en el repo |
| 4 | FEAT-003 | Alineación verificada del contrato (D-2) | `verificar-contrato.sh`, `Contrato/` | no está en el repo |
| 5 | FIX-004 | El sembrado de rendimiento vence el 2027-01-01 (D-3) | `Rendimiento/SembradoDeRendimientoTests.cs` | no está en el repo |
| 6 | DISC-001-01a | Identidad y sesión | `Sesion/`, `Cuentas/` | `prds/implementados/prd-DISC-001-01a.md` |
| 7 | DISC-001-01b | Límite de intentos fallidos | `specs/003-limite-intentos/` | `prds/implementados/prd-DISC-001-01b.md` |
| 8 | DISC-001-01c | Aislamiento entre cuentas verificado | `specs/004-aislamiento-cuentas/`, `verificar-aislamiento.sh` | `prds/pendientes/prd-DISC-001-01c.md` |

Los FIX/FEAT de infraestructura (D-1 a D-4) salieron intercalados porque el usuario decidió cerrar
la deuda de infraestructura antes de seguir con features de producto; el mapa de
`plan-DISC-001.md` los muestra como prerequisitos de todo lo demás. Empezando de cero, el orden
2→5 puede ir antes del 1: son barreras de calidad que cuanto antes estén, menos trabajo tapan.

### Pendiente

| Orden | # | Título | PRD |
|-------|---|--------|-----|
| 9 | FEAT-001b | Filtros del listado, edición y eliminación | no está en el repo |
| 10 | FEAT-001c | Resumen del mes con desglose por categoría | no está en el repo |
| 11 | 3 | Categorías propias del usuario | `prds/pendientes/prd-DISC-001-03.md` |
| 12 | 4a | Catálogo de monedas y totales por moneda | `prds/pendientes/prd-DISC-001-04a.md` |
| 13 | 4b | Registrar y filtrar en varias monedas | `prds/pendientes/prd-DISC-001-04b.md` |
| 14 | 5 | Dashboard con gráficos | `prds/pendientes/prd-DISC-001-05.md` |
| 15 | 6 | Maquetación y accesibilidad | `prds/pendientes/prd-DISC-001-06.md` |
| — | 2 | Nota descriptiva del movimiento | `prds/pendientes/prd-DISC-001-02.md` |

`2` no tiene dependencias ni bloquea a nadie: entra en cualquier hueco. El resto es secuencial por
las razones que están en `plan-DISC-001.md`.

**FEAT-001b y FEAT-001c son los que más destraban.** Entre los dos aportan los cuatro endpoints que
faltan —`GET`, `PUT` y `DELETE /api/movimientos/{id}`, y `GET /api/resumen`—, y de ellos dependen
los cinco criterios de aislamiento que la feature 004 no pudo verificar y dejó anotados en la tabla
de *Deuda registrada* de `specs/004-aislamiento-cuentas/spec.md`: AC-02, AC-03, AC-04, AC-05 y
AC-07. La barrera de aislamiento ya está en pie, así que esos endpoints nacen vigilados.

## Lo que no está acá

Los informes de validación (`*.validation.md`), los RCA, los modelos de amenazas y los informes de
verificación y SAST: nunca entraron a este repositorio. Los ADR sí están, en `docs/adr/`. Las specs
y sus planes viven en `specs/`. Tampoco hay nada de FEAT-002: ese ticket se abandonó sin llegar a
tener PRD.

FIX-003 —"Comparación por subcadena en `validate_prd.py`"— figuraba como implementado en la foto
original y no aplica acá: `validate_prd.py` no existe en este repositorio.

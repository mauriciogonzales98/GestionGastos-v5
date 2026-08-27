# Specification Quality Checklist: Aislamiento entre cuentas verificado

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-27
**Feature**: [spec.md](../spec.md)

## Content Quality

- [X] No implementation details (languages, frameworks, APIs)
- [X] Focused on user value and business needs
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic (no implementation details)
- [X] All acceptance scenarios are defined
- [X] Edge cases are identified
- [X] Scope is clearly bounded
- [X] Dependencies and assumptions identified

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes defined in Success Criteria
- [X] No implementation details leak into specification

## Notes

Dos desvíos respecto del PRD, los dos resueltos con el usuario antes de escribir la spec y
documentados en la sección *Dos correcciones al PRD que este repositorio impone*:

1. **La superficie son 2 endpoints, no 6.** Verificado contra el código: sólo existen
   `POST /api/movimientos` y `GET /api/movimientos`. Los cuatro que faltan son FEAT-001b y
   FEAT-001c, que este repositorio nunca implementó pese a que
   `plan-de-implementacion/README.md` los lista como mergeados. Los AC que dependen de ellos no se
   descartan: quedan en *Deuda registrada* con su ticket.
2. **No hay filtro global de consulta.** Verificado: ningún `HasQueryFilter` en el backend. AC-10
   del PRD se reformula como FR-004, conservando su intención —que desarmar el aislamiento haga
   ruido— sobre el mecanismo que este repositorio sí usa.

Ambos desvíos hacen que la spec sea más chica que el PRD y **explícita** sobre lo que no cubre. Un
`/speckit-analyze` que compare esta spec contra el PRD va a encontrar esos huecos: son deliberados y
están en la tabla de *Deuda registrada*, no son omisiones.

Punto de atención para `/speckit-plan`: FR-004 pide una barrera nueva, y el Principio V de la
constitución exige que toda barrera pruebe que sabe ponerse en rojo. El plan tiene que decidir su
forma —un script como `verificar-contrato.sh` y `verificar-autorizacion.sh`, o un test que inspeccione
la consulta como ya hace `MovimientosConsulta`— y esa decisión cambia el costo del ticket.

# Specification Quality Checklist: Filtros del listado, edición y eliminación

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-31
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
- [ ] No implementation details leak into specification

## Notes

**El único `[NEEDS CLARIFICATION]` se resolvió con el usuario antes de cerrar la spec.** Era de
alcance: RF-14 nombra cuatro campos editables y uno de ellos —la moneda— no se puede elegir hoy ni
siquiera al registrar, así que permitir cambiarla habría arrastrado el catálogo de monedas del
ticket 4a. Se decidió dejarla fuera: **no se puede editar lo que no se puede elegir**. Está en
*Assumptions* con su motivo y en *Deuda registrada* apuntando a 4a/4b.

**Dos ítems marcados con criterio, no por descuido:**

*"No implementation details"* queda sin tildar porque la spec nombra endpoints concretos
(`GET /api/resumen`), el "canal único de lectura" y la barrera de aislamiento. Es deliberado: el
alcance de esta feature está definido **por** la superficie que agrega y por la deuda que 004 dejó
atada a endpoints con nombre y apellido. Escribirla sin nombrarlos la volvería imposible de
verificar contra lo que realmente existe. La alternativa —decir "consultar un movimiento
individual"— no era más honesta, era más vaga.

*Reconstrucción del alcance.* El PRD de este ticket no existe en el repositorio, así que la spec se
armó desde `PRD.md` y desde la *Deuda registrada* de 004. La sección *De dónde sale esta spec* deja
constancia. Un `/speckit-analyze` que compare contra un PRD de ticket no va a encontrar nada que
comparar: no es una omisión.

**Para `/speckit-plan`:** los criterios AC-05, AC-06 y AC-09 piden respuestas *indistinguibles* de
las de un recurso inexistente. Eso es una condición sobre lo que **no** se puede observar, y esa
clase de criterio se verifica distinto que un valor esperado: hay que comparar dos respuestas entre
sí, no cada una contra una constante. El plan tiene que decidir cómo, porque cambia la forma de los
tests.

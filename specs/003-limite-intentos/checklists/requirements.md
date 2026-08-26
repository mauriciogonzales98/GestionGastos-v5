# Specification Quality Checklist: Límite de intentos fallidos de inicio de sesión

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-26
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

- **Cobertura AC → escenario**: los 13 AC del PRD están citados. US1: AC-01, AC-02, AC-03, AC-04,
  AC-07, AC-10, AC-11, AC-12. US2: AC-05, AC-06. US3: AC-08, AC-09, AC-13. Ninguno quedó suelto y
  ninguno se citó dos veces.
- **Cobertura FR → AC**: FR-001 (AC-01, AC-04, AC-07, AC-09), FR-002 (AC-01, AC-02, AC-03),
  FR-003 (AC-05), FR-004 (AC-06), FR-005 (AC-08, AC-09), FR-006 (AC-09, AC-10), FR-007 (AC-11),
  FR-008 (AC-12), FR-009 (AC-13).
- **Sobre "no implementation details"**: la spec nombra el reloj que los tests controlan y la
  convención de nombre `Rendimiento` del filtro del CI. Son restricciones de **cómo se verifica**,
  impuestas por el Principio IV de la constitución y por el PRD; se dejan porque sin ellas AC-03,
  AC-06 y AC-11 se escribirían como tests que duermen 15 minutos. No fijan diseño ni tecnología de
  la solución.
- **Decisiones diferidas al plan**, declaradas como tales en *Assumptions* y no como huecos: dónde
  vive el estado del contador y con qué esquema (el PRD pide ADR), y el criterio de purga de los
  registros vencidos.
- Sin `[NEEDS CLARIFICATION]`: el PRD ya está validado y las tres ambigüedades reales —ventana fija
  o deslizante, estado del contador al vencer, y forma de la clave del email— se resolvieron como
  supuestos explícitos, cada uno con su motivo escrito.

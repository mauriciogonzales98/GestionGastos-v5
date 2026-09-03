# Specification Quality Checklist: Monedas administrables como dato

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-03
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

### La sección de reconciliación nombra código a propósito

*De dónde sale esta spec* cita nombres de archivos, tablas y migraciones (`Dominio/Moneda.cs`,
`MovimientosConsulta.Agrupado`, `UnicaMonedaPredeterminada`). Es una excepción deliberada al
"sin detalles de implementación", y sin ella la sección no cumple su función: su trabajo es
demostrar que un requisito **ya está construido**, y eso no se demuestra sin señalar dónde.

Los requisitos, los criterios de aceptación y los Success Criteria sí están escritos sin
implementación: se pueden verificar sin saber que hay MySQL o C# del otro lado.

### Cero [NEEDS CLARIFICATION], y por qué

La única ambigüedad real de este ticket —AC-07/AC-08 del PRD contra AC-31 de la feature 006— se
resolvió **antes** de escribir la spec, con el usuario, porque no era una ambigüedad de redacción
sino una contradicción entre dos criterios ya construidos. Quedó documentada en
*De dónde sale esta spec*, fijada en FR-009 y registrada como D8-04.

### El alcance se redujo respecto del PRD, y está justificado

Cinco de los siete FR del PRD ya están construidos y dos no aplican. La spec lo documenta requisito
por requisito con su evidencia, en vez de repetirlos como trabajo pendiente. FR-04 del PRD se
difiere al ticket 4b con su motivo (D8-01).

### Un riesgo que el propio PRD nombra y esta spec hereda

*"Este ticket no cambia nada visible, y eso lo vuelve fácil de saltear o de dar por hecho."* Con el
alcance ya reducido a dos verificaciones, el riesgo es mayor, no menor. FR-001 y FR-011 son las que
lo contienen: las dos exigen **ejecutar** algo, no leer código.

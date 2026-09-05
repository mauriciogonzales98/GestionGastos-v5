# Specification Quality Checklist: Elegir y filtrar la moneda de un movimiento

**Purpose**: Validar que la spec esté completa y sea de calidad antes de pasar a la planificación
**Created**: 2026-09-04
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] Sin detalles de implementación (lenguajes, frameworks, APIs)
- [x] Centrada en el valor para el usuario y la necesidad de negocio
- [x] Escrita para quien no es desarrollador
- [x] Todas las secciones obligatorias completas

## Requirement Completeness

- [x] No quedan marcadores [NEEDS CLARIFICATION]
- [x] Los requisitos son verificables y sin ambigüedad
- [x] Los criterios de éxito son medibles
- [x] Los criterios de éxito son agnósticos de la tecnología
- [x] Todos los escenarios de aceptación están definidos
- [x] Los casos borde están identificados
- [x] El alcance está acotado
- [x] Dependencias y supuestos identificados

## Feature Readiness

- [x] Todo requisito funcional tiene su criterio de aceptación
- [x] Las historias cubren los flujos principales
- [x] La feature cumple los resultados medibles de Success Criteria
- [x] No se filtran detalles de implementación a la spec

## Notes

**Los tres marcadores [NEEDS CLARIFICATION] quedaron resueltos** en la sesión de aclaración del
2026-09-04, registrada en la sección *Clarifications* de la spec: el alcance del frontend (edición
en ventana emergente sí, barra de filtros de categoría y fecha no), la vista de totales (fuera, es
el ticket 5) y cómo se indica la moneda en cada fila (código ISO explícito). La validación cierra.

Sobre la nota de *sin detalles de implementación*: las tablas de *De dónde sale esta spec* citan
archivos y símbolos del código. Es deliberado y sigue lo que hizo la feature 008 — sin esa
reconciliación no se puede saber qué falta construir de lo que el PRD pide, y la 008 demostró que
adivinarlo lleva a rehacer lo hecho. Las secciones normativas (*Requirements*, *Success Criteria*)
no citan nada de eso.

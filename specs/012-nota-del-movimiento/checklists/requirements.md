# Specification Quality Checklist: Nota descriptiva del movimiento

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-10
**Feature**: [spec.md](../spec.md)

## Content Quality

- [ ] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [ ] Written for non-technical stakeholders
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

## Trazabilidad al PRD

El PRD del ticket es la fuente y sus identificadores no se reinterpretan. Esta tabla es la que
permite comprobarlo de un vistazo:

| PRD | Dónde quedó en la spec |
|---|---|
| `FR-01` (nota opcional de hasta 120 al registrar) | `FR-001`, `FR-002`; US1 escenarios 1, 3, 4 |
| `FR-02` (se muestra en el listado) | `FR-006`; US1 escenario 1 |
| `FR-03` (rechazar > 120 sin crear ni alterar) | `FR-003`; US1 escenario 5, US2 escenario 4 |
| `FR-04` (modificar y vaciar) | `FR-004`; US2 escenarios 1, 2, 3 |
| `FR-05` (vacía = sin nota, sin relleno ni error) | `FR-005`; US1 escenario 2 |
| `NFR-01` (texto plano, nada se ejecuta) | `NFR-001`; US1 escenario 6, `SC-003` |
| `NFR-02` (los totales no se mueven) | `NFR-002`; US2 escenario 5, `SC-005` |
| `NFR-03` (listado < 2 s p95 con 1000) | `NFR-003`; `SC-006` |
| `AC-01` a `AC-10` | US1 escenarios 1–6, US2 escenarios 1–5 |
| *Out of Scope* completo | `FR-007`, D12-04, D12-05, D12-06 |

Y lo que salió de `/speckit-clarify`, que no viene del PRD sino de decisiones que el PRD no tomaba:

| Decisión | Dónde quedó |
|---|---|
| El límite se cuenta en caracteres Unicode | `FR-003`, `FR-013`; Edge Case del emoji |
| El almacenamiento admite dos formas de "sin nota" | `FR-005`, D12-08 |
| La lectura de la API devuelve una sola | `FR-009`, `FR-011`; `SC-011` |
| La nota se escribe en un control de varias líneas | `FR-001`, `FR-012`, `FR-013` |

## Notas de la validación

Tres puntos no tenían un valor por omisión razonable y se resolvieron en *Clarifications* antes de
cerrar la checklist, no después:

1. **D11-02** (el `CHECK` de tres letras sobre `moneda.codigo`). Es la única decisión de **alcance**
   de la feature: el PRD no la menciona, y entra sólo porque esta feature abre la migración que la
   deuda venía esperando desde la 009. Quedó como `FR-010` y US3, aislada en su propia historia y en
   la prioridad más baja, justamente para que recortarla no toque nada del producto.
2. **Cómo se muestra la nota en el listado.** La tabla ya tiene seis columnas y un presupuesto de
   360 px; una séptima con texto libre no es una decisión neutra. Quedó como `FR-006` y `NFR-004`.
3. **Cómo se vacía la nota al editar** sin romper la regla "ausente nunca produce un cambio que nadie
   pidió" que el contrato de la modificación declara desde la feature 009. Quedó como `FR-004`, con
   la nota obligatoria en la modificación por la misma razón por la que la fecha ya lo es.

### Los dos ítems que dejaron de pasar

`/speckit-clarify` resolvió cuatro decisiones más, y al encodearlas la spec bajó a un nivel de
detalle que dos ítems de esta checklist no admiten: **"sin detalles de implementación"** y
**"escrita para stakeholders no técnicos"**. Hablar de la unidad en que se cuentan los caracteres, de
qué se guarda cuando no hay nota y de qué devuelve la API en ese caso **es** hablar de
implementación.

Queda desmarcado a propósito, no arreglado, por tres razones: las cuatro decisiones eran ambigüedades
reales que había que cerrar antes de planificar; son el estilo que las specs 006 a 011 de este
proyecto ya tienen, que ancla cada decisión al código con su motivo; y el costo de moverlas a
`plan.md` sería perder el *por qué* junto al requisito. **La consecuencia práctica para
`/speckit-plan`**: estas cuatro ya están decididas, así que `data-model.md` y `contracts/` las
**trasladan**, no las vuelven a decidir.

**Un punto de vigilancia para `/speckit-plan`**: la tentación de esta feature no es agregar de menos
sino de más. La barra de filtros existe desde la 011 y sumarle un campo de texto para buscar por nota
costaría muy poco — y es exactamente lo que `FR-007` y D12-04 prohíben. El plan tiene que dejar eso
verificado, no sólo escrito.

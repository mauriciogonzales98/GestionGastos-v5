# Implementation Plan: Aislamiento entre cuentas verificado

**Branch**: `004-aislamiento-cuentas` | **Date**: 2026-08-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-aislamiento-cuentas/spec.md`

## Summary

Convertir el aislamiento entre cuentas de una propiedad sostenida por convención en una propiedad
verificada, y ponerle una barrera para que dejar de cumplirla haga ruido.

No hay código de producción nuevo que agregue comportamiento: los dos endpoints de movimientos ya
acotan por cuenta. Lo que falta es que **alguien lo haya comprobado con dos cuentas reales**, y que
el día que se desarme se note. El entregable es, entonces: tests cruzados sobre los dos endpoints
que existen, una barrera que vigila el canal de lectura, y el script que le prueba a esa barrera que
sabe ponerse en rojo.

El único cambio previsible en `backend/GestionGastos.Api/` es de forma, no de conducta: consolidar
que toda lectura de movimientos pase por `MovimientosConsulta`, que hoy ya se cumple sin estar
declarado ([D-04](./research.md)).

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.301) en backend. Esta feature **no toca** el frontend.

**Primary Dependencies**: ninguna nueva. Entity Framework Core 9.0.18 + Pomelo.MySQL 9.0.0 y xUnit,
todos ya presentes.

**Storage**: MySQL 8.4.10, schema `gestiongastos_test` para la suite. Sin cambios de esquema: no hay
migración en esta feature.

**Testing**: xUnit. `FactoriaConReloj`, `RelojFijo`, `BaseDeDatosFixture` y `CuentaDePrueba` ya
existen y se reusan sin modificarlos.

**Target Platform**: API web sobre Linux.

**Project Type**: web app con `backend/` y `frontend/` separados (`AGENTS.md`).

**Performance Goals**: N/A. Esta feature no agrega trabajo al camino de la petición.

**Constraints**: no se agregan endpoints, no cambia el contrato con el frontend, no hay migración.
Si algún escenario obligara a cambiar una respuesta, deja de ser esta feature.

**Scale/Scope**: 2 endpoints de movimientos; 5 criterios del PRD diferidos por falta de endpoint.

## Constitution Check

*GATE: revisado antes de la Fase 0 y de nuevo después de la Fase 1.*

| Principio | Cómo lo cumple este plan |
|---|---|
| **I. Test-First** | Es una feature **de tests**: no hay ninguna tarea de código de producción que agregue conducta. Las dos únicas tareas que tocan `GestionGastos.Api/` son de forma (D-04) y van después del test que las exige. La barrera se escribe primero, se la ve en rojo con el acotado quitado, y recién después se declara la regla. |
| **II. Cada AC tiene su test que lo nombra** | Cada escenario de la spec cita su AC del PRD (`AC-01`, `AC-06`, `AC-08`, `AC-10`) en el nombre del test o en su documentación. Los cinco AC que no se pueden verificar están en la tabla de *Deuda registrada* de la spec: no se dan por cumplidos. |
| **III. VERIFY es una fase con puerta** | Cada historia cierra con su `[VERIFY]`: `dotnet format --verify-no-changes`, `dotnet build -warnaserror`, `dotnet test`. El cierre de la feature agrega cobertura y las **cuatro** barreras — las tres existentes más la nueva. |
| **IV. Tests deterministas y aislados** | Reloj clavado con `FactoriaConReloj` ([D-07](./research.md)); cuentas creadas por la API con email único por `Guid`; limpieza entre escenarios con `LimpiarCuentasAsync`. Ningún test duerme, ninguno depende del día de hoy ni del orden. |
| **V. Las barreras se verifican a sí mismas** | Es literalmente FR-004. `verificar-aislamiento.sh` desarma el aislamiento de las tres formas posibles, exige el rojo en cada una, restaura y exige el verde — el mismo patrón de `verificar-autorizacion.sh` ([D-03](./research.md)). |

**Resultado del gate: PASA.** Sin violaciones, así que *Complexity Tracking* queda vacío y se
elimina.

Una tensión que conviene declarar aunque no sea una violación: el Principio I pide ver un rojo antes
de escribir código, y acá **casi todos los tests van a nacer en verde**, porque el comportamiento
que verifican ya existe. Eso no es una excepción al principio: es lo que significa verificar algo
heredado. La forma de respetar el principio en esta feature es la del script de la barrera —romper a
propósito lo que el test protege y ver el rojo— y por eso cada historia lleva esa comprobación en
lugar del rojo espontáneo. Está desarrollado en la tabla de abajo.

### Cómo se consigue un rojo real en una feature que verifica lo que ya funciona

| Test | Cómo se le ve el rojo antes de darlo por bueno |
|---|---|
| Cruzados del listado (US1) | Se quita `m.UsuarioId == usuarioId` de `MovimientosConsulta`: el listado devuelve los movimientos de la otra cuenta y el test cae |
| Cruzados del alta (US2) | Se reemplaza `UsuarioId = usuarioActual.Id` por el id de la otra cuenta: el movimiento aparece en el listado ajeno y el test cae |
| Barrera del SQL (US3) | Mismo desarme que el primero: el SQL deja de nombrar `usuario_id` |
| Barrera del canal (US3) | Se agrega un uso directo de `contexto.Movimientos` fuera de `MovimientosConsulta` |

`verificar-aislamiento.sh` termina automatizando **los cuatro**: corre la suite de aislamiento
entera en cada desarme, así que el de la consulta hace caer también a los cruzados del listado, y el
de la escritura a los del alta.

Las tareas `[ROJO]` de US1 y US2 no sobran por eso: se ejecutan **antes** de que el script exista
—US3 va última— y son las que deciden si esos tests sirven, en el momento en que se escriben. El
script después convierte esa comprobación de una vez en una de siempre.

Que el script desarme la escritura no contradice [D-05](./research.md): lo que esa decisión descarta
es una comprobación en el código de producción. Que el script la desarme es otra cosa, y responde a
que sin ese paso "los cruzados de US2 detectan el desarme" se comprobaría una sola vez en la vida.

## Project Structure

### Documentation (this feature)

```text
specs/004-aislamiento-cuentas/
├── plan.md              # Este archivo
├── research.md          # Fase 0 — D-01..D-08 y riesgos
├── data-model.md        # Fase 1 — sin cambios de esquema; las invariantes que se verifican
├── quickstart.md        # Fase 1 — cómo comprobar el aislamiento a mano y correr la barrera
├── contracts/
│   └── api-http.md      # Fase 1 — el contrato NO cambia, y eso es parte del entregable
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec
└── tasks.md             # Fase 2 — lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/
│   └── Movimientos/
│       ├── MovimientosConsulta.cs      # canal único de lectura; se consolida (D-04)
│       └── MovimientosEndpoints.cs     # sin cambios de conducta
├── GestionGastos.Api.Tests/
│   ├── Integracion/
│   │   ├── AislamientoEntreCuentasTests.cs   # NUEVO — los cruzados de US1 y US2
│   │   └── BarreraDeAislamientoTests.cs      # NUEVO — el SQL y el canal (US3)
│   └── Integracion/CuentaDePrueba.cs         # se reusa sin tocar
└── verificar-aislamiento.sh                  # NUEVO — le prueba el rojo a la barrera

frontend/                                      # NO SE TOCA
```

**Structure Decision**: se respeta la separación de `AGENTS.md` — el backend en `backend/`, y el
frontend intacto. El script nuevo va junto a los otros tres, en `backend/`, porque comparte su forma
y su lugar en la puerta de cierre. No se crea ninguna carpeta nueva: los dos archivos de test entran
en `Integracion/`, que es donde vive todo lo que levanta la aplicación de verdad.

## Complejidad y alcance

Esta feature es más chica que su PRD, y el motivo está en [D-01](./research.md) y en la sección
*Deuda registrada* de la spec: cuatro de los seis endpoints que el PRD nombra no existen en este
repositorio. No se implementan acá.

No hay violaciones de la constitución que justificar, así que no hay tabla de *Complexity Tracking*.

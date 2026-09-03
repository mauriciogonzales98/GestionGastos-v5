# Implementation Plan: Monedas administrables como dato

**Branch**: `008-monedas-como-dato` | **Date**: 2026-09-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-monedas-como-dato/spec.md`

## Summary

**Esta feature no agrega comportamiento: convierte dos creencias en hechos verificados.**

El catálogo de monedas y la separación de totales por moneda ya están construidos —los trajeron
FEAT-001a y FEAT-001c— y la spec lo documenta requisito por requisito. Lo que falta es probar dos
cosas que hoy se creen porque el código está escrito así:

1. **Que sumar una moneda sea de verdad sólo un dato** (FR-001, FR-002, FR-003, AC-01, AC-02,
   AC-03). Se prueba con un script que compila una vez, agrega la moneda con SQL puro, corre los
   tests con `--no-build` y exige que el hash del ensamblado y el del árbol de fuentes no hayan
   cambiado. Un test solo no puede sostener una afirmación sobre el proceso ([D-01](./research.md)).
2. **Que la separación por moneda aguante volumen** (FR-011, AC-04), y que la respuesta siga
   trayendo los totales ya agregados y no los movimientos (FR-007, AC-10). Se prueba con un caso nuevo de
   rendimiento que reparte 1000 movimientos en dos monedas, junto al que ya mide con una — dos
   números que se comparan valen más que uno que hay que interpretar ([D-03](./research.md)).

**El trabajo es aditivo.** Ningún archivo de `backend/GestionGastos.Api/` se modifica, y eso es un
criterio de revisión, no una casualidad: si aparece uno en el diff, algo se planteó mal
([D-04](./research.md)).

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.301) en el backend. Sin trabajo de frontend.

**Primary Dependencies**: Entity Framework Core 9.0.18 + Pomelo.MySQL 9.0.0. **Ninguna nueva.**

**Storage**: MySQL 8.4.10, schema `gestiongastos`; los tests contra `gestiongastos_test`. **Sin
migración**: esta feature no toca el esquema.

**Testing**: xUnit. Un archivo de integración nuevo, un caso agregado a los de rendimiento, y un
script de verificación en bash.

**Target Platform**: API sobre Linux; el script corre en bash con el cliente `mysql` disponible.

**Project Type**: web (backend + frontend separados). **Esta feature toca sólo el backend, y dentro
del backend sólo el proyecto de tests más un script.**

**Performance Goals**: p95 < 2 s sobre 100 ejecuciones con 1000 movimientos repartidos en dos
monedas (FR-011, RNF-01).

**Constraints**:
- El resumen **no se modifica** (FR-009, [D-04](./research.md)).
- El script tiene que dejar el catálogo como lo encontró: lo altera para verificar y una moneda de
  más se lleva puesta la corrida siguiente.
- Los tests de rendimiento no corren en CI (`FullyQualifiedName!~Rendimiento`).

**Scale/Scope**: **2 archivos nuevos** —`MonedaComoDatoTests.cs` y `verificar-monedas.sh`—, 1
archivo de tests modificado (`RendimientoResumenTests.cs`) y 2 documentos actualizados (`AGENTS.md`,
`.github/workflows/ci.yml`). **Cero archivos de producción.**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Cómo lo cumple este plan |
|---|---|
| **I · Test-First (no negociable)** | Cada verificación se escribe primero y **se la ve fallar por su razón**. Para el test de integración: se corre contra un catálogo sin la moneda nueva y falla porque no está. Para el script: se corre con una versión que recompila a propósito y se exige que lo detecte — un script de verificación que nunca falló no verifica nada (Principio V aplicado a sí mismo). Para el caso de rendimiento: el guardarraíl de filas sembradas se ve fallar con el sembrado vacío. |
| **II · Cada AC tiene su test, y el test lo nombra** | Los **diez** AC de la spec quedan cubiertos y citados, de dos formas distintas que conviene no confundir. **Con test propio**: AC-01/02/03 (`MonedaComoDatoTests` y el script), AC-04 (`RendimientoResumenTests`), AC-05/06/10 y AC-08/09 (`MonedaComoDatoTests`). **Por cita, sin duplicar**: sólo AC-07, que es la regresión de que con una moneda nada cambió y que los tests del resumen de la feature 006 ya sostienen — T025 anota cuáles son. AC-08 y AC-09 **sí** llevan test propio aunque hablen de comportamiento existente: son la mitad de `006:AC-31` que esta feature decidió conservar contra el PRD (FR-009), y una decisión que se toma contra un documento necesita su propio test, no una cita. |
| **III · VERIFY es una fase con puerta** | Cada grupo de tareas cierra con su puerta de backend completa y su salida a la vista. La puerta de cierre agrega la barrera nueva a las cinco existentes. |
| **IV · Tests deterministas y aislados** | El test de integración **limpia la moneda que crea**, porque `LimpiarCuentasAsync` no toca esa tabla ([D-05](./research.md)). El script restaura el catálogo con un `trap`, como las otras cuatro barreras. El caso de rendimiento hereda el guardarraíl que ya exige que el sembrado haya caído en el mes medido. |
| **V · Las barreras se verifican a sí mismas** | Es el principio que **origina** esta feature: `verificar-monedas.sh` existe porque "se puede agregar una moneda sin tocar código" es hoy una afirmación que nadie ejecutó. Y el script mismo se ve fallar antes de darse por bueno. |

**Resultado: pasa, sin violaciones.** *Complexity Tracking* queda vacío y por eso se elimina.

Un punto que merece decirse en vez de darse por obvio: **agregar un sexto script podría parecer
complejidad de más.** No lo es por dos razones. La primera es que reemplaza a nada: hoy la propiedad
no está verificada de ninguna forma. La segunda es que el proyecto ya decidió que esta clase de
afirmación se ejecuta y no se declara, y aplicar esa decisión de forma pareja es más barato que
discutirla ticket por ticket.

## Project Structure

### Documentation (this feature)

```text
specs/008-monedas-como-dato/
├── plan.md              # Este archivo
├── research.md          # Fase 0 — las siete decisiones
├── data-model.md        # Fase 1 — el catálogo, sin cambios de esquema
├── quickstart.md        # Fase 1 — cómo verificarlo a mano
├── contracts/
│   └── monedas.md       # Fase 1 — el contrato que NO cambia, escrito para que no cambie
├── checklists/
│   └── requirements.md  # De /speckit-specify
└── tasks.md             # Fase 2 — lo genera /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/            # SIN CAMBIOS. Es la propiedad que D-04 vuelve criterio de revisión.
├── GestionGastos.Api.Tests/
│   ├── Integracion/
│   │   └── MonedaComoDatoTests.cs        # NUEVO — AC-01, AC-02, AC-03, AC-05, AC-06
│   └── Rendimiento/
│       └── RendimientoResumenTests.cs    # MODIFICADO — un caso más, con dos monedas (AC-04)
└── verificar-monedas.sh                  # NUEVO — la sexta barrera: 0 líneas, 0 recompilaciones

.github/workflows/ci.yml          # MODIFICADO — la barrera nueva, con las otras
AGENTS.md                         # MODIFICADO — la barrera nueva en la tabla de Stack
```

**Structure Decision**: se conserva la separación `backend/` + `frontend/` del proyecto. Esta feature
vive **enteramente dentro de `backend/`**, y dentro de él sólo en el proyecto de tests y en un script
de la raíz. El frontend no se toca: no consume el resumen todavía (deuda D6-01 de la feature 006) y
la lista de monedas ya es de largo variable en sus tipos.

## Constitution Check — segunda pasada, después del diseño

*Re-evaluación exigida por la constitución tras la Fase 1.*

El diseño no introdujo ninguna violación, y sí hizo aparecer **dos precisiones** que la primera
pasada no podía tener:

- **Principio I aplicado al script.** Al escribir [D-01](./research.md) quedó claro que
  `verificar-monedas.sh` también tiene que verse fallar, y **cómo**: no alcanza con romper el
  código, porque el script no mira código. Se lo pone en rojo obligándolo a recompilar entre el
  primer hash y el segundo — que es su propia forma de fallar, y la única que prueba que el hash
  sirve para algo.
- **Principio IV y una tabla que nadie limpiaba.** `LimpiarCuentasAsync` no toca `moneda`, y hasta
  ahora eso estaba bien porque ningún test creaba monedas. Éstos sí. La limpieza va en el test que
  las crea y **no** en `LimpiarCuentasAsync` ([D-05](./research.md)): meterla ahí borraría las dos
  sembradas para toda la suite, exactamente el error que ese método ya evita en categorías filtrando
  por `usuario_id != null`.

**Resultado: pasa, sin violaciones.** No hay nada que registrar en *Complexity Tracking*.

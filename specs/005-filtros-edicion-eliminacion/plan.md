# Implementation Plan: Filtros del listado, edición y eliminación

**Branch**: `005-filtros-edicion-eliminacion` | **Date**: 2026-08-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-filtros-edicion-eliminacion/spec.md`

## Summary

Se agregan tres endpoints —consultar, modificar y eliminar un movimiento por su identificador— y
dos filtros al listado que ya existe. Con eso la superficie de movimientos pasa de 2 a 5 endpoints
y el movimiento deja de ser inmutable.

El enfoque tiene un eje: **la superficie nueva tiene que nacer aislada, y eso lo tiene que
comprobar una barrera, no la buena memoria de quien la escriba**. La feature 004 dejó esa barrera
en pie, pero la revisión del código encontró que **no cubre lo que esta feature va a hacer**: su
excepción declarada era segura mientras el único acceso escrito a mano fuera un INSERT, y deja de
serlo en cuanto aparece un leer-modificar-guardar. Ese hallazgo, y su arreglo, son la parte no
obvia de este plan ([D-01](./research.md)).

Lo demás es convencional: reusar la validación del alta, generalizar el rango de fechas que hoy
sólo sabe de meses, y agregar el tipo del contrato que falta.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.301) en el backend; TypeScript 5.9 + React 19 en el
frontend. Sin cambios de versión.

**Primary Dependencies**: Entity Framework Core 9.0.19 + Pomelo.MySQL 9.0.0. **No se agrega
ninguna dependencia**: todo lo que hace falta ya está.

**Storage**: MySQL 8.4.10, esquema `gestiongastos`; los tests contra `gestiongastos_test`.
**Sin migración**: la entidad `Movimiento` no cambia de forma. Si aparece una migración, algo se
salió del alcance.

**Testing**: xUnit en backend, Vitest en frontend.

**Target Platform**: API HTTP + SPA.

**Project Type**: Web app, backend y frontend separados.

**Performance Goals**: los del listado, que no cambian. El filtro por categoría agrega un predicado
que el índice `(usuario_id, fecha DESC, id DESC)` no cubre; con los volúmenes de este proyecto no
justifica un índice nuevo, y queda anotado en [D-07](./research.md) por si algún día lo justifica.

**Constraints**:

- **El contrato no puede desalinearse.** `verificar-contrato.sh` compara `frontend/src/api/tipos.ts`
  contra el JSON real en las dos direcciones. Un tipo nuevo en el backend sin su contraparte acá es
  un rojo.
- **Todo endpoint exige sesión.** `verificar-autorizacion.sh` lo comprueba, y los tres endpoints
  nuevos entran en su radar automáticamente.
- **Ninguna lectura de movimientos fuera del canal.** `BarreraDeAislamientoTests` lo vigila — con
  el agujero de [D-01](./research.md), que este plan cierra.
- **El recorte por omisión lo decide el servidor.** Los filtros no pueden convertirlo en algo que
  el cliente elige por defecto (FR-013).

**Scale/Scope**: 3 endpoints nuevos, 1 modificado, 17 AC, 0 migraciones, 0 dependencias nuevas.

## Constitution Check

*GATE: pasa antes de Phase 0 y se re-evalúa después de Phase 1.*

| Principio | Cómo lo cumple esta feature | Estado |
|---|---|---|
| **I. Test-First** | A diferencia de 004, acá el rojo es **espontáneo y real**: los endpoints no existen, así que el primer test de cada uno falla con 404 antes de escribir una línea de producción. No hace falta el mecanismo de desarme deliberado que 004 necesitó | ✅ |
| **II. Cada AC tiene su test que lo nombra** | Los 17 AC de la spec se traducen uno a uno. Los cuatro que vienen de la deuda de 004 llevan además el identificador del PRD que arrastran | ✅ |
| **III. VERIFY es una fase con puerta** | Una tarea de VERIFY al cierre de cada historia, y la puerta entera —incluidas las cuatro barreras— antes de cerrar la feature | ✅ |
| **IV. Tests deterministas y aislados** | El listado sin filtros recorta al mes en curso **del servidor**, así que todo test que lo toque va con el reloj clavado por `FactoriaConReloj`. Es la misma trampa que 004 documentó en su D-07, y acá es peor: los filtros de fecha se prueban contra rangos concretos | ✅ |
| **V. Las barreras se verifican a sí mismas** | Esta feature **modifica** la barrera de aislamiento (D-01). El principio obliga a que el cambio pruebe que sabe ponerse en rojo: `verificar-aislamiento.sh` gana un caso de desarme por la vía nueva, y sin ese caso el cambio de la barrera no está terminado | ⚠️ **obligación explícita** |

### La única violación que este plan admite, y por qué no lo es

Modificar una barrera **dentro** de la feature que se beneficia de ella parece un conflicto de
interés: quien la afloja es quien la tiene que sortear. Acá el movimiento es el contrario —la
barrera se **estrecha**, no se afloja— y la forma de comprobarlo es objetiva: el desarme nuevo de
`verificar-aislamiento.sh` tiene que dar rojo **antes** de que el estrechamiento exista, y verde
después. Si el orden se invierte, el estrechamiento no está haciendo nada.

Por eso el estrechamiento de la barrera va **primero**, en su propia fase, antes del primer
endpoint. Escribirla después sería escribirla sabiendo qué tiene que dejar pasar.

## Project Structure

### Documentation (this feature)

```text
specs/005-filtros-edicion-eliminacion/
├── plan.md              # Este archivo
├── research.md          # Phase 0: las decisiones y su porqué
├── data-model.md        # Phase 1: entidades, invariantes y estados
├── quickstart.md        # Phase 1: cómo comprobarlo a mano
├── contracts/           # Phase 1: el contrato HTTP de los cinco endpoints
├── checklists/
│   └── requirements.md  # Ya escrito por /speckit-specify
└── tasks.md             # Phase 2: lo genera /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/
│   ├── Dominio/
│   │   └── RangoDeFechas.cs              # NUEVO: generaliza RangoDelMes (D-04)
│   └── Movimientos/
│       ├── MovimientoDtos.cs             # + MovimientoEditadoDto (D-05)
│       ├── MovimientosConsulta.cs        # + PropioPorId, + filtros en el listado
│       ├── MovimientosEndpoints.cs       # + GET/{id}, PUT/{id}, DELETE/{id}
│       └── ValidacionDelMovimiento.cs    # RENOMBRE de ValidacionDelAlta (D-05)
└── GestionGastos.Api.Tests/
    ├── Contrato/
    │   └── ContratoMovimientosTests.cs   # + MovimientoEditado
    └── Integracion/
        ├── AislamientoEntreCuentasTests.cs   # + los 4 AC de la deuda de 004
        ├── BarreraDeAislamientoTests.cs      # ESTRECHAR la excepción (D-01)
        ├── EdicionDeMovimientoTests.cs       # NUEVO
        ├── EliminacionDeMovimientoTests.cs   # NUEVO
        └── FiltrosDelListadoTests.cs         # NUEVO

backend/verificar-aislamiento.sh          # + el desarme por la vía nueva (D-01)

frontend/src/api/tipos.ts                 # + MovimientoEditado
```

**Structure Decision**: la de siempre, la que fija `AGENTS.md` — `backend/` y `frontend/`
separados, con la única excepción declarada de los tests de `Contrato/`, que leen
`frontend/src/api/tipos.ts`. Esta feature no la amplía.

**El frontend queda fuera del alcance de este plan.** La spec describe la capacidad, y los tres
endpoints más los filtros son lo que la habilita. La pantalla que los use es trabajo aparte: hoy
`frontend/` no tiene ni formulario de edición ni controles de filtro, y agregarlos duplicaría el
tamaño del ticket. Lo único que el frontend recibe acá es **el tipo del contrato**, que no es
opcional: sin él la verificación del contrato no puede comparar nada.

## Complejidad y alcance

| Decisión | Por qué se toma | La alternativa más simple, y por qué no |
|---|---|---|
| Estrechar la barrera de aislamiento antes de escribir los endpoints (D-01) | La excepción actual deja pasar una lectura sin acotar en el mismo archivo donde esta feature va a leer-modificar-guardar | Confiar en que las lecturas nuevas se escriban acotadas. Es exactamente lo que la barrera existe para no tener que confiar |
| Generalizar `RangoDelMes` a `RangoDeFechas` (D-04) | Los filtros piden un rango arbitrario; el tipo actual sólo sabe construir meses | Pasar dos `DateOnly` sueltos. Pierde el invariante `Desde <= Hasta` justo donde FR-015 lo necesita |
| Un DTO propio para la edición (D-05) | Un `fecha` opcional que significa "hoy" es correcto al registrar y una trampa al editar | Reusar `NuevoMovimientoDto`. Ata dos contratos a que nunca diverjan, que es la razón por la que `NuevaCuenta` y `Credenciales` ya son tipos separados |
| Comparar respuestas entre sí para los AC de indistinguibilidad (D-03) | "Indistinguible de inexistente" es una condición sobre dos respuestas, no sobre una | Afirmar `404` en cada una. Pasa en verde aunque los cuerpos difieran y delaten la existencia |

---

## Constitution Check — re-evaluación después del diseño

Se vuelve a pasar la constitución sobre el diseño ya escrito, que es cuando aparecen las cosas que
el primer chequeo no podía ver.

| Principio | Qué cambió al diseñar | Estado |
|---|---|---|
| **I. Test-First** | Sin cambios. El rojo es espontáneo: `404` en las rutas que todavía no existen | ✅ |
| **II. Cada AC tiene su test** | Sin cambios. 17 AC, 17 tests como mínimo | ✅ |
| **III. VERIFY con puerta** | Sin cambios | ✅ |
| **IV. Deterministas y aislados** | **Se agravó, y hay que decirlo.** No sólo el listado por omisión depende del mes en curso: el quickstart mostró que el caso que prueba los extremos del rango se escribe naturalmente con `date +%F`. Un test escrito así pasa todos los días y falla el día que el mes cambia entre dos líneas. Todo test de esta feature que toque fechas va con el reloj clavado, **también los de filtros** | ✅ con nota |
| **V. Las barreras se verifican a sí mismas** | Concretado: `verificar-aislamiento.sh` gana un **cuarto** desarme —una lectura sin acotar dentro de `MovimientosEndpoints.cs`— que hoy da verde y tiene que dar rojo. Sin ese paso el estrechamiento de D-01 no está verificado | ✅ obligación anotada |

### Lo que el diseño hizo aparecer y el primer chequeo no veía

**Un hueco declarado del contrato.** [D-08](./research.md) deja los filtros fuera de la verificación
del contrato, porque viajan como parámetros de consulta y los tests de `Contrato/` comparan JSON. No
lo cubre nada, está dicho, y no se inventa una barrera nueva para taparlo: sería una barrera sin
cicatriz que la justifique, y este repositorio ya tiene cuatro que sí la tienen.

**Dos renombres que tocan código existente.** `ValidacionDelAlta` → `ValidacionDelMovimiento` y
`RangoDelMes` → `RangoDeFechas`. Ninguno cambia comportamiento, los dos tocan archivos que esta
feature no agrega, y el segundo obliga además a registrar el tipo nuevo en `ArgumentosDePrueba` de
la barrera. No son violaciones; son trabajo que las tareas tienen que nombrar en vez de dejar que
aparezca a mitad de camino.

**Un comentario que esta feature vuelve falso.** `Movimiento` dice hoy *"Se crea y no cambia. La
edición y la baja llegan en FEAT-001b"*. Corregirlo es parte del trabajo, no una prolijidad
opcional: un comentario que miente es peor que ninguno, y este repositorio ya tuvo que arreglar un
README por lo mismo.

**Veredicto**: sin violaciones. La tabla de *Complejidad y alcance* de arriba queda como está, y no
hace falta ningún *Complexity Tracking*: las cuatro decisiones que agregan algo tienen su
alternativa más simple evaluada y descartada por escrito.

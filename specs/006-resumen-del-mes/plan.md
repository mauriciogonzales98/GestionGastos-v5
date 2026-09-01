# Implementation Plan: Resumen del mes con desglose por categoría

**Branch**: `006-resumen-del-mes` | **Date**: 2026-09-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-resumen-del-mes/spec.md`

## Summary

Se agrega un endpoint —`GET /api/resumen`— que devuelve, por cada moneda del catálogo, lo ingresado,
lo gastado, el balance y el desglose de gastos por categoría, dentro del mes en curso o del rango
que se pida.

El enfoque tiene un eje: **es la primera lectura que devuelve números derivados en vez de hechos, y
un número contaminado se ve idéntico a uno correcto**. De ahí salen las dos partes no obvias del
plan:

1. **La barrera de aislamiento no cubre lo que esta feature va a escribir.** Vigila los métodos del
   canal que devuelven `IQueryable<Movimiento>`; una agregación devuelve sumas, así que la barrera
   ni la enumera. Es la misma clase de caducidad que FEAT-001b encontró en su D-01, por otra vía
   ([D-01](./research.md#d-01--la-barrera-de-aislamiento-no-cubre-lo-que-esta-feature-va-a-escribir)).
2. **Los números que tienen que coincidir se derivan de la misma fila.** Una sola consulta agregada
   alimenta el total, el balance y el desglose, así que las igualdades que piden FR-005 y FR-009 son
   estructurales y no coincidencias a verificar
   ([D-04](./research.md#d-04--una-sola-consulta-agregada-y-la-composición-en-memoria)).

Lo demás es convencional: reusar el rango de fechas y el mes del servidor que ya existen —unificando
su validación en un solo lugar—, componer la respuesta contra el catálogo de monedas, y declarar los
tres tipos del contrato.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.301) en el backend; TypeScript 5.9 + React 19 en el
frontend. Sin cambios de versión.

**Primary Dependencies**: Entity Framework Core 9.0.19 + Pomelo.MySQL 9.0.0. **No se agrega ninguna
dependencia**: la agregación es `GroupBy` + `Sum` traducidos por el proveedor.

**Storage**: MySQL 8.4.10, esquema `gestiongastos`; los tests contra `gestiongastos_test`.
**Sin migración**: no se toca el esquema ni se agrega índice
([D-10](./research.md#d-10--sin-migración-y-el-índice-se-deja-como-está)). Si aparece una migración,
algo se salió del alcance.

**Testing**: xUnit en backend, Vitest en frontend. Los tests del resumen fijan el reloj sin
excepción ([D-08](./research.md#d-08--los-tests-fijan-el-reloj-sin-excepción)).

**Target Platform**: API HTTP + SPA.

**Project Type**: Web app, backend y frontend separados.

**Performance Goals**: RNF-01 —dashboard en < 2 s p95 con 1000 movimientos, < 4 s con 10000—. Es el
primer endpoint al que ese RNF le aplica de lleno, y a diferencia del listado, agrega: el `GROUP BY`
por categoría no lo cubre el índice `(usuario_id, fecha DESC, id DESC)`. Se mide, no se supone
([D-09](./research.md#d-09--el-rendimiento-se-mide-no-se-supone)).

**Constraints**:

- **Ningún monto ajeno puede sumar en ningún total.** Y a diferencia del listado, no se ve: no hay
  fila de más que mirar, sólo un número más grande. El aislamiento se verifica con dos cuentas
  cargadas y contra un valor esperado calculado a mano.
- **Ninguna lectura de movimientos fuera del canal**, y ahora también ninguna consulta del canal sin
  vigilar — que es el agujero que este plan cierra.
- **El resumen y el listado no pueden divergir.** Un solo intérprete del período
  ([D-03](./research.md#d-03--el-período-se-valida-igual-que-en-el-listado-y-en-un-solo-lugar)) y una
  sola fuente para los números que tienen que coincidir.
- **El contrato no puede desalinearse.** `verificar-contrato.sh` compara `frontend/src/api/tipos.ts`
  contra el JSON real en las dos direcciones; tres tipos nuevos son tres comparaciones nuevas.
- **Todo endpoint exige sesión.** `verificar-autorizacion.sh` descubre los endpoints por
  `EndpointDataSource` en runtime, así que el nuevo entra en su radar sin tocar nada — y hay que
  comprobarlo, no asumirlo.

**Scale/Scope**: 1 endpoint nuevo, 1 modificado (el listado, por el refactor del período), 0
migraciones, 0 dependencias nuevas, 1 barrera generalizada, 18 FR.

## Constitution Check

*GATE: pasa antes de Phase 0 y se re-evalúa después de Phase 1.*

| Principio | Cómo lo cumple esta feature | Estado |
|---|---|---|
| **I. Test-First** | El endpoint no existe: el primer test falla con 404 antes de que haya una línea de producción. Rojo espontáneo y real, sin desarmes deliberados. **La excepción es D-01**: el agujero de la barrera exige mostrar un **verde** que no debería estar —una agregación sin acotar que la barrera aprueba— antes de arreglarla. Ese verde es el rojo de esa tarea | ✅ |
| **II. Cada AC tiene su test que lo nombra** | Los AC de la spec se traducen uno a uno. AC-30 y AC-31 llevan además el identificador del PRD; AC-02 lleva el suyo y el de la tabla de *Deuda registrada* de la feature 004 | ✅ |
| **III. VERIFY es una fase con puerta** | Una tarea de VERIFY al cierre de cada historia, y la puerta entera —cobertura y las **cuatro** barreras— antes de cerrar la feature | ✅ |
| **IV. Tests deterministas y aislados** | El período por omisión es el mes del servidor: todo test va con el reloj fijo. Los tests de rendimiento quedan fuera de CI con el filtro que ya existe, porque miden tiempo de pared ([D-08](./research.md#d-08--los-tests-fijan-el-reloj-sin-excepción), [D-09](./research.md#d-09--el-rendimiento-se-mide-no-se-supone)) | ✅ |
| **V. Las barreras se verifican a sí mismas** | Esta feature **modifica** la barrera de aislamiento. El principio obliga a que el cambio pruebe que sabe ponerse en rojo: `verificar-aislamiento.sh` gana un quinto desarme —una agregación del canal sin `usuario_id`—, y **sin ese caso el cambio de la barrera no está terminado** | ⚠️ **obligación explícita** |

**Re-evaluación post-Phase 1**: sin cambios. El diseño no introduce ninguna violación ni ninguna
excepción nueva; *Complexity Tracking* queda vacío y por eso se eliminó de este plan.

## Project Structure

### Documentation (this feature)

```text
specs/006-resumen-del-mes/
├── plan.md              # Este archivo
├── research.md          # Las decisiones y sus alternativas descartadas
├── data-model.md        # Las formas que se agregan, y por qué ninguna se persiste
├── quickstart.md        # Cómo comprobar a mano que la feature hace lo que dice
├── contracts/           # El contrato del endpoint nuevo
├── checklists/          # requirements.md, de /speckit-specify
├── spec.md
└── tasks.md             # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/
│   ├── Dominio/
│   │   └── PeriodoPedido.cs            # NUEVO — el único intérprete de desde/hasta (D-03)
│   ├── Movimientos/
│   │   ├── MovimientosConsulta.cs      # MODIFICADO — acotado privado compartido + Agrupado (D-04)
│   │   ├── MontoAgrupado.cs            # NUEVO — la fila que devuelve la agregación
│   │   └── MovimientosEndpoints.cs     # MODIFICADO — el listado pasa a usar PeriodoPedido
│   ├── Resumenes/                      # plural: `Resumen` a secas choca con el DTO (CA1724)
│   │   ├── ResumenEndpoints.cs         # NUEVO — GET /api/resumen
│   │   ├── ResumenDtos.cs              # NUEVO — Resumen, ResumenPorMoneda, TotalPorCategoria
│   │   └── CalculoDelResumen.cs        # NUEVO — compone el agregado contra el catálogo (D-05)
│   └── Program.cs                      # MODIFICADO — app.MapResumen()
├── GestionGastos.Api.Tests/
│   ├── Integracion/
│   │   ├── ResumenDelPeriodoTests.cs   # NUEVO — US1 y US3
│   │   ├── DesglosePorCategoriaTests.cs# NUEVO — US2
│   │   ├── AislamientoEntreCuentasTests.cs  # MODIFICADO — AC-02, la deuda de 004
│   │   └── BarreraDeAislamientoTests.cs     # MODIFICADO — la generalización de D-01
│   ├── Contrato/
│   │   └── ContratoResumenTests.cs     # NUEVO — los tres tipos, en las dos direcciones
│   └── Rendimiento/
│       └── RendimientoResumenTests.cs  # NUEVO — RNF-01 (D-09)
└── verificar-aislamiento.sh            # MODIFICADO — quinto desarme

frontend/
└── src/api/tipos.ts                    # MODIFICADO — los tres tipos del contrato (D-07)
```

**Structure Decision**: se sigue la separación que ya rige el repositorio. El resumen estrena
carpeta propia (`Resumen/`) en vez de colgarse de `Movimientos/`: es un recurso distinto con su
propia ruta, igual que `Categorias/` y `Sesion/`. **La lectura, en cambio, no se muda**: la
agregación vive en `MovimientosConsulta` porque es el canal único, y sacarla de ahí sería la
excepción que la barrera existe para impedir.

**El frontend recibe sólo el contrato**, como en FEAT-001b: la pantalla que muestre estos números es
del ticket 5.

## Orden de ejecución y por qué

El orden no es por prioridad de producto: la barrera va primero porque protege a todo lo demás, y su
arreglo tiene que ser visible antes de que exista el código que podría esconderse detrás.

1. **La barrera (D-01)** — se muestra el agujero en verde, se generaliza, se prueba el rojo, se
   agrega el quinto desarme. Bloquea a todo lo demás: es lo que hace que el aislamiento del resumen
   se verifique en vez de argumentarse.
2. **El período unificado (D-03)** — refactor del listado con la suite de la 005 como red, antes de
   escribir el resumen, para que el rojo del refactor no se confunda con el de la feature.
3. **US1, el resumen del mes** — el MVP.
4. **US2, el desglose por categoría** — sobre la misma consulta agregada.
5. **US3, el período elegido** — cae casi solo con D-03 hecho; lo que agrega son sus tests.
6. **Cierre** — AC-02 de la deuda de 004, rendimiento, cobertura, las cuatro barreras y el quickstart.

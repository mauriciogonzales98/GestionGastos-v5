# Implementation Plan: Cada cuenta con sus propias categorías predefinidas

**Branch**: `021-predefinidas-por-cuenta` | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013-predefinidas-por-cuenta/spec.md`

## Summary

Saldar **D7-07**: la regla de que un movimiento no puede clasificarse con la categoría de otra cuenta
pasa de vivir sólo en dos comprobaciones de la aplicación a vivir también en la base.

El camino no es el que la deuda proponía. Los dos que proponía se midieron y fallaron: la clave
foránea compuesta rechaza las diez predefinidas (`ERROR 1452`) y el trigger no se puede crear con las
credenciales de la aplicación (`ERROR 1419`), aunque en CI —que conecta como `root`— saldría verde.
El problema no era la restricción sino la forma de los datos: mientras "predefinida" signifique
`usuario_id IS NULL`, ninguna restricción declarativa puede expresar "el dueño de la categoría es el
mismo que el del movimiento, **o la categoría no es de nadie**".

Por eso el enfoque es **eliminar el `NULL` del modelo**: las diez categorías iniciales dejan de ser
filas compartidas y pasan a entregarse, copiadas, a cada cuenta que se registra. Con toda categoría
con dueño, la regla se vuelve una clave foránea compuesta corriente. De regalo desaparece la
distinción entre categorías del sistema y propias, y con ella el `403` y el campo `esPropia` del
contrato.

Tres frentes, en este orden: **(1)** el modelo y la migración de datos, **(2)** la restricción y su
barrera, **(3)** el contrato y la pantalla.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.301) en backend; TypeScript / React 19 + Vite en frontend

**Primary Dependencies**: EF Core 9.0.18 + Pomelo.MySQL 9.0.0

**Storage**: MySQL 8.4.11 local, schema `gestiongastos`; tests contra `gestiongastos_test`

**Testing**: xUnit (backend), Vitest (frontend)

**Target Platform**: aplicación web; API en .NET, SPA en el navegador

**Project Type**: web application — `backend/` y `frontend/` separados

**Performance Goals**: sin objetivos nuevos. El catálogo pasa de 10 filas a 10 × cuentas; con el
orden de magnitud actual (9 cuentas) es irrelevante, y las consultas del catálogo ya estaban
acotadas por ámbito, así que ninguna lee más filas que antes

**Constraints**:
- La restricción tiene que desplegarse **con las credenciales de la aplicación**. Después de lo que
  midió el spike, cualquier forma que exija privilegios que el usuario de la aplicación no tiene
  queda descartada aunque funcione en CI
- La migración de datos es irreversible y no puede alterar un solo total (`FR-012`)
- Los tests corren contra MySQL real: el tipo de columna y las restricciones son justamente lo que
  se verifica

**Scale/Scope**: medido el 2026-09-12 sobre `gestiongastos` — 9 cuentas, 18 categorías, 6
movimientos, 5 de ellos apuntando a una predefinida compartida. Sin datos en producción

## Constitution Check

*GATE: antes de Phase 0. Re-evaluado después de Phase 1.*

### Antes de Phase 0

| Principio | Estado | Cómo se cumple |
|---|---|---|
| **I. Test-First** | ✅ | Cada tarea de `/speckit-tasks` lleva su test antes que su código. El caso más delicado —la migración de datos— se ataca con un test que carga una base con el estado anterior, migra y compara; se escribe y se ve en rojo antes de escribir la migración |
| **II. Cada AC tiene su test, y el test lo nombra** | ✅ | Las 15 escenas de aceptación de las cuatro historias se traducen a tests que citan `FR-xxx` / `SC-xxx` en su nombre, como ya hace `Un_Movimiento_No_Puede_Apuntar_A_Una_Categoria_Ajena_FR021_SC009` |
| **III. VERIFY es una fase con puerta** | ✅ | La puerta de cada tarea está en [quickstart.md](./quickstart.md) §1; la de cierre de feature, en §6, con las siete barreras existentes más la nueva |
| **IV. Tests deterministas y aislados** | ⚠️ **atención** | Las categorías dejan de tener identificadores estables. Cinco tests fijan hoy `CategoriaId = <número>`: si se dejan así, se vuelven intermitentes en cuanto cambie el orden de creación. Se resuelven resolviendo la categoría desde el catálogo de su cuenta (D-10 de research) |
| **V. Las barreras se verifican a sí mismas** | ✅ | La restricción nueva trae `verificar-ambito-de-categoria.sh` (D-09 de research). Además, `verificar-aislamiento.sh` va a ponerse en rojo por el escritor nuevo, y se corrige declarándolo, no aflojando la barrera |

**Resultado: pasa.** El punto de atención del principio IV es una tarea, no una violación.

### Después de Phase 1 (re-evaluación)

| Principio | Estado | Qué cambió al diseñar |
|---|---|---|
| **I. Test-First** | ✅ | Sin cambios |
| **II. Cada AC tiene su test** | ✅ | El diseño agregó dos verificaciones que no salían de ningún AC y que igual llevan test: que `usuario_id` pueda participar de dos claves foráneas (D-01), y que el `catch` del email duplicado no se trague un fallo de las categorías (D-07) |
| **III. VERIFY** | ✅ | Sin cambios |
| **IV. Deterministas y aislados** | ✅ | Resuelto en diseño: helper de catálogo por cuenta, al estilo del `CatalogoDeMonedas` que ya existe |
| **V. Barreras** | ⚠️ **con deuda declarada** | La barrera de aislamiento vigila el texto `contexto.Categorias`. Una escritura por propiedad de navegación la esquivaría. Esta feature **no** introduce ninguna (D-07 de research), pero el agujero es real — es el gemelo del que originó D7-07 — y se anota como **D13-01** en vez de dejarlo sin nombre |

**Resultado: pasa**, con una deuda anotada y ninguna violación que justificar.

## Project Structure

### Documentation (this feature)

```text
specs/013-predefinidas-por-cuenta/
├── plan.md              # Este archivo
├── research.md          # Phase 0 — las diez decisiones, con lo que se midió
├── data-model.md        # Phase 1 — el esquema antes y después, y la transición de datos
├── quickstart.md        # Phase 1 — qué correr y qué hay que ver
├── contracts/
│   └── api.md           # Phase 1 — `esPropia` se va, el 403 se va
├── checklists/
│   └── requirements.md  # 16/16
└── tasks.md             # Phase 2 — NO lo crea este comando
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/
│   ├── Dominio/
│   │   └── Categoria.cs                       # `UsuarioId` deja de ser anulable
│   ├── Categorias/
│   │   ├── CatalogoInicial.cs                 # NUEVO: los diez nombres y el alta del catálogo
│   │   ├── CategoriasConsulta.cs              # `DelAmbito` pierde el `|| UsuarioId == null`
│   │   ├── CategoriasEndpoints.cs             # se va el 403 de la predefinida
│   │   └── CategoriaDto.cs                    # se va `EsPropia`
│   ├── Cuentas/
│   │   └── CuentasEndpoints.cs                # llama a CatalogoInicial, un solo SaveChanges
│   ├── Persistencia/
│   │   └── GestionGastosDbContext.cs          # se va el HasData de Categoria; clave alternativa y foránea compuesta
│   └── Migrations/
│       ├── <fecha>_CategoriasPorCuenta.cs     # NUEVO: datos + NOT NULL, escrita a mano (D-04)
│       └── <fecha>_AmbitoDeCategoriaEnLaBase.cs  # NUEVO: clave alternativa + foránea compuesta
├── GestionGastos.Api.Tests/
│   ├── Integracion/
│   │   ├── AmbitoDeCategoriaEsquemaTests.cs   # NUEVO: SQL directo, FR-006 y FR-009 —
│   │   │                                      #   junto a MonedaCodigoEsquemaTests.cs, su par
│   │   ├── AltaDeCuentaTests.cs               # las diez al registrarse
│   │   ├── CategoriasPropiasTests.cs          # se van los dos casos de 403
│   │   ├── AislamientoDeCategoriasTests.cs    # conserva el test cruzado de FR-007
│   │   ├── BarreraDeAislamientoTests.cs       # declara el segundo escritor
│   │   └── CatalogoDeCategorias.cs            # NUEVO: helper, adiós a los ids fijos
│   ├── Migraciones/MigracionDeCatalogoTests.cs  # NUEVO: junto a MigracionDeCuentasTests.cs
│   ├── Unitarios/CatalogoInicialTests.cs      # NUEVO: los diez nombres
│   └── Contrato/ContratoCategoriasTests.cs    # sin `esPropia`
└── verificar-ambito-de-categoria.sh           # NUEVO: la octava barrera

frontend/
├── src/
│   ├── api/tipos.ts                           # se va `esPropia`
│   └── categorias/PantallaCategorias.tsx      # todas las filas con botones
└── tests/
    ├── PantallaCategorias.test.tsx
    ├── App.test.tsx                           # 5 usos de esPropia
    └── FormularioMovimiento.test.tsx          # 1 uso
```

**Structure Decision**: la de siempre —`backend/` y `frontend/` separados—, con la única excepción
ya declarada en `AGENTS.md`: los tests de `Contrato/` leen `frontend/src/api/tipos.ts`
([ADR-001](../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md)). Esta feature no
agrega ninguna excepción nueva.

## El orden en que conviene atacarlo

No es `tasks.md` —eso lo genera `/speckit-tasks`—, pero el orden no es libre y conviene dejarlo dicho:

1. **El catálogo inicial**, que nace sin que nadie lo llame. No rompe nada.
2. **La migración de datos y el alta de cuenta, juntas.** La migración borra las diez compartidas,
   así que desde ese instante una cuenta sin catálogo propio no puede cargar un movimiento.
   Separarlas deja media suite en rojo por diseño.
3. **La restricción y su barrera**, en su propia migración. Va después de los datos porque **es** la
   verificación de que la migración salió bien (D-04): ponerla antes sólo adelanta el fallo.
4. **El contrato**, las dos pilas en la misma tarea, con el rojo esperado en el medio; después la
   pantalla y el `403`.
5. **El modelo, último.** `Categoria.UsuarioId` deja de ser anulable recién cuando ya no queda
   ningún `is null` que compilar. Antes de eso rompe cinco archivos a la vez, y ninguna tarea
   intermedia podría terminar en verde — que es lo que el Principio III prohíbe.
6. **Cierre**: la puerta completa de quickstart §6, las ocho barreras, y la reescritura de la fila
   D7-07 en la spec de la 007 con lo que este spike midió.

## Complexity Tracking

> Sin violaciones de la constitución que justificar. Lo que sigue son las dos decisiones que agregan
> complejidad y por qué la alternativa simple no servía.

| Decisión | Por qué hace falta | La alternativa simple, y por qué se rechazó |
|---|---|---|
| Una **columna temporal** (`migracion_origen_id`) durante la migración | Emparejar cada copia con la predefinida de la que salió, para reapuntar los movimientos por identidad | Emparejar por `(nombre, tipo)`. Se rechazó porque **se rompe con datos que ya pueden existir**: una cuenta puede tener una categoría propia dada de baja homónima de una predefinida, y el `JOIN` reapuntaría movimientos a la categoría equivocada, en silencio |
| Un **segundo archivo autorizado** a escribir categorías (`CatalogoInicial.cs`) | El alta de cuenta tiene que crear diez categorías, y hoy sólo `CategoriasEndpoints.cs` puede escribirlas | Autorizar a `CuentasEndpoints.cs`. Se rechazó porque le abre el `DbSet` a un archivo que hace muchas otras cosas, y la autorización queda vigente para todo lo que ese archivo haga en el futuro |

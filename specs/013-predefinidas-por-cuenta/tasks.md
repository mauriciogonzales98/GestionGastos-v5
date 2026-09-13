---

description: "Task list for feature implementation"
---

# Tasks: Cada cuenta con sus propias categorías predefinidas

**Input**: Design documents from `/specs/013-predefinidas-por-cuenta/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md), [quickstart.md](./quickstart.md)

**Tests**: **obligatorios**, y no por pedido de esta spec sino por el Principio I de la constitución.
Cada tarea de código lleva su tarea de test **antes**, el rojo se muestra antes de escribir el
código, y cada test cita su `FR-xxx` / `SC-xxx` en el nombre (Principio II).

**Organization**: agrupadas por historia de usuario. **Ojo con la independencia**: tres de las cuatro
historias son P1 y **no** son independientes entre sí — es una migración de esquema, no tres
features que se puedan entregar por separado. El orden real está en *Dependencies* y no es
negociable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1, US2, US3, US4)
- Cada tarea lleva su ruta exacta

---

## Phase 1: Setup

**Purpose**: dejar registrado el estado del que se parte, porque parte de esta feature se verifica
comparando contra él.

- [X] T001 Correr la puerta completa sobre `main` sin tocar nada y guardar la salida, para tener la línea de base: `dotnet test backend/` y `pnpm --dir frontend test`
- [X] T002 [P] Escribir la consulta de la foto de totales —por cuenta y período: total ingresado, total gastado, balance y desglose por categoría— en `backend/db/foto-de-totales.sql`, y guardar su resultado actual contra `gestiongastos`. Es lo que `SC-003` compara después de migrar
- [X] T003 [P] Verificar que los dos árboles de trabajo estén limpios antes de empezar: `verificar-monedas.sh` lo exige y no puede distinguir lo que ensucia ella de lo que ya estaba sucio

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: la fuente única de los diez nombres y los helpers de test. **Nada de esto rompe la
compilación**: el catálogo inicial nace sin que nadie lo llame todavía, y el modelo no se toca.

**Por qué está partida así**: el cambio de `Categoria.UsuarioId` a no-anulable rompe cinco lugares
a la vez y no hay forma de que las tareas intermedias terminen en verde. Por eso **la propiedad de
C# sigue anulable hasta la fase 5**, y lo que cambia primero es la base. El Principio III exige una
puerta verde por tarea, no una promesa de que al final todo cierra.

- [X] T004 Escribir el test de `CatalogoInicial`: devuelve diez categorías, siete de gasto y tres de ingreso, con los nombres del catálogo original, todas activas — `backend/GestionGastos.Api.Tests/Unitarios/CatalogoInicialTests.cs` (`FR-002`). Verlo en rojo
- [X] T005 Crear `backend/GestionGastos.Api/Categorias/CatalogoInicial.cs` con los diez nombres y el alta del catálogo para un usuario. **Es el único archivo que conoce esa lista**, y escribe por `contexto.Categorias.AddRange` (research D-05, D-06)
- [X] T006 [P] Crear el helper `backend/GestionGastos.Api.Tests/Integracion/CatalogoDeCategorias.cs`, al estilo del `CatalogoDeMonedas` que ya existe: resuelve una categoría por nombre y tipo **dentro del catálogo de una cuenta** (research D-10)
- [X] T007 Reemplazar los `CategoriaId = <número>` de la suite por el helper de T006. **Eran cinco contando sólo la forma C#; contando los cuerpos JSON (`categoriaId: 1`) son más de cuarenta, en 16 archivos** — y todos se rompen igual cuando las diez compartidas desaparezcan. La medición de research D-10 contaba una sola de las dos formas. Los identificadores de categoría dejan de ser estables y un test que los fija se vuelve intermitente (Principio IV)
- [X] T008 VERIFY: `dotnet format --verify-no-changes`, `dotnet build -warnaserror` y `dotnet test backend/` en verde, con la salida a la vista

**Checkpoint**: el catálogo inicial existe y nadie lo usa todavía. La base y la app siguen como estaban.

---

## Phase 3: User Stories 3 y 1 - La base y el alta cambian juntas (Priority: P1)

**Goal**: las cuentas existentes quedan con su copia de las diez y sus movimientos reapuntados; las
cuentas nuevas nacen con las suyas.

**Independent Test**: migrar una base con el estado anterior y comparar totales y desglose antes y
después; y registrar una cuenta nueva y contar diez categorías propias.

**⚠️ Las dos historias van juntas y no es negociable**: la migración borra las diez categorías
compartidas, así que desde ese instante una cuenta sin catálogo propio no puede cargar un
movimiento. Separarlas deja un estado intermedio donde media suite está en rojo por diseño. T014 y
T015 comparten **una sola puerta**, la de T017.

### Tests ⚠️

- [X] T009 [US3] Escribir el test de la migración en `backend/GestionGastos.Api.Tests/Migraciones/MigracionDeCatalogoTests.cs`, **con la técnica que el repo ya usa en `MigracionDeCuentasTests`**: bajar el esquema a la migración anterior con `IMigrator.MigrateAsync(...)`, sembrar el estado viejo con **SQL crudo** —el modelo actual no sirve para hablarle a un esquema que ya no es el suyo—, volver a subir, y restaurar en un `finally` pase lo que pase. Va con `[Collection(BaseDeDatosSuite.Nombre)]`: mover el esquema mientras otro test lo usa es la interferencia que el Principio IV prohíbe. **Corre contra `gestiongastos_test`, como toda la suite**: `gestiongastos_migracion_test` figura en la lista blanca del fixture pero no lo usa nadie, porque el usuario de MySQL del proyecto no puede crear una tercera base — está documentado en `BaseDeDatosFixture` y verificado el 2026-09-12, `CREATE DATABASE` responde *Access denied*. El estado que fabrica: dos cuentas, las diez compartidas, una propia activa, una propia **dada de baja homónima de una predefinida**, y movimientos apuntando a las dos clases. Exige que cada cuenta quede con sus diez copias, que las propias queden intactas y que ninguna categoría quede sin dueño (`FR-010`, `FR-013`, `FR-014`, `SC-004`). Verlo en rojo

- [X] T010 [US3] Escribir el test de que los números no se mueven: totales, balance y desglose por cuenta y período, antes y después de migrar, idénticos (`FR-011`, `FR-012`, `SC-003`). **El caso de la propia dada de baja homónima es el que importa**: es el que un reapuntado por nombre rompería en silencio (research D-03). Verlo en rojo
- [X] T011 [US1] Escribir en `backend/GestionGastos.Api.Tests/Integracion/AltaDeCuentaTests.cs` los casos de `FR-002`: una cuenta nueva queda con diez categorías propias, siete de gasto y tres de ingreso, todas activas; y puede cargar un movimiento con cualquiera de ellas sin crear nada a mano (`SC-001`). Verlos en rojo
- [X] T012 [US1] Escribir en ese mismo archivo el caso de dos cuentas recién registradas: cada una recibe diez **distintas** de las de la otra aunque los nombres coincidan, y ninguna ve las ajenas (`SC-006`). Verlo en rojo
- [X] T013 [US1] Escribir en ese mismo archivo el caso de `FR-003`: si el alta falla, no queda ni la cuenta ni el catálogo. Incluye el punto que research D-07 marcó — que el `catch` del `1062` del email duplicado **no se trague** un fallo de las categorías haciéndolo pasar por email ya registrado. Verlo en rojo

### Implementation

- [X] T014 [US3] Crear la migración a mano en `backend/GestionGastos.Api/Migrations/<fecha>_CategoriasPorCuenta.cs`, pasos 1 a 6 de research D-04: columna temporal `migracion_origen_id`, copias por `INSERT ... SELECT` cruzando `usuario` con las predefinidas **que haya en la base**, reapuntado por identidad, borrado de las diez compartidas, `usuario_id NOT NULL` y baja de la columna temporal. En la misma tarea: quitar el `HasData` de `Categoria` de `GestionGastosDbContext.Sembrar` —**sin tocar la de `Moneda`**— y marcar la columna como obligatoria con `IsRequired()` (`FR-001`). **La propiedad de C# sigue siendo `long?`**: así el modelo y la base quedan sincronizados sin romper los cinco sitios que la usan. **No confiar en lo que proponga el scaffolding**: generados solos, los `DELETE` del `HasData` irían antes del reapuntado y la migración fallaría contra la foránea. **Y escribir el `Down()` de verdad**: `usuario_id` vuelve a anulable, se recrean las diez compartidas, se reapuntan los movimientos de vuelta y se borran las copias. No es opcional — sin él, T009 no puede fabricar el estado anterior, y quien lo verifica es justamente ese test
- [X] T015 [US1] Llamar a `CatalogoInicial` desde el alta en `backend/GestionGastos.Api/Cuentas/CuentasEndpoints.cs`, con **un solo** `SaveChangesAsync` —el que ya hay— para que la cuenta y su catálogo sean una sola transacción (`FR-003`), y acotar el `catch (DbUpdateException) when (EsEmailDuplicado(...))` para que siga cubriendo sólo el email duplicado
- [X] T016 [US1] *(adelantada a la fase 2: la barrera se puso en rojo en la puerta de T008, en cuanto el archivo existió — no hacía falta que nadie lo llamara)* Declarar `Categorias/CatalogoInicial.cs` como segundo escritor autorizado en `backend/GestionGastos.Api.Tests/Integracion/BarreraDeAislamientoTests.cs`, con `Add` y `AddRange` permitidos, y escribir en el comentario por qué es un archivo aparte y no una autorización a `CuentasEndpoints` (research D-06)
- [X] T017 VERIFY: `dotnet build -warnaserror`, `dotnet test backend/` completo y `./backend/verificar-aislamiento.sh` (~4 min), los tres en verde y con la salida a la vista

**Checkpoint**: los datos viejos viven en el modelo nuevo y las cuentas nuevas nacen con su catálogo.
Ninguna categoría sin dueño. La aplicación todavía no sabe que el `NULL` desapareció.

---

## Phase 4: User Story 2 - Que la regla la sostenga la base (Priority: P1)

**Goal**: la invariante de D7-07 deja de vivir sólo en el código.

**Independent Test**: con SQL directo, sin pasar por la aplicación, la base rechaza el movimiento que
apunta a la categoría de otra cuenta y acepta el que apunta a una propia.

**Va en su propia migración**, la segunda: separada de la de datos, un fallo señala el paso
correcto. Y sigue siendo la verificación de que la migración anterior salió bien — si algún
movimiento quedó fuera de su ámbito, esta migración no entra (research D-04).

### Tests ⚠️

- [ ] T018 [US2] Escribir `backend/GestionGastos.Api.Tests/Integracion/AmbitoDeCategoriaEsquemaTests.cs` con los tres casos de escritura directa de `FR-006` y `FR-009`: `INSERT` con categoría ajena rechazado, `UPDATE` a categoría ajena rechazado, `INSERT` con categoría propia aceptado (`SC-002`). Verlos en rojo — **hoy el tercero ya pasa**, así que el rojo tiene que venir de los dos primeros
- [ ] T019 [US2] Escribir en ese mismo archivo el test de que `usuario_id` de `movimiento` participa de **dos** claves foráneas a la vez, la de `usuario` y la nueva. Es lo que research D-01 marcó como "hay que probarlo, no suponerlo". Verlo en rojo

### Implementation

- [ ] T020 [US2] Agregar la clave alternativa `(id, usuario_id)` de `categoria` y la foránea compuesta `(categoria_id, usuario_id) → categoria (id, usuario_id)` de `movimiento` en `backend/GestionGastos.Api/Persistencia/GestionGastosDbContext.cs`, y quitar la foránea simple (data-model)
- [ ] T021 [US2] Crear la segunda migración, `<fecha>_AmbitoDeCategoriaEnLaBase.cs`, con esos dos cambios de esquema
- [ ] T022 [US2] Revisar lo que EF generó para `IX_movimiento_categoria_id` y no dejar dos índices que empiezan por la misma columna (data-model)
- [ ] T023 [US2] VERIFY: `dotnet test backend/` completo en verde. La suite entera es parte de este AC: la restricción no puede estar rechazando ningún caso legítimo (escena 4 de US2)

### La barrera de la restricción (Principio V)

- [ ] T024 [US2] Escribir `backend/verificar-ambito-de-categoria.sh`: quita la foránea compuesta, exige que `AmbitoDeCategoriaEsquemaTests` se ponga en **rojo**, la restaura y exige el verde. Sería la octava barrera del proyecto (`FR-019`, research D-09)
- [ ] T025 [US2] Correrla y ver los dos estados con la salida a la vista. Una barrera que nunca se vio fallar no es una barrera
- [ ] T026 [P] [US2] Registrarla en `backend/GestionGastos.Api.Tests/Integracion/BarrerasEjecutablesTests.cs` si ese test enumera las barreras existentes
- [ ] T027 [P] [US2] Agregarla al workflow en `.github/workflows/ci.yml`, junto a las demás

**Checkpoint**: D7-07 saldada. La lectura por navegación `m.Categoria!.Nombre` pasa a ser segura
**por construcción** y no por suerte.

---

## Phase 5: User Story 4 - Un catálogo que ahora es enteramente mío (Priority: P2)

**Goal**: desaparece la categoría de solo lectura, en el contrato y en la pantalla. Y recién acá,
con todos sus usos ya fuera, la propiedad deja de ser anulable.

**Independent Test**: con una cuenta recién registrada, renombrar y dar de baja cualquiera de las
diez; ninguna responde `403` y ninguna fila queda sin botones.

**El orden de esta fase importa**: el cambio de `UsuarioId` a no-anulable va **último**, cuando ya
no queda ningún `is null` que compilar. Puesto primero, rompe cinco archivos a la vez — que es el
problema que la fase 2 evita.

### Tests ⚠️

- [X] T028 [P] [US4] Cambiar en `backend/GestionGastos.Api.Tests/Integracion/CategoriasPropiasTests.cs` los dos casos que hoy esperan `403` —renombrar y dar de baja una predefinida— por casos que esperan que la operación **funcione** (`FR-015`, `SC-007`), y sacar los usos de `EsPropia` (líneas 67, 96, 101, 106, 140, 163, 220, 221, 273, 339, 363, 542 y el record `CategoriaVista`). Verlos en rojo
- [X] T029 [P] [US4] Sacar `EsPropia` de la comparación en `backend/GestionGastos.Api.Tests/Integracion/AislamientoDeCategoriasTests.cs` (línea 76), **conservando intacto** el test cruzado `Un_Movimiento_No_Puede_Apuntar_A_Una_Categoria_Ajena_FR021_SC009`, que sigue cubriendo `FR-007`
- [X] T030 [P] [US4] Actualizar `frontend/tests/PantallaCategorias.test.tsx` para exigir que **todas** las filas ofrezcan renombrar y dar de baja (`FR-017`), y sacar `esPropia` de los datos de prueba. Verlo en rojo
- [ ] T031 [US4] Escribir en `backend/GestionGastos.Api.Tests/Integracion/CategoriasPropiasTests.cs` los casos de `FR-005`: dos cuentas distintas pueden tener cada una su "Comida" de gasto, y dentro de una misma cuenta una segunda activa con ese nombre y tipo se rechaza con `400` y la clave de su campo. **Es el requisito cuyo mecanismo de garantía cambia en silencio**: hasta hoy el índice único no alcanzaba —para MySQL "sin dueño" y "dueño 7" son claves distintas, así que una propia podía llamarse igual que una predefinida (D-02 de la 007)— y al desaparecer el `NULL` empieza a cubrirlo de verdad. El test fija qué se espera, independientemente de quién lo garantice. Verlo en rojo
- [ ] T032 [P] [US4] Escribir los casos de `FR-008` en `backend/GestionGastos.Api.Tests/Integracion/AltaMovimientoTests.cs` y `EdicionDeMovimientoTests.cs`: el alta sigue rechazando una categoría dada de baja, y la edición sigue aceptándola **cuando es la que el movimiento ya tenía**. Es no-regresión de la 007 (`FR-022`, `FR-023` de esa spec), y el cambio de modelo pasa cerca. Verlos en rojo
- [ ] T033 [P] [US4] Escribir en `backend/GestionGastos.Api.Tests/Integracion/CategoriasEndpointTests.cs` el caso de `FR-004`: el catálogo devuelve **sólo las activas del ámbito** —ni las dadas de baja, ni ninguna de otra cuenta—, ordenadas por tipo y después por identificador. Hoy lo cubre de refilón el cambio de predicado de T039; esto lo verifica de frente. Verlo en rojo

### Implementation

- [X] T034 [US4] Sacar `EsPropia` de `CategoriaDto` en `backend/GestionGastos.Api/Categorias/CategoriaDto.cs` **y** `esPropia` de la interfaz `Categoria` en `frontend/src/api/tipos.ts`, **en esta misma tarea**: los tests de contrato comparan en las dos direcciones y quedan en rojo mientras sólo una pila haya cambiado. Ese rojo intermedio es el esperado, no un fallo a investigar (`FR-016`, `FR-018`, contracts §1)
- [X] T035 [US4] Eliminar el `if (categoria.UsuarioId is null)` y su `403` de `backend/GestionGastos.Api/Categorias/CategoriasEndpoints.cs` (líneas ~178 y ~218), dejando el `404` como única respuesta para lo que no es del ámbito. **La categoría propia de otra cuenta sigue respondiendo `404` y no `403`** (contracts §2)
- [X] T036 [US4] Quitar la condición `categoria.esPropia` de `frontend/src/categorias/PantallaCategorias.tsx` y su comentario de `FR-008 de la 007`: todas las filas llevan botones
- [X] T037 [P] [US4] Sacar `esPropia` de los datos de prueba de `frontend/tests/App.test.tsx` (5 usos) y `frontend/tests/FormularioMovimiento.test.tsx` (1 uso)
- [ ] T038 [US4] VERIFY parcial: las dos pilas en verde con la propiedad todavía anulable

### El último paso: el modelo se entera

- [ ] T039 [US4] `UsuarioId` deja de ser anulable en `backend/GestionGastos.Api/Dominio/Categoria.cs`, y `CategoriasConsulta.DelAmbito` pierde el `|| c.UsuarioId == null` en `backend/GestionGastos.Api/Categorias/CategoriasConsulta.cs`. **Es una sola tarea porque es un solo cambio**: a esta altura ya no queda ningún otro uso que romper
- [ ] T040 [US4] Reescribir los dos comentarios que quedaron mintiendo: el de `DelAmbitoPorId`, que explica una distinción `403`/`404` que ya no existe, y el de `Homonimas`, cuya razón de ser ya no es "el índice deja pasar una propia homónima de una predefinida" —sin `NULL`, el índice cubre el caso entero— sino devolver un `400` con su campo en vez de un choque de índice convertido en `500`. **La comprobación se conserva**; lo que cambia es por qué (research D-08)
- [ ] T041 [US4] VERIFY: `dotnet format --verify-no-changes`, `dotnet build -warnaserror`, `dotnet test backend/`, `pnpm --dir frontend lint`, `pnpm --dir frontend exec tsc --noEmit` y `pnpm --dir frontend test`, todos en verde

**Checkpoint**: las cuatro historias funcionando y el `NULL` fuera del modelo.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T042 Reescribir la fila **D7-07** en `specs/007-categorias-propias/spec.md` (línea 402): hoy propone un trigger o una clave foránea compuesta, y los dos se midieron y fallaron. Dejar anotado cómo se saldó de verdad y por qué el camino era otro
- [ ] T043 [P] Anotar **D13-01** en la tabla de deuda de esta feature: la barrera de aislamiento vigila el texto `contexto.Categorias`, así que una escritura por propiedad de navegación la esquivaría. Hoy no existe ninguna; es el gemelo del agujero de lectura que originó D7-07 (research D-06)
- [ ] T044 [P] Agregar a la tabla de *Stack* de `AGENTS.md` la barrera nueva **y `backend/verificar-nota.sh`**, que falta desde la feature 012: la tabla documenta seis barreras y el repo tiene siete
- [ ] T045 [P] Actualizar `plan-de-implementacion/README.md` con la feature cerrada y la deuda saldada, como hicieron las features 016 y 017
- [ ] T046 Correr la puerta de cierre completa de [quickstart.md](./quickstart.md) §6: cobertura, las **ocho** barreras y el build de producción del frontend, con la salida a la vista
- [ ] T047 Comparar la foto de totales de T002 contra la base ya migrada y confirmar que no se movió un número (`SC-003`)

---

## Dependencies & Execution Order

### El orden real, que no es negociable

```
Setup (T001-T003)
   ↓
Foundational (T004-T008)              ← el catálogo inicial existe y nadie lo llama
   ↓
US3 + US1  la base y el alta (T009-T017)   ← una sola puerta para T014 y T015
   ↓
US2  restricción + barrera (T018-T027)     ← segunda migración; verifica a la anterior
   ↓
US4  contrato, pantalla y modelo (T028-T041)  ← el no-anulable va último
   ↓
Polish (T042-T047)
```

### Por qué este orden y no otro

- **La propiedad de C# sigue anulable hasta T039.** Volverla no-anulable antes rompe cinco archivos
  a la vez y deja tareas que no pueden terminar en verde, contra el Principio III. Lo que cambia
  primero es la base; la aplicación se entera al final, cuando ya no queda ningún `is null` que
  compilar.
- **La migración de datos y el alta de cuenta van juntas** (T014 y T015, una sola puerta en T017).
  La migración borra las diez categorías compartidas: desde ese instante, una cuenta sin catálogo
  propio no puede cargar un movimiento.
- **La restricción va después de los datos, y en su propia migración.** Antes de migrar sólo
  adelanta el fallo; después, es la verificación de que la migración salió bien, y separada en su
  archivo un fallo señala el paso correcto (research D-04).

### Lo que NO es independiente, dicho en voz alta

La plantilla pide historias independientes y entregables de a una. **Acá no lo son**: US1, US2 y US3
son las tres P1 y las tres tocan el mismo modelo; US1 y US3 comparten literalmente una puerta. No hay
MVP parcial que se pueda desplegar — una base a medio migrar no es un producto reducido, es una base
rota. La única entrega es el conjunto.

### Parallel Opportunities

- T002 y T003 entre sí
- T006 mientras se escribe T005 (archivos distintos)
- T026 y T027 entre sí
- T028, T029 y T030 entre sí (tres archivos distintos, dos pilas)
- T037 mientras se hace T036
- T043, T044 y T045 entre sí
- **No** son paralelas aunque lo parezcan: T009 y T010 comparten `MigracionDeCatalogoTests.cs`;
  T011, T012 y T013 comparten `AltaDeCuentaTests.cs`; T018 y T019 comparten
  `AmbitoDeCategoriaEsquemaTests.cs`; y T028 y T031 comparten `CategoriasPropiasTests.cs`. Van en
  secuencia

### Dentro de cada historia

- El test se escribe y **se ve en rojo** antes del código (Principio I)
- La puerta de VERIFY de cada grupo en verde antes de pasar al siguiente (Principio III)

---

## Implementation Strategy

### No hay MVP parcial

Esta feature entra entera o no entra. El punto de corte más temprano que deja la base **consistente**
es el final de US2 (T027): ahí los datos están migrados, la restricción puesta y la invariante
sostenida. US4 es lo único diferible — si se frenara ahí, el contrato quedaría con un `esPropia` que
siempre vale `true` y una pantalla que esconde botones sin motivo. Feo, pero no roto.

### Puntos de control

1. **T008** — el catálogo inicial existe; nada más cambió
2. **T017** — los datos viejos migrados y las cuentas nuevas con su catálogo
3. **T027** — D7-07 saldada, con su barrera vista en rojo
4. **T041** — las cuatro historias y el `NULL` fuera del modelo
5. **T047** — los números no se movieron

---

## Notes

- `[P]` = archivos distintos, sin dependencias pendientes
- Commit por tarea o por grupo lógico, **nunca con la puerta en rojo** (Principio III)
- `verificar-monedas.sh` exige los dos árboles limpios: commiteá antes de correrla
- `verificar-aislamiento.sh` tarda ~4 min **medidos** y `verificar-contrato.sh` ~2,5 min: no los
  corras en cada tarea, corré lo que toca la tarea y todo antes de cerrar
- El rojo de los tests de contrato en T034 es esperado y dura lo que dura la tarea

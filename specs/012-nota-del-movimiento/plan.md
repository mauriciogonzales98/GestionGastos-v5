# Implementation Plan: Nota descriptiva del movimiento

**Branch**: `012-nota-del-movimiento` | **Date**: 2026-09-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/012-nota-del-movimiento/spec.md`

## Summary

Cada movimiento puede llevar una nota de texto libre, opcional, de hasta 120 caracteres, que se
escribe al registrar, se corrige o se vacía al editar, y se lee en el listado. La categoría dice de
qué tipo es un gasto; la nota dice cuál fue.

**Es la primera feature en cinco que construye las dos mitades.** Las 008 a 011 se encontraron el
backend hecho una y otra vez; acá la verificación contra el código devolvió cero: no hay columna, ni
propiedad en el dominio, ni campo en ninguno de los tres DTO, ni control en el formulario, ni columna
en el listado, ni test de rendimiento del listado. Y es la primera desde la 007 que **abre una
migración**, que es lo que permite saldar de paso una deuda que venía esperando exactamente eso desde
la feature 009.

El enfoque está en [research.md](./research.md), trece decisiones (D-01 a D-13). Las cuatro que más
condicionan el resto:

- **La normalización de lectura vive en el tipo, no en los cuatro lugares que lo construyen** (D-04).
  `MovimientoDto` se arma en cuatro puntos de los endpoints y `FR-011` exige que los cuatro devuelvan
  lo mismo para una fila guardada sin valor. En el tipo, el quinto punto hereda la regla sin saber que
  existe.
- **El límite se cuenta en caracteres Unicode en las tres capas** (D-02). Es la unidad que
  `varchar(120)` ya usa, así que una nota que las dos validaciones aceptan entra siempre en la columna.
- **`FR-007` se verifica con una barrera, y el criterio de la 011 da acá el resultado opuesto** (D-09).
  Afirmar que la nota **no** aparece en el filtro del listado es una afirmación de ausencia hecha
  inspeccionando texto: informa verde cuando es cierta y cuando la inspección dejó de mirar. Es la
  forma exacta de `verificar-desglose.sh`.
- **Una sola migración con los dos cambios** (D-10). La nota y la restricción de tres letras sobre el
  código de moneda. Con el plan DISC-001 terminándose acá, ya no hay un próximo ticket al que
  apuntarle la deuda.

**Lo que esta feature no hace, y está blindado**: no se busca, no se filtra, no se agrupa y no se
totaliza por la nota (`FR-007`). La barra de filtros existe desde la 011 y sumarle un campo de texto
costaría muy poco — y es exactamente lo que convertiría la nota en la segunda taxonomía informal que
`PRD:RF-33` evita.

## Technical Context

**Language/Version**: TypeScript 6.x (frontend) · C# / .NET 10 (SDK 10.0.301) (backend)

**Primary Dependencies**: React 19 + Vite 8 · EF Core 9.0.18 + Pomelo.MySQL 9.0.0. **Ninguna
dependencia nueva** (`NFR-005`, comprobada contra los manifiestos al cerrar). Contar caracteres
Unicode y recorrer una cadena por code points son operaciones de la biblioteca estándar de las dos
plataformas (D-02)

**Storage**: MySQL 8.4.10, esquema `gestiongastos`; los tests contra `gestiongastos_test`. **Con
migración, la primera desde la feature 007**: agrega `movimiento.nota` (`varchar(120)` anulable) y la
restricción de tres letras sobre `moneda.codigo` (D-01, D-10, ver [data-model.md](./data-model.md))

**Testing**: xUnit (backend) · Vitest 4 sobre happy-dom 20 (frontend). **Un test de rendimiento nuevo**
—el del listado, que no existía— y **una barrera de shell nueva**, `verificar-nota.sh` (D-09, D-11)

**Target Platform**: navegador moderno + API HTTP sobre Linux/Windows. Ancho objetivo 360 px, heredado
de la feature 011: la columna nueva entra por el envoltorio desplazable que ya está puesto

**Performance Goals**: el listado con 1000 movimientos **con nota** en menos de 2 s en el p95 sobre 100
ejecuciones (`NFR-003`, `PRD:AC-10`). Medido sobre la respuesta de la API, no sobre el navegador, y la
spec lo dice (D-11). Referencia de magnitud: el resumen agrupa las mismas 1000 filas en 6 ms

**Constraints**: 120 caracteres Unicode exactos, ni uno más ni uno menos · 0 lugares donde se pueda
filtrar, ordenar o agrupar por la nota · 0 variación en totales, balance y desglose · 0 dependencias
nuevas · 0 tests existentes tocados fuera de la tabla de D-12 · 0 columnas de texto que hagan desbordar
la página a 360 px

**Scale/Scope**: 1 migración con 2 cambios de esquema · 1 propiedad de dominio · 3 DTO del backend y 3
tipos del contrato · 4 puntos de construcción del DTO que pasan a heredar una regla · 1 validación
compartida por alta y edición · 1 campo de formulario que sirve a las dos pantallas que escriben · 1
columna de listado · 1 barrera nueva · 1 test de rendimiento nuevo · 7 tests existentes en el
presupuesto de D-12

## Constitution Check

*GATE: se evalúa antes de Phase 0 y otra vez después de Phase 1.*

| Principio | Cómo lo cumple este plan | Estado |
|---|---|---|
| **I · Test-First** | El rojo de esta feature es el más honesto de las últimas cinco, porque **no hay nada construido**: no existe la columna, ni el campo del DTO, ni el control, ni la columna del listado. Ningún primer test puede pasar por accidente. El rojo más barato está en el contrato (D-13, paso 2): agregar `nota` a `tipos.ts` pone en rojo los tests de `Contrato/` antes de que exista el campo en el DTO, y el `switch` del caso del alta **lanza una excepción con instrucciones** en vez de un fallo opaco. El único rojo que hay que mirar con cuidado es el de `FR-011`: tiene que fallar contra una fila guardada **sin valor**, no contra una fila sin nota cualquiera, o estaría verificando otra cosa | ✅ |
| **II · Cada AC tiene su test, y el test lo nombra** | Los 13 FR y 5 NFR de la spec, más los 10 AC del PRD del ticket, citan su identificador en el nombre del test. Se cierran además **cuatro AC del PRD del producto que nunca tuvieron test** —`AC-50` a `AC-53`, los de `RF-33`— y `AC-10` del ticket estrena la medición del listado, que no existía. `PRD:AC-55` ya tiene test y **se extiende, no se duplica** (D-07, fila 2 de D-12) | ✅ |
| **III · VERIFY es una fase con puerta** | Una tarea VERIFY al cierre de cada historia. La Historia 1 y la 2 tocan **las dos pilas** —migración, contrato y pantalla—, así que su puerta es completa de los dos lados desde el principio; no hay acá el atajo de las cuatro historias de sólo frontend que tuvo la 011. La Historia 3 toca sólo el esquema. Al cierre, las dos pilas, cobertura, el build de producción y **siete** barreras | ✅ |
| **IV · Tests deterministas y aislados** | Nada nuevo depende de la fecha real: el sembrado de rendimiento genera fechas ancladas al año de la fecha que recibe, que es la lección de FIX-004 y ya está resuelta en `SembradoDeRendimiento`. El test de `FR-011` escribe las dos representaciones de "sin nota" **explícitamente** en vez de esperar que alguna aparezca sola. Ningún número fijo sobre el tamaño del catálogo de monedas (regla D-10 de la 009), que el caso de `FR-010` podría romper con facilidad | ✅ |
| **V · Las barreras se verifican a sí mismas** | La barrera nueva (D-09) **se prueba contra el cambio que tiene que impedir**: se le agrega a la consulta el acotado por nota, se exige el rojo, se restaura y se exige el verde. Es la misma forma de las seis que ya existen. Las seis no se tocan, y dos mira esta feature de cerca: `verificar-contrato.sh`, porque el campo viaja en las tres formas del movimiento, y `verificar-aislamiento.sh`, porque la nota es **el primer campo de texto libre que el aislamiento tiene que tapar** | ✅ |

**Resultado**: sin violaciones. *Complexity Tracking* queda vacío.

**Cinco cosas que la puerta va a exigir y conviene saber antes de empezar:**

1. **La puerta de cierre pasa a durar ~13 min de barreras**, porque son siete. `verificar-aislamiento.sh`
   solo tarda ~7 min y `verificar-contrato.sh` ~2,5. No se saltean por apuro; se arrancan temprano.
2. **`verificar-monedas.sh` exige los dos árboles limpios** o no puede distinguir lo que ensució ella de
   lo que ya estaba sucio. Commitear antes de correr las barreras.
3. **`FR-010` podía romper `verificar-monedas.sh`, y no lo hace. Verificado, no supuesto.** Esa barrera
   agrega una moneda al catálogo con SQL puro: si su código no fuera tres letras, la restricción nueva
   lo rechazaría y la barrera fallaría por una razón que no es la que vigila. Siembra con **`XTS`**
   —que ISO 4217 reserva para pruebas— y los códigos que usan los tests son `XCA`, `XCE`, `XCT`,
   `XED`, `XEL`, `XMV`, `XPF`, `XSC` y `EUR`: **los diez son tres letras**, así que la restricción es
   compatible con todo lo que el proyecto siembra hoy. Era el único acoplamiento real de la Historia 3,
   y está cerrado antes de empezar.
4. **Hay una migración, así que `dotnet test` la aplica.** `BaseDeDatosFixture` sólo acepta
   `gestiongastos_test` o `gestiongastos_migracion_test` y limpia tablas: apuntarlo al esquema de
   desarrollo se lleva los datos puestos.
5. **El linter del backend corre con `-warnaserror` y no perdona `Migrations/`… salvo que ya lo haga.**
   `verificar-linter.sh` comprueba que una violación deliberada rompa el build en código escrito a mano
   y **no** lo rompa dentro de `Migrations/`. La migración nueva entra en la carpeta exenta, así que no
   hay nada que ajustar — pero es la primera vez en cinco features que esa exención se usa de verdad.

### Reevaluación después de Phase 1

La constitución exige evaluar la puerta **otra vez** con el diseño escrito. Hecho sobre
[research.md](./research.md), [data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md)
y [quickstart.md](./quickstart.md):

**Resultado: sigue sin violaciones.** El diseño no agregó ninguna capa, ninguna carpeta, ninguna
dependencia y ningún endpoint. Tres cosas que la Phase 1 cambió respecto de la evaluación previa, y
ninguna en contra:

1. **El diseño encontró un lugar único donde no lo había** (D-04). La evaluación previa daba por hecho
   que `FR-011` se cumpliría en los cuatro puntos que construyen el DTO; el diseño lo reduce a uno. Es
   Principio IV reforzado: la regla se hereda por construcción en vez de depender de cuatro acuerdos.
2. **El acoplamiento de riesgo se cerró antes de empezar.** La evaluación previa dejaba
   `verificar-monedas.sh` como algo a mirar; [data-model.md](./data-model.md) lo verificó contra los
   doce códigos que el proyecto siembra hoy. Es Principio V: la barrera existente no cambia de
   resultado, y eso se comprobó en vez de suponerse.
3. **La barrera nueva quedó más delicada de lo que parecía, y eso la justifica más** (D-09). La nota
   tiene que aparecer en la proyección de la consulta y nunca en su filtro, así que la afirmación es más
   fina que la de `verificar-desglose.sh` — que busca una palabra que no debe aparecer en ningún lado.
   Una verificación más fina es exactamente la que hay que **ver fallar** antes de creerle.

**Lo que la Phase 1 confirmó que hay que vigilar durante la implementación**, y que no se resuelve
escribiendo un documento: el rojo de `FR-011` tiene que producirse contra una fila guardada **sin
valor**. Un test que use una fila cualquiera sin nota puede pasar en verde desde el principio —si la
fila se guardó con la cadena vacía, no hay nada que normalizar— y entonces la barrera de `FR-011`
existiría sin haber verificado nunca lo suyo. Es el único rojo de la feature que se puede fingir sin
darse cuenta.

## Project Structure

### Documentation (this feature)

```text
specs/012-nota-del-movimiento/
├── plan.md              # Este archivo
├── research.md          # Phase 0: las trece decisiones
├── data-model.md        # Phase 1: el esquema, la migración y las dos restricciones
├── quickstart.md        # Phase 1: cómo se valida a mano lo que ningún test cubre
├── contracts/
│   └── api.md           # Phase 1: las tres formas del movimiento con la nota
├── checklists/
│   └── requirements.md  # De /speckit-specify, revalidado por /speckit-clarify
└── tasks.md             # Phase 2: lo genera /speckit-tasks, NO este comando
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/
│   ├── Dominio/
│   │   └── Movimiento.cs               # + Nota (string?)
│   ├── Movimientos/
│   │   ├── MovimientoDtos.cs           # + Nota en las tres formas; la normalización de D-04
│   │   ├── ValidacionDelMovimiento.cs  # + la regla del largo, clave de error `nota`
│   │   └── MovimientosEndpoints.cs     # el alta y la edición pasan la nota al dominio
│   ├── Persistencia/
│   │   └── GestionGastosDbContext.cs   # la columna y la restricción de FR-010
│   └── Migrations/
│       └── <fecha>_NotaDelMovimiento.cs  # NUEVO: la columna + el CHECK de tres letras
├── GestionGastos.Api.Tests/
│   ├── Contrato/ContratoMovimientosTests.cs      # fila 1 de D-12
│   ├── Movimientos/                              # fila 7 de D-12, + los casos nuevos
│   ├── Integracion/BarreraDeLaNotaTests.cs       # NUEVO: la nota no entra en el filtro
│   └── Rendimiento/RendimientoListadoTests.cs    # NUEVO: lo que AC-10 pide y nadie medía
└── verificar-nota.sh                             # NUEVO: la séptima barrera

frontend/
├── src/
│   ├── api/
│   │   ├── tipos.ts                    # + nota en las tres formas del movimiento
│   │   └── cliente.ts                  # el alta y la edición la mandan
│   ├── movimientos/
│   │   ├── CamposDelMovimiento.tsx     # + el campo, + `nota` en CAMPOS_CON_LUGAR
│   │   └── ListadoMovimientos.tsx      # + la columna
│   └── estilos/
│       └── componentes.css             # la regla de ancho de la columna nueva
└── tests/
    ├── NotaDelMovimiento.test.tsx      # NUEVO: el campo, el límite, el texto plano
    ├── TecladoFormulario.test.tsx      # fila 2 de D-12
    ├── ListadoMovimientos.test.tsx     # fila 3 de D-12
    ├── cliente.test.ts                 # fila 4 de D-12
    └── VentanaDeEdicion.test.tsx       # fila 5 de D-12
```

**Structure Decision**: la estructura del proyecto no cambia. Backend y frontend siguen separados en
sus carpetas, y la única excepción declarada del proyecto —los tests de `Contrato/` leen
`frontend/src/api/tipos.ts`, por [ADR-001](../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md)—
sigue siendo la única y sigue siendo lectura en una sola dirección. El archivo de la barrera nueva va
en `backend/`, donde están las otras seis.

**Ninguna carpeta nueva.** El test de la barrera va a `Integracion/`, donde ya viven
`BarreraDeAislamientoTests` y `BarreraDelDesgloseTests` — las dos que inspeccionan el SQL de una
consulta, que es exactamente lo que hace la nueva. No nace ninguna capa ni ninguna carpeta.

## Complexity Tracking

*Sin violaciones de la constitución. Tabla vacía a propósito.*

Vale anotar igual la única decisión que **agrega** peso al proyecto, para que se vea que se pesó:
`verificar-nota.sh` suma ~1 min a una puerta de cierre que ya dura ~12. Se acepta porque el criterio
escrito por la feature 011 —una barrera existe cuando un test verde no se distingue de un test que dejó
de verificar— da positivo acá por dos razones independientes (D-09), y porque el `FR-007` que protege es
**la decisión de producto central del ticket**: sin verificación, es una intención escrita en un
documento.

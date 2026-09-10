# Implementation Plan: Maquetación, filtros del listado y accesibilidad

**Branch**: `011-maquetacion-filtros-accesibilidad` | **Date**: 2026-09-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/011-maquetacion-filtros-accesibilidad/spec.md`

## Summary

La aplicación se termina de vestir y se termina de usar. Dos mitades que el plan trata como una sola
feature porque comparten la misma superficie —todas las pantallas— y la misma puerta:

- **La pasada de maquetación y accesibilidad** que el ticket 6 encarga: una paleta declarada donde
  hoy no hay ni un color, una regla para cada una de las cinco clases que el código nombra sin
  respaldo, 360 px de ancho utilizable, y el piso de `PRD:RNF-06` verificado sobre la aplicación
  entera en vez de ticket por ticket.
- **Las tres deudas que la 010 mandó acá**: el borrado de un movimiento, los dos acotados del
  listado que nunca tuvieron control, y el formato del monto según los decimales de su moneda.

**Casi no hay backend, otra vez.** `GET /api/movimientos` acota por `desde`, `hasta`, `categoriaId` y
`monedaId` desde FEAT-001b; `DELETE /api/movimientos/{id}` existe, está probado y **nunca tuvo un
cliente**. El único cambio del servidor en toda la feature es un campo más en `MonedaDto`:
`decimales`, que está en la base desde FEAT-001a y no salía a la red porque nadie lo consumía.

El enfoque técnico está en [research.md](./research.md), trece decisiones (D-01 a D-13). Las tres que
más condicionan el resto:

- **La paleta cambia de lado** (D-01): la declara el CSS y el verificador la lee del archivo. Es la
  inversión de lo que hizo la 010, con el mismo argumento —una sola fuente— aplicado a un caso que
  dejó de ser cuatro colores de un componente.
- **El intérprete del período no se duplica** (D-05): `ControlesDelPeriodo` sube de `dashboard/` a
  `periodo/` y lo usan las dos pantallas. `PeriodoPedido` sigue siendo el único que decide qué es un
  rango válido.
- **El presupuesto de tests tocables está escrito de antemano** (D-12), fila por fila y con el
  requisito que justifica cada una. Es lo que convierte a `SC-007` en algo verificable en vez de en
  una intención.

## Technical Context

**Language/Version**: TypeScript 6.x (frontend) · C# / .NET 10 (SDK 10.0.301) (backend)

**Primary Dependencies**: React 19 + Vite 8 · EF Core 9.0.18 + Pomelo.MySQL 9.0.0. **Ninguna
dependencia nueva** (`NFR-004`, verificada contra los manifiestos al cerrar): sin librería de
accesibilidad, sin navegador sin cabeza, sin framework de estilos. Los tres verificadores que leen
del disco se escriben con `fs` y expresiones regulares (D-02), y la cuenta de contraste ya existe
desde la 010

**Storage**: MySQL 8.4.10, esquema `gestiongastos`; los tests contra `gestiongastos_test`. **Sin
migración**: `moneda.decimales` está en la base desde la migración `Inicial` (ver
[data-model.md](./data-model.md))

**Testing**: xUnit (backend) · Vitest 4 sobre happy-dom 20 (frontend), más **tres archivos de test
en entorno `node`** —clases, paleta y anchos— sobre un helper compartido que lee del disco (D-02)

**Target Platform**: navegador moderno + API HTTP sobre Linux/Windows. **Ancho objetivo: 360 px**
(`FR-003`), decisión de este proyecto y no de `PRD-001`

**Performance Goals**: sin objetivos nuevos. `NFR-005` sólo pide no empeorar: una sola consulta al
servidor por cambio de acotado, que es lo que D-06 garantiza con el botón único

**Constraints**: 0 clases sin regla · 0 pantallas con desborde horizontal a 360 px · 100 % de los
controles con foco visible y etiqueta · 4,5:1 y 3:1 en la paleta · 0 dependencias nuevas · 0 tests
existentes tocados fuera de la tabla de D-12

**Scale/Scope**: 5 superficies (acceso, movimientos, ventana de edición, categorías, dashboard) · 1
DTO de backend tocado · 1 tipo del contrato tocado · 1 endpoint estrenado del lado del cliente
(`DELETE`) · 2 acotados enchufados · ~10 archivos de frontend tocados y 2 nuevos · 3 archivos CSS
que pasan de 139 líneas a la hoja de estilos real del proyecto

## Constitution Check

*GATE: se evalúa antes de Phase 0 y otra vez después de Phase 1.*

| Principio | Cómo lo cumple este plan | Estado |
|---|---|---|
| **I · Test-First** | Cada tarea lleva su test antes y su rojo mostrado. Acá el rojo es barato y honesto en las cinco historias: no hay botón de eliminar, no hay control de categoría, no hay `decimales` en el contrato y no hay ni un color declarado. El primer test de cada historia falla por ausencia, no por compilación. **El caso delicado es `FR-001`**: su test tiene que verse fallar contra una entrada con una clase sin regla antes de que se escriban las reglas — y hoy hay cinco de verdad, así que el rojo inicial es el estado real del proyecto | ✅ |
| **II · Cada AC tiene su test, y el test lo nombra** | Los 20 FR y 5 NFR de la spec citan su identificador en el nombre del test. Se cierran además **cuatro AC del PRD del producto que hoy no tienen ninguna pantalla** —`AC-21`, `AC-23`, `AC-24`, `AC-26` (`SC-009`)— y los nueve AC del PRD del ticket 6. `PRD:AC-55` ya tiene test (`TecladoFormulario.test.tsx`): se extiende a las demás pantallas, no se duplica | ✅ |
| **III · VERIFY es una fase con puerta** | Una tarea VERIFY al cierre de cada historia. Cuatro de las cinco tocan **sólo frontend**, así que su puerta es `lint` + `tsc --noEmit` + `test`, que corre en segundos. La Historia 5 toca el contrato y arrastra la puerta del backend entera. Al cierre de la feature, las dos pilas, cobertura y las **seis** barreras | ✅ |
| **IV · Tests deterministas y aislados** | Nada nuevo depende de la fecha real: el rango del listado entra como texto y lo interpreta el servidor, igual que en el dashboard. Los verificadores de D-02 leen archivos del repositorio, que no cambian entre corridas. **Se hereda la regla D-10 de la 009**: ningún número fijo sobre el tamaño del catálogo de monedas, y ahora tampoco sobre la cantidad de colores de la paleta | ✅ |
| **V · Las barreras se verifican a sí mismas** | Esta feature **no agrega ninguna barrera de shell**, y D-10 escribe el criterio con el que se decidió. Lo que sí estrena son cuatro verificadores, y se les aplica la misma regla: **cada uno se prueba contra una entrada que tiene que dar rojo** (D-03), la de anchos incluida — que es la que estuvo a punto de quedar afuera por verificar reglas en vez de medir. Las cinco barreras existentes no se tocan; `verificar-monedas.sh` es la que más de cerca mira esta feature, porque `FR-019` toca las dos pilas | ✅ |

**Resultado**: sin violaciones. *Complexity Tracking* queda vacío.

**Cuatro cosas que la puerta va a exigir y conviene saber antes de empezar:**

1. **`verificar-monedas.sh` es la barrera crítica de esta feature.** `FR-019` toca `formatearMonto`,
   que es exactamente el código que esa barrera vigila en las dos pilas: sumar una moneda al catálogo
   tiene que seguir costando 0 líneas. Un `decimales` cableado a mano en el frontend la rompe, y ése
   es el punto.
2. **`verificar-monedas.sh` exige los dos árboles limpios antes de correr**, o no puede distinguir lo
   que ensució ella de lo que ya estaba sucio. Commitear antes.
3. **La Historia 5 pone en rojo los tests de contrato en el momento en que `decimales` aparezca de un
   solo lado.** Es lo esperado y es el orden correcto: primero el tipo del frontend, se ve el rojo,
   después el DTO.
4. **El cierre corre las seis barreras: ~11 minutos.** No se acorta.

### Re-evaluación después de Phase 1

Los artefactos de diseño no introdujeron ninguna violación. Tres puntos que el diseño **agregó** y
que refuerzan la constitución en vez de tensionarla:

- **D-12 convierte `SC-007` en algo que se puede verificar.** "No modificar tests para acomodar un
  cambio visual" es, sin una lista, una intención que cada quien interpreta al momento de tener el
  test rojo delante. Con la tabla escrita antes de empezar, cualquier test fuera de ella es una
  señal, y agregarle una fila cuesta nombrar el requisito que la justifica. Es el Principio I
  aplicado al alcance en vez de a una función.
- **D-03 evita que los cuatro verificadores nuevos nazcan sin haberse visto fallar.** Es la trampa que la
  010 ya encontró con la cuenta de contraste, y que `verificar-desglose.sh` documenta desde la 007:
  hasta la feature que lo descubrió, la suite entera estaba en verde con la barrera rota.
- **D-01 elige el lado en vez de dejar dos.** La regla de la 010 —una sola fuente para los colores—
  se cumple mejor invirtiéndola que copiándola, y el motivo (el verificador cubre lo que alguien
  agregue sin agendarlo) es el mismo `NFR-003` que gobierna la comprobación de clases. Las dos
  verificaciones terminan con la misma forma, que es una señal de que la forma es la correcta.

**Una tensión que el diseño no resuelve y no disimula**: `FR-003` no se verifica midiendo, se
verifica por regla (D-04). El plan lo dice en el nombre de la tarea, en el quickstart y en la deuda
**D11-01**. Un verde ahí significa "ninguna regla de estilo puede producir desborde", no "no
desborda".

## Project Structure

### Documentation (this feature)

```text
specs/011-maquetacion-filtros-accesibilidad/
├── plan.md              # Este archivo
├── spec.md              # Qué y por qué, con la verificación contra el código
├── research.md          # D-01 a D-13: las decisiones de diseño
├── data-model.md        # Lo que se lee y el único campo que empieza a viajar
├── contracts/api.md     # `decimales` en GET /api/monedas, y los cuatro acotados que ya existen
├── quickstart.md        # Los pasos a mano, incluidos los que ningún test puede cubrir
├── checklists/
│   └── requirements.md  # 16/16 en verde
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── GestionGastos.Api/
│   └── Monedas/MonedasEndpoints.cs              # ÚNICO cambio de producción: MonedaDto suma Decimales (D-08)
└── GestionGastos.Api.Tests/
    └── Contrato/                                # se ponen en rojo solos cuando el campo aparece de un lado

frontend/
├── src/
│   ├── api/tipos.ts                             # Moneda suma `decimales`; AcotadoDelListado suma categoriaId, desde, hasta
│   ├── api/cliente.ts                           # eliminarMovimiento() NUEVO; obtenerMovimientos acota por cuatro
│   ├── estilos/
│   │   ├── base.css                             # NUEVO contenido: la paleta en :root, tipografía y espaciados (D-01)
│   │   ├── disposicion.css                      # las reglas de l-* que faltan, y la barra de filtros
│   │   └── componentes.css                      # las 5 clases sin regla, y la tabla desplazable a 360 px
│   ├── ui/
│   │   ├── contraste.ts                         # deja de tener colores; se queda con la cuenta (D-01)
│   │   └── formatearMonto.ts                    # recibe los decimales del catálogo (D-08)
│   ├── periodo/
│   │   └── ControlesDelPeriodo.tsx              # MOVIDO desde dashboard/: ahora lo usan dos pantallas (D-05)
│   ├── movimientos/
│   │   ├── FiltrosDelListado.tsx                # NUEVO: categoría + rango + moneda, un solo Aplicar (D-06)
│   │   ├── ListadoMovimientos.tsx               # la columna de eliminación, con su confirmación (D-07)
│   │   └── PantallaMovimientos.tsx              # el acotado de cuatro, el borrado y el foco después (D-09, D-11)
│   ├── dashboard/PantallaDashboard.tsx          # importa ControlesDelPeriodo de su lugar nuevo
│   └── resumen/GastosPorCategoria.tsx           # deja de inyectar variables de color (D-01)
└── tests/
    ├── fuentes.ts                               # NUEVO: la lectura del disco que comparten los verificadores
    ├── ClasesConRegla.test.ts                   # NUEVO (entorno node): FR-001, con su caso rojo (D-02, D-03)
    ├── Paleta.test.ts                           # NUEVO (entorno node): NFR-001 sobre la paleta del CSS
    ├── AnchoDeLasPantallas.test.ts              # NUEVO (entorno node): FR-003/FR-004 por regla (D-04)
    ├── Accesibilidad.test.tsx                   # NUEVO: foco y etiqueta en las cinco superficies (NFR-002)
    ├── FiltrosDelListado.test.tsx               # NUEVO
    ├── EliminarMovimiento.test.tsx              # NUEVO
    ├── Contraste.test.ts                        # se queda con la cuenta; la paleta la mide Paleta.test.ts (D-12)
    ├── ListadoMovimientos.test.tsx              # eliminación y decimales (D-12)
    ├── PantallaMovimientos.test.tsx             # el acotado con botón (D-12)
    ├── PantallaDashboard.test.tsx               # el import nuevo (D-12)
    ├── GastosPorCategoria.test.tsx              # sin inyección de color (D-12)
    └── monedas.fixture.ts                       # `decimales` en el fixture (D-12)
```

**Structure Decision**: se mantiene la separación `backend/` / `frontend/` de `AGENTS.md`, con su
única excepción declarada ([ADR-001](../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md)),
que esta feature **sí ejercita**: `decimales` cambia el contrato, así que los tests de `Contrato/`
tienen algo nuevo que leer de `frontend/src/api/tipos.ts`. Es la primera vez desde la 009.

Una carpeta nueva de frontend, `periodo/`, y ninguna capa nueva. Sigue la misma frontera que la 010
trazó entre `resumen/` y `dashboard/`: **lo que pinta algo que dos pantallas usan, sube; lo que
decide qué pedir, se queda en la pantalla**. `ControlesDelPeriodo` devuelve dos fechas tal como se
escribieron; qué se pide con ellas —un listado o un resumen— lo decide cada pantalla.

Los tres archivos nuevos de test que corren en entorno `node` van en `frontend/tests/` como el
resto, con la directiva de entorno arriba. No hay una carpeta aparte: son tests del frontend, y
partirlos por su entorno de ejecución escondería que verifican requisitos de la misma spec.

## Complexity Tracking

Sin violaciones de la constitución que justificar.

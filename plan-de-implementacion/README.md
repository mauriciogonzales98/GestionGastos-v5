# Plan de implementación — copia consolidada

Copia de solo lectura de lo que hace falta para construir este proyecto **desde cero**: el PRD del
producto, el plan con el orden y sus razones, y los PRD de los tickets.

> **Es una copia parcial, y conviene saberlo antes de leerla.** La carpeta llegó con el commit de
> arranque de v5 (`3a863ff`) trayendo el PRD del producto, el plan y **dos** PRD de ticket. Los
> demás PRD que este README nombraba —los de FEAT-001a/b/c, FIX-001 a FIX-004 y FEAT-003— nunca
> entraron a este repositorio, y la carpeta `docs/daw/` que figuraba como fuente viva tampoco
> existe acá. La columna *PRD* de las tablas dice, para cada ticket, si su archivo está o no.
>
> El estado de cada ticket de las tablas de abajo está **verificado contra el código**, no contra
> lo que decía la foto original.

## Contenido

| Archivo | Qué es |
|---------|--------|
| `PRD.md` | PRD de referencia del producto, con todas las modificaciones aplicadas |
| `plan-DISC-001.md` | El plan: concepto, decisiones, mapa de dependencias y orden de los tickets |
| `prds/implementados/` | Los PRD de tickets ya mergeados a `main` que están en este repositorio |
| `prds/pendientes/` | Los PRD de los tickets que faltan |

## Recorrido completo, en orden

### Ya implementado

Verificado contra el código el 2026-09-03.

| Orden | Ticket | Título | Qué lo demuestra en el código | PRD |
|-------|--------|--------|-------------------------------|-----|
| 1 | FEAT-001a | Alta de movimientos y listado simple | `POST` y `GET /api/movimientos` | no está en el repo |
| 2 | FIX-001 | Linter del backend .NET (D-1) | `backend/.editorconfig`, `verificar-linter.sh` | no está en el repo |
| 3 | FIX-002 | Bit de ejecución de `verificar-linter.sh` | los cuatro scripts en `100755` | no está en el repo |
| 4 | FEAT-003 | Alineación verificada del contrato (D-2) | `verificar-contrato.sh`, `Contrato/` | no está en el repo |
| 5 | FIX-004 | El sembrado de rendimiento vence el 2027-01-01 (D-3) | `Rendimiento/SembradoDeRendimientoTests.cs` | no está en el repo |
| 6 | DISC-001-01a | Identidad y sesión | `Sesion/`, `Cuentas/` | `prds/implementados/prd-DISC-001-01a.md` |
| 7 | DISC-001-01b | Límite de intentos fallidos | `specs/003-limite-intentos/` | `prds/implementados/prd-DISC-001-01b.md` |
| 8 | DISC-001-01c | Aislamiento entre cuentas verificado | `specs/004-aislamiento-cuentas/`, `verificar-aislamiento.sh` | `prds/pendientes/prd-DISC-001-01c.md` |
| 9 | FEAT-001b | Filtros del listado, edición y eliminación | `specs/005-filtros-edicion-eliminacion/`, `GET`/`PUT`/`DELETE /api/movimientos/{id}` | no está en el repo |
| 10 | FEAT-001c | Resumen del mes con desglose por categoría | `specs/006-resumen-del-mes/`, `GET /api/resumen`, `Resumenes/` | no está en el repo |
| 11 | DISC-001-03 | Categorías propias del usuario | `specs/007-categorias-propias/`, `Categorias/` con su canal y su validación, `POST`/`PUT`/`DELETE /api/categorias`, la migración `DiscriminadorDeCategoria`, `frontend/src/categorias/PantallaCategorias.tsx` y `verificar-desglose.sh` | `prds/pendientes/prd-DISC-001-03.md` |
| 12 | DISC-001-04a | Catálogo de monedas y totales por moneda | `specs/008-monedas-como-dato/`, `verificar-monedas.sh`. **La mayor parte ya venía construida**: la tabla `moneda` y su semilla salieron con FEAT-001a, y el resumen por moneda con FEAT-001c. Esta feature la **verificó** —que sumar una moneda cueste 0 líneas y 0 recompilaciones, y que la separación aguante 1000 movimientos en dos monedas— y documentó requisito por requisito qué ya estaba hecho | `prds/pendientes/prd-DISC-001-04a.md` |
| 13 | DISC-001-04b | Registrar y filtrar en varias monedas | `specs/009-elegir-y-filtrar-moneda/`, `GET /api/monedas`, `monedaId` en el alta, la edición y el acotado del listado, `Monedas/`, `CamposDelMovimiento.tsx` y `VentanaDeEdicion.tsx`. **Saldó las tres deudas que la 008 le dejó**: D8-01 (rechazar una moneda fuera del catálogo, que hasta acá no se podía ni probar porque no había entrada que validar), D8-02 y D8-03. Extendió `verificar-monedas.sh` a `frontend/src/`: la promesa de que sumar una moneda cuesta 0 líneas sólo estaba protegida del lado del backend | `prds/pendientes/prd-DISC-001-04b.md` |

| 14 | DISC-001-05 | Dashboard con gráficos | `specs/010-dashboard-con-graficos/`, el resumen del mes en la pantalla principal (`frontend/src/resumen/`) y el dashboard con su período y su filtro de moneda (`frontend/src/dashboard/`), más `ui/contraste.ts` y `docs/adr/ADR-002`. **Saldó las dos deudas que la 009 le dejó**: D9-06 (la vista de totales, que no existía en ninguna pantalla) y D9-02 (filtrar por moneda). **No tocó el backend**: `GET /api/resumen` ya devolvía lo que hacía falta desde FEAT-001c, así que los únicos cambios de `backend/` son dos tests. Y **corrigió una premisa del PRD**: decía que el volumen de 10000 movimientos nunca se había medido, y `RendimientoResumenTests` lo medía desde la 006 — da 33 ms contra un techo de 4000 | `prds/pendientes/prd-DISC-001-05.md` |

Los FIX/FEAT de infraestructura (D-1 a D-4) salieron intercalados porque el usuario decidió cerrar
la deuda de infraestructura antes de seguir con features de producto; el mapa de
`plan-DISC-001.md` los muestra como prerequisitos de todo lo demás. Empezando de cero, el orden
2→5 puede ir antes del 1: son barreras de calidad que cuanto antes estén, menos trabajo tapan.

| 15 | DISC-001-06 | Maquetación y accesibilidad | `specs/011-maquetacion-filtros-accesibilidad/`, la paleta en `frontend/src/estilos/base.css` con sus verificadores (`ClasesConRegla`, `Paleta`, `AnchoDeLasPantallas`, `Accesibilidad`), el borrado de un movimiento, `frontend/src/movimientos/FiltrosDelListado.tsx` y `frontend/src/periodo/`. **Se amplió respecto de su PRD**, por decisión del usuario: además de la pasada de maquetación y accesibilidad saldó las tres deudas que la 010 le mandaba —**D10-01** (la barra de filtros y la eliminación), **D10-02** (el formato del monto) y **D10-07** (la paleta)—, y con ellas **D8-05**, **D9-01** y **D9-05**. **Estrenó el borrado**: `DELETE /api/movimientos/{id}` existía desde FEAT-001b y era el único endpoint de la API que nunca había tenido un cliente. Y **cerró la mitad que faltaba de D10-10**: el fallo intermitente sin identificar de la 010 apareció con nombre —`RendimientoLimiteTests…AC12`— y pasó a ser D11-03 | `prds/pendientes/prd-DISC-001-06.md` |

| 16 | DISC-001-02 | Nota descriptiva del movimiento | `specs/012-nota-del-movimiento/`, `movimiento.nota` con su migración, el campo de varias líneas en `CamposDelMovimiento.tsx`, la séptima columna del listado y la normalización de lectura en `MovimientoDto`. **Estrenó dos cosas y saldó una deuda vieja**: la séptima barrera del proyecto (`verificar-nota.sh`, que impide que la nota se vuelva un eje de clasificación — `PRD:RF-33`) y la **primera medición del listado** (`RendimientoListadoTests`: no existía ninguna, así que la afirmación de `RNF-01` sobre esa consulta no estaba respaldada por nada; da 17 ms contra un techo de 2000). Y saldó **D11-02** —el `CHECK` de tres letras sobre `moneda.codigo`, que venía de la 009 como D9-09 y de la 010 como D10-03— en la misma migración, que era lo único que le faltaba: **no era una deuda teórica**, había tres códigos inválidos en los fixtures del propio proyecto pasando en verde desde la 009 | `prds/pendientes/prd-DISC-001-02.md` |

### Pendiente

**Ninguno. El plan DISC-001 está completo.**

Los dieciséis tickets del recorrido están implementados y verificados contra el código. Lo que quedó
abierto son deudas anotadas, no trabajo pendiente del plan, y viven en la tabla *Deuda registrada* de
la spec de cada feature — la última es
[`specs/012-nota-del-movimiento/spec.md`](../specs/012-nota-del-movimiento/spec.md).

**Un detalle que conviene saber al leer esas tablas**: hasta la feature 011, cada deuda que no se
saldaba se le apuntaba a "el próximo ticket". Con el plan terminado **ya no hay próximo ticket**, así
que las que sigan abiertas necesitan que alguien decida hacerlas — no van a caer solas dentro de otra
feature.

### Trabajo posterior al plan

Deudas saldadas por decisión, ya sin ticket que las arrastre. Van acá porque no son features y no
entran en la tabla de arriba.

| Rama | Deuda | Qué se decidió |
|------|-------|----------------|
| `013` | **D12-02** — el techo de 50 ms de `003:AC-12`, abierta desde la feature 009 como D9-08 y reanotada en la 010, la 011 y la 012 | Medida, resultó que **el techo no era el problema**: los 50 ms de `PRD:NFR-02` quedan igual y lo que cambió es el **estimador**, de percentil 95 a mediana. La distribución es bimodal y los 50 ms caen adentro de su cola, así que el p95 sobre n=100 pasaba o fallaba según la tasa de atascos. Además **la premisa estaba al revés** —los atascos son de máquina fría, no de contención: bajo carga 0 de 500 muestras llegaron a 20 ms, aislado 11— y el p95 **no** protegía la cota de la purga, que era lo único que podía justificarlo. El detalle está en el ajuste de [`specs/003-limite-intentos/spec.md`](../specs/003-limite-intentos/spec.md) |

**El ticket 4b dejó dos cosas sin construir, y están anotadas.** La barra de filtros de categoría y
fecha y la interfaz de eliminación —la mitad de frontend de FEAT-001b, que salió como feature de
backend— quedaron para el ticket 6; la vista de totales, para el ticket 5. El criterio con el que se
cortó: lo que todavía no tiene pantalla es porque está más adelante en el plan, no porque se haya
olvidado.

**El ticket 3 es el primero que toca las dos pilas.** Los diez anteriores dejaron el backend con
sus endpoints y el frontend con el contrato declarado; éste agrega los tres endpoints de categorías
**y** la pantalla que los usa, que es la primera pantalla nueva desde el acceso. También estrenó la
quinta barrera del proyecto, `verificar-desglose.sh`, y con ella saldó la deuda D6-04 que la feature
006 había dejado anotada: el desglose del resumen no puede empezar a filtrar por `categoria.activa`,
y ahora hay algo que se pone en rojo si lo hace.

**FEAT-001b y FEAT-001c eran los que más destrababan, y ya están.** Entre los dos aportaron los
cuatro endpoints que faltaban —`GET`, `PUT` y `DELETE /api/movimientos/{id}`, y `GET /api/resumen`—
y con ellos quedaron saldados los cinco criterios de aislamiento que la feature 004 no había podido
verificar: AC-02, AC-03, AC-04, AC-05 y AC-07. La tabla de *Deuda registrada* de
`specs/004-aislamiento-cuentas/spec.md` quedó sin ninguna fila pendiente.

Las dos features fueron **de backend**: el frontend recibió sólo la declaración del contrato en
`frontend/src/api/tipos.ts`. Las pantallas que consumen esos endpoints —el listado con filtros, la
edición, y el gráfico que grafique el resumen— son de los tickets 5 y 6.

## Lo que no está acá

Los informes de validación (`*.validation.md`), los RCA, los modelos de amenazas y los informes de
verificación y SAST: nunca entraron a este repositorio. Los ADR sí están, en `docs/adr/`. Las specs
y sus planes viven en `specs/`. Tampoco hay nada de FEAT-002: ese ticket se abandonó sin llegar a
tener PRD.

FIX-003 —"Comparación por subcadena en `validate_prd.py`"— figuraba como implementado en la foto
original y no aplica acá: `validate_prd.py` no existe en este repositorio.

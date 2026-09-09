---

description: "Task list for 011-maquetacion-filtros-accesibilidad"
---

# Tasks: Maquetación, filtros del listado y accesibilidad

**Input**: Design documents from `/specs/011-maquetacion-filtros-accesibilidad/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md)

**Tests**: obligatorios. No es una opción de esta feature: el Principio I de
`.specify/memory/constitution.md` prohíbe escribir código de producción sin un test que ya haya
fallado.

**Organization**: por historia de usuario, en orden de prioridad.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[TEST]**: escribe una verificación · **[ROJO]**: la corre y exige que falle · **[VERIFY]**: puerta
- **[Story]**: US1, US2, US3, US4, US5

---

## Lo que conviene leer antes de empezar

**1 · El rojo inicial de US1 no hay que fabricarlo: ya está.** Hay **cinco** clases referenciadas sin
regla —`c-campo`, `c-formulario-acceso`, `c-formulario-movimiento__error`, `c-resumen`,
`c-totales-moneda`— y **cero colores declarados**. El primer test de la feature falla contra el
estado real del proyecto, que es la mejor forma del Principio I que se puede pedir.

**2 · Dos verificadores nuevos, y los dos tienen que verse fallar** ([D-03](./research.md)). No
alcanza con que den verde sobre la aplicación: cada uno lleva **su propia tarea** que lo corre contra
una entrada sintética que debe dar rojo. Es el Principio V aplicado a algo que no es un `.sh`, y es
la trampa que `verificar-desglose.sh` documenta desde la 007: hasta la feature que lo descubrió, la
suite entera estaba en verde con la barrera rota.

**3 · La tabla de [D-12](./research.md) es el presupuesto, y es cerrada.** Siete tests existentes se
pueden tocar, cada uno con el requisito que lo justifica. **Cualquier test fuera de esa tabla que
haya que retocar es la señal de que la maquetación se comió el comportamiento**, que es exactamente
lo que `FR-020` y `SC-007` existen para atrapar. Agregar una fila cuesta nombrar el requisito en el
mensaje del commit; si no hay ninguno, lo que hay que cambiar es el código.

**4 · La paleta va primero y no es una preferencia** ([D-13](./research.md)). `NFR-001` mide
contraste sobre la paleta declarada, y hoy los dos colores que existen están anotados en el código
como *"los del navegador, no una elección"*. Medir antes de elegirlos es medir contra relleno y tener
que rehacerlo.

**5 · `AnchoDeLasPantallas.test.ts` no mide anchos, y se llama por lo que hace**
([D-04](./research.md)). jsdom no maqueta. El test comprueba **reglas de estilo**; que efectivamente
no desborde es el paso 1 del [quickstart](./quickstart.md) y la deuda **D11-01**. No escribir un
test que afirme "no desborda": estaría afirmando algo que no midió.

**6 · `verificar-monedas.sh` es LA barrera de esta feature.** `FR-019` toca `formatearMonto`, que es
el código que esa barrera vigila en las dos pilas. Un `{ ARS: 2, JPY: 0 }` escrito a mano en el
frontend dejaría toda la suite en verde con la promesa de `RF-32` rota del único lado que el usuario
mira. Y exige los **dos árboles limpios**: commitear antes de correrla.

**7 · La confirmación del borrado ya está escrita, en otra pantalla** ([D-07](./research.md)).
`PantallaCategorias` usa dos botones y un `role="alert"` que dice la consecuencia entera, y explica
por qué no usa `window.confirm`: *"ése no se puede maquetar —el ticket 6 no podría tocarlo—"*. Ese
comentario se escribió para este ticket. Se reutiliza el patrón, no se inventa uno.

**8 · Ningún test escribe un número fijo sobre el catálogo de monedas** (regla D-10 de la 009), ni
**sobre la cantidad de colores de la paleta**. Los dos se derivan de lo que se leyó. Ya se rompió una
vez por lo primero.

---

## Phase 1: Setup

**Purpose**: saber de qué verde se parte, para que cualquier rojo posterior sea atribuible.

- [X] T001 Correr la puerta completa de las dos pilas sobre la rama recién sacada —`pnpm --dir frontend lint`, `pnpm --dir frontend format`, `pnpm --dir frontend exec tsc --noEmit`, `pnpm --dir frontend test`, `dotnet format backend/GestionGastos.slnx --verify-no-changes`, `dotnet build backend/GestionGastos.slnx -warnaserror`, `dotnet test backend/`— y anotar el conteo de tests de cada una. Es la línea de base: sin ella, un rojo del primer día se confunde con uno heredado. Anotar también, en el mismo lugar, **la lista de las cinco clases sin regla**, que es el rojo que US1 tiene que ver

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: la lectura de archivos que US1 y US2 comparten, escrita una sola vez.

**⚠️ CRÍTICO**: los tres verificadores de US1 y el de US2 leen los mismos archivos del disco. Tres
copias del recorrido divergen el día que alguien agregue una carpeta.

- [X] T002 Crear `frontend/tests/fuentes.ts`: un módulo que devuelve el contenido de todos los `frontend/src/**/*.tsx` y de todos los `frontend/src/estilos/*.css`, recorriendo el árbol con `fs` y **sin ninguna lista escrita a mano** (`NFR-003`). Dejar escrita en el archivo la razón: una pantalla agregada en el ticket 2 tiene que quedar cubierta sola, igual que una moneda agregada al catálogo. Exportar además la extracción de clases referenciadas y la de reglas declaradas, que son las dos mitades que `ClasesConRegla.test.ts` compara
- [X] T003 [VERIFY] Puerta del frontend (`lint` + `tsc --noEmit` + `test`), con su salida. Un helper que no compila bloquea las dos primeras historias

---

## Phase 3: US1 — La aplicación se ve terminada (P1) 🎯 MVP

**Goal**: una paleta declarada donde hoy no hay ni un color, una regla para cada clase que el código
nombra, y 360 px de ancho utilizable.

**Independent Test**: comparar las clases referenciadas en el código con las reglas de la hoja de
estilos y exigir 0 sin regla; abrir cada pantalla a 360 px y verificar que nada desborda.

### Los verificadores, y su rojo

- [X] T004 [TEST] [P] [US1] Crear `frontend/tests/ClasesConRegla.test.ts` (entorno `node`): usando `fuentes.ts`, **0 clases `l-`, `c-` o `u-` referenciadas en el código sin una regla en la hoja de estilos** (`FR-001`, `NFR-003`, `PRD-06:AC-04`). La aserción reporta **cuáles** faltan, no sólo cuántas: un fallo que dice "esperaba 0, recibí 5" obliga a averiguar a mano lo que el test ya sabe
- [X] T005 [TEST] [P] [US1] En `frontend/tests/ClasesConRegla.test.ts`: **el verificador se ve fallar** ([D-03](./research.md)). Se le pasa como texto un fragmento que referencia una clase que la hoja sintética no declara, y se exige que la detecte. Sin esto, el día que la expresión regular deje de reconocer un `className` informaría 0 clases sin regla por no haber encontrado ninguna clase
- [X] T006 [TEST] [P] [US1] Crear `frontend/tests/Paleta.test.ts` (entorno `node`): extraer de `frontend/src/estilos/base.css` las propiedades personalizadas de color y medir con `relacionDeContraste` cada par que una tabla de umbrales declare — **4,5:1 en texto normal, 3:1 en texto grande y en componentes** (`NFR-001`, `PRD:RNF-06`, `PRD-06:AC-06`). **Ningún número fijo sobre cuántos colores hay**: la cantidad se deriva de lo que se leyó (punto 8)
- [X] T007 [TEST] [P] [US1] En `frontend/tests/Paleta.test.ts`: **el verificador se ve fallar** ([D-03](./research.md)), con un par declarado por debajo de su umbral. Es la misma forma que la 010 le dio a `Contraste.test.ts`, y por el mismo motivo
- [X] T008 [TEST] [P] [US1] Crear `frontend/tests/AnchoDeLasPantallas.test.ts` (entorno `node`): ninguna regla de `frontend/src/estilos/*.css` declara un ancho fijo mayor a 360 px, y el contenedor de la tabla del listado declara desborde horizontal desplazable (`FR-003`, `FR-004`). **El nombre del test y su descripción dicen que verifica reglas, no anchos** ([D-04](./research.md)): jsdom no maqueta, y un test que afirmara "no desborda" estaría afirmando algo que no midió
- [X] T009 [TEST] [P] [US1] En `frontend/tests/AnchoDeLasPantallas.test.ts`: **el verificador se ve fallar** ([D-03](./research.md)), con una hoja sintética que declara un ancho fijo de 400 px y con otra cuyo contenedor de tabla no declara desborde desplazable. Es el tercero de los tres verificadores que leen del disco y le corresponde su caso rojo igual que a los otros dos: sin él, el día que la expresión regular deje de reconocer una declaración de ancho informaría 0 violaciones por no haber encontrado ninguna regla
- [X] T010 [ROJO] [US1] Correr `pnpm --dir frontend test` y exigir **ROJO** en T004, T006 y T008, verificando que el motivo es el estado real del proyecto —las cinco clases sin regla de T001 y la ausencia de paleta— y no un import roto. T005, T007 y T009 tienen que dar **verde** ya: verifican que el verificador detecta, no que la aplicación cumple. Mostrar la salida

### La implementación

- [X] T011 [US1] Declarar la paleta en `frontend/src/estilos/base.css`, bajo `:root`: texto, fondo, acento, error, la barra del desglose y su riel (`FR-002`, [D-01](./research.md)). Dejar escrito en el archivo **por qué el CSS y no el módulo TypeScript** —un color existe antes de que corra JavaScript, y el verificador que lee el archivo cubre cualquier color que alguien agregue sin agendarlo— y que ése es el reemplazo del comentario que hoy dice que colores y tipografía son del ticket 6. Verde de T006
- [X] T012 [US1] Sacar los colores de `frontend/src/ui/contraste.ts`, que se queda **sólo con la cuenta**, y hacer que `frontend/src/estilos/componentes.css` tome `--color-*` directamente. `frontend/src/resumen/GastosPorCategoria.tsx` deja de inyectar variables en su elemento. Actualizar `frontend/tests/Contraste.test.ts` y `frontend/tests/GastosPorCategoria.test.tsx` — **están en la tabla de D-12, filas 1 y 2**
- [X] T013 [P] [US1] Escribir en `frontend/src/estilos/componentes.css` y `disposicion.css` las reglas de las cinco clases sin regla, más tipografía y espaciados. Respetar la regla que `disposicion.css` ya declara: **nada de clases utilitarias sueltas**, y un `c-` no define su propia posición ni su margen externo. Verde de T004
- [X] T014 [US1] Las reglas de 360 px en `frontend/src/estilos/componentes.css` y `disposicion.css`: el contenedor desplazable de la tabla del listado, y quitar todo ancho fijo mayor al objetivo (`FR-003`, `FR-004`). Verde de T008
- [X] T015 [VERIFY] [US1] Puerta del frontend completa, con su salida

**Checkpoint**: el proyecto tiene paleta y ninguna clase huérfana. `SC-001` y `SC-004` en verde;
`SC-002` en verde **por la vía de la regla**, con el paso manual pendiente.

---

## Phase 4: US2 — Se usa entera con el teclado y se deja leer (P2)

**Goal**: el piso de `PRD:RNF-06` verificado sobre las cinco superficies, no ticket por ticket.

**Independent Test**: recorrer con el teclado los controles de cada pantalla y exigir foco visible y
etiqueta accesible en cada uno.

**Depende de US1**: el foco visible se dibuja con `currentcolor` sobre la paleta nueva, y hasta que
la paleta exista no hay contra qué medirlo.

- [X] T016 [TEST] [P] [US2] Crear `frontend/tests/Accesibilidad.test.tsx`: por cada una de las **cinco superficies** —acceso, movimientos, la ventana de edición, categorías y dashboard— montar la pantalla y exigir que **todo** control interactivo tenga un nombre accesible no vacío (`FR-005`, `NFR-002`). La lista de controles se **deriva del árbol montado**, no se escribe: un control agregado después queda cubierto solo
- [X] T017 [TEST] [P] [US2] En `frontend/tests/Accesibilidad.test.tsx`: **el verificador se ve fallar** ([D-03](./research.md)), montando un árbol sintético con un control sin etiqueta. Un recorrido que no encuentra controles informa verde igual que uno que los encontró todos etiquetados
- [X] T018 [TEST] [P] [US2] En `frontend/tests/Accesibilidad.test.tsx`: el orden del foco corresponde al orden de lectura de cada pantalla (`FR-006`, `PRD-06:AC-08`), afirmado sobre el orden real del DOM
- [X] T019 [TEST] [P] [US2] En `frontend/tests/TecladoFormulario.test.tsx`: con el foco en el último control del formulario de registro, avanzar **saca el foco del formulario** y no lo deja atrapado (`FR-007`, `PRD-06:AC-09`). El recorrido completo y el envío ya están cubiertos por el test que existe desde FEAT-001a (`PRD:AC-55`): **se extiende, no se duplica**
- [X] T020 [TEST] [P] [US2] En `frontend/tests/VentanaDeEdicion.test.tsx`: con la ventana abierta el foco no se escapa al fondo, y al cerrarse vuelve a un lugar previsible (`FR-009`). Lo da el `<dialog>` nativo con `showModal()` desde la feature 005 — **este test afirma lo que la plataforma ya hace**, que es lo que impide que un refactor a un `<div role="dialog">` lo pierda en silencio
- [X] T021 [TEST] [P] [US2] En `frontend/tests/Accesibilidad.test.tsx`: en las tres pantallas con formulario, un campo inválido expone su motivo **asociado al campo** —`aria-describedby` y `aria-invalid`— y no como un cartel suelto (`FR-008`, `PRD-06:AC-03`). `CampoConError` ya arma la tripleta en un solo lugar: el test verifica que **todas** las pantallas pasen por él
- [X] T022 [ROJO] [US2] Correr `pnpm --dir frontend test` y exigir **ROJO** en T016, T018, T019 y T021, con el motivo verificado en cada caso. T017 y T020 pueden dar verde ya —el primero verifica al verificador, el segundo lo que el `<dialog>` nativo aporta— y eso también hay que decirlo en la salida. Mostrarla
- [X] T023 [US2] Poner las etiquetas y los nombres accesibles que falten en `frontend/src/acceso/`, `frontend/src/movimientos/`, `frontend/src/categorias/` y `frontend/src/dashboard/`. Los botones de una fila dicen **sobre qué actúan** —igual que `Renombrar {nombre}` en categorías—, no sólo "Editar". Verde de T016
- [X] T024 [US2] Revisar el foco visible contra la paleta de US1 en `frontend/src/estilos/base.css`: el `outline: 3px solid currentcolor` heredado tiene que seguir viéndose sobre los fondos nuevos, y su contraste entra en la tabla de umbrales de `Paleta.test.ts`. **El `outline` no se anula sin reemplazo**, que es la regla que el archivo ya declara
- [X] T025 [US2] Corregir, en los componentes de `frontend/src/` que lo necesiten, el orden del DOM donde el foco no siga el orden de lectura y el mensaje de error que no esté asociado a su campo vía `frontend/src/ui/CampoConError.tsx`. Verde de T018, T019 y T021
- [X] T026 [VERIFY] [US2] Puerta del frontend completa, con su salida

**Checkpoint**: `PRD:RNF-06` verificado sobre la aplicación entera. `SC-003` en verde. La mitad del
ticket 6 está entregada.

---

## Phase 5: US3 — Borrar un movimiento que cargué mal (P3)

**Goal**: `PRD:RF-15` y `PRD:AC-21`, el único requisito del PRD del producto sin una línea de
pantalla. El endpoint existe desde FEAT-001b y nunca tuvo cliente.

**Independent Test**: eliminar un movimiento del listado y verificar que desaparece y que su monto
deja de sumar en el resumen del mes.

- [X] T027 [TEST] [P] [US3] En `frontend/tests/cliente.test.ts`: `eliminarMovimiento(id)` pide `DELETE /api/movimientos/{id}`, resuelve con un `204`, lanza `ErrorDeSesion` con un `401` y propaga el `404`. **El `404` no se interpreta**: el servidor responde lo mismo si no existe, si es de otra cuenta o si ya se borró, y el cliente no debe intentar distinguirlos ([contracts/api.md](./contracts/api.md))
- [X] T028 [TEST] [P] [US3] Crear `frontend/tests/EliminarMovimiento.test.tsx`: apretar "Eliminar" en una fila muestra la confirmación de dos botones con la consecuencia **dicha entera**, y cancelar **no llama al servidor** (`FR-010`, `FR-011`, `SC-005`). Es el patrón de `PantallaCategorias`, no un `window.confirm` ([D-07](./research.md))
- [X] T029 [TEST] [P] [US3] En `frontend/tests/EliminarMovimiento.test.tsx`: confirmar quita la fila del listado y **vuelve a pedir el resumen**, sin recargar la pantalla y sin volver a pedir el listado (`FR-012`, `PRD:AC-21`, [D-11](./research.md))
- [X] T030 [TEST] [P] [US3] En `frontend/tests/EliminarMovimiento.test.tsx`: un `404` muestra el motivo y **deja de mostrar la fila**; un `401` vuelve al acceso con un aviso que dice qué pasó con ese movimiento (`FR-013`). El aviso del `401` sigue la forma de los que ya existen para el alta y la edición: la pantalla está por desaparecer, así que el mensaje tiene que decir qué pasó con **este** movimiento
- [X] T031 [TEST] [P] [US3] En `frontend/tests/EliminarMovimiento.test.tsx`: eliminar la **única** fila deja el mensaje de vacío del listado, no una tabla sin filas; y después de que la fila desaparece **el foco no queda en el principio de la página** ([D-09](./research.md)). El segundo es la variante de `FR-005` que sólo aparece cuando algo se borra, y por eso ninguna pantalla anterior se la encontró
- [X] T032 [TEST] [P] [US3] En `frontend/tests/ListadoMovimientos.test.tsx`: el botón de eliminar **nombra sobre qué actúa** —la fecha y el monto del movimiento—, no sólo "Eliminar" (`FR-005`). **Está en la tabla de D-12, fila 5**
- [X] T033 [ROJO] [US3] Correr `pnpm --dir frontend test` y exigir **ROJO** en T027 a T032, verificando que el motivo es la ausencia del botón y de la función del cliente. Mostrar la salida
- [X] T034 [US3] `eliminarMovimiento(id)` en `frontend/src/api/cliente.ts`, con la forma de `darDeBajaCategoria`: pasa por `pedirSinCuerpo`, que ya sabe tratar un `204` y convertir un `401` en `ErrorDeSesion`. Verde de T027
- [X] T035 [US3] La columna de eliminación con su confirmación en `frontend/src/movimientos/ListadoMovimientos.tsx` (`FR-010`). Verde de T028 y T032
- [X] T036 [US3] El borrado en `frontend/src/movimientos/PantallaMovimientos.tsx`: quita la fila del estado, llama a `recargarResumen()` **siempre** —sin averiguar antes si el movimiento caía en el mes, que es la clase de cuenta que la pantalla no hace—, lleva el foco a un destino estable y anuncia por la región `role="status"` que ya existe. Dejar escrito en el código, como comentario, que **eliminar con la ventana de edición abierta sobre esa misma fila no es alcanzable** porque el `<dialog>` modal vuelve inerte el fondo: es una decisión documentada, no un caso olvidado. Verde de T029, T030 y T031
- [X] T037 [VERIFY] [US3] Puerta del frontend completa, con su salida

**Checkpoint**: `PRD:AC-21` cubierto. El único endpoint de la API sin cliente ya no lo es.

---

## Phase 6: US4 — Ver sólo lo que estoy buscando (P4)

**Goal**: los dos acotados que el servidor resuelve desde FEAT-001b y que nunca tuvieron control.
Salda **D9-01** / **D10-01**.

**Independent Test**: elegir una categoría y un rango y verificar que el listado muestra únicamente
lo que cae dentro de los dos.

- [X] T038 [TEST] [P] [US4] En `frontend/tests/cliente.test.ts`: `obtenerMovimientos` acota por los **cuatro** parámetros, y **lo que vale `null` no se manda** — el servidor entiende la ausencia, y mandar `desde=` vacío lo obligaría a interpretar una cadena vacía como fecha. Sin argumentos, ningún parámetro sale ([contracts/api.md](./contracts/api.md))
- [X] T039 [TEST] [P] [US4] Crear `frontend/tests/FiltrosDelListado.test.tsx`: los tres controles se aplican con **un solo botón** y producen **una sola petición**; escribir una fecha no dispara ninguna (`NFR-005`, [D-06](./research.md)). Y **dos "Aplicar" seguidos**: si la primera respuesta resuelve última, **no pisa** al listado de la segunda. El botón único hace el caso menos frecuente, no imposible, y es la cicatriz `22e3e96` de la feature 009 — la guarda `vigente` existe por eso y este test es lo que impide que un refactor se la lleve puesta
- [X] T040 [TEST] [P] [US4] En `frontend/tests/FiltrosDelListado.test.tsx`: elegir una categoría muestra únicamente los de esa categoría (`PRD:AC-23`), y sin elegir ninguna se ven los de todas (`PRD:AC-24`, `FR-014`). El selector ofrece **sólo categorías activas**, que es lo que el catálogo devuelve
- [X] T041 [TEST] [P] [US4] En `frontend/tests/FiltrosDelListado.test.tsx`: un rango elegido incluye **los dos extremos** (`PRD:AC-26`), y sin rango elegido el control **muestra el mes actual sin que la pantalla lo haya calculado** (`PRD:AC-25`, `FR-015`). **De dónde sale ese mes**: `GET /api/movimientos` devuelve un arreglo pelado y no dice qué período aplicó, pero `GET /api/resumen` **sí devuelve `desde` y `hasta`**, y la pantalla principal ya tiene ese `Resumen` cargado. El control se prefija desde ahí: el dato lo sigue decidiendo el servidor y no aparece un segundo intérprete ([D-05](./research.md)). Y con el rango prefijado, la petición **igual se manda sin parámetros** mientras la persona no lo toque
- [X] T042 [TEST] [P] [US4] En `frontend/tests/FiltrosDelListado.test.tsx`: un rango invertido y un rango con un solo extremo muestran **el mensaje del servidor al lado de los campos**, bajo la clave `rango`, y el listado **conserva lo que estaba mostrando** (`FR-018`). La pantalla no reimplementa las reglas del período: `PeriodoPedido` es el único intérprete ([D-05](./research.md))
- [X] T043 [TEST] [P] [US4] En `frontend/tests/FiltrosDelListado.test.tsx`: los tres acotados combinados muestran sólo lo que cumple los tres (`FR-016`), y una combinación sin resultados **lo dice, sin ningún error** (`AC-08` de la historia)
- [X] T044 [TEST] [P] [US4] En `frontend/tests/PantallaMovimientos.test.tsx`: cambiar un acotado **vuelve a pedir al servidor** y no filtra en la pantalla la lista que ya tenía (`FR-017`). Filtrar del lado del cliente se vería igual y mostraría sólo lo que ya se había traído. **Está en la tabla de D-12, fila 3**: el acotado por moneda pasa a aplicarse con el botón
- [X] T045 [ROJO] [US4] Correr `pnpm --dir frontend test` y exigir **ROJO** en T038 a T044, con el motivo verificado. Mostrar la salida
- [X] T046 [US4] `AcotadoDelListado` suma `categoriaId`, `desde` y `hasta` en `frontend/src/api/tipos.ts`, y `obtenerMovimientos` los manda sólo si están, en `frontend/src/api/cliente.ts`. **Reemplazar el comentario que dice que la barra de filtros nunca se construyó**: ya no es cierto. Verde de T038
- [X] T047 [US4] Mover `ControlesDelPeriodo.tsx` de `frontend/src/dashboard/` a `frontend/src/periodo/` y darle props de **valor inicial** para los dos extremos, que hoy arrancan siempre vacíos. Actualizar el import de `frontend/src/dashboard/PantallaDashboard.tsx` ([D-05](./research.md)). Dejar escrita la frontera en el archivo: lo que pinta algo que dos pantallas usan, sube. Las props nuevas no cambian la decisión de D-08 de la feature 010 —**el componente sigue sin validar nada**— y son lo que permite que el listado muestre el mes que el servidor eligió sin calcularlo. `frontend/tests/PantallaDashboard.test.tsx` es **la fila 4 de la tabla de D-12**
- [X] T048 [US4] Crear `frontend/src/movimientos/FiltrosDelListado.tsx`: categoría, el `ControlesDelPeriodo` reutilizado y moneda, con un único "Aplicar". Los extremos arrancan con el `desde`/`hasta` del `Resumen` que la pantalla ya tiene, y **mientras nadie los toque la petición sale sin parámetros de período** (`FR-015`). La barra usa las clases de disposición de US1 y **funciona a 360 px**. Verde de T039 a T043
- [X] T049 [US4] Enchufarlo en `frontend/src/movimientos/PantallaMovimientos.tsx`: el efecto de carga depende de los cuatro acotados, y la guarda contra la respuesta que llega tarde se conserva tal cual — con tres controles el caso es más probable, no distinto. Quitar el `<select>` de moneda suelto, que pasa a vivir en la barra. Verde de T044
- [X] T050 [VERIFY] [US4] Puerta del frontend completa, con su salida

**Checkpoint**: `PRD:AC-23`, `AC-24`, `AC-25` y `AC-26` cubiertos. `SC-006` y `SC-009` en verde. La
deuda D9-01 está saldada.

---

## Phase 7: US5 — El monto se lee en la escala de su moneda (P5)

**Goal**: `decimales` empieza a viajar y a mandar. Salda **D8-05** / **D9-05** / **D10-02**.

**Independent Test**: mostrar un monto de una moneda con 0 decimales y verificar que no aparecen
centavos, en las tres pantallas.

**Es la única historia que toca el backend**, y arrastra su puerta entera.

- [X] T051 [TEST] [P] [US5] Agregar `decimales` a `interface Moneda` en `frontend/src/api/tipos.ts`, reemplazando el comentario que hoy explica por qué no viajaba por el que explica qué hace. **Correr `dotnet test backend/` y exigir ROJO en los tests de `Contrato/`**: el campo está de un solo lado, y ése es exactamente el rojo que la barrera del contrato existe para producir. Mostrar la salida
- [X] T052 [TEST] [P] [US5] En `frontend/tests/` (donde vive el test de `formatearMonto`): con `decimales: 0` no se muestran centavos y con `decimales: 2` sí; **cuando el catálogo y lo que `Intl` deduce del código ISO no coinciden, gana el catálogo** (`FR-019`, [D-08](./research.md)); y un código que `Intl` no puede interpretar sigue degradando al número con su código al lado, **respetando igual los decimales del catálogo**. El `try/catch` no se toca: protege contra `moneda.codigo` sin `CHECK`, que es la deuda **D11-02** y es independiente de la escala
- [X] T053 [TEST] [P] [US5] En `frontend/tests/ListadoMovimientos.test.tsx`, `ResumenDelPeriodo.test.tsx` y `PantallaDashboard.test.tsx`: las **tres** pantallas que muestran plata usan la escala del catálogo. Actualizar `frontend/tests/monedas.fixture.ts` con `decimales`. **Son las filas 4, 5 y 6 de la tabla de D-12** — la 7 es la de `Contrato/`, que la tocan T051 y T055
- [X] T054 [ROJO] [US5] Correr `pnpm --dir frontend test` y exigir **ROJO** en T052 y T053, verificando que el motivo es que `formatearMonto` todavía no recibe la escala. Mostrar la salida
- [X] T055 [US5] `MonedaDto` suma `Decimales` en `backend/GestionGastos.Api/Monedas/MonedasEndpoints.cs`. **Es el único cambio de código de producción del backend en toda la feature.** Va **último en el record**, después de `EsPredeterminada`, y el ejemplo de [contracts/api.md](./contracts/api.md) se corrige para que coincida. Si los tests de contrato comparan sólo nombres el orden da igual; el primer rojo de T051 es donde se comprueba cuál de las dos cosas hacen. Sin migración: la columna existe desde `20260823220228_Inicial`. Verde de T051
- [X] T056 [US5] `formatearMonto(monto, monedaCodigo, decimales)` en `frontend/src/ui/formatearMonto.ts` fija `minimumFractionDigits` y `maximumFractionDigits` con el dato del catálogo, en la rama normal **y en la degradada**. Actualizar los tres puntos de llamada. **Ninguna lista de monedas escrita a mano en ningún lado**: es lo que `verificar-monedas.sh` vigila (punto 6). Verde de T052 y T053
- [X] T057 [VERIFY] [US5] Puerta de **las dos pilas**: `lint` + `format` + `tsc --noEmit` + `test` del frontend, y `dotnet format --verify-no-changes` + `dotnet build -warnaserror` + `dotnet test backend/`. Con su salida

**Checkpoint**: las cinco historias entregadas. Las tres deudas que la 010 mandó acá están saldadas.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T058 Commitear todo. `verificar-monedas.sh` **exige los dos árboles limpios** o no puede distinguir lo que ensució ella de lo que ya estaba sucio: `git status --short` tiene que estar vacío antes del paso siguiente
- [X] T059 Correr `./backend/verificar-monedas.sh` (~1 min) y mostrar la salida. Es **la barrera crítica de esta feature** (punto 6): verifica que `FR-019` no haya cableado una lista de monedas en ninguna de las dos pilas
- [X] T060 [P] Correr las otras cinco barreras con su salida: `verificar-contrato.sh` (~2,5 min, el campo nuevo del contrato), `verificar-autorizacion.sh`, `verificar-desglose.sh`, `verificar-linter.sh` y `verificar-aislamiento.sh` (~7 min)
- [X] T061 [P] Cobertura del backend: `dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings`, con su salida
- [X] T062 [P] Build de producción del frontend: `pnpm --dir frontend build`
- [X] T063 **Comprobar que no entró ninguna dependencia** (`NFR-004`, `SC-008`): `git diff main -- frontend/package.json frontend/pnpm-lock.yaml 'backend/**/*.csproj'` tiene que estar vacío. Es la restricción que se decidió a mano en *Clarifications* frente a axe-core y a un navegador sin cabeza, y hasta acá no la comprobaba nadie
- [X] T064 **Comprobar la tabla de [D-12](./research.md) contra lo que realmente se tocó**: `git diff --stat main -- frontend/tests backend/GestionGastos.Api.Tests`. Cada test modificado tiene que estar en la tabla; si hay uno que no está, decir cuál y qué requisito lo justifica — o revertirlo. **Es la verificación de `SC-007`**, y es la única forma de que `FR-020` sea algo más que una intención
- [X] T065 Correr los pasos a mano del [quickstart](./quickstart.md). **Los pasos 1 a 4 no los cubre ningún test**: los 360 px medidos de verdad, el recorrido completo con teclado, el árbol de accesibilidad y si la paleta se ve bien. Si no hay navegador en el entorno, anotarlo como **D10-09**, que sigue abierta desde la feature 010
- [X] T066 Actualizar `plan-de-implementacion/README.md`: mover DISC-001-06 a la tabla de implementado, con qué lo demuestra en el código y qué deudas saldó. Anotar que el plan queda con **un solo ticket pendiente**, el 2 (Nota descriptiva del movimiento)
- [X] T067 Cerrar la tabla de *Deuda registrada* de [spec.md](./spec.md): confirmar cuáles de D11-01 a D11-07 quedaron efectivamente abiertas, y anotar en las tablas de las features 008, 009 y 010 que D8-05, D9-01, D9-05, D10-01 y D10-02 están saldadas

---

## Dependencies & Execution Order

### Phase Dependencies

```text
Setup (T001)
  └─ Foundational (T002–T003)  ← bloquea US1 y US2
       ├─ US1 (T004–T015)      ← la paleta y las reglas de disposición
       │    └─ US2 (T016–T026) ← el foco se mide sobre la paleta de US1
       ├─ US3 (T027–T037)      ← independiente de US1 y US2
       ├─ US4 (T038–T050)      ← independiente; usa las clases de US1 si ya están
       └─ US5 (T051–T057)      ← independiente
            └─ Polish (T058–T067)
```

### Lo que sí depende de algo

- **US2 depende de US1**, y es la única dependencia real entre historias: `NFR-001` mide contraste
  sobre la paleta declarada, y hasta que exista se mediría contra los dos colores que hoy están
  anotados como *"los del navegador, no una elección"* ([D-13](./research.md)).
- **US3, US4 y US5 son independientes entre sí y de US1**. Se benefician de que US1 esté hecha —los
  controles que agregan usan sus clases de disposición— pero no la necesitan para funcionar. Si
  hiciera falta mostrar algo antes de terminar la maquetación, se adelantan pagando una segunda
  pasada sobre los controles nuevos.
- **US4 mueve un archivo que US1 no toca** (`ControlesDelPeriodo.tsx`) y **US5 toca el mismo
  `formatearMonto` que US3 usa en el listado**: si las dos se hacen en paralelo, US5 va después de
  T035.

### Dentro de cada historia

`[TEST]` → `[ROJO]` → implementación → `[VERIFY]`. El `[ROJO]` no es ceremonia: es la tarea que
comprueba que el test falla **por la razón esperada** y no por un import roto, y su salida se
muestra.

### Parallel Opportunities

- **T004, T006 y T008** son tres archivos de test distintos: se escriben en paralelo.
- **T016, T017, T018 y T021** viven en el mismo archivo pero son casos independientes; se pueden
  repartir si dos personas coordinan el archivo.
- **T027 a T032** son cinco archivos y seis casos independientes.
- **T038 a T044**: seis de los siete están en `FiltrosDelListado.test.tsx`, uno en cada archivo del
  cliente y de la pantalla.
- **T060, T061 y T062** son tres comandos que no se pisan: la cobertura, las barreras y el build.

## Parallel Example: US1

```text
# Los tres verificadores, tres archivos, sin dependencias entre sí:
T004  ClasesConRegla.test.ts     — 0 clases sin regla
T006  Paleta.test.ts             — contraste de cada par
T008  AnchoDeLasPantallas.test.ts — reglas de ancho

# Y sus tres casos que tienen que fallar, uno por archivo (D-03):
T005  ClasesConRegla.test.ts      — una clase sin regla, detectada
T007  Paleta.test.ts              — un par por debajo del umbral, detectado
T009  AnchoDeLasPantallas.test.ts — un ancho fijo de 400 px, detectado
```

---

## Implementation Strategy

### MVP: sólo US1

La paleta declarada y 0 clases huérfanas. Es lo que hace que la aplicación deje de verse como un
formulario sin estilos, y es la precondición de US2. Entregable solo: `SC-001`, `SC-002` y `SC-004`.

### Orden sugerido con una sola persona

1. **T001–T003** (setup y el helper compartido).
2. **US1**, entera. Es la que desbloquea.
3. **US2**, que es la otra mitad del ticket 6. Con las dos, el encargo original está entregado.
4. **US5** antes que US3 y US4 si se quiere sacar temprano el único cambio de backend, porque es el
   que arrastra la puerta larga y la barrera del contrato.
5. **US3** y **US4**, en ese orden: la primera es chica y de valor inmediato, la segunda es la más
   grande de las cinco.
6. **Polish**, con las seis barreras y —sobre todo— **T064**, que es donde `SC-007` se verifica de
   verdad.

### Dónde está el riesgo

No en el código: casi todo lo que hace falta del lado del servidor ya existe. El riesgo es de
**alcance**, y tiene un nombre: una vez que alguien abre el CSS, todo se ve mejorable. `FR-020`,
`SC-007`, la tabla de D-12 y la tarea T064 son las cuatro capas que lo acotan, y las cuatro apuntan
a la misma pregunta — *¿qué requisito justifica este cambio?*

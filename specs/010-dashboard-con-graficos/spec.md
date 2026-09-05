# Feature Specification: Dashboard con gráficos

**Feature Branch**: `010-dashboard-con-graficos`

**Created**: 2026-09-05

**Status**: Draft

**Input**: Ticket DISC-001-05 — "Dashboard con gráficos"
(`plan-de-implementacion/prds/pendientes/prd-DISC-001-05.md`), octavo de los nueve PRD de DISC-001.
Salda las deudas **D9-02** (filtrar el dashboard por moneda) y **D9-06** (la vista de totales en
pantalla) de la feature 009.

---

## De dónde sale esta spec

Las features 008 y 009 aprendieron, a fuerza de encontrarse el trabajo ya hecho, que el PRD hay que
verificarlo contra el código antes de planificar. Esta spec empieza igual: **requisito por requisito
contra el código**, y recién después dice qué queda.

El resultado sorprende para el otro lado que en la 008. Ahí el backend estaba casi entero y la
feature terminó verificando lo construido. Acá pasa lo mismo con el backend — **el cálculo del
dashboard existe y funciona desde FEAT-001c** — pero el frontend está en cero: no hay una sola línea
que pida el resumen. Ésta es, de las nueve, la feature con el reparto más desparejo.

### Lo que el PRD pide y ya está construido

| Lo que pide | Dónde está | Desde |
|---|---|---|
| `PRD:FR-01` — el total de gastos por categoría y por moneda de un período | `Resumenes/CalculoDelResumen.cs`, `ResumenPorMoneda.GastosPorCategoria` | FEAT-001c |
| `PRD:FR-02` — el balance por moneda, ingresos menos gastos, sin mezclar monedas | `ResumenPorMoneda.Balance`, una entrada por moneda y nada que sume a través de ellas | FEAT-001c |
| `PRD:FR-03` — acotar por rango de fechas con los extremos incluidos | `GET /api/resumen?desde&hasta`, interpretado por `Dominio/PeriodoPedido.cs` | FEAT-001b / 006 |
| `PRD:FR-06` — período sin movimientos: ceros por moneda y ningún error | `CalculoDelResumen` compone sobre el **catálogo**, no sobre el resultado del agregado (D-05 de la 006), así que devuelve una entrada por cada moneda tenga o no movimientos | FEAT-001c |
| `PRD:NFR-02` — agregación en la base, misma agregación que el resumen, nada sumado en el cliente | `MovimientosConsulta.Agrupado`: **una sola consulta** que agrupa por moneda, tipo y categoría a la vez, de la que se componen los tres totales y el desglose | FEAT-001c |
| Que el desglose incluya las categorías propias y las dadas de baja que conservan movimientos | `verificar-desglose.sh`, la barrera que impide que el desglose filtre por `categoria.activa` | 007 |
| `PRD:NFR-01` — el resumen en < 2 s p95 con 1000 movimientos y < 4 s con **10000** | `Rendimiento/RendimientoResumenTests.cs`, `[InlineData(1000, 2000)]` y `[InlineData(10_000, 4000)]`, más el caso repartido en dos monedas | FEAT-001c / 008 |
| Que las barras del gráfico no se intercambien solas entre dos pedidos idénticos | el desempate por id del desglose en `CalculoDelResumen`, escrito con esa razón textual | FEAT-001c |

Tres de esas filas merecen leerse dos veces.

**`PRD:AC-09` no estrena la igualdad entre el desglose del dashboard y el del resumen: la hereda.**
`ResumenEndpoints` lleva escrito en su propio comentario *"Es un endpoint y no dos"*, con el
argumento entero: dos endpoints que tienen que dar lo mismo son dos endpoints que algún día no van a
darlo. La pantalla principal es este mismo resumen pedido sin período. `PRD:NFR-02` ya está cumplido
del lado del servidor, y lo único que esta feature tiene que hacer es **no romperlo**: no pedir el
dashboard a otro lado ni sumar nada en la pantalla.

**`PRD:AC-07` —el período vacío— también está resuelto, y a propósito.** Fue la decisión D-05 de la
feature 006: las monedas salen del catálogo y no del agregado, justamente para que un período sin
movimientos devuelva ceros en lugar de una lista vacía. La 006 dejó dicho que si salieran del
agregado, ese criterio quedaría *"a cargo del cliente"*. No quedó.

**El desempate por id del desglose se escribió pensando en este ticket.** El comentario dice, con
todas las letras, que sin él *"las barras del gráfico se intercambian solas entre dos pedidos
idénticos"*. Es la primera vez que hay barras.

### Lo que falta de verdad

Tres cosas, y son de tamaños muy distintos.

1. **Toda la pantalla.** `frontend/src/api/cliente.ts` no tiene `obtenerResumen`, y `tipos.ts`
   declara `Resumen`, `ResumenPorMoneda` y `TotalPorCategoria` sin que nadie los use — están ahí
   porque el test de contrato los compara, no porque alguna vista los pinte. No hay gráfico, no hay
   totales en pantalla, no hay controles de rango de fechas, no hay filtro de moneda. **Es la deuda
   D9-06 entera, y es el grueso del trabajo de esta feature.**
2. **El acotado por moneda del dashboard** (`PRD:FR-04`, deuda **D9-02**). Ver la sección siguiente:
   no es tan simple como agregarle un parámetro al endpoint.
3. **La cita de `PRD:AC-11` y `PRD:AC-12` en el test que ya los mide.** El PRD supone que el
   volumen de 10000 movimientos nunca se probó, y **se equivoca**: `RendimientoResumenTests` mide
   los dos escalones de `RNF-01` sobre `GET /api/resumen` desde la feature 006 (`401d294`), con
   `[InlineData(10_000, 4000)]` escrito, y la 008 le agregó el caso repartido en dos monedas
   (`be7a662`). Lo que falta es de forma, no de fondo: ese test cita `RNF-01`, `AC-04` y `FR-011`
   **de la 006**, y ningún test nombra `AC-11` ni `AC-12` de este ticket, que es lo que el Principio
   II de la constitución exige para considerarlos cubiertos.

### La brecha que el PRD no podía prever

**`PRD:FR-07` exige que el resumen del mes en curso de la pantalla principal no se altere cuando se
cambian los filtros del dashboard. Ese resumen no se muestra en ninguna pantalla.**

Es exactamente la deuda D9-06, y la feature 009 la difirió acá con esta razón: *"el recálculo se
verifica contra la API, que ya lo hace bien desde FEAT-001c. La vista de totales es del ticket 5"*.
Así que `PRD:AC-08` —*"el sistema dejará el resumen del mes en curso de la pantalla principal con
los mismos totales y el mismo desglose que antes del cambio"*— no se puede demostrar del lado del
usuario mientras no exista lo que tiene que quedar quieto.

**Esta spec lo resuelve construyendo las dos cosas**: el resumen del mes en curso en la pantalla
principal —que es lo que D9-06 pedía— y el dashboard con su período y sus filtros propios. No son
dos vistas del mismo dato duplicado: son el **mismo endpoint** pedido de dos maneras, que es
precisamente el diseño que `ResumenEndpoints` eligió y documentó. Construir sólo el dashboard
dejaría `PRD:FR-07` sin nada que verificar, y construir sólo la pantalla principal no sería este
ticket.

### La tensión que hereda de la feature 009

`PRD:FR-04` pide filtrar el dashboard por moneda. La 009 dejó escrito, en el código y con una
barrera, **lo contrario para el resumen**:

> `monedaId: null` explícito, y no por omisión (D-05 de la feature 009). El acotado por moneda es del
> LISTADO. Si se colara hasta acá, los totales de un período ya cerrado darían otro número sin que
> nadie tocara un movimiento. El resumen informa sobre TODAS las monedas del catálogo, siempre.

Y lo sostiene un test: `ResumenDelPeriodoTests.El_Resumen_No_Hereda_El_Acotado_Por_Moneda_Del_Listado`.

No es una contradicción: es que **son dos cosas distintas con el mismo nombre**. `PRD:RF-29` prohíbe
que un total mezcle monedas, así que el servidor ya devuelve los universos separados —una entrada
por moneda, cada una con sus tres totales y su desglose— y ningún número cambia según qué monedas se
pidan. Filtrar el dashboard por dólares es **elegir cuál de esos universos se mira**, no cambiar
ningún cálculo.

**La sesión de aclaración lo resolvió del lado de la pantalla**: el filtro de moneda del dashboard
elige cuál de los bloques ya separados se mira, y el endpoint del resumen no cambia. La garantía de
la 009 queda intacta, su test sigue significando lo mismo, y `FR-013` —que el desglose de una moneda
sea el mismo esté o no aplicado el filtro— se cumple por construcción en vez de por vigilancia: no
hay dos cálculos que puedan discrepar porque hay uno solo.

---

## Lo que hace distinta a esta feature

**Es la primera vez que el proyecto dibuja algo.** Todo lo construido hasta acá son formularios,
listas y mensajes: elementos con semántica propia, que un test lee por su rol y que un lector de
pantalla anuncia sin ayuda. Un gráfico no tiene nada de eso. Un valor que sólo existe como la altura
de una barra no se puede leer, no se puede testear sin inspeccionar píxeles, y no cumple `PRD:RNF-06`.

De ahí sale la decisión que gobierna toda la feature y que el PRD ya trae escrita: **el gráfico es la
segunda forma de ver el dato, nunca la única**. El equivalente textual (`FR-005`) no es una concesión
de accesibilidad puesta al costado: es **lo que hace verificables a `AC-01` y `AC-05`**. Los tests de
esta feature afirman sobre el texto; el gráfico se afirma por su presencia y por sus atributos, no
por su forma.

Y una segunda: **es la primera vez que el usuario elige un período desde la pantalla.** `desde` y
`hasta` existen y están probados desde FEAT-001b, pero siempre se ejercitaron por API. Todo lo que
`PeriodoPedido` ya sabe rechazar —medio rango, rango invertido— pasa a tener que decirse en pantalla,
al lado del control, con la clave `rango` que el servidor ya devuelve para eso.

---

## Clarifications

### Session 2026-09-05

- **P: `PRD:FR-07` exige que el resumen del mes en curso de la pantalla principal no se altere, pero
  ese resumen no se muestra en ninguna pantalla (deuda D9-06). ¿Se construye acá?**
  R: **Sí, se construyen las dos cosas.** La pantalla principal muestra el resumen general del mes
  en curso; el dashboard con filtros es **otra pantalla**. Es lo que salda D9-06 y lo único que
  vuelve verificable a `PRD:AC-08` del lado del usuario: para que algo pueda quedarse quieto,
  primero tiene que estar a la vista.
- **P: ¿Dónde vive cada cosa, y en qué orden?**
  R: **La pantalla principal, de arriba abajo: el resumen general del mes en curso, el formulario de
  registro, y debajo el listado de movimientos.** El dashboard es una **tercera vista navegable**,
  como la de categorías. Que sean dos pantallas distintas no es una preferencia de diseño: es lo que
  hace que "el resumen del mes" y "el dashboard" sean dos cosas nombrables, que es exactamente lo
  que `FR-012` necesita para poder exigir que una no se mueva cuando la otra cambia.
- **P: ¿El filtro de moneda del dashboard acota lo que el servidor calcula, o elige cuál de los
  bloques que el servidor ya devuelve separados se mira?**
  R: **Elige cuál se mira. Es un filtro de pantalla y el servidor no se toca.** El servidor ya
  devuelve los universos separados —una entrada por moneda, con sus totales y su desglose— y ningún
  número depende de qué monedas se pidan, así que acotar es mostrar menos de lo que ya llegó, no
  calcular otra cosa. Con esto la garantía que la 009 blindó —*el resumen informa sobre TODAS las
  monedas del catálogo, siempre*— queda intacta y sin reabrirse, y **D9-02 resulta ser trabajo de
  frontend únicamente**. No es sumar en el cliente: no se suma nada, se elige qué bloque pintar.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cómo vengo este mes, apenas entro (Priority: P1) 🎯 MVP

Al entrar, arriba de todo, la persona ve el resumen del mes en curso: por cada moneda, cuánto
ingresó, cuánto gastó, el balance entre los dos, y en qué categorías se fueron esos gastos. Debajo
sigue el formulario para registrar, y más abajo el listado.

**Why this priority**: Es la deuda **D9-06** y es la mitad de la pregunta que PRD-001 dice que la
aplicación tiene que contestar —*cómo vengo este mes*—, hoy calculada bien por el servidor y no
mostrada en ningún lado. Y es lo que `FR-012` necesita que exista: para exigir que algo no se mueva,
primero tiene que estar a la vista. Es la historia más chica de las cuatro y la que más valor
entrega por sí sola.

**Independent Test**: Se registran ingresos y gastos en dos monedas, se recarga la pantalla
principal y se comprueba que el resumen del mes aparece arriba con los totales, el balance y el
desglose de cada moneda, sin haber navegado a ninguna otra pantalla.

**Acceptance Scenarios**:

1. **Given** una cuenta con ingresos y gastos del mes en curso, **When** se abre la pantalla
   principal, **Then** el resumen aparece **por encima** del formulario de registro y del listado, y
   cada moneda muestra lo ingresado, lo gastado y su balance.
2. **Given** el resumen a la vista, **When** se registra un movimiento nuevo, **Then** los totales
   reflejan el movimiento recién registrado sin recargar la página.
3. **Given** una moneda cuyos gastos superan a sus ingresos en el mes, **When** se mira su balance,
   **Then** se muestra un balance negativo, presentado como un resultado y no como un error.
4. **Given** la pantalla principal, **When** se busca cómo cambiar el período del resumen, **Then**
   no hay forma de hacerlo: el resumen de esta pantalla es siempre el del mes en curso.

---

### User Story 2 - Ver en qué se me va la plata (Priority: P2)

Desde la pantalla principal la persona va al dashboard y ve, para cada moneda, cuánto gastó en cada
categoría del período — representado como un gráfico y también escrito, categoría por categoría, con
su total.

**Why this priority**: Es la otra mitad de la pregunta de PRD-001 —*en qué se me va la plata*— y es
lo que da nombre al ticket. Va después de P1 porque necesita una pantalla nueva entera y porque el
desglose ya se ve, sin gráfico, en el resumen de la principal.

**Independent Test**: Se registran gastos en varias categorías y en dos monedas, se navega al
dashboard y se comprueba que cada categoría aparece con su total, separada por moneda, en el gráfico
y en el texto.

**Acceptance Scenarios**:

1. **Given** una cuenta con gastos en varias categorías dentro del período, **When** se abre el
   dashboard, **Then** cada categoría aparece con un total igual a la suma de los montos de sus
   gastos en ese período y en esa moneda, y esos totales están representados gráficamente.
2. **Given** el dashboard mostrando el desglose, **When** se leen sus valores sin interpretar el
   gráfico, **Then** el nombre y el total de cada categoría representada están disponibles en forma
   textual.
3. **Given** una cuenta con gastos en pesos y en dólares en la **misma** categoría y período,
   **When** se abre el dashboard, **Then** el total en pesos y el total en dólares aparecen por
   separado y ningún total incluye montos de la otra moneda.
4. **Given** un catálogo con más categorías que colores distinguibles, **When** se inspecciona el
   gráfico, **Then** cada categoría se distingue por al menos un atributo además del color.
5. **Given** el dashboard abierto, **When** se vuelve a la pantalla principal, **Then** se llega a
   ella sin perder la sesión ni recargar, igual que se vuelve de la pantalla de categorías.

---

### User Story 3 - Mirar el período que yo elija (Priority: P3)

En el dashboard, la persona elige un rango de fechas —un mes anterior, un trimestre, un año— y todo
lo que el dashboard muestra se recalcula para ese rango, sin que el resumen del mes en curso de la
pantalla principal se mueva.

**Why this priority**: Es lo que distingue al dashboard del resumen de la principal: aquél está
clavado al mes calendario por decisión de FEAT-001c, y éste es el lugar donde el usuario elige qué
mirar. Va tercera porque las dos anteriores ya entregan valor sobre el período por omisión.

**Independent Test**: Se registran movimientos en dos meses distintos, se elige el rango del mes
anterior y se comprueba que los totales son los de ese mes y sólo los de ese mes, y que al volver a
la principal el resumen del mes en curso muestra los mismos números que antes.

**Acceptance Scenarios**:

1. **Given** movimientos dentro y fuera del rango elegido, **When** se aplica el rango, **Then** los
   totales por categoría y el balance de cada moneda se calculan únicamente con los movimientos cuya
   fecha cae dentro de ese rango, **incluidos sus extremos**.
2. **Given** un rango aplicado en el dashboard, **When** se vuelve a la pantalla principal, **Then**
   su resumen del mes en curso muestra los mismos totales y el mismo desglose que antes del cambio.
3. **Given** el dashboard acotado al mes en curso, **When** se compara su desglose por categoría con
   el del resumen de la pantalla principal, **Then** los totales son los mismos para las mismas
   categorías.
4. **Given** un rango con la fecha de inicio posterior a la de fin, **When** se intenta aplicarlo,
   **Then** se muestra el motivo del rechazo junto al control del período y los totales que estaban
   a la vista no se reemplazan por un vacío.

---

### User Story 4 - Mirar una sola moneda (Priority: P4)

En el dashboard, la persona elige una moneda y ve únicamente los totales y el balance de esa moneda.
Sin elegir ninguna, ve todas.

**Why this priority**: Salda **D9-02** y es lo que vuelve legible el dashboard de quien opera en
varias monedas. Va última porque, sin ella, el dashboard ya muestra todo lo que hay que mostrar: es
un recorte de la vista, no un dato nuevo.

**Independent Test**: Se registran movimientos en dos monedas, se elige una y se comprueba que sólo
se ven los suyos; se vuelve a "todas" y se comprueba que aparecen las dos con los mismos números.

**Acceptance Scenarios**:

1. **Given** una cuenta con movimientos en pesos y en dólares, **When** se filtra el dashboard por
   dólares, **Then** se muestran únicamente los totales por categoría y el balance en dólares.
2. **Given** el dashboard recién abierto, **When** no se eligió ninguna moneda, **Then** se muestran
   las de todas las monedas.
3. **Given** el dashboard filtrado por una moneda, **When** se comparan sus totales con los de esa
   misma moneda sin filtro, **Then** son idénticos: filtrar recorta lo que se ve, nunca lo que se
   calcula.
4. **Given** una moneda agregada al catálogo como dato, **When** se abre el selector de moneda del
   dashboard, **Then** aparece, sin que haya hecho falta tocar código.
5. **Given** una moneda elegida en el dashboard, **When** se vuelve a la pantalla principal,
   **Then** su resumen sigue mostrando **todas** las monedas: el filtro es del dashboard.

---

### Edge Cases

- **Período sin ningún movimiento**: se muestran los totales y el balance de cada moneda en cero, se
  indica que no hay datos para graficar, y **no** se muestra ningún mensaje de error. Un período
  vacío es una respuesta, no una falla.
- **Una moneda del catálogo sin movimientos en el período, con otras que sí tienen**: aparece con sus
  totales en cero, igual que las demás. El servidor informa sobre todas las monedas del catálogo
  siempre, y la pantalla no las esconde.
- **Una categoría dada de baja que conserva movimientos en el período**: sigue apareciendo en el
  desglose con su total. Es lo que `verificar-desglose.sh` protege, y el dashboard no puede ser el
  lugar donde ese movimiento deje de sumar.
- **Rango invertido o medio rango** (una sola de las dos fechas): se rechaza con su motivo al lado
  del control, y no se muestra un resultado vacío. Un vacío se lee como *"no hay nada"* y esconde que
  la pregunta estaba mal formada.
- **El resumen no se puede cargar** (servidor caído, red): se dice que no se pudo cargar, y no se
  muestran ceros. Ceros y "no hay datos" son la misma pantalla diciendo dos cosas opuestas.
- **La sesión vence mientras el dashboard está abierto**: se vuelve al acceso con el aviso de
  siempre, como cualquier otra pantalla protegida.
- **Un período con muchas categorías**: el desglose sigue siendo legible y ninguna categoría queda
  sin su equivalente textual.
- **Dos categorías con el mismo total**: su orden es estable entre dos pedidos idénticos; las barras
  no se intercambian solas.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE mostrar, en una sección de dashboard, el total de gastos agrupado por
  categoría y por moneda dentro del período seleccionado, representado gráficamente.
  *(`PRD:FR-01`, `PRD:RF-19`)*
- **FR-002**: El sistema DEBE mostrar, por cada moneda del catálogo, lo ingresado, lo gastado y un
  balance igual a lo ingresado menos lo gastado en esa moneda dentro del período.
  *(`PRD:FR-02`, `PRD:RF-20`, `PRD:RF-29`)*
- **FR-003**: Ningún total mostrado DEBE incluir montos de más de una moneda, y el sistema NO DEBE
  convertir entre monedas ni ofrecer un total consolidado. *(`PRD:RF-29`)*
- **FR-004**: El sistema DEBE permitir que la persona elija un rango de fechas para el dashboard,
  con sus extremos incluidos, y DEBE recalcular con ese rango todo lo que muestra.
  *(`PRD:FR-03`, `PRD:RF-21`)*
- **FR-005**: El sistema DEBE rechazar un rango inválido —una sola de las dos fechas, o la de inicio
  posterior a la de fin— mostrando el motivo junto al control del período, sin reemplazar por un
  vacío los totales que estaban a la vista. *(hereda las reglas ya existentes del período)*
- **FR-006**: El sistema DEBE permitir acotar lo que el dashboard muestra a una sola moneda, tomando
  **todas las monedas** como valor por omisión. El acotado DEBE ser **de presentación**: elige cuál
  de los bloques que el servidor ya devuelve separados se muestra, y NO DEBE cambiar lo que se le
  pide al servidor ni lo que éste calcula. *(`PRD:FR-04`, `PRD:RF-30`, deuda D9-02)*
- **FR-006b**: El sistema NO DEBE hacer que el resumen del período herede el acotado por moneda: el
  servidor sigue informando sobre todas las monedas del catálogo, siempre. *(la garantía D-05 de la
  009 y el test que la sostiene)*
- **FR-007**: Las monedas ofrecidas para acotar DEBEN salir del catálogo, nunca de una lista escrita
  a mano, de modo que una moneda agregada como dato aparezca sin tocar código. *(`008:RF-32`, lo que
  `verificar-monedas.sh` protege en las dos pilas desde la 009)*
- **FR-008**: El sistema DEBE presentar los totales por categoría también en forma textual, con el
  nombre y el total de cada categoría representada, de modo que cada valor del gráfico se pueda leer
  sin interpretarlo. *(`PRD:FR-05`, `PRD:RNF-06`)*
- **FR-009**: El sistema DEBE indicar que no hay datos cuando el período seleccionado no tiene
  movimientos, mostrando los totales y el balance en cero para cada moneda y **sin** mostrar ningún
  mensaje de error. *(`PRD:FR-06`, `PRD:AC-31` de PRD-001)*
- **FR-010**: El sistema DEBE distinguir un fallo de carga de un período vacío: un fallo se dice como
  fallo y no se presenta como ceros. *(`AGENTS.md`: nunca un catch silencioso)*
- **FR-011**: El sistema DEBE mostrar el resumen del mes en curso **en la pantalla principal, por
  encima del formulario de registro y del listado**, con sus totales por moneda y su desglose por
  categoría. *(deuda **D9-06**; es lo que `FR-012` exige que quede quieto)*
- **FR-011b**: El dashboard DEBE vivir en **una pantalla propia**, alcanzable desde la pantalla
  principal y con vuelta a ella, igual que la de categorías. La pantalla principal NO DEBE ofrecer
  los filtros del dashboard: su resumen es siempre el del mes en curso.
- **FR-011c**: El resumen de la pantalla principal y el del dashboard DEBEN salir de la **misma**
  lectura del servidor —la principal, pedida sin período— y no de dos caminos distintos.
  *(`PRD:AC-09`; es la decisión que `ResumenEndpoints` documenta como "es un endpoint y no dos")*
- **FR-012**: El sistema NO DEBE alterar el resumen del mes en curso de la pantalla principal cuando
  se cambian el rango de fechas o la moneda del dashboard. *(`PRD:FR-07`, `PRD:RF-22`)*
- **FR-013**: El desglose por categoría del dashboard acotado al mes en curso y el del resumen de la
  pantalla principal DEBEN mostrar los mismos totales para las mismas categorías, y el desglose de
  una moneda DEBE ser el mismo esté o no aplicado el filtro de moneda. *(`PRD:AC-09`, `PRD:NFR-02`)*
- **FR-014**: Los totales DEBEN venir ya agregados desde el servidor: el sistema NO DEBE sumar
  montos en el cliente ni pedir la lista de movimientos individuales para calcularlos.
  *(`PRD:NFR-02`)*
- **FR-015**: El desglose DEBE incluir las categorías propias de la cuenta y las dadas de baja que
  conservan movimientos en el período. *(`007`, lo que `verificar-desglose.sh` protege)*
- **FR-016**: El orden en que se muestran las categorías del desglose DEBE ser estable entre dos
  pedidos idénticos, incluso cuando dos categorías tienen el mismo total.
- **FR-017**: El dashboard DEBE exigir sesión, como todo el resto de la aplicación, y reaccionar al
  vencimiento de la sesión igual que las demás pantallas. *(la barrera de autorización de la 004)*

### Non-Functional Requirements

- **NFR-001**: El dashboard DEBE cargar en menos de 2 s en el percentil 95 sobre una cuenta con 1000
  movimientos, y en menos de 4 s en el percentil 95 sobre una cuenta con 10000, medido sobre 100
  ejecuciones. **Ya se cumple del lado del servidor**; lo que esta feature agrega es la cita de
  `PRD:AC-11` y `PRD:AC-12` en el test que lo mide, y no volver a pedir el resumen más veces de las
  necesarias desde la pantalla. *(`PRD:NFR-01`, `PRD:RNF-01`, `PRD:AC-33` de PRD-001)*
- **NFR-002**: La respuesta que alimenta el dashboard DEBE traer, por cada moneda, sus tres totales
  ya calculados y a lo sumo una fila por categoría, y NO DEBE traer la lista de movimientos
  individuales. *(`PRD:NFR-02`)*
- **NFR-003**: Todos los elementos del dashboard DEBEN cumplir contraste AA —4,5:1 en texto normal,
  3:1 en texto grande y en componentes de interfaz— y cada categoría del gráfico DEBE distinguirse
  por al menos un atributo además del color. *(`PRD:NFR-03`, `PRD:RNF-06`)*
- **NFR-004**: Las mediciones de tiempo DEBEN nombrarse siguiendo la convención que el filtro
  `FullyQualifiedName!~Rendimiento` del CI ya reconoce: miden tiempo de pared y en un runner
  compartido dan rojos que no dicen nada. *(`AGENTS.md`, y la deuda D9-08 de la 009)*

### Key Entities

Esta feature **no agrega ninguna entidad ni ninguna columna**. Lee las que ya existen:

- **Resumen de un período**: lo que se derivó de los movimientos de una cuenta entre dos fechas. No
  se persiste: se calcula cada vez que se pide, y por eso editar o borrar un movimiento se refleja
  sin invalidar nada.
- **Resumen por moneda**: la unidad que `PRD:RF-29` vuelve indivisible —lo ingresado, lo gastado, el
  balance y el desglose de **una** moneda—. Dos de éstos son dos universos separados y nada se suma
  nunca a través de ellos.
- **Total por categoría**: cuánto suma una categoría dentro de una moneda y un período, con el
  nombre **vigente** de la categoría viajando junto a su identificador.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Alguien que abre el dashboard puede decir en qué categoría gastó más en el período,
  por cada moneda, sin hacer ninguna cuenta a mano.
- **SC-002**: Cada valor representado en el gráfico está disponible en forma textual: el 100 % de las
  categorías graficadas se pueden leer con su nombre y su total sin interpretar el gráfico.
- **SC-003**: El desglose del dashboard acotado al mes en curso y el de la pantalla principal
  coinciden en el 100 % de las categorías, y esa coincidencia se verifica automáticamente.
- **SC-004**: Cambiar el rango de fechas o la moneda del dashboard deja el resumen del mes en curso
  con exactamente los mismos números que antes del cambio.
- **SC-005**: El dashboard carga en menos de 2 s en el percentil 95 con 1000 movimientos y en menos
  de 4 s con 10000, medido sobre 100 ejecuciones.
- **SC-006**: Ningún elemento del dashboard baja de 4,5:1 de contraste en texto normal ni de 3:1 en
  texto grande y componentes, y ninguna categoría del gráfico se distingue únicamente por su color.
- **SC-007**: Agregar una moneda al catálogo con SQL puro la hace aparecer en el selector del
  dashboard sin modificar ni una línea de código ni recompilar nada.
- **SC-008**: Un período sin movimientos muestra ceros y la indicación de que no hay datos, y cero
  mensajes de error.

## Assumptions

- **El dashboard usa el endpoint del resumen que ya existe, sin crear un segundo.** Es la decisión
  que `ResumenEndpoints` documenta —*"es un endpoint y no dos"*— y `PRD:AC-09` la exige. Un endpoint
  nuevo para el dashboard sería exactamente lo que ese comentario previene.
- **El período por omisión del dashboard es el mes en curso**, decidido por el servidor, igual que
  hoy. Que el filtro exista no convierte al valor por omisión en algo que el cliente elige.
- **No se guardan los filtros del dashboard entre sesiones**: el PRD lo excluye explícitamente.
- **El filtro de moneda no viaja al servidor**, así que cambiar de moneda en el dashboard no cuesta
  una petición: los datos de todas las monedas ya están en la respuesta que se pidió por el período.
  El rango de fechas sí viaja, porque sí cambia el cálculo.
- **El gráfico es de gastos por categoría.** El desglose de ingresos por categoría está fuera de
  alcance en PRD-001; el ingreso entra al dashboard por el balance.
- **No se filtra el dashboard por categoría**: sus filtros son el rango de fechas y la moneda.
- **La dependencia del gráfico, si la hay, se decide y se justifica en el PLAN y se registra como
  ADR.** `AGENTS.md` exige justificar toda librería nueva, y `PRD:RF-19` justifica que haya un
  gráfico pero no que haya una librería: un gráfico de barras se dibuja sin dependencia alguna. El
  costo de la superficie sobrante es lo que el PLAN tiene que pesar.
- **El riesgo de rendimiento que el PRD marca como el más grande ya está medido y en verde.** El PRD
  dice que 10000 movimientos *"nunca se midió"* y que el PLAN debería atacarlo temprano; la
  reconciliación muestra que la 006 lo midió y la 008 lo amplió. Sigue habiendo trabajo —nombrar los
  AC de este ticket en ese test— pero no es un riesgo abierto, y el PLAN no tiene que ordenarse
  alrededor de él.
- **Los tests afirman sobre el equivalente textual, no sobre el render del gráfico.** Es lo que
  vuelve verificables a `FR-001` y `FR-003` sin inspeccionar píxeles, y es la razón por la que
  `FR-008` no es opcional.
- **El contraste se verifica sobre los colores declarados**, no sobre una captura de pantalla.

## Deuda registrada

Lo que esta feature **no** va a dejar hecho, con el ticket que lo cubre. Se hereda la forma de la
tabla de las features 004, 006, 007, 008 y 009.

| # | Qué queda | Por qué no acá | Quién lo cubre |
|---|---|---|---|
| D10-01 | **La barra de filtros de categoría y de rango de fechas del listado, y la interfaz de eliminación de un movimiento** | Es **D9-01**, que sigue apuntando al mismo lugar. El rango de fechas que esta feature construye es el del **dashboard**: el del listado es otro control sobre otra pantalla, y arrastrarlo acá sería hacer la mitad de FEAT-001b de contrabando | Ticket 6 (Maquetación y accesibilidad) |
| D10-02 | **El formato regional del monto según la moneda** y la columna `decimales`, que sigue sin usarse | Es **D8-05** y **D9-05**, que siguen apuntando al mismo lugar | Ticket 6 (Maquetación y accesibilidad) |
| D10-03 | **`CHECK` sobre `moneda.codigo` para exigir tres letras** | Es **D9-09**. Esta feature agrega un segundo lugar donde un código que `Intl` no entiende se puede cruzar, así que el argumento se refuerza; pero sigue necesitando una migración, y hay que ver si esta feature tiene alguna | El próximo ticket que toque el esquema |
| D10-04 | **Revisar el techo de 50 ms de `003:AC-12`**, que da rojos intermitentes | Es **D9-08**. Esta feature agrega mediciones nuevas y hereda la convención de nombre que el CI filtra, pero no re-decide el criterio de una feature anterior | El ticket que decida si ese techo sigue siendo el correcto |
| D10-05 | **Evolución en el tiempo**: líneas, comparación entre meses, tendencias | Fuera de alcance explícito del PRD: `RF-19` pide el total por categoría dentro de un período, no su variación | Nadie. Es una decisión de producto |
| D10-06 | **Elegir el tipo de gráfico, personalizar colores u orden; exportar a PDF, Excel o imagen; presupuestos, topes y alertas** | Fuera de alcance explícito de PRD-001 | Nadie |

## Dependencies

- **`DISC-001-04a` y `DISC-001-04b` mergeados en `main`** — están: la 009 se mergeó en `5d40f64`. Sin
  el catálogo de monedas y los totales separados, `FR-002`, `FR-006` y el escenario 3 de la historia
  1 no se pueden cumplir sin violar `PRD:RF-29`.
- **FEAT-001c mergeado en `main`** — está: `GET /api/resumen` y su consulta agregada, que `FR-013`
  obliga a compartir y que `FR-012` obliga a no alterar.
- **`DISC-001-03` (categorías propias) mergeado en `main`** — está: el dashboard tiene que graficar
  también las categorías propias y las dadas de baja que conservan movimientos.
- **El filtro `FullyQualifiedName!~Rendimiento` del CI**, declarado en la sección *Stack* de
  `AGENTS.md`, que `NFR-004` necesita.
- **Una eventual dependencia externa de gráficos**, a decidir y justificar en el PLAN.

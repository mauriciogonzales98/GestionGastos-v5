# Feature Specification: Identidad visual, navegación y pantallas más compactas

**Feature Branch**: `025-identidad-visual`

**Created**: 2026-09-29

**Status**: Draft

**Input**: Pedido del usuario el 2026-09-29, al revisar la experiencia de crear la cuenta: *"se ve
totalmente crudo, sin colores ni nada"*. Esta feature define un sistema visual para toda la app
—paleta, tipografía, espaciados y el aspecto de botones, campos y mensajes— y lo aplica completo a
la pantalla de acceso (iniciar sesión y crear cuenta), que es lo primero que ve cualquier persona.
El alcance es **visual**: el flujo de acceso no cambia.

---

## De dónde sale esta spec

### Lo que ya hay (revisado el 2026-09-29)

La app no está del todo sin estilo, y conviene saber qué hay para no rehacerlo por error. La feature
011 dejó puesto **el mínimo necesario para cumplir la accesibilidad**, no una identidad:

- **Una paleta de siete colores**: fondo blanco, texto casi negro, un solo azul de acento, un rojo de
  error, un gris de borde y los dos colores de la barra del dashboard. No hay color de superficie, ni
  de éxito, ni texto secundario.
- **Botones todos iguales**: fondo blanco con borde y texto azul. No hay un botón principal que se
  distinga de uno secundario. En la pantalla de acceso, "Entrar" se ve igual que "Crear cuenta" del
  conmutador.
- **El conmutador entre "Iniciar sesión" y "Crear cuenta"** marca el modo activo sólo con negrita y
  subrayado, así que se lee como un enlace y no como una opción elegida.
- **La confirmación del alta** es texto plano, sin nada que la distinga del resto del texto.
- **La tipografía** es la del sistema, con un único tamaño para todo salvo los títulos por defecto
  del navegador.
- **La pantalla de acceso** es una columna de elementos apilados arriba a la izquierda, sin
  contenedor ni jerarquía.

Y también dejó **tres verificadores que esta feature tiene que respetar y extender, no esquivar**: uno
mide el contraste de cada color declarado contra el fondo sobre el que se usa, otro comprueba que
toda clase que el código nombra tenga su regla de estilo, y otro que ninguna pantalla declare un
ancho fijo mayor a 360 px.

### Una consecuencia de cómo está armado el estilo

Los botones, los campos y los mensajes de error **se estilan por tipo de elemento para toda la app**,
no por pantalla. Eso significa que no es posible cambiarle el aspecto a un botón "sólo en la pantalla
de acceso" sin armar un segundo estilo en paralelo, que es justo lo que hay que evitar: dos aspectos
distintos para el mismo control.

Por eso el alcance se parte así:

- **Los elementos básicos** —paleta, tipografía, espaciado, botones, campos, mensajes— cambian en
  **toda la app**. Las demás pantallas los heredan sin tocarlas.
- **La navegación** entre secciones cambia en **toda la parte con sesión**: pasa a ser una barra
  lateral en escritorio y una barra inferior en el celular (ver *Clarifications*). Es el marco que
  comparten todas las pantallas, así que no puede estrenarse en una sola.
- **La composición interna** —cómo se ordena y se enmarca el contenido de cada pantalla— cambia en
  cuatro lugares: la pantalla de acceso, **el formulario de movimiento** y **el resumen del mes**,
  que pasan a ser más compactos, y **las acciones de cada fila** de categorías y de movimientos, que
  pasan a ser íconos (ver *Clarifications*). El listado y los filtros de movimientos, el resto de categorías y el dashboard
  mantienen su disposición adentro del marco nuevo y se recomponen en features posteriores.

### Lo que esta feature conserva a propósito

Dos cosas de la pantalla de acceso se ven como defectos y no lo son. Son decisiones de seguridad y
**no se tocan**:

- Después de crear la cuenta, el mensaje dice *"Si el email no estaba registrado, la cuenta fue
  creada. Ya podés iniciar sesión."* Es ambiguo adrede: así nadie puede averiguar qué emails tienen
  cuenta probando altas.
- Crear la cuenta **no deja la sesión abierta**: hay que iniciar sesión después. Por la misma razón.

---

## Clarifications

### Session 2026-09-29

- Q: ¿Modo oscuro sí o no? → A: **Sí.** La app sigue la preferencia de claro u oscuro que la
  persona tenga configurada en su dispositivo. Salda D11-05. Como la paleta es de toda la app, el
  modo oscuro vale para todas las pantallas, no sólo la de acceso.
- Q: ¿Qué tono tiene que transmitir la app? → A: **Violeta o lila, sobrio.** Un violeta como único
  color de acento, sobre neutros; nada de degradados ni de varios colores compitiendo. Reemplaza al
  azul actual.
- Q: ¿Cómo se verifica que se ve bien en el celular? → A: **Con un navegador real en las pruebas
  automáticas**, que mida anchos y áreas tocables en vez de deducirlos de las reglas escritas. Es una
  dependencia de pruebas, no de estilo, y queda justificada en `NFR-003`.
- Q: El usuario pidió una barra de navegación lateral en lugar de los botones, y ver su nombre en
  lugar del email. ¿Cómo se empaquetan? → A: **La barra entra en esta feature**, porque es visual y
  la comparten todas las pantallas. **El nombre va en una feature propia (015)**, porque toca la
  base de datos, el alta de cuenta y el PRD.
- Q: ¿Cómo se ve la navegación en el celular? → A: **Barra lateral en escritorio y barra fija abajo
  en el celular**, con las tres secciones al alcance del pulgar.
- Q: ¿Cómo se carga el nombre? (para la 015) → A: **Obligatorio al crear la cuenta.** Las cuentas
  que ya existen muestran el email hasta que la persona cargue su nombre desde una sección nueva de
  "Mi cuenta". No es alcance de esta feature: queda registrado para no volver a preguntarlo.
- Q: El usuario pidió que el formulario para registrar un movimiento sea más compacto, con campos que
  compartan renglón, "como la fecha y el tipo de gasto". ¿Cómo se agrupan? → A: el tipo (gasto o
  ingreso) pasa a ser dos opciones pegadas en un renglón propio; **monto y moneda** comparten
  renglón, porque se leen juntos ("1500 ARS"); **categoría y fecha** comparten otro; la nota va a lo
  ancho. Como la moneda sube al lado del monto, el orden de los campos cambia.
- Q: El usuario pidió que el resumen del mes sea más compacto. En la pantalla de movimientos, ¿sigue
  mostrando el desglose por categoría? → A: **No: el desglose queda sólo en el dashboard.** En
  movimientos, el resumen muestra ingresado, gastado y balance de cada moneda, que es lo que pide
  `PRD:RF-22` para la pantalla principal; el desglose es `PRD:RF-19`, que el PRD ubica en el
  dashboard.
- Q: Una moneda sin ningún movimiento en el período, ¿cómo se muestra? → A: **En una línea corta**
  ("USD — sin movimientos este mes"). Sigue apareciendo, porque esconderla se leería como si la
  moneda no existiera en el catálogo, pero no ocupa un bloque entero con ceros.
- Q: El usuario pidió que, en categorías, "Renombrar" y "Dar de baja" sean íconos, "como un lápiz y
  una cruz o algo parecido". → A: **Un lápiz para renombrar y un tacho de basura para dar de baja.**
  Se elige el tacho y no la cruz porque en la misma fila, al renombrar, aparece "Cancelar", y una
  cruz se lee como cerrar o cancelar, no como eliminar. La confirmación antes de la baja se conserva.
- Q: El usuario pidió íconos también en los botones de movimientos, como regla general: *"si podemos
  evitar palabras cuando se puede simplificar con un ícono en el botón, mejor; si queda ambiguo,
  entonces es preferible la palabra"*. → A: queda como **criterio para toda la app** (`FR-045`), y
  aplicado botón por botón en `FR-046`.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Una pantalla de acceso que se ve terminada (Priority: P1) 🎯 MVP

Una persona abre la app por primera vez. Ve el nombre de la aplicación, un bloque único con el
formulario, la elección entre "Iniciar sesión" y "Crear cuenta" como dos opciones claramente
distinguibles, y un botón principal que se destaca del resto. Nada le hace pensar que la app está a
medio hacer.

**Why this priority**: es el pedido. Y es la primera pantalla de todas: si se ve rota, nadie llega a
la segunda.

**Independent Test**: se abre la pantalla de acceso sin sesión y se comprueba que tenga nombre de la
app, contenedor del formulario, conmutador con el modo activo distinguible y botón principal con
aspecto de acción principal.

**Acceptance Scenarios**:

1. **Given** una persona sin sesión, **When** abre la app, **Then** ve el nombre de la aplicación y
   el formulario dentro de un único bloque visual, centrado en la pantalla.
2. **Given** la pantalla de acceso, **When** está en "Iniciar sesión", **Then** esa opción se ve
   elegida y "Crear cuenta" se ve disponible, y la diferencia se percibe también sin color.
3. **Given** la pantalla de acceso, **When** la persona mira los botones, **Then** el de enviar
   ("Entrar" o "Crear mi cuenta") es el único con aspecto de acción principal.
4. **Given** la pantalla de acceso, **When** la persona cambia entre los dos modos, **Then** el
   bloque no salta de lugar ni cambia de ancho.

---

### User Story 2 - Crear la cuenta desde el celular (Priority: P1)

Una persona crea su cuenta desde un teléfono de 360 px de ancho. Puede tocar cada campo y cada botón
sin errarle, el texto se lee sin hacer zoom, al tocar un campo la pantalla no se agranda sola y no
tiene que desplazarse de costado.

**Why this priority**: el producto apunta a ser mobile. Una identidad visual que se diseña para
escritorio y después se adapta se hace dos veces.

**Independent Test**: se recorre el alta completa a 360 px de ancho y se comprueba el tamaño de cada
control tocable, el tamaño del texto de los campos y la ausencia de desplazamiento horizontal.

**Acceptance Scenarios**:

1. **Given** una pantalla de 360 px, **When** la persona abre la pantalla de acceso, **Then** el
   bloque del formulario ocupa el ancho disponible dejando margen a los costados, y no hay
   desplazamiento horizontal.
2. **Given** una pantalla de 360 px, **When** la persona toca un campo, **Then** el texto del campo
   tiene tamaño suficiente para que el navegador no amplíe la página por su cuenta.
3. **Given** una pantalla táctil, **When** la persona toca cualquier botón o campo de la pantalla de
   acceso, **Then** el área tocable mide al menos 44 × 44 px.
4. **Given** una pantalla de escritorio, **When** la persona abre la pantalla de acceso, **Then** el
   bloque del formulario conserva un ancho de lectura acotado y no se estira a todo el ancho.

---

### User Story 3 - Que cada situación se vea como lo que es (Priority: P2)

Mientras crea su cuenta, la persona puede encontrarse con un error en un campo, un error que no es
de ningún campo, la confirmación del alta, o el botón esperando la respuesta. Cada una se reconoce
de un vistazo, y la confirmación nunca se confunde con un error.

**Why this priority**: hoy la confirmación del alta es texto plano. Después de crear la cuenta, la
persona no tiene una señal clara de que salió bien y de qué hacer ahora.

**Independent Test**: se provoca cada uno de los cuatro estados en la pantalla de acceso y se
comprueba que cada uno tenga su aspecto propio y que se distingan entre sí también sin color.

**Acceptance Scenarios**:

1. **Given** un email con formato inválido, **When** la persona intenta crear la cuenta, **Then** el
   campo se marca como erróneo —no sólo por color— y el motivo aparece pegado a él.
2. **Given** credenciales incorrectas, **When** la persona intenta entrar, **Then** el error aparece
   como mensaje del formulario, con aspecto de error, y **ningún campo se marca** (sigue sin decir
   cuál de los dos estaba mal).
3. **Given** un alta enviada, **When** llega la respuesta, **Then** la confirmación aparece con
   aspecto de resultado positivo, distinto del de error, y el formulario queda en "Iniciar sesión"
   con el email puesto, como hoy.
4. **Given** un envío en curso, **When** la persona mira el botón, **Then** se ve ocupado y no se
   puede volver a enviar.

---

### User Story 4 - El resto de la app hereda los controles sin romperse (Priority: P2)

Una persona que ya tiene cuenta entra y usa movimientos, categorías y el dashboard. Los botones,
campos y mensajes tienen el aspecto nuevo; la disposición de esas pantallas es la de siempre, y nada
quedó desbordado, tapado o ilegible.

**Why this priority**: es la consecuencia inevitable de la P1 —los controles básicos son de toda la
app— y hay que verificarla en vez de suponerla.

**Independent Test**: se recorren las pantallas de movimientos, categorías y dashboard a 360 px y en
escritorio, y la suite existente de esas pantallas sigue en verde sin modificarse.

**Acceptance Scenarios**:

1. **Given** cualquier pantalla con sesión, **When** se muestra, **Then** sus botones y campos usan el
   aspecto nuevo y cumplen el mismo contraste que en la pantalla de acceso.
2. **Given** la pantalla de movimientos a 360 px, **When** se muestra con el listado, **Then** la
   página no se desplaza de costado (el desplazamiento sigue confinado a la tabla).
3. **Given** una pantalla con varios botones de acción (editar, eliminar, filtrar), **When** se
   muestra, **Then** ninguno toma el aspecto de acción principal por accidente.

---

### User Story 5 - Moverse por la app desde una barra de navegación (Priority: P1)

Una persona con sesión iniciada ve siempre las tres secciones de la app —Movimientos, Dashboard y
Categorías— y sabe en cuál está. En la computadora las tiene en una barra al costado; en el celular,
en una barra abajo, al alcance del pulgar. Pasa de una sección a otra con un toque, sin volver
primero a movimientos. También ve con qué cuenta está trabajando y puede cerrar la sesión.

**Why this priority**: lo pidió el usuario. Y hoy la navegación es un camino de ida y vuelta:
movimientos es el centro, y para ir del dashboard a categorías hay que pasar por ahí.

**Independent Test**: se inicia sesión y se recorren las tres secciones desde la barra, en escritorio
y a 360 px, comprobando que la sección actual se marque y que se pueda cerrar la sesión desde ambos
anchos.

**Acceptance Scenarios**:

1. **Given** una persona con sesión en escritorio, **When** está en cualquier sección, **Then** ve una
   barra lateral con las tres secciones, la cuenta con la que está trabajando y la opción de cerrar
   sesión.
2. **Given** una persona con sesión en un teléfono de 360 px, **When** está en cualquier sección,
   **Then** ve una barra fija abajo con las tres secciones, y la cuenta y el cierre de sesión siguen a
   su alcance.
3. **Given** la barra de navegación, **When** la persona mira las secciones, **Then** la sección
   actual se distingue de las otras también sin color, y los lectores de pantalla la anuncian como la
   página actual.
4. **Given** la persona en el dashboard, **When** elige "Categorías" en la barra, **Then** llega a
   categorías directamente, sin pasar por movimientos.
5. **Given** una persona que usa sólo el teclado, **When** recorre la barra, **Then** puede llegar a
   cada sección y al cierre de sesión, y activarlos.
6. **Given** el teléfono con la barra inferior, **When** la persona desplaza hasta el final de una
   sección, **Then** la barra no tapa el último contenido de la pantalla.

---

### User Story 6 - Registrar un movimiento con un formulario compacto (Priority: P2)

Una persona registra un gasto. El formulario ocupa mucho menos alto que hoy: elige gasto o ingreso,
escribe el monto con la moneda al lado, elige categoría y fecha en el mismo renglón, agrega una nota
si quiere, y guarda. En el celular ve el formulario casi entero sin desplazarse.

**Why this priority**: lo pidió el usuario. Es lo que más se usa de la app: se carga un movimiento por
cada gasto, todos los días.

**Independent Test**: se abre el formulario de alta a 360 px y en escritorio, se comprueba la
agrupación de los campos y su altura total, y se registra un movimiento completo con el teclado.

**Acceptance Scenarios**:

1. **Given** el formulario de alta en escritorio, **When** se muestra, **Then** el tipo ocupa un
   renglón, monto y moneda comparten otro, categoría y fecha comparten otro, y la nota va a lo ancho.
2. **Given** el formulario de alta a 360 px, **When** se muestra, **Then** monto y moneda siguen en el
   mismo renglón; categoría y fecha comparten renglón si cada una conserva su ancho mínimo legible,
   y si no, se apilan. Nunca provocan desplazamiento horizontal.
3. **Given** el formulario de alta, **When** la persona lo recorre con el teclado, **Then** el orden
   es tipo, monto, moneda, categoría, fecha, nota, guardar: el mismo en que se ven.
4. **Given** un error en un campo que comparte renglón, **When** aparece el motivo, **Then** se
   muestra pegado a ese campo y no desarma el renglón del otro.
5. **Given** la ventana para editar un movimiento, **When** se abre, **Then** usa la misma agrupación
   que el alta.

---

### User Story 7 - Un resumen del mes que se lee de un vistazo (Priority: P2)

Una persona entra a movimientos y ve arriba, en poco espacio, cuánto ingresó, cuánto gastó y su
balance del mes, moneda por moneda. Una moneda que no usó en el mes aparece en una sola línea. El
detalle por categoría lo encuentra en el dashboard.

**Why this priority**: lo pidió el usuario. Hoy el resumen de movimientos incluye el desglose por
categoría con sus barras para cada moneda, y empuja el formulario y el listado —lo que se usa todos
los días— muy abajo.

**Independent Test**: se abre movimientos con movimientos en una moneda y ninguno en otra, y se
comprueba la forma de cada una y la ausencia del desglose; después se abre el dashboard y se
comprueba que el desglose sigue ahí.

**Acceptance Scenarios**:

1. **Given** movimientos en pesos en el mes, **When** la persona abre movimientos, **Then** el
   resumen muestra para pesos ingresado, gastado y balance en un único renglón de tres cifras, con su
   nombre arriba de cada una.
2. **Given** ningún movimiento en dólares en el mes, **When** la persona abre movimientos, **Then**
   dólares aparece en una sola línea que dice que no tuvo movimientos en el período.
3. **Given** el resumen en movimientos, **When** se muestra, **Then** no incluye el desglose por
   categoría.
4. **Given** el dashboard, **When** se muestra, **Then** incluye el desglose por categoría de cada
   moneda, como hoy.
5. **Given** un balance negativo, **When** se muestra, **Then** se distingue como negativo también sin
   color (por el signo).
6. **Given** un teléfono de 360 px, **When** se muestra el resumen, **Then** las tres cifras de una
   moneda entran sin desplazamiento horizontal, aun con montos de siete cifras.

---

### User Story 8 - Íconos en lugar de palabras donde no hay ambigüedad (Priority: P3)

En la lista de categorías y en el listado de movimientos, cada fila muestra un lápiz para editar y un
tacho para eliminar, en lugar de dos botones con texto largo. En el resto de la app, los botones
cuyo significado un ícono no dice sin dudas —guardar, aplicar, cancelar, confirmar una eliminación—
conservan su palabra.

**Why this priority**: lo pidió el usuario. Hoy cada fila lleva dos botones con texto, a veces con el
nombre repetido ("Renombrar Supermercado", "Dar de baja Supermercado"), y en el teléfono la fila se
parte en varias líneas.

**Independent Test**: se abren categorías y movimientos y se comprueba que cada fila tenga los dos
íconos, que cada uno se anuncie con su acción y con qué fila es, y que las eliminaciones sigan
pidiendo confirmación con palabras.

**Acceptance Scenarios**:

1. **Given** la lista de categorías, **When** se muestra, **Then** cada fila tiene un ícono de lápiz
   para renombrar y un ícono de tacho para dar de baja.
2. **Given** un lector de pantalla, **When** llega a un ícono, **Then** lo anuncia con la acción y la
   categoría, como hoy ("Renombrar Supermercado").
3. **Given** una persona con mouse, **When** apoya el puntero sobre un ícono, **Then** ve el nombre de
   la acción; y con el teclado, al llegar con el foco, también.
4. **Given** el tacho de una categoría, **When** la persona lo toca, **Then** aparece la misma
   confirmación que hoy, con su advertencia de que no se puede deshacer.
5. **Given** un teléfono de 360 px, **When** se muestra la lista, **Then** el nombre y los dos íconos
   de cada categoría entran en un solo renglón.
6. **Given** el listado de movimientos, **When** se muestra, **Then** cada movimiento tiene un lápiz
   para editar y un tacho para eliminar, y un lector de pantalla anuncia de qué movimiento es cada
   uno.
7. **Given** el tacho de un movimiento, **When** la persona lo toca, **Then** la confirmación aparece
   como hoy, con sus dos opciones en palabras.
8. **Given** cualquier formulario de la app, **When** se muestra, **Then** su botón de envío conserva
   la palabra.

---

### User Story 9 - Usar la app en modo oscuro (Priority: P2)

Una persona tiene su teléfono en modo oscuro. Abre la app de noche y la pantalla no la encandila:
la app se muestra en oscuro, con el mismo carácter violeta y sobrio, y todo se lee igual de bien.

**Why this priority**: lo pidió el usuario, y es el momento barato de hacerlo: se eligen los colores
una sola vez para los dos modos, en lugar de revisar cada uno otra vez más adelante.

**Independent Test**: se abre la app con la preferencia del dispositivo en oscuro y se comprueba que
la paleta sea la oscura y que cada par de contraste cumpla su umbral también en ese modo.

**Acceptance Scenarios**:

1. **Given** un dispositivo configurado en modo oscuro, **When** la persona abre la app, **Then** la
   pantalla de acceso se muestra con la paleta oscura desde la primera pintura, sin un destello
   blanco antes.
2. **Given** un dispositivo configurado en modo claro, **When** la persona abre la app, **Then** se
   muestra con la paleta clara.
3. **Given** la app abierta, **When** la persona cambia la preferencia del dispositivo, **Then** la
   app pasa al otro modo sin recargar y sin perder lo que estaba escribiendo.
4. **Given** el modo oscuro, **When** la persona entra a movimientos, categorías o el dashboard,
   **Then** esas pantallas también se muestran en oscuro y se leen igual de bien.

---

### Edge Cases

- **Un mensaje de error largo** (varios motivos del servidor juntos) se parte en varias líneas dentro
  del bloque, sin ensancharlo.
- **Un email muy largo** en el campo se desplaza dentro del campo, no ensancha el bloque.
- **La persona agranda el texto del navegador al 200 %**: el formulario sigue usable, sin
  superposiciones, y el bloque crece en alto, no en ancho.
- **La persona tiene activada la preferencia de reducir movimiento**: si hay transiciones, se
  suprimen.
- **El gestor de contraseñas del navegador rellena los campos**: el campo rellenado sigue legible con
  la paleta nueva.
- **El botón en estado "Creando…"** no cambia de ancho respecto de "Crear mi cuenta", para que el
  bloque no salte.
- **Los controles que dibuja el propio navegador** (el selector de fecha, las listas desplegables,
  las barras de desplazamiento) siguen el modo oscuro y no aparecen blancos en medio de la pantalla
  oscura.
- **Una sección con un formulario a medio completar**: si la persona cambia de sección desde la barra,
  pasa lo mismo que hoy cuando aprieta un botón para irse. La barra no agrega ni quita avisos.
- **Un email muy largo en la barra**: se corta con puntos suspensivos y no ensancha la barra; el email
  completo sigue disponible para quien lo quiera leer.
- **El teléfono con barra de gestos o esquinas redondeadas**: la barra inferior no queda debajo de la
  zona que el sistema operativo usa para sus gestos.
- **Pantalla muy alta y angosta, o apaisada y baja** (teléfono girado): el bloque no queda cortado y
  se puede desplazar verticalmente hasta el botón.

## Requirements *(mandatory)*

### Functional Requirements

**El sistema visual**

- **FR-001**: La app DEBE tener un sistema visual único y con nombre, del que salen todos los
  aspectos visuales: paleta de colores con rol (fondo, superficie, texto, texto secundario, acento,
  texto sobre acento, error, éxito, borde, foco), escala tipográfica de al menos cuatro niveles,
  escala de espaciado y radios de borde.
- **FR-002**: Todo color del sistema DEBE declarar contra qué fondo se usa, y cada par DEBE medirse
  contra su umbral de contraste. Un color nuevo sin par declarado DEBE poner la verificación en
  rojo, igual que hoy.
- **FR-003**: Los botones DEBEN tener al menos dos variantes distinguibles: **principal** (la acción
  que completa lo que la persona vino a hacer) y **secundaria** (todo lo demás). Una pantalla DEBE
  tener como máximo una acción principal a la vista por formulario.
- **FR-004**: Los campos DEBEN tener aspecto propio para los estados normal, con foco, con error y
  deshabilitado. El estado de error DEBE distinguirse por algo más que el color.
- **FR-005**: Los mensajes de error y de confirmación DEBEN tener aspecto propio y distinto entre sí,
  y ninguno DEBE depender sólo del color para reconocerse.
- **FR-006** *(corregido el 2026-10-04, a pedido de quien usa la app)*: La tipografía DEBE servirse
  **desde el repositorio** y NO DEBE pedirse a ningún servidor externo. La del sistema operativo
  queda como respaldo mientras la fuente carga y si no carga.

  Decía "DEBE ser la del sistema operativo". El motivo escrito eran dos cosas —no descargar nada y no
  contarle a un tercero quién abre la app— y **sólo la segunda era un requisito**: es sobre la
  privacidad de quien usa la app, y se conserva entera sirviendo el archivo desde el repositorio. La
  primera era el precio que se pagaba por la segunda, y ahora cuesta 20 KB una vez. Un `<link>` a un
  servidor de fuentes habría sido una línea y habría roto la parte que importa en silencio.
- **FR-007**: El texto de los campos DEBE medir al menos 16 px, para que los navegadores de teléfono
  no amplíen la página al tocar un campo.

**La pantalla de acceso**

- **FR-008**: La pantalla de acceso DEBE mostrar el nombre de la aplicación y el formulario dentro de
  un único bloque visual, centrado horizontalmente, con ancho acotado en escritorio y con margen
  lateral a 360 px.
- **FR-009**: El conmutador entre "Iniciar sesión" y "Crear cuenta" DEBE verse como un par de
  opciones con una elegida, y la opción elegida DEBE distinguirse también sin color. El estado
  elegido DEBE seguir saliendo del mismo atributo que hoy lo anuncia a los lectores de pantalla, para
  que lo que se ve y lo que se anuncia no puedan desincronizarse.
- **FR-010**: Todo control tocable de la pantalla de acceso DEBE tener un área de al menos 44 × 44 px.
- **FR-011**: La pantalla de acceso NO DEBE provocar desplazamiento horizontal de la página en ningún
  ancho entre 360 px y 1440 px, ni con el texto del navegador al 200 %.
- **FR-012**: El bloque del formulario NO DEBE cambiar de ancho al alternar entre los dos modos ni
  al pasar el botón a su estado de envío.

**Lo que no cambia**

**La navegación**

- **FR-022**: Toda pantalla con sesión DEBE mostrar una barra de navegación con las tres secciones
  —Movimientos, Dashboard, Categorías—, la cuenta con la que se está trabajando y la opción de cerrar
  sesión. Reemplaza a los botones de navegación de la pantalla de movimientos y a los botones
  "Volver" de las otras dos.
- **FR-023**: En anchos de escritorio la barra DEBE ubicarse al costado de la pantalla y permanecer a
  la vista mientras se desplaza el contenido. En anchos de teléfono DEBE ubicarse fija en la parte
  inferior con las tres secciones; la cuenta y el cierre de sesión DEBEN seguir alcanzables desde
  cualquier sección. El ancho exacto en que se pasa de una a otra se fija en el plan.
- **FR-024**: La sección actual DEBE distinguirse de las otras también sin color, y DEBE anunciarse a
  los lectores de pantalla como la página actual.
- **FR-025**: Cada sección de la barra DEBE poder alcanzarse y activarse con el teclado, y tener un
  área tocable de al menos 44 × 44 px.
- **FR-026**: La barra inferior NO DEBE tapar contenido: el final de cada sección DEBE poder verse
  entero por encima de ella, y la barra DEBE respetar las zonas reservadas del sistema operativo del
  teléfono.
- **FR-027**: La cuenta DEBE mostrarse por su email, como hoy. La feature 015 la reemplaza por el
  nombre; esta feature sólo deja el lugar donde va a ir.
- **FR-028**: Al iniciar sesión, la sección inicial DEBE seguir siendo Movimientos.

**El formulario de movimiento**

- **FR-029** *(corregido el 2026-10-04, a pedido de quien usa la app)*: El formulario para registrar
  un movimiento DEBE presentarse **dentro de un recuadro**, igual que el resumen, y agrupar los
  campos así: **el tipo y la fecha en el renglón de arriba**, el tipo como dos opciones pegadas a la
  izquierda y la fecha a la derecha; **la categoría a lo ancho**; **monto y moneda en un mismo
  renglón**; la nota a lo ancho; y el botón de guardar al final.

  El orden anterior —tipo, monto y moneda, categoría y fecha— venía de research D-07, que puso el
  monto segundo por ser el dato principal. **La práctica dijo otra cosa**: primero lo que encuadra el
  movimiento (de qué tipo es y cuándo fue), después en qué se gastó, y al final cuánto. Entre una
  cuenta y alguien cargando sus gastos todos los días, gana el segundo.

  El tipo se achica y deja de ocupar el ancho entero, pero **conserva sus 44 px de alto**: el piso de
  área tocable no se negocia (`FR-010`, `FR-025`).
- **FR-030** *(corregido el 2026-10-04 junto con `FR-029`)*: En pantallas angostas, monto y moneda
  DEBEN mantenerse en el mismo renglón. **El tipo y la fecha** DEBEN compartir el renglón de arriba
  sólo cuando la fecha conserve un ancho mínimo legible, fijado en el plan; si no, la fecha DEBE
  bajar a su propio renglón. Las dos opciones del tipo DEBEN quedar **siempre lado a lado**, nunca
  una encima de la otra. Ninguna agrupación DEBE provocar desplazamiento horizontal. *(Corregido en
  el plan: el borrador decía "sin cortar el nombre de la categoría", y un nombre puede tener hasta 50
  caracteres, que no entran enteros en ningún renglón compartido de 360 px.)*
- **FR-031**: El orden en que se recorren los campos con el teclado y en que los anuncia un lector de
  pantalla DEBE coincidir con el orden en que se ven.
- **FR-032**: El formulario compacto DEBE ocupar, a 360 px y sin errores a la vista, menos alto que el
  actual. La reducción se mide en el plan contra la altura de hoy.
- **FR-033**: La ventana de edición de un movimiento DEBE usar la misma agrupación que el alta.
- **FR-048** *(nuevo el 2026-10-04, a pedido de quien usa la app)*: La barra de acotado del listado
  DEBE mostrarse **debajo del encabezado "Movimientos del mes"**, dentro de la sección del listado.
  Hasta acá quedaba entre el formulario de carga y la tabla, donde se leía como un paso más del
  formulario en lugar de como lo que es: lo que acota la tabla que tiene debajo. DEBE verse también
  cuando el acotado no devolvió ningún movimiento, que es justamente cuando hace falta ensancharlo.
- **FR-034**: Qué campos existen, qué valores aceptan, qué se propone por defecto y qué se valida NO
  DEBE cambiar: el cambio es de disposición.

**El resumen del mes**

- **FR-035**: En la pantalla de movimientos, el resumen DEBE mostrar para cada moneda ingresado,
  gastado y balance, y NO DEBE mostrar el desglose por categoría.
- **FR-036**: En el dashboard, el resumen DEBE seguir mostrando el desglose por categoría de cada
  moneda.
- **FR-037** *(corregido el 2026-10-04, al levantar la app y mirarla)*: Las tres cifras de una moneda
  DEBEN mostrarse **cada una con su nombre y sin pisarse nunca**, con montos de hasta siete cifras
  enteras. Desde 48rem comparten un único renglón; por debajo se apilan, con el nombre a la
  izquierda y el monto a la derecha.

  **Decía "en un único renglón también a 360 px", y era imposible.** Cada monto de siete cifras
  necesita 107 px y la columna mide 90: para que las tres entraran habría que bajar la letra a unos
  12 px, por debajo del escalón más chico del sistema visual y justo en el dato más importante de la
  pantalla. Lo peor es **cómo fallaba**: con `white-space: nowrap` un monto que no entra no se corta,
  se sale de su columna y pinta encima del de al lado. Las tres seguían compartiendo renglón y la
  página no desbordaba, así que las dos pruebas que existían daban verde describiendo una pantalla
  ilegible. El detalle está en `research.md`, D-13.
- **FR-038**: Una moneda sin ningún movimiento en el período DEBE mostrarse en una sola línea que lo
  diga, en las dos pantallas. Como un monto siempre es mayor a cero, "sin movimientos" equivale a
  ingresado y gastado en cero; una moneda con movimientos cuyo balance da cero se muestra completa.
- **FR-039**: El período ("del 1 al 30 de septiembre") DEBE seguir visible en el encabezado del
  resumen, y la aclaración de que el resumen y el listado no miran lo mismo DEBE seguir apareciendo
  cuando corresponde.
- **FR-040**: Los totales NO DEBEN cambiar: el cambio es de presentación, y los números que se
  muestran siguen siendo los que calcula el servidor.

**Las acciones de categoría**

- **FR-041**: Cada categoría de la lista DEBE ofrecer renombrar con un ícono de lápiz y dar de baja
  con un ícono de tacho de basura, en lugar de botones con texto.
- **FR-042**: Cada ícono DEBE tener nombre accesible con la acción y la categoría, el mismo texto que
  hoy tienen los botones, y DEBE mostrar el nombre de la acción al apoyar el puntero y al recibir el
  foco.
- **FR-043**: Cada ícono DEBE tener un área tocable de al menos 44 × 44 px, y el nombre de la categoría
  con sus dos íconos DEBE entrar en un renglón a 360 px; un nombre largo se corta con puntos
  suspensivos antes de empujar los íconos a otra línea.
- **FR-044**: La baja DEBE seguir pidiendo confirmación, con la misma advertencia. El renombre DEBE
  seguir funcionando igual; sus botones "Guardar" y "Cancelar" conservan el texto.

**Íconos o palabras en los botones**

- **FR-045**: El criterio para toda la app DEBE ser: un botón se muestra **sólo con ícono** cuando el
  ícono se entiende sin dudas en ese lugar; si queda cualquier duda, el botón lleva **la palabra**. En
  la práctica, un botón lleva palabra cuando su acción:
  - envía un formulario o confirma algo (guardar, entrar, aplicar);
  - confirma o descarta una acción que no se puede deshacer, donde la palabra dice exactamente qué
    pasa;
  - tiene un ícono que se lee de dos maneras (una cruz puede ser cerrar, cancelar o borrar).

  Todo botón sólo con ícono DEBE tener nombre accesible, mostrar el nombre de su acción al apoyar el
  puntero y al recibir el foco, y un área tocable de al menos 44 × 44 px.
- **FR-046**: Aplicado a los botones que existen hoy:

  | Botón | Cómo queda | Por qué |
  |-------|-----------|---------|
  | Editar un movimiento | ícono de lápiz | acción de fila; el lápiz es inequívoco |
  | Eliminar un movimiento | ícono de tacho | acción de fila; el tacho es inequívoco |
  | Renombrar una categoría | ícono de lápiz | ídem |
  | Dar de baja una categoría | ícono de tacho | ídem |
  | Secciones de la barra | ícono y palabra | son lugares, no acciones; `FR-022` |
  | Cerrar sesión | palabra | un ícono de "salir" se confunde con volver o cerrar la ventana |
  | Entrar, Crear mi cuenta, Guardar, Crear categoría, Aplicar | palabra | envían un formulario |
  | Confirmar y eliminar, No eliminar, Confirmar la baja, No dar de baja | palabra | deciden algo irreversible |
  | Cancelar (al editar o renombrar) | palabra | una cruz se lee también como borrar |
  | Iniciar sesión / Crear cuenta (conmutador) | palabra | son dos modos, no acciones |

  Un botón que se agregue después se decide con `FR-045`.
- **FR-047**: El nombre accesible de editar y eliminar un movimiento DEBE decir de qué movimiento se
  trata, como ya lo hace hoy el de eliminar. Hoy el de editar dice sólo "Editar", y con varios
  movimientos en la lista un lector de pantalla anuncia muchos "Editar" iguales.

**Lo que no cambia**

- **FR-013**: El flujo de acceso DEBE quedar idéntico: los mismos dos modos, los mismos textos —en
  particular el mensaje posterior al alta—, el alta sin sesión abierta, el paso a "Iniciar sesión"
  con el email puesto, y el rechazo de credenciales sin marcar ningún campo.
- **FR-014**: La pantalla de acceso DEBE seguir cumpliendo `PRD:RNF-06`: contraste AA, foco visible
  en todo control, etiqueta asociada a todo campo, y uso completo con el teclado.
- **FR-015**: Las pantallas de movimientos, categorías y dashboard DEBEN heredar los elementos básicos
  del sistema visual y quedar dentro del marco de la navegación sin que se modifique la disposición
  de su contenido, y sin perder nada de lo que hoy cumplen:
  contraste, foco visible, ausencia de desplazamiento horizontal de la página.
- **FR-016**: Si el sistema visual incluye transiciones o animaciones, DEBEN suprimirse cuando la
  persona tiene activada la preferencia de reducir movimiento.

**Modo oscuro y carácter**

- **FR-017**: El sistema visual DEBE tener dos paletas, clara y oscura, con los mismos roles de color,
  y la app DEBE mostrar la que corresponda a la preferencia configurada en el dispositivo de la
  persona, en todas las pantallas. Si el dispositivo no expresa preferencia, se usa la clara.
- **FR-018**: La app DEBE cambiar de paleta cuando la persona cambia la preferencia del dispositivo,
  sin recargar la página y sin perder lo que haya escrito en un formulario.
- **FR-019**: La paleta que corresponde DEBE aplicarse desde la primera pintura: la app no DEBE
  mostrarse un instante en claro antes de pasar a oscuro.
- **FR-020**: Los controles que dibuja el propio navegador DEBEN seguir el modo activo.
- **FR-021**: El carácter visual DEBE ser **sobrio, con un violeta o lila como único color de
  acento** sobre neutros, en los dos modos. El violeta reemplaza al azul actual en todos sus usos:
  acento, foco y barra del dashboard. No DEBE haber degradados ni un segundo color de acento; el
  error y el éxito tienen su color porque son estados, no decoración.

### Non-Functional Requirements

- **NFR-001**: El texto normal DEBE cumplir contraste de al menos 4,5:1 sobre su fondo, y el texto
  grande, los bordes de los controles y el anillo de foco al menos 3:1, para **cada** fondo sobre el
  que se usen (página, superficie del bloque, botón principal) **y en cada uno de los dos modos**.
  Un par que se mida en un modo y no en el otro DEBE poner la verificación en rojo.
- **NFR-002**: Esta feature NO DEBE agregar dependencias para el estilo —librerías de componentes,
  frameworks de CSS, fuentes—. Si en el plan aparece una razón para agregar una, se justifica en esta
  spec antes de agregarla.
- **NFR-003**: Las pruebas automáticas DEBEN incluir un navegador real que **mida**, en la pantalla
  de acceso: la ausencia de desplazamiento horizontal a 360 px, 768 px y 1440 px (`FR-011`); el área
  de cada control tocable (`FR-010`); el ancho estable del bloque al cambiar de modo y al enviar
  (`FR-012`); y la paleta aplicada con la preferencia en claro y en oscuro (`FR-017`). En la parte con
  sesión DEBE medir: la ausencia de desplazamiento horizontal a 360 px en movimientos, categorías y
  dashboard (`FR-015`); la ubicación de la barra —al costado en escritorio, abajo en el teléfono—
  (`FR-023`); el área tocable de cada sección (`FR-025`); que el final del contenido quede visible
  por encima de la barra inferior (`FR-026`); la agrupación del formulario de movimiento a 360 px y
  en escritorio, con su altura (`FR-029`, `FR-030`, `FR-032`); el resumen en un renglón por moneda a
  360 px (`FR-037`); la fila de categoría en un renglón a 360 px (`FR-043`); y el área tocable de
  todo botón sólo con ícono, en categorías y en movimientos (`FR-045`).

  **Justificación de la dependencia**, como pide `AGENTS.md`: el simulador de navegador que usan hoy
  los tests no calcula tamaños, así que sólo puede comprobar que una regla esté escrita, no que la
  pantalla se vea como debe. Por eso D11-01 y D12-01 siguen abiertas desde la feature 011. Una
  feature cuyo contenido es entero visual no se puede dar por verificada sin medir, y la versión
  mobile va a necesitar esta misma herramienta. Es una dependencia **de pruebas**: no llega a la app
  que usa la persona. La prohibición de `NFR-004` de la feature 011 era para esa feature y se
  levanta acá con este motivo.
- **NFR-004**: Las pruebas existentes del formulario de acceso DEBEN seguir en verde **sin
  modificarse**. Que una de ellas necesite cambiar es la señal de que el flujo cambió, y eso está
  fuera de alcance.
- **NFR-005**: Las pruebas con navegador real DEBEN correr también en el CI, igual que el resto de la
  puerta. Si en el CI resultan inestables por medir tiempos, sólo pueden excluirse las que miden
  tiempo, nunca las que miden tamaños.

### Key Entities

- **Sistema visual**: el conjunto con nombre de colores con rol, niveles de tipografía, pasos de
  espaciado y radios. Es la única fuente de cualquier aspecto visual de la app.
- **Par de contraste**: un color de frente, el fondo sobre el que se usa y el umbral que tiene que
  cumplir. Cada color del sistema pertenece al menos a uno.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100 % de los pares de contraste declarados cumple su umbral, y no queda ningún color
  del sistema sin par.
- **SC-002**: Una persona completa el alta desde un teléfono de 360 px sin hacer zoom y sin
  desplazarse de costado, en menos de un minuto.
- **SC-003**: El 100 % de los controles tocables de la pantalla de acceso mide al menos 44 × 44 px.
- **SC-004**: Los cuatro estados de la pantalla de acceso —error de campo, error general,
  confirmación, envío en curso— se distinguen entre sí vistos en escala de grises.
- **SC-005**: Cero cambios de comportamiento del acceso: las pruebas existentes del formulario de
  acceso pasan sin editar una línea.
- **SC-006**: Cero regresiones en el contenido de las demás pantallas: sus pruebas siguen en verde, y
  ninguna produce desplazamiento horizontal de la página a 360 px. **Hay que distinguir dos clases de
  cambio en las pruebas existentes**, porque sólo una de las dos es una decisión de producto:

  **a) Cambian lo que afirman** —y cada una se justifica por el requisito que lo causa—: las de
  **navegación**, porque los botones de ir y "Volver" los reemplaza la barra (`FR-022`); las que
  fijan el **orden de los campos** del formulario de movimiento (`FR-029`); la que espera el
  **desglose en la pantalla de movimientos** (`FR-035`); la que espera una moneda vacía **con sus
  totales en cero** (`FR-038`); y las que buscan el botón "Editar", porque su nombre pasa a decir qué
  movimiento edita (`FR-047`).

  **b) Cambian sólo para compilar**: toda prueba que monte `PantallaMovimientos`,
  `PantallaCategorias` o `PantallaDashboard` les pasa hoy las props de navegación, que son
  obligatorias y que `FR-022` elimina. Borrar una prop que ya no existe no es una decisión de
  producto y no necesita justificarse requisito por requisito; lo que **no** puede cambiar son sus
  aserciones. Son seis archivos, listados en
  [research.md, D-11](./research.md#d-11--qué-pruebas-existentes-cambian-y-por-qué). En total cambian
  **once** archivos de prueba: cinco por el grupo (a) y seis por el grupo (b).

  **Las que no cambian en nada**: `FormularioAcceso.test.tsx` (`NFR-004`), y las **aserciones** de
  categorías —los íconos conservan el nombre accesible de los botones actuales (`FR-042`)— y las de
  eliminar un movimiento.
- **SC-007**: Cero dependencias de estilo nuevas. La única dependencia nueva es la del navegador de
  pruebas, justificada en `NFR-003`.
- **SC-008**: Con el dispositivo en modo oscuro, el 100 % de las pantallas se muestra en oscuro desde
  la primera pintura, y el 100 % de los pares de contraste del modo oscuro cumple su umbral.
- **SC-009**: Desde cualquier sección se llega a cualquier otra con un solo toque o clic.
- **SC-010**: Una persona registra un gasto completo a 360 px viendo todos los campos y el botón de
  guardar con, como máximo, un desplazamiento vertical.
- **SC-011**: El resumen del mes en movimientos, con dos monedas y una de ellas sin movimientos, ocupa
  menos de la mitad del alto que ocupa hoy con los mismos datos.

## Assumptions

- **No hay logotipo ni material de marca.** El nombre de la aplicación, en texto, hace de marca. Si
  aparece un logo, se suma después sin cambiar el sistema.
- **La tipografía del sistema alcanza.** Se lee bien en cada plataforma, no hay que descargarla y no
  le cuenta a ningún tercero quién abre la app.
- **44 × 44 px** es la medida de área tocable que piden las guías de las dos plataformas mobile
  principales; WCAG AA pide 24 × 24 y ésta la supera.
- **360 px** sigue siendo el ancho mínimo objetivo, el mismo que fijó la feature 011.
- **Las demás pantallas conservan su disposición**: si con los controles nuevos alguna se ve
  desprolija pero funciona y cumple, se anota como deuda y se resuelve al recomponerla, no acá.
- **Las barras del dashboard no cambian de criterio**: siguen de un solo color (D11-04). Toman el
  violeta nuevo, pero no pasan a codificar categorías por color.
- **Los íconos se dibujan dentro del proyecto**, igual que el gráfico del dashboard (ADR-002): son
  **siete** —las tres secciones, el lápiz y el tacho (estos dos compartidos entre categorías y
  movimientos), y el de error y el de éxito de los mensajes—, y una librería de íconos sería una
  dependencia de estilo que `NFR-002` no permite. *(El borrador decía cinco: contaba los de navegar
  y los de las filas, y no los dos de los mensajes, que salen de `FR-005` —cada estado se reconoce
  por algo más que el color— y quedaron definidos recién en el plan.)* Cada ícono de navegación va
  acompañado de su texto: un ícono solo no le dice a nadie adónde lleva.
- **En el teléfono, la cuenta y el cierre de sesión van en una franja superior** chica, y no como
  cuarto elemento de la barra inferior: la barra es para moverse entre secciones, y cerrar sesión no
  es una sección.
- **El modo sigue al dispositivo y no hay un selector dentro de la app.** Un botón para elegir claro u
  oscuro a mano, distinto de lo que dice el dispositivo, queda fuera de alcance: necesita recordar la
  elección de cada persona, y eso es otra feature.
- **El tono exacto del violeta se elige en el plan**, con el contraste medido en los dos modos. Lo que
  fija esta spec es el carácter —sobrio, un solo acento violeta o lila—, no el código de color.

## Deuda que esta feature salda

> **Estado al cerrar la implementación (2026-10-03).** Lo de abajo era el plan; esto es lo que
> quedó.

- **D11-05 — modo oscuro: SALDADA.** Los dos modos están declarados en `base.css` con los mismos
  roles, `tests/Paleta.test.ts` mide el contraste de **cada par en los dos modos**, y
  `tests/ModoOscuro.navegador.test.tsx` emula la preferencia del dispositivo y comprueba, en las
  cuatro pantallas, que ninguna pinte una superficie clara. El modo lo elige el navegador y nadie
  más: `tests/PrimeraPintura.test.ts` verifica que ningún archivo de `src/` lo elija desde
  JavaScript, que es lo que garantiza que no haya destello en la primera pintura (`FR-019`).
- **D11-01 — el desborde medido en vez de deducido: SALDADA.** Hay un navegador en la suite
  (proyecto `navegador` de Vitest) y seis anchos de referencia compartidos en `tests/anchos.ts`. El
  barrido de cierre `tests/Responsive.navegador.test.tsx` recorre **las cuatro pantallas × los seis
  anchos × los dos modos** afirmando las tres invariantes transversales.

  Y no era una formalidad: ese barrido **encontró un desborde real que estuvo abierto desde la
  feature 011** —el texto oculto del encabezado de acciones se escapaba del envoltorio desplazable y
  corría la página entera a 360 px con el listado lleno—. Ninguna prueba lo había visto porque las
  que medían el desborde montaban el listado vacío. El detalle está en `research.md`, D-13.
- **D12-01 — qué queda abierto.** Lo que esta feature cierra es que **la página no desborde**: eso
  ahora se mide, con una nota larga y en los seis anchos. Lo que sigue abierto es si esa lectura es
  **cómoda**: a 360 px una nota larga empuja las seis columnas que identifican al movimiento fuera
  de la vista y hay que desplazar el envoltorio para volver a verlas. No se resuelve acá porque la
  salida —acotar la columna de la nota— exige cambiar `FR-012`, que es una decisión de producto; va
  junto con el rediseño de la disposición del listado, o sea **D14-01**.

## Una regla anterior que esta feature NO rompe (corregido en el plan)

El primer borrador de esta spec decía que `FR-035` dejaba sin objeto la regla de la feature 010 que
exige que **el desglose del dashboard acotado al mes y el de la pantalla principal coincidan**
(su `FR-013` y `SC-003`). **Era falso**, y se vio al buscar la prueba que la verifica: esa prueba no
compara dos pantallas, compara **dos respuestas del servidor** —el resumen pedido sin período y el
pedido con el mes en curso— (`T031` de la 010, en `ResumenDelPeriodoTests.cs`). El servidor sigue
devolviendo el desglose en las dos, aunque la pantalla de movimientos deje de mostrarlo, así que la
regla y su prueba quedan en pie sin tocarse.

## Una regla anterior que esta feature SÍ rompe, y cómo se salda

`FR-038` deja a **`PRD:AC-31` describiendo algo que la pantalla ya no hace**. El AC decía que sin
ningún movimiento en el período *"el total ingresado, el total gastado y el balance **se muestran en
cero para cada moneda**"*; con esta feature, una moneda sin movimientos se muestra en una línea que
lo dice.

**Lo peligroso es cómo se esconde.** AC-31 describe la pantalla, pero lo verifican **pruebas del
backend** —`ResumenDelPeriodoTests.cs` y `MonedaComoDatoTests.cs`, que miran la respuesta del
servidor—, y el servidor no cambia: sigue devolviendo ceros (`FR-040`). O sea que la suite se queda
entera en verde mientras el PRD describe una pantalla que ya no existe. Es el mismo patrón que ya
pasó con la feature 013 y terminó en la versión 6 del PRD.

**Se salda acá**, no se anota como deuda: el PRD sube a **versión 7** y AC-31 se reescribe **en su
lugar**, partido en dos — AC-31 para lo que se ve y AC-31b para lo que el servidor devuelve, que es
la confusión que lo hacía frágil. La razón original se conserva: la moneda sigue apareciendo.

Esta sección es la simétrica de la de arriba. Aquélla existe porque un borrador afirmó romper una
regla y era falso; ésta, porque romper una y no decirlo es peor.

## Deuda que esta feature deja anotada

| ID | Qué | Por qué no se hace acá | Quién la toma |
|----|-----|------------------------|---------------|
| D14-01 | **Recomponer el resto del contenido**: el listado y los filtros de movimientos, el alta de categorías y el dashboard, con el sistema visual —contenedores, jerarquía— | Esta feature estrena el sistema en la pantalla de acceso, el marco de navegación, el formulario de movimiento, el resumen y las acciones de categoría, para probarlo antes de extenderlo | Las features siguientes de la vuelta visual |
| D14-02 | **Mostrar el nombre de la persona en lugar del email** | Toca la base de datos, el alta de cuenta y el requisito RF-01 del PRD: no es un cambio visual. Ya está decidido cómo: obligatorio en el alta, y las cuentas existentes lo cargan desde "Mi cuenta" mientras se sigue mostrando el email | Feature 015 |

## Dependencies

- Ninguna feature pendiente. Se apoya en la paleta y los verificadores de la feature 011, que
  extiende.

# Feature Specification: Límite de intentos fallidos de inicio de sesión

**Feature Branch**: `003-limite-intentos`

**Created**: 2026-08-26

**Status**: Draft

**Input**: Ticket DISC-001-01b. PRD de referencia:
[`plan-de-implementacion/prds/pendientes/prd-DISC-001-01b.md`](../../plan-de-implementacion/prds/pendientes/prd-DISC-001-01b.md)
(FR-01..FR-06, NFR-01..NFR-03, AC-01..AC-13).

---

## Por qué esta feature

`002-identidad-sesion` entregó el inicio de sesión, y lo entregó **sin ninguna protección contra la
fuerza bruta**: hoy cualquiera puede probar contraseñas contra un email de forma indefinida, tan
rápido como el servidor conteste. Esa ventana quedó registrada como riesgo aceptado en el PRD de
`01a`, con la condición de que este ticket fuera el siguiente. Es el siguiente.

RNF-05 del PRD del producto fija la regla completa: tras 5 intentos fallidos consecutivos sobre un
mismo email, todo intento nuevo sobre ese email se rechaza durante al menos 15 minutos, **sin
revelar si ese email está registrado**.

Lo que hace propio a este ticket es que es la única parte de la autenticación con **estado que no es
la sesión**: un contador por email que hay que guardar en algún lado, que tiene que sobrevivir a un
reinicio y que se vence solo.

### Un ajuste al PRD que este repositorio impone

Queda escrito acá porque cambia **cómo** se verifica, no qué se verifica.

**AC-13 se mide con medianas y no con percentiles 95.** El PRD dice "percentil 95", y así se
escribió al principio. No es medible de esa forma: el p95 de dos series tomadas en una máquina
compartida mide la cola de contención del entorno, no el código — y la mide de forma asimétrica,
porque el rechazo por credenciales hace dos escrituras que el rechazo por bloqueo no hace, así que
bajo carga esa asimetría se amplifica. Medido en una misma corrida: p95 de 114 ms contra 202 ms
—88 ms, rojo— mientras las medianas daban 110 ms contra 119 ms, que son 9 ms. El test fallaba 1 de
cada 2 veces bajo carga y pasaba 5 de 5 aislado, que es la definición de un test intermitente y lo
que el Principio IV prohíbe.

La mediana no pierde nada de lo que hay que atrapar: el fallo que ese test existe para ver es una
diferencia sistemática de ~120 ms, y sobre esa señal la mediana es más sensible que el p95.
Comprobado desarmando la verificación del hash del camino bloqueado: 2 ms contra 118 ms. **AC-12
sigue midiéndose con percentil 95**, porque ahí se mide un solo camino contra sí mismo y no hay dos
series que comparar. El cambio se hizo en la rama de `004`.

**La ventana de 15 minutos no se verifica esperando 15 minutos.** El Principio IV de la constitución
prohíbe tests que dependan del reloj real, y `002` ya resolvió lo mismo para la expiración de la
sesión, adelantando un reloj que los tests controlan. AC-03, AC-06 y AC-11 se verifican adelantando
ese reloj, no durmiendo. El único momento en que se mide tiempo de pared es en AC-12 y AC-13, que son criterios
**sobre** el tiempo y no pueden ser otra cosa.

### Lo que NO entra

Es la lista de *Out of Scope* del PRD, que se repite acá porque cada punto es algo que alguien
podría dar por incluido:

- **Bloqueo por dirección IP o por dispositivo.** FR-06 lo excluye a propósito: el límite es por
  email. Si el riesgo de denegación de servicio se materializa, un límite por IP se suma, no
  reemplaza a éste.
- **CAPTCHA** o cualquier desafío interactivo.
- **Desbloqueo manual**, ni por la persona ni por un administrador: la ventana se levanta sola y no
  hay pantalla ni operación para adelantarla.
- **Avisarle a nadie que su cuenta quedó bloqueada.** El PRD del producto deja fuera todo envío de
  correo.
- **Límite de intentos sobre el alta de cuentas** o sobre cualquier otra operación: esto cubre el
  inicio de sesión.
- **Endurecer la política de contraseñas.**
- **Registro de auditoría de los intentos fallidos.** El contador es estado operativo, no una
  bitácora: no hay pantalla ni consulta que lo lea.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Probar contraseñas deja de ser gratis (Priority: P1)

Alguien que prueba contraseñas contra un email —el suyo o el de otra persona— llega hasta el quinto
intento fallido y a partir de ahí deja de poder intentar: la aplicación rechaza todo intento nuevo
sobre ese email, incluso uno que traiga la contraseña correcta.

**Why this priority**: es la feature. Sin ella no hay nada que verificar en las otras dos historias,
porque no existe ningún bloqueo que se levante ni que oculte nada. Entregada sola ya cierra el
riesgo aceptado de `01a`.

**Independent Test**: fallar cinco veces seguidas contra un email y comprobar que el sexto intento
se rechaza, incluso presentando la contraseña correcta.

**Acceptance Scenarios**:

1. **Given** un email con 5 intentos de inicio de sesión fallidos consecutivos, **When** se intenta
   un sexto, **Then** la aplicación lo rechaza (AC-01).
2. **Given** un email con 5 fallos consecutivos, **When** dentro de los 15 minutos se presenta su
   contraseña **correcta**, **Then** el intento se rechaza y no queda ninguna sesión iniciada
   (AC-02).
3. **Given** un email bloqueado hace menos de 15 minutos, **When** se intenta cualquier acceso sobre
   él, **Then** se sigue rechazando (AC-03).
4. **Given** un email con 4 fallos consecutivos —uno menos que el límite—, **When** se presenta la
   contraseña correcta, **Then** la sesión se inicia normalmente (AC-04).
5. **Given** el email A con 5 fallos consecutivos, **When** se presenta la contraseña correcta del
   email B, **Then** la sesión de B se inicia: el bloqueo alcanza a un email, no a la aplicación
   (AC-07).
6. **Given** un email con 5 fallos consecutivos hechos desde un navegador, **When** se intenta
   acceder a ese mismo email desde otro navegador dentro de la ventana, **Then** también se rechaza:
   lo que está bloqueado es el email, no quien lo intenta (AC-10).
7. **Given** un email dentro de su ventana de bloqueo, **When** la aplicación se reinicia, **Then**
   se lo sigue rechazando hasta que se cumplan los 15 minutos (AC-11).
8. **Given** el inicio de sesión con la comprobación del límite activa, **When** se lo compara con
   el mismo inicio de sesión sin ella sobre 100 ejecuciones, **Then** la diferencia en el percentil
   95 es de a lo sumo 50 ms (AC-12).

---

### User Story 2 - El bloqueo se levanta solo (Priority: P2)

La persona a la que le pasó de olvidarse la contraseña y fallar cinco veces recupera el acceso sola:
espera, vuelve a intentar con la contraseña correcta y entra. Nadie tuvo que desbloquearla, y sus
fallos anteriores dejaron de contar.

**Why this priority**: sin esto el bloqueo es permanente y la feature convierte un olvido en una
cuenta perdida, porque no hay recuperación de contraseña ni desbloqueo manual. Va después de la
historia 1 porque necesita que exista el bloqueo para poder levantarlo.

**Independent Test**: bloquear un email, adelantar el reloj más de 15 minutos, y comprobar que la
contraseña correcta vuelve a iniciar sesión sin que nadie haya intervenido.

**Acceptance Scenarios**:

1. **Given** un email con fallos previos que no llegaron a bloquearlo, **When** un intento sobre él
   resulta exitoso, **Then** su contador queda en cero y hacen falta 5 fallos nuevos para bloquearlo
   (AC-05).
2. **Given** un email bloqueado, **When** transcurren 15 minutos desde el quinto fallo, **Then** un
   intento con la contraseña correcta inicia sesión, sin que nadie haya intervenido (AC-06).

---

### User Story 3 - El bloqueo no delata qué emails existen (Priority: P3)

Quien golpea la aplicación con una lista de emails no puede usar el bloqueo para separar los que
tienen cuenta de los que no: le responde exactamente lo mismo en los dos casos.

**Why this priority**: es la condición que RNF-05 le pone al bloqueo, y es lo que evita que la
protección se convierta en un enumerador de cuentas. Va última porque es una propiedad **de** las
respuestas de las historias anteriores: sin bloqueo no hay respuesta de bloqueo que comparar.

**Independent Test**: bloquear un email registrado y otro que no lo está, y comparar las dos
respuestas —mensaje, código y tiempo— entre sí y contra la de una contraseña incorrecta.

**Acceptance Scenarios**:

1. **Given** un intento rechazado por el límite y otro rechazado por credenciales incorrectas,
   **When** se comparan las dos respuestas, **Then** tienen el mismo mensaje y el mismo código
   (AC-08).
2. **Given** un email **no registrado** con 5 intentos fallidos consecutivos, **When** se intenta un
   sexto, **Then** se lo rechaza con el mismo mensaje y el mismo código que a un email registrado y
   bloqueado (AC-09).
3. **Given** el rechazo de un intento sobre un email bloqueado y el rechazo por credenciales
   incorrectas sobre uno no bloqueado, **When** se miden los dos sobre 100 ejecuciones, **Then** la
   diferencia en la **mediana** es de a lo sumo 50 ms (AC-13). El PRD dice "percentil 95"; por qué
   no se puede medir así, más abajo.

---

### Edge Cases

- **Email no registrado**: acumula contador igual que uno registrado (FR-01). Si no lo hiciera, la
  diferencia entre "se bloquea" y "no se bloquea nunca" diría exactamente lo que AC-09 prohíbe
  decir.
- **Intentos hechos durante la ventana de bloqueo**: se rechazan y **no** alargan la ventana. El PRD
  cuenta los 15 minutos desde el quinto fallo; si cada intento rechazado la reiniciara, cualquiera
  podría dejar a otra persona afuera para siempre golpeando cada 14 minutos.
- **La ventana vence sin que nadie haya intentado nada**: el email queda desbloqueado y su contador
  en cero. Hacen falta 5 fallos nuevos para volver a bloquearlo; no queda "a un fallo del límite".
- **Fallos no consecutivos**: 4 fallos, un ingreso exitoso y 4 fallos más no bloquean. El éxito
  reinicia el contador (FR-03) y "consecutivos" quiere decir eso.
- **El mismo email escrito con mayúsculas distintas**: cuenta como el mismo. Si `Ana@x.com` y
  `ana@x.com` llevaran contadores separados, el límite se multiplicaría por cada forma de escribirlo.
- **Un intento sin email, o con un email vacío**: lo rechaza la validación del inicio de sesión, como
  hoy. No hay contador de "el email vacío".
- **La persona bloqueada no sabe por qué**: ve el mismo mensaje que ante una contraseña incorrecta.
  Es una tensión deliberada con la usabilidad, y gana la privacidad porque el PRD la pide
  explícitamente. Su salida es esperar 15 minutos.
- **Denegación de servicio dirigida**: quien conozca el email de otra persona puede dejarla afuera
  15 minutos, y repetirlo. Es el comportamiento que RNF-05 pide, está registrado como riesgo
  aceptado en el PRD, y su mitigación —un límite por IP **además** de éste— es otro ticket.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** *(PRD FR-01)*: El sistema MUST contar los intentos de inicio de sesión fallidos
  consecutivos por cada email presentado, corresponda ese email a una cuenta existente o no.
- **FR-002** *(PRD FR-02)*: El sistema MUST rechazar todo intento de inicio de sesión sobre un email
  que acumule 5 fallos consecutivos, durante al menos 15 minutos contados desde el quinto fallo,
  **incluidos** los intentos que presenten la contraseña correcta.
- **FR-003** *(PRD FR-03)*: El sistema MUST reiniciar a cero el contador de fallos consecutivos de
  un email cuando un intento sobre ese email resulta exitoso.
- **FR-004** *(PRD FR-04)*: El sistema MUST volver a permitir el inicio de sesión sobre un email
  bloqueado una vez transcurrida la ventana de 15 minutos, sin que intervenga ninguna persona.
- **FR-005** *(PRD FR-05)*: El sistema MUST responder a un intento rechazado por el límite con el
  mismo mensaje y el mismo código que a uno rechazado por credenciales incorrectas.
- **FR-006** *(PRD FR-06)*: El sistema MUST contar los fallos y aplicar el bloqueo **por email
  presentado**, y no por sesión, por navegador ni por dirección de origen.
- **FR-007** *(PRD NFR-01)*: El sistema MUST conservar el contador y el estado de bloqueo de un
  email durante los 15 minutos de la ventana, incluso si la aplicación se reinicia dentro de ese
  lapso.
- **FR-008** *(PRD NFR-02)*: El sistema MUST agregar a lo sumo 50 ms al tiempo de respuesta del
  inicio de sesión, en el percentil 95, por comprobar el límite.
- **FR-009** *(PRD NFR-03)*: El sistema MUST tardar lo mismo —dentro de 50 ms en la **mediana**— en
  rechazar un intento sobre un email bloqueado que uno con credenciales incorrectas sobre un email
  no bloqueado, de modo que el cronómetro no distinga un caso del otro.

### Key Entities *(include if feature involves data)*

- **Intentos fallidos de un email**: cuántos fallos consecutivos lleva acumulados un email
  presentado y cuándo fue el último. Existe para emails registrados y no registrados por igual, y no
  implica que exista una cuenta con ese email. De él se desprende si el email está dentro de su
  ventana de bloqueo y hasta cuándo. Se reinicia con un intento exitoso y deja de tener efecto
  cuando la ventana vence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ningún email admite más de 5 intentos fallidos consecutivos sin quedar bloqueado, y el
  100 % de los intentos sobre un email bloqueado dentro de su ventana se rechazan, incluidos los que
  traen la contraseña correcta.
- **SC-002**: El 100 % de los emails bloqueados vuelven a admitir el inicio de sesión al cumplirse
  los 15 minutos, sin intervención de ninguna persona.
- **SC-003**: Las respuestas a un rechazo por límite y a un rechazo por credenciales incorrectas son
  indistinguibles en mensaje y en código, esté el email registrado o no.
- **SC-004**: La diferencia de tiempo entre un rechazo por límite y un rechazo por credenciales
  incorrectas es de a lo sumo 50 ms en la **mediana** sobre 100 ejecuciones, y comprobar el límite
  agrega a lo sumo otros 50 ms en el percentil 95 al inicio de sesión. Los dos estadísticos son
  distintos a propósito: ver el ajuste de más abajo.
- **SC-005**: El 100 % de los bloqueos vigentes siguen vigentes después de reiniciar la aplicación,
  hasta que se cumplan sus 15 minutos.
- **SC-006**: Un bloqueo sobre un email no afecta a ningún otro email: el 100 % de los intentos
  correctos sobre emails no bloqueados inician sesión.
- **SC-007**: Cada criterio de aceptación del PRD (AC-01..AC-13) tiene al menos un test automatizado
  que lo nombra por su identificador.

## Assumptions

- **La ventana es fija, no deslizante.** Los 15 minutos se cuentan desde el quinto fallo y los
  intentos hechos durante el bloqueo no la reinician. El PRD dice "contados desde el quinto fallo";
  una ventana deslizante permitiría un bloqueo indefinido de la cuenta ajena.
- **Al vencer la ventana el contador queda en cero.** El PRD no lo dice; la alternativa —dejarlo en
  5— haría que un solo fallo posterior volviera a bloquear 15 minutos, encadenando bloqueos de por
  vida sobre una cuenta cuya contraseña alguien tipeó mal cinco veces.
- **El email se cuenta como lo resuelve hoy el inicio de sesión**: recortado de espacios y sin
  distinguir mayúsculas de minúsculas, igual que la búsqueda de la cuenta en `002`. El contador y la
  búsqueda tienen que usar la misma clave; si no, el límite se esquiva cambiando una letra de
  mayúscula.
- **Un intento "fallido" es un intento de inicio de sesión con credenciales rechazadas.** Una
  petición que ni siquiera pasa la validación —sin email, sin contraseña— no cuenta como intento, y
  un intento rechazado por el propio bloqueo tampoco suma al contador: ya está bloqueado.
- **El estado del contador se persiste**, porque FR-007 exige que sobreviva a un reinicio; dónde y
  con qué esquema es decisión del plan, y el PRD pide que quede registrada como ADR.
- **Los registros vencidos se pueden purgar.** Esta spec sólo exige que el estado viva los 15
  minutos de su ventana; el criterio de purga es decisión del plan.
- **Una sola instancia de la aplicación.** Con dos contando por separado el límite efectivo se
  duplicaría; el PRD registra el riesgo y FR-007 empuja la decisión hacia un estado compartido, que
  lo resuelve de paso.
- **Los criterios medidos por tiempo (AC-12, AC-13) se nombran siguiendo la convención
  `Rendimiento`** que ya usan FEAT-001a/b/c, para que el filtro del CI
  (`FullyQualifiedName!~Rendimiento`) los alcance y no den rojos sin significado en un runner
  compartido. En local corren.
- **El frontend no cambia.** El rechazo por límite le llega como el mismo error que una contraseña
  incorrecta —eso es FR-005— así que la pantalla de acceso ya lo muestra bien sin tocar nada.

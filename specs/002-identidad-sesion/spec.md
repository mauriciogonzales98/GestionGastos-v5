# Feature Specification: Identidad y sesión

**Feature Branch**: `002-identidad-sesion`

**Created**: 2026-08-24

**Status**: Draft

**Input**: Ticket DISC-001-01a. PRD de referencia:
[`plan-de-implementacion/prds/pendientes/prd-DISC-001-01a.md`](../../plan-de-implementacion/prds/pendientes/prd-DISC-001-01a.md)
(FR-01..FR-07, NFR-01..NFR-03, AC-01..AC-12).

---

## Por qué esta feature

FEAT-001a dejó la aplicación entera funcionando sobre **un único usuario semilla**: una fila fija en
la base, detrás de la abstracción `IUsuarioActual`. Fue deliberado — el modelo lleva la pertenencia
al usuario desde el día uno para que la autenticación **reemplace** esa abstracción en vez de
exigir una migración de datos.

Hoy eso significa que cualquiera que abra la aplicación ve y modifica los mismos movimientos.

Esta feature construye la mitad que produce identidad: registrarse, entrar, salir, y que ninguna
pantalla ni endpoint responda sin sesión. El momento en que `IUsuarioActual` deja de devolver la
fila semilla y pasa a devolver al usuario autenticado es la costura que hace observable todo lo
demás.

### Dos ajustes al PRD que este repositorio impone

Quedan escritos acá porque cambian qué se puede verificar, y no notarlos produciría criterios sin
test o tests sin objeto.

1. **El PRD asume `main` con FEAT-001a, `b` y `c` mergeados. Acá sólo existe `001a`.** No hay
   resumen del mes ni filtros ni edición. AC-08 del PRD dice "el listado **y el resumen**": la
   mitad del listado se verifica, la del resumen **no tiene sobre qué verificarse** y se difiere al
   ticket que construya el resumen. No se inventa un resumen para poder testear el criterio.
2. **AC-09 es un criterio de migración, no de comportamiento.** Sólo es observable una vez: exige
   aplicar la migración sobre una base que **todavía** contiene la fila semilla. Después de
   borrarla no hay estado inicial que reproduzca el escenario. Se verifica contra la migración, con
   su propia base de partida, y nunca contra la API.

### Lo que NO entra

- **El límite de intentos fallidos** y su ventana de bloqueo: es el ticket `01b`.
- **El aislamiento entre cuentas verificado con dos usuarios reales**: es el ticket `01c`. Acá el
  usuario actual pasa a salir de la sesión, que es el prerequisito, pero la verificación con dos
  cuentas y sus criterios propios es de aquel ticket.
- **Recuperación de contraseña**, cambio de contraseña y edición del perfil: no están en el PRD.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Crear una cuenta y entrar con ella (Priority: P1)

Una persona que abre la aplicación por primera vez crea su cuenta con su email y una contraseña, y
a partir de ese momento puede iniciar sesión con esas mismas credenciales y ver sus propios
movimientos, que arrancan vacíos.

**Why this priority**: es la que produce identidad. Sin ella no existe ninguna cuenta contra la que
iniciar sesión, así que ninguna de las otras dos historias tiene sujeto. Entregada sola, ya cambia
el producto: deja de haber una única cuenta compartida.

**Independent Test**: crear una cuenta con un email no registrado, cerrar la aplicación, y volver a
entrar con ese email y esa contraseña.

**Acceptance Scenarios**:

1. **Given** un email que no corresponde a ninguna cuenta, **When** la persona completa el alta con
   ese email y una contraseña, **Then** la cuenta queda creada y esas mismas credenciales permiten
   iniciar sesión (AC-01).
2. **Given** un email que ya corresponde a una cuenta existente, **When** alguien intenta darse de
   alta con ese email, **Then** el alta se rechaza, sigue habiendo exactamente una cuenta con ese
   email, y la contraseña de la cuenta original queda intacta (AC-02).
3. **Given** una cuenta recién creada, **When** se inspecciona su contraseña almacenada, **Then**
   lo que se ve tiene el formato de un hash bcrypt o argon2 y no la contraseña original (AC-10).
4. **Given** dos cuentas distintas creadas con la **misma** contraseña, **When** se comparan los dos
   valores almacenados, **Then** son distintos entre sí (AC-11).
5. **Given** un email cualquiera, **When** se intenta un alta, **Then** la respuesta es la misma
   —mismo mensaje y mismo código— esté ese email registrado o no (NFR-03).

---

### User Story 2 - Entrar, salir, y que sin sesión no se pueda hacer nada (Priority: P2)

Una persona con cuenta inicia sesión y llega a la pantalla principal; cuando termina, cierra sesión
y vuelve a la pantalla de inicio de sesión. Mientras no tenga sesión iniciada, ninguna pantalla ni
operación de la aplicación le responde.

**Why this priority**: es lo que convierte la identidad en una frontera. La historia 1 crea cuentas;
ésta hace que tener cuenta signifique algo. Va después porque necesita cuentas que existan.

**Independent Test**: iniciar sesión con credenciales correctas, cerrar sesión, y comprobar que a
partir de ahí cualquier pantalla u operación de la aplicación exige autenticarse de nuevo.

**Acceptance Scenarios**:

1. **Given** una cuenta registrada, **When** su dueño presenta el email y la contraseña correctos,
   **Then** la sesión queda iniciada y llega a la pantalla principal (AC-03).
2. **Given** credenciales que no corresponden a ninguna cuenta, o una contraseña que no corresponde
   al email indicado, **When** se intenta iniciar sesión, **Then** el acceso se rechaza y no queda
   ninguna sesión iniciada (AC-04).
3. **Given** alguien sin sesión iniciada, **When** solicita una pantalla o una operación de la
   aplicación distinta de las de alta e inicio de sesión, **Then** la operación se deniega, **su
   efecto no se ejecuta**, y queda en la pantalla de inicio de sesión (AC-05).
4. **Given** un usuario con sesión iniciada, **When** cierra sesión, **Then** vuelve a la pantalla
   de inicio de sesión y un intento posterior de acceder a la aplicación vuelve a exigir
   autenticación (AC-06).
5. **Given** una sesión que lleva más de 24 h sin actividad, **When** su usuario solicita una
   pantalla de la aplicación, **Then** la operación se deniega y se exige autenticarse de nuevo
   (AC-12).
6. **Given** un email cualquiera, **When** se intenta iniciar sesión con una contraseña incorrecta,
   **Then** la respuesta es la misma esté ese email registrado o no (NFR-03).

---

### User Story 3 - Los movimientos son de quien los cargó (Priority: P3)

Los movimientos que una persona registra quedan a nombre de su cuenta, y el listado que ve se
calcula únicamente sobre los suyos.

**Why this priority**: es la consecuencia de las dos anteriores y lo que le da sentido al producto
con más de una cuenta. Va última porque necesita sesiones reales, y porque hasta que exista una
segunda cuenta su efecto no se puede distinguir del comportamiento actual.

**Independent Test**: con sesión iniciada, registrar un movimiento y verificar que su propietario es
esa cuenta y no la semilla, y que el listado sólo trae los movimientos de esa cuenta.

**Acceptance Scenarios**:

1. **Given** un usuario con sesión iniciada, **When** registra un movimiento, **Then** el propietario
   del movimiento es el usuario de esa sesión (AC-07).
2. **Given** un usuario con sesión iniciada, **When** abre el listado, **Then** se calcula únicamente
   sobre los movimientos cuyo propietario es el usuario de esa sesión (AC-08, la parte del listado).
3. **Given** una base que todavía contiene la fila de usuario semilla y sus movimientos, **When** se
   aplica la migración que introduce las cuentas, **Then** no queda ningún movimiento asociado a la
   semilla ni la fila de usuario semilla (AC-09).

---

### Edge Cases

- **Email ya registrado**: el alta se rechaza sin tocar la cuenta existente y **sin revelar** que
  ese email ya estaba: la respuesta es la misma que la de un alta con email nuevo (NFR-03). Es una
  tensión deliberada con la usabilidad, y gana la privacidad porque el PRD la pide explícitamente.
- **Contraseña correcta, email inexistente**: se rechaza igual que una contraseña incorrecta, y con
  la misma respuesta y el mismo tiempo de respuesta perceptible. Responder distinto —o notoriamente
  más rápido— delata qué emails existen.
- **Sesión expirada a mitad de una operación**: la operación se deniega y **no se ejecuta su
  efecto**. No se guarda a medias ni se guarda "por las dudas".
- **Cerrar sesión dos veces**, o cerrar una sesión que ya expiró: no es un error; termina en la
  pantalla de inicio de sesión igual.
- **Volver atrás en el navegador después de cerrar sesión**: no devuelve el acceso. Lo que decide es
  la sesión, no la pantalla que se esté mostrando.
- **La aplicación arranca vacía tras la migración**: la primera cuenta que se registre empieza sin
  ningún movimiento. Lo que se haya cargado probando se pierde, y se acepta a cambio de no dejar en
  el modelo una lógica de adopción que corre una sola vez en la vida del producto.
- **Alta con email o contraseña ausentes o vacíos**: se rechaza indicando qué falta. Acá no aplica
  la indistinguibilidad: no se está revelando nada sobre qué cuentas existen.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** *(PRD FR-01)*: El sistema MUST permitir crear una cuenta indicando email y contraseña.
- **FR-002** *(PRD FR-02)*: El sistema MUST rechazar el alta cuando el email ya corresponde a una
  cuenta existente, dejando la cuenta original sin cambios.
- **FR-003** *(PRD FR-03)*: El sistema MUST iniciar sesión cuando el email y la contraseña
  presentados corresponden a una cuenta existente, y rechazar el acceso en caso contrario.
- **FR-004** *(PRD FR-04)*: El sistema MUST exigir una sesión iniciada para acceder a cualquier
  pantalla y a cualquier operación de la aplicación, con la única excepción de las de alta e inicio
  de sesión.
- **FR-005** *(PRD FR-05)*: El sistema MUST permitir cerrar la sesión, dejando a la persona en la
  pantalla de inicio de sesión.
- **FR-006** *(PRD FR-06)*: El sistema MUST resolver el usuario actual —el propietario que se asigna
  a cada movimiento que se escribe y el que acota cada consulta que se lee— al usuario de la sesión
  iniciada, en lugar de a una fila fija.
- **FR-007** *(PRD FR-07)*: El sistema MUST eliminar de la base, mediante la migración que introduce
  las cuentas, la fila de usuario semilla y todos los movimientos asociados a ella.
- **FR-008** *(PRD NFR-01)*: El sistema MUST almacenar cada contraseña como un hash bcrypt o argon2,
  nunca en texto plano ni con cifrado reversible, de modo que el valor almacenado no permita
  recuperar la contraseña original.
- **FR-009** *(PRD NFR-02)*: El sistema MUST expirar la sesión tras 24 h sin actividad, exigiendo
  autenticarse nuevamente a partir de ese momento.
- **FR-010** *(PRD NFR-03)*: El sistema MUST responder al alta y al inicio de sesión con el mismo
  mensaje y el mismo código, esté el email registrado o no.

### Key Entities *(include if feature involves data)*

- **Cuenta**: la persona que usa la aplicación. La identifica su email, único en todo el sistema.
  Guarda el verificador de su contraseña, del que no se puede recuperar la contraseña original.
  Extiende la entidad de usuario que FEAT-001a ya creó para que la pertenencia de los movimientos
  fuera una relación real desde el principio.
- **Sesión**: el hecho de que una cuenta esté autenticada ahora. Tiene un momento de última
  actividad, y deja de valer 24 h después de él.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100 % de los intentos de acceder a una pantalla u operación de la aplicación sin
  sesión iniciada terminan en la pantalla de inicio de sesión, y ninguno ejecuta su efecto.
- **SC-002**: El 100 % de las contraseñas almacenadas son irrecuperables a partir de lo guardado, y
  dos cuentas con la misma contraseña nunca comparten el mismo valor almacenado.
- **SC-003**: Las respuestas de alta e inicio de sesión son indistinguibles entre un email
  registrado y uno que no lo está, tanto en el mensaje como en el código de respuesta.
- **SC-004**: Una persona puede crear su cuenta e iniciar sesión por primera vez en menos de 2
  minutos, sin ayuda ni documentación.
- **SC-005**: El 100 % de los movimientos registrados con una sesión iniciada quedan a nombre de esa
  cuenta, y el listado que ve cada cuenta se calcula únicamente sobre los suyos.
- **SC-006**: Tras aplicar la migración sobre una base con la fila semilla, quedan cero movimientos
  asociados a ella y cero filas de usuario semilla.
- **SC-007**: Cada criterio de aceptación citado en esta spec (AC-01..AC-08 y AC-10..AC-12) tiene al
  menos un test automatizado que lo nombra por su identificador. AC-09 también, con la salvedad de
  que su test corre contra la migración y no contra la API.

## Assumptions

- **Sesión propia de la aplicación, no un proveedor externo.** El PRD habla de email y contraseña
  guardada con hash, lo que descarta delegar la identidad en un tercero. No hay SSO ni OAuth.
- **Una sesión por vez y por navegador.** El PRD no menciona sesiones concurrentes ni gestión de
  dispositivos, y nada en el producto las necesita todavía.
- **La expiración se cuenta desde la última actividad**, no desde el inicio de sesión: el PRD dice
  "24 h sin actividad" (NFR-02). Usar algo dentro de la sesión la renueva.
- **No hay verificación del email por correo.** El PRD no la pide y agregarla implicaría un envío de
  correo que el proyecto no tiene.
- **No hay recuperación de contraseña.** No está en el PRD; una cuenta cuya contraseña se pierde no
  es recuperable en esta feature.
- **El email se compara sin distinguir mayúsculas de minúsculas**, que es lo que espera cualquiera
  que escriba su propio email. Sin esto, `Ana@x.com` y `ana@x.com` serían dos cuentas.
- **La cantidad mínima de caracteres de la contraseña** no está en el PRD. Se asume un mínimo
  razonable y se declarará en el plan; no se agregan reglas de composición (mayúsculas, símbolos),
  que empeoran las contraseñas más de lo que las mejoran.
- **La aplicación arranca vacía.** No se migra lo cargado con el usuario semilla: la decisión está
  registrada en el PRD y en `concept-DISC-001.md`, con fecha 2026-08-20.

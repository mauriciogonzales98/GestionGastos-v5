# Phase 0 — Research: Identidad y sesión

Decisiones técnicas previas al diseño. El stack ya está fijado en `AGENTS.md` y no se investiga acá;
lo que se resuelve son las incógnitas que la spec dejó abiertas y las que nacen de introducir
autenticación en una aplicación que hasta ahora no tenía ninguna.

---

## D-01 — Cómo se representa la sesión

**Decisión**: autenticación por **cookie** de ASP.NET Core (`AddAuthentication().AddCookie(...)`),
con la cookie marcada `HttpOnly`, `SameSite=Strict` y `Secure` fuera de desarrollo. **No hay tabla
de sesiones**: el ticket va cifrado dentro de la cookie.

**Rationale**: frontend y API salen del mismo origen —el proxy de Vite en desarrollo, un solo host
en producción—, que es justo el caso donde la cookie es la opción simple y segura. `HttpOnly` la
vuelve inalcanzable desde JavaScript, así que un XSS no puede robar la sesión; con un token en
`localStorage` sí podría. `SameSite=Strict` cubre CSRF sin agregar un token anti-CSRF, porque la
aplicación no tiene navegación entrante desde otros sitios.

Sin tabla de sesiones el modelo no crece y no hay que limpiar filas vencidas. La contra —no se puede
revocar una sesión desde el servidor— no aplica todavía: el PRD no pide "cerrar sesión en todos los
dispositivos", y `01b` (bloqueo por intentos fallidos) actúa sobre el login, no sobre sesiones ya
abiertas.

**Alternativas consideradas**: JWT en `localStorage` — el patrón más común en tutoriales y el peor
acá: queda expuesto a XSS, no se puede revocar, y obliga a escribir el manejo de expiración a mano
en el cliente. JWT en cookie — suma el tamaño y la complejidad del JWT sin ninguna ventaja sobre el
ticket cifrado que el framework ya emite. Tabla de sesiones — habilita revocación, pero agrega una
entidad, una migración y una limpieza periódica para un requisito que nadie pidió.

> **Consecuencia operativa que hay que anotar**: las cookies se cifran con las claves de Data
> Protection. En desarrollo viven en `~/.aspnet/DataProtection-Keys` y sobreviven a un reinicio; en
> un contenedor sin volumen persistente se regeneran en cada arranque y **todas las sesiones se
> caen**. No es un problema de esta feature, pero sí de cómo se despliegue.

---

## D-02 — Con qué se hashea la contraseña

**Decisión**: **`BCrypt.Net-Next` 4.2.0**, con el factor de trabajo por defecto de la librería
(11). Dependencia nueva del backend, justificada acá como exige `AGENTS.md`.

**Rationale**: NFR-01 exige textualmente bcrypt o argon2. Eso descarta el `PasswordHasher<T>` que
trae ASP.NET Core, que usa **PBKDF2**: es un hash aceptable, pero no es ninguno de los dos que el
PRD nombra, y cumplir "el espíritu" de un requisito que está escrito con nombre propio es
justamente lo que un AC no perdona.

`BCrypt.Net-Next` genera y almacena la sal **dentro** del hash, así que dos cuentas con la misma
contraseña producen valores distintos sin que el código tenga que administrar sales — que es
exactamente AC-11, gratis y por construcción en vez de por disciplina.

**Alternativas consideradas**: `Isopoh.Cryptography.Argon2` 2.0.0 — argon2id es el estado del arte
y resiste mejor el hardware dedicado, pero exige elegir a mano tres parámetros (memoria, iteraciones,
paralelismo) y una elección pobre lo deja peor que bcrypt con sus defaults. Para un proyecto sin
amenaza modelada, la opción con menos superficie para equivocarse gana. `PasswordHasher<T>` del
framework — cero dependencias nuevas, pero incumple NFR-01 tal como está escrito.

---

## D-03 — Qué significa "24 h sin actividad"

**Decisión**: expiración **deslizante** (`SlidingExpiration = true`) con `ExpireTimeSpan` de 24 h.
El reloj de la autenticación se toma del `TimeProvider` inyectado
(`CookieAuthenticationOptions.TimeProvider`), el mismo que ya usa el resto de la aplicación.

**Rationale**: NFR-02 dice "24 h **sin actividad**", no "24 h desde el login". Deslizante es
literalmente eso: cada petición autenticada renueva el ticket. Fijarla desde el login echaría a
alguien que está usando la aplicación en ese momento, que es un comportamiento distinto del que el
PRD pide.

Que el reloj salga del `TimeProvider` es lo que vuelve verificable AC-12 sin esperar 24 h: el test
adelanta el reloj y comprueba que la sesión dejó de valer. Es la misma costura que D-03 de
FEAT-001a abrió para las fechas, reusada acá — y la única forma de cumplir el Principio IV, que
prohíbe tests que dependan del paso del tiempo real.

**Alternativas consideradas**: expiración absoluta — más simple y más segura en abstracto, pero
contradice el requisito. Refresh tokens — infraestructura para un problema que la expiración
deslizante ya resuelve.

---

## D-04 — Cómo se cumple "las respuestas no delatan si el email existe" (NFR-03)

**Decisión**: tres medidas, y las tres hacen falta:

1. **El login** responde `401` con el **mismo** `ProblemDetails` para email inexistente y para
   contraseña incorrecta.
2. **El alta** responde `201` con el mismo cuerpo exista o no la cuenta. Si el email ya estaba, no
   se crea nada y no se toca la cuenta original.
3. Cuando el email **no existe**, el login **igual ejecuta un hash** contra un verificador
   descartable antes de responder.

**Rationale**: los puntos 1 y 2 igualan el mensaje y el código, que es lo que NFR-03 pide. El punto 3
iguala el **tiempo**, y sin él los otros dos son decorativos: bcrypt con factor 11 tarda del orden
de 100 ms, así que un "no existe" que responde en 2 ms y un "contraseña incorrecta" que responde en
100 ms distinguen las cuentas registradas midiendo con un cronómetro. Un canal lateral de ese
tamaño no es teórico.

**El costo, que se acepta con los ojos abiertos**: quien se da de alta con un email ya registrado
recibe un `201` y después no puede entrar. Es confuso, y es exactamente lo que NFR-03 ordena a
cambio de no publicar la lista de emails registrados. AC-02 sigue verificándose del lado de los
datos —una sola cuenta, contraseña original intacta—, no del lado de la respuesta.

**Alternativas consideradas**: rechazar el alta con "ese email ya está registrado" — mucho mejor de
usar y prohibido por NFR-03. Confirmar por correo el alta de un email ya existente — es la salida
que usan los productos serios, y exige un envío de correo que este proyecto no tiene.

---

## D-05 — `IUsuarioActual` deja de devolver la fila semilla

**Decisión**: `UsuarioSemilla` **se elimina**. La única implementación de `IUsuarioActual` pasa a
leer el identificador del usuario del `ClaimsPrincipal` de la petición, y **lanza** si no hay
sesión.

**Rationale**: es la costura que FEAT-001a dejó preparada a propósito — D-05 de aquella feature dice
que el ticket `1a` *reemplaza* la abstracción en vez de migrar datos. La interfaz no cambia, así que
`MovimientosEndpoints` no se toca: sigue asignando el propietario a mano en el `INSERT` (FR-010 de
FEAT-001a) y sigue acotando la lectura por `usuarioActual.Id`.

Que **lance** en vez de devolver un valor por omisión es deliberado: si algún endpoint quedara sin
proteger, un `IUsuarioActual` que devolviera `0` o `null` escribiría filas huérfanas en silencio.
Lanzar convierte ese descuido en un error visible. La protección real es la autorización; esto es la
red debajo.

**Alternativas consideradas**: dejar `UsuarioSemilla` como respaldo cuando no hay sesión — es
precisamente el agujero que este ticket viene a cerrar. Pasar el usuario por parámetro a cada
endpoint — ruido en cada firma, y nada obliga a usarlo.

---

## D-06 — La migración que borra la semilla

**Decisión**: una migración que **primero** borra los movimientos cuyo `usuario_id` es el de la
semilla y **después** la fila de usuario, y en el mismo cambio agrega la columna del verificador de
contraseña a `usuario`. `Down` no restituye los datos.

**Rationale**: el orden lo impone la clave foránea de `movimiento.usuario_id`, que es `RESTRICT`:
borrar el usuario primero falla. Va todo en una migración porque son un solo hecho —"las cuentas
llegan y la semilla se va"— y partirlo dejaría una migración intermedia donde `usuario` ya no tiene
semilla pero todavía no tiene contraseñas.

`Down` no restituye porque no puede: los datos de desarrollo no se guardan en ningún lado y
fabricarlos daría una base que se parece a la anterior sin serlo. Un `Down` que miente es peor que
uno que declara que no revierte.

**Alternativas consideradas**: conservar la semilla y adoptarla como primera cuenta — obligaría a
inventarle una contraseña y dejaría en el modelo una lógica de adopción que corre una sola vez en la
vida del producto. Está descartado en el PRD, decidido el 2026-08-20.

---

## D-07 — Cómo se verifica AC-09, que sólo es observable una vez

**Decisión**: un test de migración con **su propia base** (`gestiongastos_migracion_test`), separada
de `gestiongastos_test`. El test migra hasta `Inicial`, siembra la fila semilla y movimientos suyos,
aplica la migración de este ticket y verifica que no queda ninguno de los dos.

**Rationale**: AC-09 exige como estado de partida una base que **todavía** tenga la semilla, y la
suite normal corre sobre una base que ya está migrada hasta la última. Meterlo ahí obligaría a
desmigrar entre tests, que rompe el aislamiento del Principio IV.

Una base propia mantiene el test determinista y repetible. Como efecto lateral, el fixture existente
—que hoy **exige** que la base se llame `gestiongastos_test` para no arrasar el esquema de
desarrollo por accidente— tiene que admitir un segundo nombre, y sólo ése: la lista blanca se
extiende con un nombre más, no se abre.

**Alternativas consideradas**: verificar AC-09 a mano una vez y anotarlo — lo prohíbe el Principio
II. Correrlo contra la API — no hay endpoint que exponga la semilla, y el criterio es sobre el
estado de la base tras migrar.

---

## D-08 — Dos pantallas sin router

**Decisión**: la aplicación decide qué mostrar —pantalla de autenticación o pantalla de
movimientos— según **haya sesión o no**, sin agregar React Router.

**Rationale**: D-11 de FEAT-001a descartó el router porque había una sola pantalla, y dejó dicho que
se justificaría "con un caso real". Éste no lo es: no hay dos rutas que enrutar, hay **un estado con
dos valores**. Nadie navega a `/login`; se llega ahí por no tener sesión. Un router agregaría una
dependencia, un historial y URLs que no significan nada, y `AGENTS.md` prohíbe dependencias sin
justificar.

Además evita una clase de error propia del router: una URL que se puede escribir a mano y que
muestra la pantalla protegida antes de que el servidor diga que no. Acá lo que gobierna es la
respuesta del servidor.

**Alternativas consideradas**: React Router con rutas protegidas — el patrón estándar, y el ticket 5
(dashboard) sí va a traer el caso real que lo justifique. Ahí se agrega, con su motivo.

---

## D-09 — Cómo sabe el frontend que hay sesión

**Decisión**: un endpoint `GET /api/sesion` que devuelve la cuenta actual o `401`. La aplicación lo
consulta al arrancar; el `401` es lo que la manda a la pantalla de autenticación. Además, **cualquier
`401` de cualquier petición** produce el mismo efecto.

**Rationale**: la cookie es `HttpOnly`, así que el frontend **no puede** mirarla — que es
precisamente lo que la hace segura. Necesita preguntarle al servidor. Tratar todo `401` igual es lo
que hace que AC-12 funcione sin que el cliente lleve la cuenta del tiempo: la sesión expira, la
siguiente petición vuelve `401`, y la aplicación cae a la pantalla de autenticación sola.

Que el cliente no calcule expiraciones es deliberado. Un reloj en el cliente se desincroniza, se
puede adelantar, y duplicar la regla en dos lados garantiza que algún día discrepen.

**Alternativas consideradas**: una cookie legible con la fecha de expiración — obliga a mantener dos
fuentes de verdad y no evita ninguna petición. Guardar en `localStorage` que "hay sesión" — miente
en cuanto la cookie caduca.

---

## D-10 — Dependencias nuevas (requieren justificación por `AGENTS.md`)

**Decisión**: una sola dependencia nueva de producción en el backend, `BCrypt.Net-Next` 4.2.0
(justificada en D-02). **Ninguna** dependencia nueva en el frontend.

**Rationale**: la autenticación por cookie, la autorización y la expiración deslizante son del
framework; no hace falta nada más. Del lado del frontend, las dos pantallas son componentes y el
estado de sesión es estado del componente raíz: sin router, sin librería de estado, sin cliente HTTP
nuevo.

**Alternativas consideradas**: ASP.NET Core Identity completo — trae usuarios, roles, claims,
confirmación por correo, 2FA y una docena de tablas. Resuelve un problema mucho más grande que el de
este PRD y su hasher no cumple NFR-01 sin reemplazarlo igual.

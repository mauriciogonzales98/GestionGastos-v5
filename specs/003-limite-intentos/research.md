# Phase 0 — Research: Límite de intentos fallidos de inicio de sesión

Decisiones técnicas previas al diseño. El stack está fijado en `AGENTS.md` y no se investiga acá.
Lo que se resuelve son las dos incógnitas que la spec dejó explícitamente abiertas —dónde vive el
estado del contador y con qué criterio se purga— y las que aparecen al mirar de cerca los tres AC
que no son "contá hasta cinco": AC-11 (sobrevivir a un reinicio), AC-12 y AC-13 (tiempo).

---

## D-01 — Dónde vive el contador

**Decisión**: una **tabla propia**, `intento_de_acceso`, con el email como clave única y una fila
por email presentado. **No** son columnas de `usuario`.

**Rationale**: FR-001 exige contar los fallos de un email **exista o no una cuenta con ese email**.
Una columna en `usuario` sólo puede contar los de las cuentas registradas: un email inexistente no
tiene fila donde acumular nada, así que nunca llegaría a bloquearse. Ahí AC-09 —"un email no
registrado se bloquea igual y con la misma respuesta"— pasa de ser un test a ser imposible, y el
bloqueo se convierte justo en el enumerador de cuentas que RNF-05 quiere evitar: alcanzaría con
fallar seis veces y mirar si la sexta respuesta cambia.

Que sea tabla y no memoria es lo que pide FR-007: el estado tiene que sobrevivir a un reinicio. De
paso resuelve el riesgo que el PRD anota sobre dos instancias contando por separado — con el
contador en la base, las dos cuentan sobre la misma fila.

`Usuario.cs` ya dice, desde `002`, que el contador **no** va en esa entidad y que es de este ticket.
Esta decisión es la que aquel comentario anticipaba.

**Alternativas consideradas**: `IMemoryCache` con expiración de 15 minutos — es la opción más corta
de escribir y **incumple FR-007** de frente: un reinicio dentro de la ventana desbloquea a todos, y
"al menos 15 minutos" deja de ser cierto. Redis — resuelve persistencia y multi-instancia, y agrega
un servicio nuevo a desplegar, a configurar y a tener caído, para un contador que MySQL ya sabe
guardar. `AddRateLimiter` del framework — cuenta peticiones por ventana deslizante, no fallos
consecutivos por email presentado en el cuerpo, y su estado vive en memoria: no es esta feature.

---

## D-02 — La ventana se deriva de la última marca, no se guarda un `bloqueado_hasta`

**Decisión**: la fila guarda `fallos_consecutivos` y `ultimo_fallo` (UTC). Un email está bloqueado
cuando `fallos_consecutivos >= 5` **y** `ahora - ultimo_fallo < 15 min`. No hay columna
`bloqueado_hasta`, y **un intento rechazado por el bloqueo no toca la fila**.

**Rationale**: dos datos y ninguna redundancia que pueda quedar desincronizada. Que el intento
rechazado no escriba nada es lo que hace que la ventana sea **fija desde el quinto fallo**, que es
lo que la spec asumió y lo que el PRD dice ("contados desde el quinto fallo"): si cada intento
rechazado moviera `ultimo_fallo`, la ventana se volvería deslizante y cualquiera podría dejar a otra
persona afuera para siempre golpeando su email cada 14 minutos. Como efecto colateral bienvenido, el
camino del rechazo por bloqueo no escribe en la base, así que es barato y no se puede convertir en
un vector de escritura.

**Alternativas consideradas**: guardar `bloqueado_hasta` — hace la consulta más obvia de leer y
agrega un estado que hay que mantener coherente con el contador en cada camino; con dos columnas ya
alcanza. Guardar la marca de cada intento en filas separadas y contar — es una bitácora, y el PRD
la deja explícitamente fuera de alcance.

---

## D-03 — Cuándo vuelve el contador a cero

**Decisión**: tres caminos, y el tercero es también el criterio de purga.

1. **Un intento exitoso borra la fila** (FR-003).
2. **Un fallo posterior al vencimiento de la ventana** deja el contador en 1, no en 6: la ventana
   que venció se lleva puestos los cinco fallos que la causaron.
3. **Un email sin ningún intento durante 24 h vuelve a foja cero**, y su fila se borra. Es el mismo
   criterio con el que se purgan los registros vencidos: el `DELETE` de las filas con `ultimo_fallo`
   de más de 24 h se ejecuta en el mismo camino que ya escribe —el del intento fallido—, no como
   tarea periódica.

**Rationale**: el punto 2 lo pide la spec y su motivo está ahí: dejar el contador en 5 al vencer la
ventana encadenaría bloqueos de por vida sobre alguien que tipeó mal su contraseña cinco veces.

El punto 3 resuelve el riesgo de crecimiento que el PRD manda a decidir acá. Sin él, la tabla
acumula una fila por cada email jamás presentado —incluida la lista completa que use un atacante— y
nada la vacía nunca. Elegirlo como *regla de negocio* y no como tarea de limpieza tiene la ventaja
de que es una sola regla en vez de dos que podrían contradecirse: la fila no existe porque el email
ya no cuenta, no porque un `cron` pasó por ahí.

**El precio, dicho en voz alta**: con el reinicio por inactividad, alguien puede probar 4 contraseñas
por día sobre un email, indefinidamente, sin bloquearse nunca. Son ~1.460 intentos por año contra un
hash bcrypt de factor 11: irrelevante frente a las millones de pruebas por segundo que RNF-05 quiere
frenar. La alternativa —contador eterno— compra ese caso a cambio de una tabla que sólo crece y de
bloquear a quien falló una vez por mes durante cinco meses.

**Alternativas consideradas**: tarea de fondo periódica (`BackgroundService`) — más piezas, y un
proceso que borra filas mientras otro las lee. Purgar en cada lectura — el camino de lectura es el
que corre en **todos** los inicios de sesión, incluidos los exitosos; cargarle un `DELETE` gasta el
presupuesto de NFR-02 en el camino más frecuente. Purgar sólo al escribir un fallo mantiene el costo
donde ya se estaba pagando.

---

## D-04 — El rechazo por bloqueo tiene que costar lo mismo que el rechazo por contraseña incorrecta

**Decisión**: cuando el email está bloqueado, el endpoint **igual verifica un hash** —el de la
cuenta si existe, o el `HashDescartable` que `002` ya tiene si no— y recién entonces responde el
`401`. No se responde antes de tiempo.

**Rationale**: es AC-13, y es el único AC de esta feature que se pierde por hacer lo obvio. Lo obvio
es comprobar el bloqueo primero y salir enseguida: eso responde en ~2 ms, contra los ~100 ms que
cuesta bcrypt en el camino de contraseña incorrecta. Esa diferencia es enorme y se mide con
cualquier cronómetro; convierte al bloqueo en un oráculo que dice "este email acumuló cinco fallos",
que es exactamente lo que RNF-05 prohíbe publicar. `002` ya había resuelto la mitad de este
problema con el mismo mecanismo, para que un email inexistente no respondiera más rápido que una
contraseña incorrecta (D-04 de `002`); acá se extiende al tercer caso.

Es deliberadamente **trabajo desperdiciado**: se hashea una contraseña cuyo resultado se ignora. Va
con su comentario, porque un lector futuro que "optimice" ese `if` rompe AC-13 sin que ningún test
funcional se ponga en rojo.

**Alternativas consideradas**: dormir un tiempo fijo para igualar — hace el tiempo *más* predecible
y bloquea un hilo del servidor; el hash real ya tiene la duración correcta porque es la misma
operación. Responder con un retardo aleatorio — el ruido se promedia sobre 100 ejecuciones y el
percentil 95 sigue delatando la diferencia.

---

## D-05 — Cómo se incrementa el contador con dos peticiones a la vez

**Decisión**: el incremento es un **UPSERT atómico** (`INSERT ... ON DUPLICATE KEY UPDATE`) sobre la
fila del email, ejecutado como SQL con `ExecuteSqlInterpolatedAsync`. Nunca leer-modificar-guardar
con EF.

**Rationale**: leer el contador, sumarle uno en C# y guardar es la carrera clásica: cinco peticiones
en paralelo leen 0 y guardan 1, y el email queda a un fallo del límite después de cinco fallos. Con
`ON DUPLICATE KEY UPDATE` el que cuenta es MySQL, sobre la fila bloqueada, y el resultado no depende
del intercalado. El repositorio ya se comió esta clase de bug una vez —`a51a46c`, dos altas
simultáneas con el mismo email daban 500— y la cicatriz está en `AGENTS.md`.

El mismo `UPDATE` resuelve el punto 2 de [D-03](#d-03--cuándo-vuelve-el-contador-a-cero) sin leer
antes: si la ventana venció, el contador se pone en 1; si no, se le suma 1. Es una condición dentro
del SQL, no una rama en C#.

**Alternativas consideradas**: token de concurrencia optimista de EF con reintento — funciona y
agrega una columna, una excepción a atrapar y un bucle de reintentos que hay que testear. Un `lock`
en el proceso — no sirve con dos instancias, que es justo lo que FR-007 empuja a soportar.

---

## D-06 — Dónde se engancha la comprobación

**Decisión**: **dentro del endpoint `POST /api/sesion`**, en `SesionEndpoints`, extraído a un
servicio propio (`LimiteDeIntentos`) inyectado como scoped. No es un middleware ni un filtro.

**Rationale**: el límite se aplica a una sola operación y su clave —el email— viaja en el **cuerpo**
de la petición. Un middleware tendría que leer y rebobinar el cuerpo para enterarse de a qué email
se refiere, que es una complicación entera para no ganar nada: no hay una segunda operación que
quiera este comportamiento, y el PRD deja fuera de alcance aplicarlo al alta. Extraerlo a un
servicio, en cambio, sí paga: deja el endpoint legible y hace testeable la lógica de la ventana sin
levantar la aplicación.

La barrera de autorización de `002` no se toca: `/api/sesion` sigue siendo uno de los dos endpoints
`AllowAnonymous` declarados, y su lista de excepciones no cambia.

**Alternativas consideradas**: middleware — descrito arriba. Filtro de endpoint (`AddEndpointFilter`)
— sí puede leer el cuerpo tipado, pero deja la regla lejos del código que la explica para un solo
endpoint.

---

## D-07 — Cómo se verifica que el bloqueo sobrevive a un reinicio (AC-11)

**Decisión**: el test **descarta la aplicación y levanta otra** sobre la misma base —una segunda
`WebApplicationFactory`— y comprueba que el email sigue rechazado. No se mata ningún proceso ni se
reinicia nada del sistema operativo.

**Rationale**: lo que AC-11 verifica es que el estado **no vive en el proceso**. Levantar una
aplicación nueva es exactamente esa pregunta: todo lo que estaba en memoria se perdió, todo lo que
estaba en la base sigue ahí. Un test que reiniciara un proceso de verdad sería lento, dependiente
del entorno e intermitente, y el Principio IV lo prohíbe.

**Cuidado con el falso verde**: el reloj de la aplicación nueva tiene que quedar donde estaba el de
la vieja. Si arranca en el instante real y el test venía adelantando un `RelojFijo`, la ventana
puede aparecer vencida —o eterna— por el salto del reloj y no por lo que se está probando. La
segunda factoría se construye con su reloj puesto en el mismo instante.

**Alternativas consideradas**: leer la tabla directamente y afirmar que la fila está — verifica que
se escribió, no que el bloqueo se **aplica** después del reinicio, que es lo que dice el AC.

---

## D-08 — Cómo se miden AC-12 y AC-13 sin escribir dos tests intermitentes

**Decisión**: son los únicos tests de esta feature que miden tiempo de pared. Van en
`GestionGastos.Api.Tests/Rendimiento/`, con `Rendimiento` en el nombre completamente calificado para
que el filtro del CI (`FullyQualifiedName!~Rendimiento`) los excluya, igual que los de FEAT-001a/b/c.
Miden 100 ejecuciones, comparan **percentil 95** contra percentil 95 y descartan las primeras
ejecuciones de calentamiento.

Además, cada uno lleva su **test funcional hermano** que verifica la conducta que produce el tiempo,
y ése **sí** corre en el CI: que el camino del email bloqueado ejecuta una verificación de hash
(D-04). Es la misma estrategia con la que `002` cubrió su NFR-03 sin depender del cronómetro.

**Rationale**: un test de tiempo en un runner compartido da rojos que no significan nada, y el
Principio IV prohíbe el rojo intermitente. Pero borrarlos deja AC-12 y AC-13 sin verificación, y el
Principio II no lo permite. La salida es la que el proyecto ya eligió: el test de tiempo existe,
corre en local y queda fuera del CI; la propiedad que lo hace cierto queda cubierta por un test
determinista que corre siempre.

**Sobre AC-12 en particular**: pide comparar el login "con la comprobación activa y sin ella". No se
va a mantener una versión del endpoint sin límite para poder medirla: la comparación es contra el
costo del mismo endpoint sin fila de contador —el camino donde la comprobación consulta y no
encuentra nada—, que es lo más cercano a "sin la comprobación" que se puede medir sin duplicar
código de producción para un test.

> **Revisión al implementar (2026-08-26)**: medir "con fila y sin fila" no aísla lo que el AC
> pregunta —las dos mediciones incluyen la comprobación—, así que el test mide directamente **lo que
> la comprobación agrega**: su consulta más su escritura, que es la diferencia entre los dos
> endpoints, aislada. Se mide el caso peor, el del intento fallido, que consulta *y* escribe; el
> login exitoso sólo consulta. El p95 medido quedó en el orden de 1 ms contra los 50 ms que admite
> el criterio.

**Alternativas consideradas**: `[Fact(Skip=...)]` — deuda invisible y sin ejecutar nunca; el
proyecto ya tiene la convención del filtro y funciona. Medir promedio en vez de percentil 95 — el
AC dice percentil 95, y el promedio esconde justo la cola que delata.

---

## D-09 — Sin dependencias nuevas, sin cambios en el contrato, sin tocar el frontend

**Decisión**: no se agrega ninguna librería. El contrato HTTP no cambia de forma, y `frontend/` no
se modifica.

**Rationale**: FR-005 exige que el rechazo por límite sea **idéntico** al rechazo por credenciales
incorrectas: mismo mensaje, mismo código. Un tipo nuevo, un campo `bloqueado` o un `429` serían
exactamente la filtración que AC-08 prohíbe. Como la respuesta ya existe, la pantalla de acceso ya
la muestra bien y no hay nada que cambiar del otro lado.

Consecuencia práctica: los tests de `Contrato/` no se tocan, pero la barrera del contrato se corre
igual en la puerta de cierre, porque su trabajo es avisar si algo se desalineó sin querer.

**Alternativas consideradas**: devolver `429 Too Many Requests` — es lo que dice el manual para un
límite de tasa, y acá **rompe AC-08**: un código distinto le dice al atacante "este email acumuló
cinco fallos". Es el caso donde la práctica habitual y el requisito se contradicen, y gana el
requisito, que además tiene su motivo escrito.

# Research — Aislamiento entre cuentas verificado

Decisiones de diseño de `004-aislamiento-cuentas`, con lo que se descartó y por qué. Todo lo que
dice "verificado" se comprobó sobre el código de `main` antes de escribirlo.

---

## D-01 · La superficie real son dos endpoints

**Decisión**: esta feature cubre `POST /api/movimientos` y `GET /api/movimientos`. Los otros cuatro
que el PRD nombra quedan en la tabla de *Deuda registrada* de la spec.

**Verificación**: las rutas registradas en el backend son siete —`/api/categorias`, `/api/cuentas`,
`POST` y `GET /api/movimientos`, y las tres de `/api/sesion`—. No existe `GET /api/movimientos/{id}`,
ni `PUT`, ni `DELETE`, ni `GET /api/resumen`. El cliente del frontend tampoco los consume. El propio
código lo dice sin ambigüedad en `MovimientosEndpoints.cs:87`: *"Sin Location: no existe
GET /api/movimientos/{id}, así que la URL apuntaría a un 404"*.

**Por qué el PRD dice otra cosa**: se escribió contra `plan-de-implementacion/README.md`, que lista
FEAT-001b y FEAT-001c como mergeados. En este repositorio nunca se implementaron: las specs
existentes son `001` (alta y listado, que es el corte `a`), `002` y `003`.

**Alternativa descartada**: implementar `001b` y `001c` dentro de este ticket. Son dos tickets
propios con su propio PRD; hacerlos acá los dejaría sin spec y convertiría una feature de
verificación en una de tres features.

---

## D-02 · Nada llega heredado: no hay filtro global de consulta

**Decisión**: no se introduce el `HasQueryFilter` que el PRD da por existente. El aislamiento sigue
siendo una condición escrita en la consulta, y la barrera de FR-004 vigila **esa** condición.

**Verificación**: no hay ningún `HasQueryFilter` en `backend/GestionGastos.Api/`. El acotado vive en
`MovimientosConsulta.cs:22`, como `m.UsuarioId == usuarioId`.

**Rationale**: agregar un filtro global es un cambio de arquitectura que toca toda lectura de
movimientos, presente y futura, y cambiaría el comportamiento de consultas que hoy funcionan. Esta
feature es de verificación: la spec dice explícitamente que no agrega superficie. Un filtro global
además **no elimina** el trabajo de este ticket —no se aplica a las escrituras, que es la mitad de
lo que hay que verificar—, así que lo compraríamos caro y a cambio de la mitad del problema.

**Consecuencia que hay que decir**: el PRD sostiene que "buena parte del aislamiento llega heredada,
sin que nadie escriba una línea nueva". En este repositorio **eso es falso**. El aislamiento de la
lectura es tan convencional como el de la escritura: las dos mitades dependen de que alguien se
acuerde de escribir la condición. Eso hace que este ticket sea *más* necesario que lo que su PRD
supone, no menos.

**Alternativa descartada**: introducir el filtro global y luego escribir AC-10 tal cual. Queda
registrado como candidato para el ticket que agregue el tercer, cuarto y quinto endpoint de
movimientos: con seis lugares que acotar en vez de uno, la cuenta da distinto.

---

## D-03 · La barrera es un test sobre la consulta, más un script que le prueba el rojo

**Decisión**: FR-004 se implementa en dos piezas, siguiendo el patrón que el repositorio ya usa tres
veces (contrato, autorización, linter):

1. **`BarreraDeAislamientoTests`**: un test que mira el SQL que genera la consulta del listado y
   exige que acote por `usuario_id`, más un test que exige que ninguna otra parte del código lea
   `contexto.Movimientos` fuera del canal único (ver D-04).
2. **`backend/verificar-aislamiento.sh`**: desarma el aislamiento a propósito de las **tres**
   formas en que se puede desarmar —quitarle el acotado a la consulta, leer fuera del canal, y
   hacer que el alta asigne un propietario ajeno—, exige el **rojo** en cada una, restaura y exige
   el verde.

**Rationale**: el Principio V de la constitución no admite una barrera que nunca se vio fallar. La
pieza 1 sola pasaría en verde el día que se rompa el descubrimiento, que es el único día que
importa. El patrón existe y funciona: `verificar-autorizacion.sh` hace exactamente esto con un
endpoint desprotegido.

La pieza 1 tiene precedente directo en este mismo archivo de tests:
`La_Consulta_Pide_El_Orden_Explicitamente_Y_No_Lo_Hereda_Del_Indice` ya usa `ToQueryString()` sobre
`MovimientosConsulta` por un motivo idéntico —que mirar el resultado no alcanza para saber si la
consulta pidió lo que tenía que pedir—.

**Lo que esta barrera NO agrega, y conviene no engañarse**: los tests cruzados de US1 y US2 ya
detectan que el `.Where` actual desaparezca. Si se borra, el listado de una cuenta devuelve los
movimientos de la otra y el test cruzado se pone rojo solo. El valor de la barrera está en el otro
caso: una consulta **nueva** que nadie acote. Ahí el test cruzado no ve nada, porque no sabe que esa
consulta existe. Por eso la barrera vigila el canal (D-04) y no sólo la condición.

**Alternativa descartada**: un analizador de Roslyn propio que exija el acotado en toda consulta
sobre `Movimientos`. Es la única barrera realmente estructural, y es cara: un proyecto de
analizador, sus tests, y su empaquetado, para vigilar dos usos. Queda anotada para cuando la
superficie crezca.

---

## D-04 · Un solo canal de lectura, y la barrera vigila que nadie lo esquive

**Decisión**: toda lectura de movimientos pasa por `MovimientosConsulta`. La barrera comprueba dos
cosas: que ese canal acota por cuenta, y que **ningún otro archivo de producción** lee
`contexto.Movimientos`.

**Verificación**: hoy ya se cumple sin que nadie lo haya declarado. Los únicos dos usos de
`.Movimientos` en producción son la lectura en `MovimientosConsulta.cs:21` y la escritura en
`MovimientosEndpoints.cs:75`. La barrera convierte esa coincidencia en una regla.

**Rationale**: es lo que cierra el riesgo que el PRD nombra y deja sin mitigar —"el filtro no aplica
a las escrituras y eso es fácil de olvidar en el próximo endpoint"—. Vigilar la condición protege la
consulta que existe; vigilar el canal protege la que todavía no se escribió, que es la que va a
fallar.

**Costo honesto**: es una regla de forma, no de fondo. Alguien puede escribir una consulta nueva
dentro de `MovimientosConsulta` sin acotarla, y la barrera del canal no lo ve —lo vería la del SQL,
pero sólo sobre `DelMes`—. Cubre el descuido, no la mala fe. Para más que eso hace falta el
analizador de D-03.

---

## D-05 · La escritura no lleva barrera propia

**Decisión**: FR-002 se verifica con los tests cruzados de US2 y nada más. No se agrega una
comprobación en `SaveChanges` ni un interceptor.

**Rationale**: acá el test funcional **sí** es sensible al desarme. Si el alta deja de tomar el
propietario de la sesión, el movimiento cae en la cuenta equivocada —o en ninguna, porque
`usuario_id` es obligatorio y tiene clave foránea— y el test cruzado se pone rojo de inmediato. Es
la diferencia con la lectura, donde el descuido futuro pasa inadvertido: acá no hay "otra escritura"
posible que escape al canal, porque escribir un movimiento con dueño ajeno es exactamente lo que el
test comprueba que no pasa.

**Alternativa descartada**: validar en `SaveChangesAsync` que todo `Movimiento` agregado lleve el
`UsuarioId` de la sesión. Obligaría a que el `DbContext` conozca `IUsuarioActual`, que es una
dependencia nueva de la persistencia hacia la sesión, y cambiaría el comportamiento de las
escrituras de toda la aplicación por un riesgo que hoy no está sin cubrir. Anotada para el ticket
que agregue el `PUT`, donde sí aparece un camino de escritura que puede cambiar el propietario.

**Matiz que hay que no perder**: "sin barrera propia" quiere decir sin comprobación en el código de
producción. El script **sí** desarma la escritura, y por un motivo distinto: sin ese paso, que los
cruzados sepan detectar el desarme se comprueba una sola vez, en la tarea que los escribe. Con él,
se comprueba en cada corrida, y el día que alguien debilite ese test sin querer, se nota.

---

## D-06 · Dos cuentas de verdad, con datos que se parecen

**Decisión**: los escenarios cruzados usan `CuentaDePrueba.CrearYEntrarAsync`, que ya existe, y las
dos cuentas registran movimientos **en el mismo mes, en la misma fecha y con la misma categoría**.

**Rationale**: es la mitigación del riesgo que el PRD nombra —"un test de aislamiento puede dar
verde sin probar nada"—. Tres formas de que eso pase, y cómo se cierran:

| Cómo pasa en verde sin probar nada | Cómo se cierra |
|---|---|
| Las dos cuentas terminan siendo la misma | `CuentaDePrueba` genera un email por `Guid` y devuelve el `Id` real; los escenarios comparan los dos ids |
| La otra cuenta no tiene movimientos | Las dos siembran movimientos propios antes de cada comprobación |
| Los datos no se parecen y el aislamiento lo hace la casualidad | Misma fecha, misma categoría, montos distintos para poder distinguirlos |

**Consecuencia**: cada escenario cruzado comprueba el estado de la **otra** cuenta después de la
operación, no sólo el resultado de la propia. Es AC-08 del PRD, y es lo que distingue "mi listado
está bien" de "el suyo no cambió".

---

## D-07 · El listado sólo devuelve el mes en curso, así que el reloj va clavado

**Decisión**: los escenarios usan `FactoriaConReloj` con una fecha fija, y siembran dentro de ese
mes.

**Verificación**: `GET /api/movimientos` recorta al mes actual del servidor
(`MovimientosEndpoints.cs:102`), y ese recorte no es un parámetro: los filtros de rango llegan con
FEAT-001b.

**Rationale**: sin reloj fijo, un escenario sembrado "hoy" cruza el fin de mes y falla el día 1 sin
que nada haya cambiado. El Principio IV lo prohíbe, y `002` y `003` ya resolvieron lo mismo con esta
misma costura.

---

## D-08 · Las categorías quedan fuera, y ya están cubiertas de antemano

**Decisión**: no se verifica el aislamiento de `GET /api/categorias`. Sí se deja anotado que el alta
de movimientos **ya** acota la categoría a las predefinidas del sistema y las propias de la cuenta.

**Verificación**: `MovimientosEndpoints.cs:34-39` busca la categoría con
`c.UsuarioId == null || c.UsuarioId == usuarioActual.Id`, y el comentario de ese bloque explica que
se escribió pensando en el ticket 3.

**Rationale**: hoy todas las categorías son predefinidas y globales, así que no hay dos cuentas
entre las cuales aislarlas: un test cruzado sobre categorías no puede fallar, que es la misma razón
por la que `001` dejó fuera todo el aislamiento. Es el ticket 3.

---

## Riesgos de esta feature

1. **Que los tests cruzados den verde sin probar nada.** Es el riesgo principal y es de los que no
   se notan: un test de aislamiento roto se ve exactamente igual que uno que funciona. Mitigado por
   D-06, y por el script de D-03, que exige ver el rojo.
2. **Que la barrera del canal se vuelva ruido.** Si el próximo ticket necesita leer movimientos
   desde otro lado por un motivo legítimo, la barrera lo va a frenar. Es lo que tiene que hacer: la
   salida es agregar el método al canal, no apagar la barrera.
3. **Que la deuda registrada se lea como "ya está cubierto".** Cinco AC del PRD no se verifican en
   este ticket. Están en una tabla con su ticket al lado, y el `/speckit-analyze` los va a marcar
   como huecos. Son deliberados.

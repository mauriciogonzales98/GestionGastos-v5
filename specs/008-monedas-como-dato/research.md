# Research: Monedas administrables como dato

**Feature**: `008-monedas-como-dato` · **Fecha**: 2026-09-03

Las decisiones que el plan toma antes de escribir nada. Cada una con lo que se eligió, por qué, y
qué se descartó.

---

## D-01 · Qué significa exactamente "0 recompilaciones", y cómo se prueba

**Decisión**: la afirmación se prueba con un **script de verificación**, `backend/verificar-monedas.sh`,
que compila **una sola vez**, agrega la moneda con **SQL puro**, corre los tests con
`dotnet test --no-build`, y al terminar exige dos cosas:

| Mitad de la afirmación | Cómo se comprueba |
|---|---|
| **0 recompilaciones** | El hash del ensamblado, tomado antes y después. Si cambió, algo recompiló |
| **0 líneas de código modificadas** | `git status --porcelain backend/GestionGastos.Api/` **vacío** |

**Las dos mitades necesitan mecanismos distintos, y ahí hay una trampa que conviene ver antes de
caer en ella.** Un hash del árbol de fuentes tomado antes y después sólo detecta lo que cambió
**durante** la ventana del script: un archivo modificado antes de arrancar entra en los dos hashes,
que siguen siendo iguales, y el script pasa en verde con el árbol sucio. `git status` no tiene ese
punto ciego — compara contra el commit, no contra sí mismo hace un minuto.

Al hash del ensamblado no le pasa lo mismo porque `--no-build` es lo que le da sentido: si algo
recompiló, fue el script.

**Por qué**: AC-01 no es una afirmación sobre el comportamiento del sistema, es una afirmación sobre
el **proceso**: "0 líneas de código modificadas y 0 recompilaciones". Un test de integración no la
puede sostener — corre dentro de un proceso que ya se compiló, y su verde no distingue "no hizo
falta recompilar" de "recompilamos y no nos dimos cuenta". El hash sí lo distingue, y `--no-build`
convierte la recompilación en un error en vez de en algo que pasa inadvertido.

Es además la forma que el proyecto ya tiene para esta clase de afirmación: cinco scripts
`verificar-*.sh` existen porque *"una barrera que nunca se vio fallar no es una barrera"*
(Principio V). Éste es el mismo argumento aplicado a una promesa de producto: una promesa que nunca
se ejecutó no es una promesa, es una intención.

**Alternativas descartadas**:

- **Sólo un test de integración.** Cubre el comportamiento —la moneda nueva aparece— pero no la
  afirmación sobre el proceso, que es la mitad que RF-032 pide. Se escribe igual, pero como
  complemento, no como reemplazo.
- **Levantar la aplicación publicada y pegarle por HTTP.** Sería la prueba más literal, pero
  agrega un servidor, un puerto y un ciclo de vida que hay que administrar desde bash, y no prueba
  nada que el par hash + `--no-build` no pruebe ya. Complejidad sin hallazgo.
- **Revisar el código a ojo y declararlo.** Es exactamente lo que el proyecto decidió no volver a
  hacer.

---

## D-02 · Cómo se registra un movimiento en la moneda nueva sin el selector de 4b

**Decisión**: **cambiando cuál es la predeterminada, también como dato.** El script agrega la moneda
y le pasa la marca de predeterminada; el alta —que ya lee `EsPredeterminada` del catálogo— empieza a
registrar en ella sin que nadie toque una línea.

**Por qué**: AC-02 pide registrar un movimiento con la moneda nueva "por la vía que el sistema
permita", y hoy el sistema permite exactamente una: la predeterminada. Esperar al selector de 4b
dejaría AC-02 sin verificar durante un ticket entero, que es justo el riesgo que el PRD nombra. Y la
vía elegida no es un rodeo: mover la predeterminada **es** administración del catálogo como dato, que
es lo que RF-032 promete.

**El detalle que hay que escribir bien**: `ux_moneda_unica_predeterminada` es un índice único sobre
una columna generada que vale `1` para la predeterminada y `NULL` para el resto. Un `UPDATE` que
apague una y prenda otra **en la misma sentencia** puede violarlo transitoriamente según el orden en
que el motor toque las filas. Van dos sentencias, apagar primero y prender después. Entre las dos hay
un instante sin predeterminada, y en ese instante un alta fallaría con `SingleAsync` — irrelevante en
un script de verificación, y anotado acá para que nadie copie el patrón a código de producción sin
pensarlo.

**Alternativas descartadas**:

- **Insertar el movimiento directamente en la base.** Probaría que la columna acepta el valor, no que
  la aplicación sabe usarlo. El `INSERT` esquiva justamente el código que se quiere verificar.
- **Diferir AC-02 al ticket 4b.** Deja sin probar la única promesa que este ticket tiene para dar.

---

## D-03 · Qué se mide en el rendimiento, y qué se deja quieto

**Decisión**: se agrega **un caso** a `RendimientoResumenTests` que siembra 1000 movimientos
repartidos en **dos** monedas, y se deja intacto el que ya mide con una. El sembrado reparte por
moneda además de por categoría y por tipo.

**Por qué**: el `GROUP BY` del resumen agrupa por moneda, tipo y categoría. Con una sola moneda ese
primer nivel no discrimina nada, así que el caso existente —1000 filas, todas en ARS— **no ejercita
la agrupación que esta feature dice sostener**. Repartir en dos monedas duplica los grupos sin
duplicar las filas, que es exactamente la condición que NFR-03 acota.

El caso viejo se queda porque es la referencia: si el nuevo se pone en rojo y el viejo en verde, el
costo lo agregó la segunda moneda y no el volumen. Dos números que se comparan valen más que uno
que hay que interpretar.

**El techo se mantiene en 2 s para 1000 filas**, el mismo de RNF-01. Aflojarlo porque hay dos
monedas sería cambiar el criterio para que la medición pase, que es lo contrario de medir.

**Alternativas descartadas**:

- **Cambiar el sembrado existente a dos monedas.** Se perdería la referencia de una sola moneda y con
  ella la posibilidad de atribuir un rojo.
- **Medir también el escalón de 10 000.** El PRD acota NFR-03 a 1000 filas en dos monedas. Medir de
  más alarga una suite que ya tarda, sin un criterio que lo pida.

---

## D-04 · El resumen no se toca, y el plan lo trata como restricción

**Decisión**: `CalculoDelResumen`, `ResumenDtos`, el contrato del resumen y sus tipos en el frontend
**no se modifican en esta feature**. FR-009 lo fija y esta decisión lo hace operativo: cualquier tarea
que proponga tocarlos está mal planteada.

**Por qué**: es la contradicción que la spec resolvió en favor del AC-31 de la feature 006. La razón
está en *De dónde sale esta spec*, y acá interesa la consecuencia práctica: **el trabajo de esta
feature es aditivo**. Se agregan un script, un test de integración y un caso de rendimiento. No se
edita ningún archivo de producción.

Eso hace que la feature tenga una propiedad rara y valiosa: **si algún archivo de
`backend/GestionGastos.Api/` aparece modificado en el diff, algo se planteó mal.** Es un criterio de
revisión, no una regla de estilo.

**Alternativas descartadas**:

- **Aprovechar el viaje para agregar el índice por `categoria_id`** que la 006 dejó anotado como
  D6-05. Su propia deuda dice que se agrega **con el número en la mano**, y el número todavía no
  existe: sale de D-03. Si el caso nuevo pasa, el índice sigue sin justificarse.

---

## D-05 · Dónde vive el test de integración, y por qué no en los de rendimiento

**Decisión**: un archivo nuevo, `backend/GestionGastos.Api.Tests/Integracion/MonedaComoDatoTests.cs`,
con el filtro `FullyQualifiedName~MonedaComoDato` que el script usa.

**Por qué**: el filtro tiene que seleccionar exactamente los tests que el script corre con
`--no-build`, y tiene que ser estable. Un archivo propio con un nombre que nombra la propiedad hace
las dos cosas, y sigue la forma de `BarreraDelDesgloseTests` y `BarreraDeAislamientoTests`.

**Y hay que limpiar lo que ensucia.** Estos tests agregan una moneda al catálogo, que es una tabla
que `LimpiarCuentasAsync` **no** toca: hoy borra movimientos, categorías propias y cuentas, y las
monedas quedan porque nadie las creaba. Una moneda de más sobreviviendo a un test rompe al siguiente
que cuente entradas del resumen. La limpieza va **en el test que la crea** y no en
`LimpiarCuentasAsync`: agregarla ahí borraría las dos monedas sembradas para toda la suite, que es lo
mismo que le pasó a las categorías predefinidas y por lo que ese método filtra por `usuario_id != null`.

**Alternativas descartadas**:

- **Meterlos en `ResumenEndpointTests`.** El filtro dejaría de ser específico y el `--no-build` del
  script correría media suite del resumen, que es más lento y más frágil sin ser más verificación.

---

## D-06 · El orden dentro de la puerta

**Decisión**: `verificar-monedas.sh` va **con las otras barreras, después de los tests y antes de
`verificar-linter.sh`**, y se suma a la tabla de *Stack* de `AGENTS.md` y al workflow de CI.

**Por qué**: no modifica ningún archivo de código —a diferencia de las otras cuatro, que sí lo
hacen— así que no invalida el `--no-build` de nadie y podría ir en cualquier lado. Va con las
barreras porque es una de ellas conceptualmente, y porque agruparlas hace que quien lea el CI vea
todas las verificaciones de propiedades en un solo bloque.

**Sí escribe en la base**: agrega una moneda y mueve la predeterminada. Tiene que dejar las dos cosas
como estaban, con un `trap` de restauración como el que ya usan las otras cuatro, porque un catálogo
alterado se lleva puesta la suite entera de la corrida siguiente.

---

## D-07 · Qué NO se investiga, porque ya está resuelto en el código

Anotado para que ninguna tarea lo reabra:

- **Si el catálogo se cachea.** No: `CalculoDelResumen` lo lee en cada pedido. Por eso NFR-01 se
  cumple sin invalidar nada. Si algún día se cacheara, invalidarlo pasaría a ser parte de RF-032.
- **Si el orden de las monedas es estable.** Sí: `OrderBy(m => m.Id)`, decidido en la 006 por el
  mismo motivo que el desempate del desglose. Una moneda nueva entra al final.
- **Si el frontend se rompe con una moneda más.** No la consume todavía: la deuda D6-01 dice que el
  resumen no tiene pantalla. `ResumenPorMoneda[]` ya es una lista de largo variable.
- **Si hace falta migración.** No. Esta feature no cambia el esquema: es la primera desde la 005 que
  no lo toca.

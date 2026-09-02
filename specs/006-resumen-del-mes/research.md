# Research: Resumen del mes con desglose por categoría

Las decisiones que el plan toma y por qué. Cada una nombra la alternativa que se descartó: sin eso
no es una decisión, es una preferencia.

---

## D-01 — La barrera de aislamiento no cubre lo que esta feature va a escribir

**Decisión**: generalizar `Todas_Las_Consultas_Del_Canal_Acotan_Por_Cuenta` para que vigile
**cualquier** método del canal que devuelva un `IQueryable<>`, y no sólo los que devuelven
`IQueryable<Movimiento>`. Agregar el quinto desarme a `verificar-aislamiento.sh`.

**El hallazgo**. La barrera se descubre por reflexión, que es lo correcto, pero filtra así:

```csharp
.Where(m => typeof(IQueryable<Movimiento>).IsAssignableFrom(m.ReturnType))
```

Toda lectura escrita hasta hoy devuelve movimientos, así que la condición cubría el 100 % del canal
y nadie tenía cómo notar que era más angosta que su propósito. **El resumen es la primera lectura
que no devuelve movimientos**: devuelve sumas. Un método del canal que devuelva
`IQueryable<MontoAgrupado>` sin acotar por cuenta pasa la barrera en verde — la barrera ni siquiera
lo enumera.

Es exactamente el patrón que FEAT-001b encontró en su D-01, y conviene decirlo con todas las
letras: **una condición de una barrera caduca cuando cambia lo que la barrera tiene que cubrir, y
caduca en silencio.** La primera vez fue una exención por archivo que era segura mientras el archivo
sólo hiciera un INSERT. Ésta es una condición de tipo que era segura mientras toda lectura
devolviera filas de movimientos. Las dos veces el código quedó correcto y la barrera quedó ciega.

**Cómo se comprueba que el arreglo sirve**: antes de generalizarla, se agrega al canal una
agregación **sin** `usuario_id` y se corre la barrera. Tiene que quedar en **verde** — ése es el
agujero, mostrado y no argumentado. Después se generaliza y el mismo método la tiene que poner en
rojo. El paso 5/7 de `verificar-aislamiento.sh` deja eso automatizado para siempre.

**Alternativa descartada**: calcular el resumen fuera del canal, encima de `Filtrado`, que ya viene
acotado. Funciona y es seguro **hoy**, pero deja el agujero abierto para la próxima agregación que
alguien escriba adentro del canal, que es justo donde el mensaje de error de la otra mitad de la
barrera lo manda ("la salida es agregar el método a `MovimientosConsulta`"). Se hace igual lo de
derivar de `Filtrado` (ver [D-04](#d-04--una-sola-consulta-agregada-y-la-composición-en-memoria)),
pero **además** de arreglar la barrera, no en lugar de.

---

## D-02 — Un endpoint, no dos

**Decisión**: `GET /api/resumen`, con `desde` y `hasta` opcionales. La pantalla principal (RF-22) lo
pide sin parámetros; el dashboard (RF-19, RF-20, RF-21) lo pide con un rango.

**Motivo**: AC-30 exige que el total del mes de la pantalla principal sea **igual** al del dashboard
filtrado por el mes actual. Dos endpoints que tienen que dar lo mismo son dos endpoints que algún
día no van a darlo — y el día que diverjan, quien mire la pantalla no tiene forma de saber cuál de
los dos números está mal. Con un solo endpoint, AC-30 no es un test que hay que acordarse de correr:
es una identidad.

**Alternativa descartada**: `GET /api/resumen/mes` y `GET /api/dashboard`. Le ahorraría al cliente
un parámetro y le costaría al proyecto una igualdad que sólo un test sostiene.

---

## D-03 — El período se valida igual que en el listado, y en un solo lugar

**Decisión**: extraer la validación del período —los dos extremos van juntos o no va ninguno; el
rango invertido se rechaza; sin rango, el mes en curso del servidor— a `Dominio/PeriodoPedido.cs`, y
usarla desde el listado y desde el resumen.

**Motivo**: FR-005 exige que el resumen y el listado describan el mismo conjunto ante el mismo
período. Si cada uno interpreta `desde`/`hasta` por su cuenta, esa igualdad depende de que las dos
interpretaciones no se separen nunca. Con un solo intérprete, no hay dos que puedan separarse. El
mensaje de error también queda uno solo, que es lo que la pantalla espera.

**Costo**: toca el endpoint del listado, que hoy está en verde. Es un refactor con la suite de la
005 entera como red, y va antes de escribir el resumen para que el rojo del refactor no se mezcle
con el rojo de la feature nueva.

**Alternativa descartada**: copiar las tres validaciones. Trece líneas duplicadas hoy, y una
divergencia silenciosa la primera vez que alguien arregle una sola de las dos copias.

---

## D-04 — Una sola consulta agregada, y la composición en memoria

**Decisión**: una única consulta que agrupa por `(moneda, tipo, categoría)` y devuelve `SUM(monto)`
por grupo. El total ingresado, el total gastado, el balance y el desglose se componen **en memoria a
partir de esas mismas filas**.

**Motivo**: FR-005 y FR-009 son igualdades entre números —la suma del desglose tiene que dar el
total gastado, y el total del resumen tiene que dar lo mismo que sumar el listado—. Derivarlos de la
misma fila las vuelve **estructurales**: no pueden fallar sin que falle todo. Con dos consultas
—una de totales y otra de desglose— la igualdad pasa a ser una coincidencia que hay que verificar, y
que se rompe con un `WHERE` que se toque en una sola de las dos.

La agregación va **al canal**, derivada del mismo acotado privado que usa `Filtrado`:

```text
DeLaCuenta(contexto, usuarioId, rango, categoriaId)   ← privado: el WHERE con usuario_id, una vez
  ├── Filtrado(...)  = DeLaCuenta + OrderBy           ← el listado, como hoy
  └── Agrupado(...)  = DeLaCuenta + GroupBy + Sum     ← el resumen
```

Así el acotado por cuenta se escribe **una vez** y las dos lecturas lo heredan por construcción, en
vez de que cada una se acuerde. Y con [D-01](#d-01--la-barrera-de-aislamiento-no-cubre-lo-que-esta-feature-va-a-escribir)
arreglado, la barrera igual inspecciona el SQL de `Agrupado` y exige el `usuario_id` en su `WHERE`:
construcción **y** vigilancia, que no es redundancia sino las dos mitades del Principio V.

**Por qué el `OrderBy` sale del método compartido**: `Filtrado` ordena, y agrupar sobre una consulta
ordenada le pide a EF que traduzca un `ORDER BY` que el `GROUP BY` va a descartar. Que hoy funcione
no lo hace una buena idea: el orden es un requisito del listado (D-04 de la feature 001), no del
acotado.

**Volumen**: las filas del agregado son, como mucho, monedas × tipos × categorías. Con el catálogo
actual son decenas. Componer eso en memoria no es traer el listado a la aplicación para sumarlo a
mano — la suma la hace el motor, que es donde tiene que hacerse.

**Alternativa descartada**: dos o tres consultas separadas, una por cada número que hay que
informar. Más simple de leer cada una, y cada una con su propio `WHERE` que puede quedar distinto.

---

## D-05 — Las monedas salen del catálogo, no de los movimientos

**Decisión**: la respuesta trae una entrada por cada fila de `Monedas`, y los totales agregados se
vuelcan sobre esa lista. Una moneda sin movimientos queda en cero.

**Motivo**: es lo que decidió el usuario para AC-31 (asentado en *Assumptions* de la spec). La
consecuencia técnica es que **la lista de monedas no se deriva del resultado de la agregación**: si
saliera de ahí, un período vacío devolvería una lista vacía y AC-31 quedaría a cargo del cliente.

Leer `contexto.Monedas` no toca la barrera de aislamiento: el catálogo no es de nadie —no tiene
`usuario_id`— y la barrera vigila `Movimientos`, no cualquier `DbSet`. Es la misma lectura que ya
hace el alta para encontrar la predeterminada.

**Alternativa descartada**: devolver sólo las monedas con movimientos. Menos bytes, y el caso vacío
resuelto de una forma distinta por cada cliente que lo consuma.

---

## D-06 — La respuesta devuelve el período que se usó

**Decisión**: el resumen incluye `desde` y `hasta`, siempre, también cuando el cliente no los mandó.

**Motivo**: FR-002 dice que el mes en curso **lo decide el servidor**. Si la respuesta no lo dice,
el cliente que quiera titular "Agosto 2026" tiene que calcular el mes por su cuenta — y ahí vuelve a
existir un segundo criterio de "hoy", en la zona horaria del navegador, que es exactamente lo que
FR-002 quiso evitar. Devolverlo cuesta dos campos y cierra el tema.

**Alternativa descartada**: que el cliente calcule el mes. Barato hasta la primera persona que abra
la aplicación a las 23:40 del último día del mes desde otra zona horaria.

---

## D-07 — El contrato se declara con interfaces con nombre, sin objetos anidados

**Decisión**: `Resumen`, `ResumenPorMoneda` y `TotalPorCategoria` son tres `export interface`
separadas en `tipos.ts`. Ninguna declara un objeto inline.

**Motivo**: `TiposDelFrontend.CamposDeInterfaz` cuenta los campos de **todo** el cuerpo de la
interfaz con un regex multilínea. Un objeto anidado inline haría que los campos del hijo aparezcan
como campos del padre, y la comparación contra el JSON real fallaría con un mensaje que no señala la
causa. El parser está escrito para fallar ruidosamente en vez de aprobar de más; darle una forma que
no sabe leer es gastarse esa garantía.

Cada nivel se compara contra su nodo del JSON real, en las dos direcciones, igual que
`ContratoMovimientosTests` hace con `Movimiento`.

---

## D-08 — Los tests fijan el reloj, sin excepción

**Decisión**: todo test del resumen usa `FactoriaConReloj` con una fecha fija.

**Motivo**: el período por omisión es el mes en curso **del servidor**. Un test que no fije el reloj
pasa 11 meses al año y se cae el primer día del doceavo, o el día que corra a las 23:59 del 31. Es
la misma trampa que las features 004 y 005 documentaron, y acá es peor: el resumen sin parámetros es
el caso principal, no un borde.

Cuidado extra con los **rangos que cruzan el fin de mes**: un test que siembre "el mes pasado"
calculándolo desde el reloj fijo tiene que hacerlo con aritmética de meses, no restando 30 días.

---

## D-09 — El rendimiento se mide, no se supone

**Decisión**: un test de rendimiento del resumen en `Rendimiento/`, con el sembrado que ya existe.

**Motivo**: RNF-01 pide el dashboard en menos de 2 s p95 con 1000 movimientos y menos de 4 s con
10000. El resumen **es** el dashboard, así que es el primer endpoint al que ese RNF le aplica de
lleno. Y a diferencia del listado, agrega: el `GROUP BY` por categoría no está cubierto por el
índice `(usuario_id, fecha DESC, id DESC)`, que ordena pero no agrupa.

La expectativa es que igual alcance —el índice acota el conjunto antes de agrupar, y el conjunto
acotado es de un mes—, pero es una expectativa, y RNF-01 es un número. Se mide.

Va con el filtro de CI que ya existe (`FullyQualifiedName!~Rendimiento`): en un runner compartido
estos tests dan rojos que no dicen nada.

---

## D-10 — Sin migración, y el índice se deja como está

**Decisión**: no se toca el esquema. No se agrega índice por `categoria_id`.

**Motivo**: la entidad no cambia de forma; el resumen es una lectura. Un índice por `categoria_id`
ayudaría al `GROUP BY`, pero el conjunto ya viene acotado a un período de una cuenta —decenas o
cientos de filas— y un índice de más se paga en cada `INSERT`. Si [D-09](#d-09--el-rendimiento-se-mide-no-se-supone)
diera rojo, esto se reabre con un número en la mano en vez de con una intuición.

**Si aparece una migración en esta feature, algo se salió del alcance.**

---

## D-11 — El techo del monto agregado no es el del movimiento

**Anotación, no decisión.** Un movimiento es `decimal(11,2)` con techo 999.999.999,99 (D-01 de la
feature 001). **Una suma de movimientos no tiene ese techo**: mil movimientos en el máximo lo pasan.

`SUM` en MySQL amplía la precisión y `decimal` de C# tiene rango de sobra, así que no hay nada que
implementar. Queda escrito porque el reflejo natural es tipar el total igual que el monto, y ahí sí
habría un desborde silencioso el día que alguien lo mapee a `decimal(11,2)` en un DTO o en una
proyección.

El JSON transporta números, y el frontend los recibe como `number`: sin problema en los volúmenes de
este producto.

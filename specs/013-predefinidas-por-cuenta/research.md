# Phase 0 — Research: Cada cuenta con sus propias categorías predefinidas

**Fecha**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

Todo lo que sigue se midió contra la base real —`gestiongastos_test` y `gestiongastos`, MySQL
8.4.11— o se leyó del código sobre `main` con la feature 012 mergeada. Nada acá es una estimación.

---

## D-01 — La restricción es una clave foránea compuesta, con clave alternativa en `categoria`

**Decisión**

```
categoria:   UNIQUE (id, usuario_id)                  ← clave alternativa
movimiento:  FOREIGN KEY (categoria_id, usuario_id)
                 REFERENCES categoria (id, usuario_id)
```

y desaparece la foránea simple `FK_movimiento_categoria_categoria_id`, que la compuesta contiene.

**Rationale**

Es la forma **declarativa**: no ejecuta código, no necesita privilegios especiales, la aplica el
motor en toda escritura y se despliega con las credenciales de la aplicación. La condición que
expresa —"el dueño del movimiento y el dueño de su categoría son el mismo"— deja de necesitar el
`OR ... IS NULL` en cuanto toda categoría tiene dueño, que es lo que hace la feature.

`usuario_id` de `movimiento` pasa a participar de **dos** foráneas: la que ya tenía contra `usuario`
y esta nueva. MySQL lo admite; hay que confirmarlo con un test de esquema y no darlo por hecho.

**Alternatives considered**

| Alternativa | Por qué se rechazó |
|---|---|
| `CHECK` de esquema | La condición cruza dos tablas y un `CHECK` de MySQL no puede consultar otra tabla. Lo dice el propio enunciado de D7-07 |
| La misma foránea compuesta **sin** este cambio de modelo | **Probada y falla**: rechaza las diez predefinidas con `ERROR 1452`, porque una fila padre con `NULL` en la clave referenciada no puede ser referenciada por nadie. Es la medición que originó esta feature |
| Trigger `BEFORE INSERT` / `BEFORE UPDATE` | **Probado y falla**: `ERROR 1419 (SUPER privilege / binary logging)` aun con `ALL PRIVILEGES` sobre la base de tests, y sobre `gestiongastos` el usuario de la aplicación no tiene siquiera el privilegio `TRIGGER`. Peor: `ci.yml` conecta como `root`, así que en CI se crearía y saldría verde, y explotaría al migrar en local o en producción |

---

## D-02 — La migración no lleva la lista de los diez nombres: copia lo que encuentra

**Decisión**: las copias salen de un `INSERT ... SELECT` que cruza `usuario` con las predefinidas que
**hay en la base**, no de una lista escrita en el archivo de migración.

```sql
INSERT INTO categoria (usuario_id, nombre, tipo, activa, discriminador, <origen>)
SELECT u.id, c.nombre, c.tipo, c.activa, 0, c.id
FROM usuario u CROSS JOIN categoria c
WHERE c.usuario_id IS NULL;
```

**Rationale**: una migración es un hecho histórico y tiene que seguir siendo correcta dentro de dos
años, cuando el catálogo inicial del código haya cambiado. Si copiara una lista literal, migraría a
un catálogo que quizá nunca estuvo en esa base. Copiando lo que hay, migra lo que efectivamente
existía. Además evita la tercera copia de los diez nombres.

**Alternatives considered**: lista literal en la migración (queda desactualizada y miente); llamar al
código de producción desde la migración (ata el pasado al presente: el día que `CatalogoInicial`
cambie, una migración vieja cambia de significado).

---

## D-03 — El reapuntado va por una columna temporal, no por nombre

**Decisión**: la migración agrega `migracion_origen_id INT NULL` a `categoria`, guarda ahí de qué
predefinida salió cada copia, reapunta los movimientos por esa columna, y la borra al terminar.

```sql
UPDATE movimiento m
  JOIN categoria copia
    ON copia.usuario_id = m.usuario_id
   AND copia.migracion_origen_id = m.categoria_id
  SET m.categoria_id = copia.id;
```

**Rationale**: emparejar por `(nombre, tipo)` **es ambiguo y se rompe con datos que ya existen**. Una
cuenta no puede tener una categoría propia **activa** homónima de una predefinida —`FR-005` de la
007 lo rechaza—, pero sí puede tener una **dada de baja** con ese nombre: para eso existe el
discriminador. Ese caso haría que el `JOIN` por nombre encontrara dos filas y reapuntara movimientos
a la categoría equivocada, en silencio y sin error. La columna temporal empareja por identidad y no
tiene ese borde.

**Alternatives considered**: emparejar por `(nombre, tipo, activa)` (sigue siendo frágil: dos bajas
homónimas conviven por diseño); ids determinísticos para las copias (obliga a reservar rangos y a
que la migración sepa cuántas cuentas hay).

> **Revisado en el PR #40 (2026-09-13).** La columna temporal se eliminó —ver D-04, mezclaba DDL con
> pasos de datos y eso rompía la atomicidad— y el emparejamiento pasó a incluir `discriminador = 0`
> además de `(usuario_id, nombre, tipo)`. Da la misma precisión y por un motivo más fuerte: el índice
> único `(usuario_id, nombre, tipo, discriminador)` **garantiza** que haya a lo sumo una candidata,
> así que la desambiguación la sostiene una restricción del esquema y no una columna que hay que
> acordarse de poner y sacar.
>
> **Y este análisis tenía un hueco.** Cubría la homónima **dada de baja** y daba por imposible la
> homónima **activa**, porque `FR-005` de la 007 la rechaza. Esa regla la hacía cumplir la
> aplicación: el índice único no podía, que es exactamente la deuda D-02 que esta misma feature cita.
> Por SQL directo ese estado entra, y entonces la migración falla —correctamente, `FR-014` pide
> fallar antes que completarse a medias— con el `1062` del índice, que nombra la cuenta y el nombre
> de la fila culpable. Queda como edge case en la spec y con test propio.

---

## D-04 — El orden de la migración, y quién verifica que salió bien

**Decisión**, en **dos** archivos de migración escritos a mano — los pasos 1 a 4 en el primero, el 5
en el segundo:

1. Insertar las copias, una por cuenta y por predefinida (D-02)
2. Reapuntar los movimientos (D-03)
3. Borrar las diez predefinidas compartidas
4. `ALTER TABLE categoria MODIFY usuario_id BIGINT NOT NULL`
5. *(segunda migración)* Crear la clave alternativa y la foránea compuesta; quitar la foránea
   simple (D-01)

> **Corregido durante la revisión del PR #40 (2026-09-13).** La versión original abría con un
> `ALTER TABLE categoria ADD COLUMN migracion_origen_id INT NULL` y lo quitaba al final, para
> emparejar cada copia con su original por identidad. Eso resultó ser un error, y no menor: ver el
> *Rationale* de abajo. La columna se eliminó y el emparejamiento pasó a apoyarse en el
> `discriminador`, que da la misma precisión sin tocar el esquema.

**Por qué en dos archivos y no en uno**: el paso 7 verifica al resto, y separado, un fallo señala el
paso correcto en vez de dejar "la migración falló" a secas. Además desacopla las dos historias, que
si no compartirían literalmente el mismo archivo.

**Rationale — la verificación de `FR-014` sale gratis, y ése es el motivo del orden.** El paso 4
falla si quedó una categoría sin dueño. El paso 5 falla si quedó un movimiento apuntando fuera de su
ámbito. No hace falta escribir una comprobación aparte: **las restricciones mismas son la
verificación**.

**Y una premisa que esta decisión daba por buena y era falsa.** Decía: *"como cada migración corre en
una transacción, un fallo deja la base como estaba en vez de completarse a medias"*. **En MySQL no.**
Un `ALTER TABLE` confirma la transacción abierta y la termina, así que todo lo que venga después
queda fuera de ella. Medido el 2026-09-13 durante la revisión del PR #40, con el caso mínimo:

```sql
START TRANSACTION;
ALTER TABLE prueba ADD COLUMN tmp INT NULL;
INSERT INTO prueba (id) VALUES (2);
ROLLBACK;
SELECT id FROM prueba;   -- devuelve 1 y 2: el INSERT sobrevivió al ROLLBACK
```

Con la columna de trabajo como primer paso, esto tenía consecuencia directa: se forzó el fallo
—una cuenta con una categoría propia **activa** homónima de una predefinida, que el esquema anterior
admitía— y la migración dejó la columna puesta. El reintento murió con `Duplicate column name`, y la
base quedó trabada sin poder avanzar ni volver. `FR-014` pide fallar en vez de completarse a medias,
y eso incluye poder volver a intentarlo.

**Por eso los pasos de datos van juntos y el único `ALTER TABLE` va último.** Los tres primeros
comparten la transacción que EF abre y se deshacen juntos; el cuarto es el que la cierra, y no hay
nada escrito debajo.

**Lo que NO cambia acá es la propiedad de C#.** `Categoria.UsuarioId` sigue siendo `long?` hasta el
final de la feature, con la columna marcada `IsRequired()` para que el modelo y la base queden
sincronizados. Volverla no-anulable rompe cinco archivos a la vez y deja tareas que no pueden
terminar en verde, contra el Principio III de la constitución.

**Sobre el paso 4 y el scaffolding**: quitar el `HasData` de `Categoria` hace que EF genere por su
cuenta los `DELETE` de las filas 1 a 10. Generados automáticamente **irían antes** del reapuntado y
la migración fallaría contra la foránea existente. Por eso la migración se escribe a mano y no se
confía en lo que el scaffolding proponga.

**Idempotencia (edge case de la spec)**: la da `__EFMigrationsHistory`. No se escribe a mano.

**El `Down()` se escribe completo, y no por prolijidad.** Vuelve `usuario_id` a anulable, recrea las
diez compartidas, reapunta los movimientos de vuelta y borra las copias. El motivo es concreto: la
única forma honesta de testear una migración de datos es fabricar el estado anterior, y la técnica
que este repo ya usa para eso —`MigracionDeCuentasTests`— baja el esquema, siembra con SQL crudo y
vuelve a subir. Bajar el esquema **ejecuta el `Down()`**. Sin `Down()`, `FR-010` a `FR-014` se
quedan sin test que no pase por desarmar restricciones, que es lo que hace una barrera y no lo que
debería hacer un test. El reapuntado de vuelta sí puede ir por `(nombre, tipo)`: los nombres de las
diez compartidas son únicos por tipo, así que en esa dirección no hay ambigüedad.

---

## D-05 — El catálogo inicial se muda del esquema al alta de cuenta

**Decisión**: se quita el `HasData` de `Categoria` de `GestionGastosDbContext.Sembrar`. Los diez
nombres pasan a un archivo de producción nuevo, `Categorias/CatalogoInicial.cs`, que es el único que
los conoce. La semilla de `Moneda` **no se toca**: las monedas siguen siendo del sistema.

**Rationale**: el catálogo deja de ser un hecho del esquema y pasa a ser una consecuencia del alta de
una cuenta. Mantenerlo en `HasData` sería imposible: `HasData` no puede sembrar filas que dependen
de un `usuario_id` que todavía no existe.

---

## D-06 — Quién puede escribir categorías: la barrera de aislamiento hay que extenderla, y con cuidado

**El problema, medido**: `BarreraDeAislamientoTests` declara **un solo** archivo de producción
autorizado a escribir `contexto.Categorias` —`Categorias/CategoriasEndpoints.cs`— y una sola
operación permitida —`Add`—. El alta de cuenta ahora tiene que crear diez categorías. Tal como está,
la barrera se pone en rojo.

**Decisión**: el catálogo inicial se escribe **dentro de `Categorias/CatalogoInicial.cs`**, que se
declara como segundo escritor con `Add` y `AddRange` permitidos. `CuentasEndpoints` lo llama y no
toca el `DbSet` de categorías.

**Rationale**: la alternativa —autorizar a `CuentasEndpoints` a escribir categorías— le abre el
acceso al `DbSet` a un archivo que hace muchas otras cosas, y la autorización queda vigente para
todo lo que ese archivo haga en el futuro. Un archivo de una sola responsabilidad mantiene la
excepción del tamaño de lo que efectivamente hace falta.

**Lo que esto NO cubre, y queda anotado como deuda**: la barrera vigila el texto
`contexto.Categorias`. Una escritura por **propiedad de navegación** —`usuario.Categorias.Add(...)`—
la esquivaría sin que nada se ponga en rojo. Hoy no existe ninguna y esta feature no la introduce
(D-07), pero el agujero es real y es el mismo que ya tuvo la lectura por navegación —`m.Categoria!.Nombre`—
que originó D7-07. Se anota como **D13-01**.

---

## D-07 — El alta de cuenta y su catálogo, en un solo `SaveChanges`

**Decisión**: `CatalogoInicial` recibe el `Usuario` recién construido y hace
`contexto.Categorias.AddRange(...)` con cada categoría enlazada a ese usuario; `CuentasEndpoints`
hace **un solo** `SaveChangesAsync`, el que ya hace hoy.

**Rationale**: `FR-003` pide que la cuenta y su catálogo nazcan juntos o no nazcan. Un solo
`SaveChanges` es una sola transacción implícita: no hace falta abrir una explícita. Además EF resuelve
solo el `usuario_id` de las diez filas a partir del identificador que genera el `INSERT` del usuario.

**Lo que hay que revisar al implementar**: el alta de cuenta atrapa hoy el `1062` del email
duplicado para responder igual que un alta exitosa (NFR-03). Ese `catch` ahora abarca también el
`INSERT` de las diez categorías; hay que verificar que un choque de email siga sin dejar nada
escrito, y que el `catch` no se trague un fallo de las categorías haciéndolo pasar por email
duplicado. Es el único lugar de esta feature donde un catch existente cambia de alcance.

---

## D-08 — Qué se simplifica solo, y qué NO se toca aunque parezca que sobra

Con el `NULL` fuera del modelo, varias cosas dejan de tener sentido:

| Qué | Qué pasa |
|---|---|
| `CategoriasConsulta.DelAmbito` | Pasa de `c.UsuarioId == null \|\| c.UsuarioId == usuarioId` a `c.UsuarioId == usuarioId`. Queda idéntico al predicado de movimientos |
| El `403` de `CategoriasEndpoints` sobre una predefinida | Desaparece: ya no hay categoría intocable. Todo lo que no es del ámbito es `404`, como cualquier id inexistente |
| `CategoriaDto.EsPropia` y `Categoria.esPropia` del contrato | Se eliminan de las dos pilas: siempre valdrían `true` |
| El `if (categoria.UsuarioId is null)` que separaba `403` de `404` | Se elimina |
| `CategoriasConsulta.Homonimas` | **Se conserva**, pero su comentario se reescribe. Hoy dice que existe porque el índice único deja pasar una propia homónima de una predefinida (D-02 de la 007); eso deja de ser cierto — sin `NULL`, el índice cubre el caso entero. Sigue existiendo por otra razón: devolver un `400` con su campo en vez de un choque de índice convertido en `500` |
| Las dos comprobaciones de `MovimientosEndpoints` | **Se conservan** (`FR-007`). La foránea es el piso, no el reemplazo: quien usa la aplicación tiene que seguir recibiendo el error de validación con su campo |

---

## D-09 — La restricción nueva trae su barrera

**Decisión**: se agrega `backend/verificar-ambito-de-categoria.sh`, que quita la foránea compuesta,
exige que el test de esquema se ponga en **rojo**, la restaura y exige el verde.

**Rationale**: es el Principio V de la constitución, y el precedente del repo es unánime — la nota
(012) y el código de moneda (014) trajeron cada una la suya. Un test de esquema que afirma "la base
rechaza esto" informa verde tanto cuando la restricción está puesta como cuando el test dejó de
verificarla. Sería la octava barrera del proyecto.

---

## D-10 — Los tests que fijan identificadores de categoría dejan de servir

**Medido**: 5 usos de `CategoriaId = <número>` en la suite, más los tests que dan por hecho que el
catálogo tiene exactamente diez filas globales.

**Decisión**: las categorías dejan de tener identificadores estables —cada cuenta recibe los suyos al
registrarse—, así que los tests resuelven la categoría desde el catálogo de **su** cuenta. Se agrega
un helper al estilo de `CatalogoDeMonedas`, que ya existe para el mismo problema.

---

## Hallazgo lateral (no bloquea, se reporta)

La tabla de *Stack* de `AGENTS.md` documenta seis barreras y el repo tiene **siete**:
`backend/verificar-nota.sh` no figura. No es de esta feature, pero conviene arreglarlo cuando se
agregue la octava, para no dejar la tabla dos veces desactualizada.

# Research — Categorías propias del usuario

Las decisiones de diseño de esta feature, con lo que se descartó y por qué. Lo que la spec dejó
explícitamente al plan está en D-01, D-02 y D-06; el resto salió de mirar el código antes de escribir.

---

## D-01 · La unicidad tiene que dejar de chocar contra una fila dada de baja

**El problema.** FR-009 exige poder crear una categoría con el mismo nombre y tipo que una que uno
mismo dio de baja. El índice `ux_categoria_ambito_nombre_tipo` es `UNIQUE (usuario_id, nombre, tipo)`
y no mira `activa`, así que la fila nueva choca contra la vieja y el alta falla. Es la única
migración que esta feature necesita.

**Lo que se descartó, y por qué cada una no sirve:**

| Alternativa | Por qué no |
|---|---|
| Agregar `activa` al índice: `(usuario_id, nombre, tipo, activa)` | Aguanta **una** baja. A la segunda —crear "Gimnasio", darla de baja, recrearla, darla de baja otra vez— hay dos filas con `activa = 0` y el mismo nombre, y vuelven a chocar |
| Índice parcial `WHERE activa = 1` | MySQL no tiene índices parciales. Es la solución de PostgreSQL y acá no existe |
| Columna generada `IF(activa, 0, id)` dentro del índice | MySQL **prohíbe** que una columna generada referencie una `AUTO_INCREMENT`, y `id` lo es |
| Columna `desactivada_en DATETIME NULL` dentro del índice | Al revés de lo que hace falta: en un índice único de MySQL **los NULL no chocan entre sí**, así que las filas activas —todas con NULL— dejarían de tener unicidad, que es justo el caso que importa |
| Sacar el índice único y validar sólo en la aplicación | Deja la unicidad a merced de dos pedidos simultáneos. Y es la clase de garantía que este proyecto pone en la estructura, no en el recuerdo |
| Renombrar la fila al darla de baja (`"Gimnasio (dada de baja)"`) | Rompe FR-010: el nombre que se conserva es el que los movimientos tienen que seguir mostrando |

**La decisión.** Una columna real `discriminador BIGINT NOT NULL DEFAULT 0`, escrita por la
aplicación, dentro del índice: `UNIQUE (usuario_id, nombre, tipo, discriminador)`.

- Mientras la categoría está activa vale **0**, así que dos activas con el mismo nombre chocan —que
  es lo que FR-005 necesita del motor—.
- Al darla de baja, la aplicación le escribe **su propio `id`** en el mismo `UPDATE` que apaga
  `activa`. Como el `id` es único, esa fila no vuelve a chocar con nadie nunca, y se pueden acumular
  todas las bajas homónimas que hagan falta.

Es una columna que no significa nada para el producto y sólo existe para el índice. Está bien que
así sea, y por eso se llama `discriminador` y no algo que sugiera que se puede leer: **nada de la
aplicación la consulta**, sólo se escribe.

---

## D-02 · La comprobación contra las predefinidas la hace la aplicación, y no puede hacerla el índice

FR-005 pide rechazar una categoría propia que se llame igual que una **predefinida**. El índice no
puede: las predefinidas tienen `usuario_id = NULL` y las propias tienen un número, así que para
MySQL son claves distintas y no chocan. Además, dos `NULL` tampoco chocan entre sí.

**Decisión**: la comprobación es una consulta en la aplicación, sobre el ámbito completo de la
cuenta —`usuario_id IS NULL OR usuario_id = @yo`— filtrando por `activa`.

**El detalle que importa: eso no reintroduce una carrera.** La única colisión que dos pedidos
simultáneos pueden provocar es *dos propias con el mismo nombre*, y de ésa se sigue encargando el
índice de D-01. Contra las predefinidas no hay carrera posible: son diez filas sembradas que nadie
crea ni borra en caliente. La comprobación de la aplicación no es el candado, es el **mensaje**: sin
ella el error sería un choque de índice sin explicación, y con ella FR-005 puede decir el motivo.

**Cómo se comparan los nombres (FR-007).** La collation de la base es `utf8mb4_0900_ai_ci` —*ai* de
accent-insensitive, *ci* de case-insensitive—, así que "Comida", "comida" y "cómida" ya son iguales
para el motor y para el índice, sin escribir nada. Lo único que hay que agregar es el **recorte de
espacios al principio y al final**, que la collation no hace: se recorta al recibir, antes de
validar y antes de guardar, así que lo que se compara y lo que se guarda son la misma cadena.

---

## D-03 · Un canal único de lectura de categorías, con su barrera

El aislamiento de movimientos vive en `MovimientosConsulta` y lo vigila `BarreraDeAislamientoTests`.
Las categorías no tenían de qué aislarse: eran diez filas de todos. Desde esta feature sí.

**Decisión**: `Categorias/CategoriasConsulta.cs`, mismo patrón —métodos estáticos que devuelven
`IQueryable`, el acotado por ámbito escrito **una vez** en un método privado— y extender la barrera
existente para que también lo vigile.

**Por qué se copia el patrón en vez de generalizar la barrera a "toda lectura de cualquier tabla".**
Porque el ámbito de una categoría **no es el mismo predicado** que el de un movimiento: un
movimiento es de una cuenta y punto, una categoría puede ser de la cuenta **o de nadie**. Una
barrera que exija `usuario_id` en el `WHERE` de las dos sirve igual, pero la que las construye no
puede ser la misma función. Se comparte la vigilancia, no el acotado.

**Y la barrera se prueba antes de escribir el canal**, con el desarme correspondiente en
`verificar-aislamiento.sh`. Es la tercera vez en este proyecto que una condición de esa barrera
caduca al cambiar lo que tiene que cubrir; la segunda y la tercera fueron en la feature 006. Agregar
una tabla vigilada sin ver el rojo primero es la cuarta esperando.

---

## D-04 · FR-021 ya está implementado, y lo que hay que hacer es no romperlo

Buscar antes de escribir: el alta y la edición de un movimiento **ya** acotan la categoría por
ámbito.

```csharp
c.Id == id && (c.UsuarioId == null || c.UsuarioId == usuarioActual.Id) && c.Activa
```

Está desde FEAT-001b, con su motivo escrito en el código: *"Buscar sólo por id dejaría entrar la
categoría privada de otra cuenta cuando el ticket 3 las introduzca"*. Y hay dos tests que lo
defienden en `ValidacionMovimientoTests`: uno con una categoría ajena y otro con una dada de baja.

**Decisión**: no se toca el predicado del **alta**. Lo único que cambia es la **edición**, por
FR-023: tiene que aceptar la categoría dada de baja **que el movimiento ya tenía**, y sólo ésa.

La forma es una condición más, no un predicado más laxo:

> es válida si (está activa) **o** (es la que este movimiento ya tenía)

Escribirlo como "la edición no filtra por `activa`" sería más corto y estaría mal: dejaría mover un
movimiento a **cualquier** categoría archivada, y ahí la baja lógica sí quedaría cosmética. El test
existente `Rechaza_Una_Categoria_Dada_De_Baja` se queda **tal cual** —cubre el alta— y se le suma su
espejo en la edición, con los dos casos: la que ya tenía pasa, otra distinta no.

---

## D-05 · El desglose del resumen no puede empezar a filtrar por `activa`

Es la deuda D6-04 que dejó la feature 006, y es la forma más fácil de romper esta feature: agregar
`activa` a la tabla y filtrarla "por prolijidad" en todas las consultas que la tocan. Si eso pasa en
la consulta del resumen, los totales históricos cambian solos y nadie se entera.

**Decisión**: dos capas, porque una sola no alcanza.

1. **El test de comportamiento** (AC-06): se mide el resumen, se da de baja una categoría con
   movimientos, se vuelve a medir y **ningún número se movió**. Es la traducción directa del AC.
2. **La barrera** (Principio V): un test que inspecciona el SQL que genera
   `MovimientosConsulta.Agrupado` y **exige que no nombre `activa`**. Sin ella, el día que alguien
   agregue el filtro, el test de comportamiento se pone en rojo y el arreglo "obvio" es actualizar
   los números esperados. La barrera dice qué está mal en vez de qué dio distinto.

La barrera se ve fallar antes de que exista lo que vigila: se agrega el filtro a propósito, se
comprueba el rojo, se saca.

---

## D-06 · Qué responde cada rechazo

El proyecto ya tiene dos formas: `Results.ValidationProblem` con la clave del campo —para que el
frontend ponga el mensaje al lado del control— y el `404` uniforme para lo que no es tuyo.

| Caso | Respuesta | Por qué |
|---|---|---|
| Nombre vacío o de más de 50 (FR-006) | `400` con la clave `nombre` | Es un error de campo y tiene dónde mostrarse |
| Nombre repetido (FR-005) | `400` con la clave `nombre` | Idem. El mensaje dice que ya existe, sin decir si la que choca es propia o predefinida: no hace falta y es una fuga menos |
| Categoría de otra cuenta, o id inexistente (FR-013) | `404`, mismo cuerpo | Cualquier diferencia confirma que la fila existe |
| Categoría **predefinida** que se intenta modificar o dar de baja (FR-008) | `403` | Acá **no** va el `404` uniforme: la persona la está viendo en su selector, así que decirle "no existe" es mentirle sobre algo que tiene a la vista. Y no hay nada que ocultar: el catálogo predefinido es el mismo para todo el mundo |
| Dar de baja algo ya dado de baja | `204`, igual que la primera vez | El estado final es el mismo. Un error acá obligaría al cliente a distinguir dos situaciones idénticas |

---

## D-07 · El contrato gana un campo: `esPropia`

La pantalla de gestión tiene que saber cuáles puede renombrar y dar de baja. Hoy `CategoriaDto` es
`(id, nombre, tipo)` y no alcanza: sin ese dato, la pantalla tendría que deducirlo —por el id, o
probando y viendo el `403`—, que son dos formas de adivinar.

**Decisión**: `CategoriaDto` pasa a `(id, nombre, tipo, esPropia)`.

**No** se expone `activa`: el listado ya sólo devuelve activas (FR-002), así que sería un campo que
vale siempre `true`. Y **no** se expone `usuarioId`: el cliente no tiene qué hacer con el número de
cuenta, y `esPropia` responde la única pregunta que la pantalla necesita.

Los tres endpoints nuevos —alta, renombre, baja— entran al contrato con la misma verificación en las
dos direcciones que ya tienen movimientos, categorías y el resumen.

---

## D-08 · El catálogo sube a `App.tsx`

FR-019 pide que el catálogo sobreviva a la navegación entre la pantalla principal y la de gestión, y
FR-015 sigue exigiendo a lo sumo una petición por carga. Hoy el catálogo vive en el estado de
`PantallaMovimientos`, que se desmonta al navegar: volver lo pediría de nuevo.

**Decisión**: el catálogo sube a `App.tsx` —que ya es quien alterna login ↔ movimientos— y baja por
props a las dos pantallas, junto con las funciones que lo modifican. Es el mismo lugar donde ya vive
la sesión, y por el mismo motivo: es el estado que sobrevive a los cambios de pantalla.

**Se descartó** un contexto de React: agrega una indirección para un dato que dos pantallas
hermanas necesitan y que ya tiene un padre común a un nivel de distancia. Y **se descartó** volver a
pedirlo al volver, que es lo que haría FR-019 innecesaria pero rompe FR-015 en cuanto la persona
navegue dos veces.

`FormularioMovimiento` no cambia de forma: sigue recibiendo `categorias` por props. Lo que cambia es
de dónde vienen, y eso no lo ve.

---

## D-09 · La pantalla de gestión, sin enrutador

Decidido en la sesión de clarificación: una vista más en el estado de `App.tsx`, como ya se alterna
login ↔ movimientos. Sin dependencias nuevas —la regla de `AGENTS.md`— y sin URL propia; recargar
vuelve a la principal, y está anotado en *Assumptions* para que no se lea como un olvido.

---

## D-10 · La migración, y lo que tiene que sobrevivir

Una sola migración, con dos cosas: la columna `discriminador` (D-01) y el índice único rehecho para
incluirla.

**Lo que la migración no puede tocar**: las diez categorías predefinidas, con sus ids fijos, sus
nombres y sus tipos. SC-005 lo mide y hay un test que lo comprueba después de migrar. Las filas
existentes nacen con `discriminador = 0`, que es lo correcto: todas están activas.

**No hace falta migrar `usuario_id` ni `activa`**: existen desde la migración `Inicial`, porque la
feature 001 las anticipó (su D-06). El PRD creía que este ticket las agregaba; ver la tabla de
reconciliación de la spec.

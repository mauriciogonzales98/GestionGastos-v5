# Data Model — Aislamiento entre cuentas verificado

**Esta feature no cambia el esquema.** No hay entidad nueva, no hay columna nueva, no hay migración.
Lo que sigue no es un diseño a construir: son las invariantes que ya existen en la base y que esta
feature convierte en algo verificado.

Se documentan porque son el objeto de los tests. Un test de aislamiento que no sepa exactamente qué
invariante está protegiendo termina comprobando que dos listas son distintas, que es otra cosa.

---

## Movimiento — lo que ya existe

| Campo | Tipo | Qué aporta al aislamiento |
|---|---|---|
| `Id` | `bigint` autoincremental | **Correlativo entre cuentas.** Los identificadores de dos cuentas se intercalan: la cuenta B puede nombrar el movimiento que A acaba de crear sin adivinar nada. Hoy no hay endpoint que reciba un id, y por eso esa deuda no es teórica |
| `UsuarioId` | `bigint`, obligatorio, FK a `usuario` con `Restrict` | **Es el aislamiento.** Todo movimiento tiene dueño, la base no admite uno sin él, y el dueño tiene que existir |
| `CategoriaId` | FK a `categoria` | La categoría se acota aparte, en el alta, a las predefinidas y las propias ([D-08](./research.md)) |
| `Fecha`, `Monto`, `Tipo`, `MonedaId` | — | No participan del aislamiento. Importan por otro motivo: los escenarios cruzados los hacen **coincidir** entre las dos cuentas, para que ningún test pase por casualidad ([D-06](./research.md)) |

Hay además un índice `(usuario_id, fecha, id)`. No es una decisión de esta feature, pero conviene
tenerlo presente: es lo que hace que MySQL devuelva las filas ya acotadas y ordenadas, y por lo tanto
lo que vuelve insuficiente mirar sólo el resultado de una consulta. Es el mismo motivo por el que
`001` necesitó una segunda capa para verificar el orden.

## Las invariantes que esta feature verifica

**INV-01 — Todo movimiento tiene exactamente un dueño, y es una cuenta que existe.**
La base ya lo garantiza: `usuario_id` es obligatorio y tiene clave foránea. No hace falta un test
propio; se apoya en él INV-03.

**INV-02 — Ninguna lectura de movimientos devuelve filas de otra cuenta.**
Hoy la sostiene una condición escrita a mano en `MovimientosConsulta`. Es lo que verifican los
escenarios cruzados de US1 y lo que vigila la barrera de US3.

**INV-03 — El dueño de un movimiento lo decide la sesión, nunca el cuerpo de la petición.**
El alta asigna `UsuarioId` desde `IUsuarioActual`. El cuerpo del alta no tiene campo de propietario
—`NuevoMovimientoDto` son `tipo`, `monto`, `categoriaId` y `fecha`—, así que un propietario que
llegue en el JSON es un campo desconocido y se descarta al deserializar. Eso hace que INV-03 se
cumpla hoy por **dos** motivos independientes, y el test tiene que seguir valiendo si mañana el DTO
gana un campo: por eso el escenario manda el propietario ajeno en el cuerpo igual, y comprueba dónde
cayó el movimiento.

**INV-04 — Ninguna operación de una cuenta altera los movimientos de otra.**
Es AC-08 del PRD. Se comprueba sobre la **otra** cuenta, releyendo su listado antes y después.

## Lo que NO se modela acá

- **El contador de intentos de acceso** (`intento_de_acceso`, ticket `01b`): no tiene dueño y no
  participa del aislamiento. Su fila existe para un email, no para una cuenta.
- **Las categorías**: hoy son globales (`usuario_id` nulo). Su aislamiento es el ticket 3
  ([D-08](./research.md)).
- **Las monedas**: catálogo global sin propietario.

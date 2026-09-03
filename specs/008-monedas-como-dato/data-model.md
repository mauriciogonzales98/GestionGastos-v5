# Data Model: Monedas administrables como dato

**Feature**: `008-monedas-como-dato` · **Fecha**: 2026-09-03

> **Esta feature no cambia el esquema.** Es la primera desde la 005 que no lleva migración. Este
> documento describe el modelo **tal como ya existe**, porque es lo que las verificaciones ejercitan
> y porque un plan que no lo escriba deja a quien implemente adivinando qué puede tocar.

---

## `moneda` — el catálogo

Existe desde la migración `Inicial` (2026-08-23) y se administra **como dato**: la aplicación la lee
y nunca la escribe.

| Columna | Tipo | Regla |
|---|---|---|
| `id` | `smallint` PK | Autoincremental |
| `codigo` | `char(3)` | ISO 4217. **Único** (`IX_moneda_codigo`) |
| `nombre` | `varchar` | Nombre visible |
| `simbolo` | `varchar` | Símbolo visible |
| `decimales` | `tinyint`, default `2` | Cuántos decimales admite. **Dato de la moneda, no constante del código.** Hoy nadie la usa: el formato es del ticket 6 (deuda D8-05) |
| `es_predeterminada` | `bit(1)` | **Exactamente una fila en verdadero** |
| `unica_predeterminada` | `tinyint` GENERADA, virtual | `1` si es la predeterminada, `NULL` si no. No existe en el modelo de EF: la creó la migración `UnicaMonedaPredeterminada` con SQL crudo |

**Filas sembradas**: `1 · ARS · Peso argentino · $ · predeterminada` y
`2 · USD · Dólar estadounidense · US$`.

### La invariante, y dónde vive

`ux_moneda_unica_predeterminada` es un índice **único** sobre `unica_predeterminada`. MySQL admite
varios `NULL` en un índice único, así que la restricción limita las predeterminadas a una sola y deja
libres a todas las demás.

**Esto es FR-004 y es la pieza que hace administrable al catálogo.** Sin ella, la invariante viviría
sólo en el código, y el alta —que hace `SingleAsync(m => m.EsPredeterminada)`— elegiría una sin
criterio si hubiera dos, o reventaría si hubiera cero. Administrar por fuera de la aplicación exige
que la base sepa defenderse sola de quien la administra.

**Consecuencia para D-02**: mover la predeterminada son **dos sentencias**, apagar y después prender.
Una sola que haga ambas cosas puede violar el índice transitoriamente según el orden en que el motor
toque las filas.

---

## `movimiento.moneda_id` — el vínculo

| Columna | Tipo | Regla |
|---|---|---|
| `moneda_id` | `smallint` NOT NULL | FK a `moneda.id`, `RESTRICT`. Índice `IX_movimiento_moneda_id` |

**Nació con la clave foránea en `Inicial`**, y ése es el motivo por el que FR-07 y AC-09 del PRD no
aplican: nunca hubo un movimiento sin moneda que una migración tuviera que normalizar, y no puede
haberlo.

El `RESTRICT` es lo que impide borrar una moneda que ya nombra movimientos. El PRD deja el borrado
fuera de alcance y esta feature no lo prueba: la base ya lo impide.

**La moneda no viaja en ninguna petición.** Ni `NuevoMovimientoDto` ni `MovimientoEditadoDto` la
llevan; el alta la toma del catálogo. Es el motivo entero por el que FR-04 del PRD se difiere al
ticket 4b (deuda D8-01): no hay entrada que validar.

---

## Lo que se deriva y no se persiste

Ninguno de los totales existe como fila. `ResumenPorMoneda` y `TotalPorCategoria` se calculan en cada
pedido a partir de los movimientos, agrupando por `(moneda_id, tipo, categoria_id)`.

**Esa agrupación es FR-005 y FR-006, y es estructural**: dos monedas son dos grupos distintos, así
que no hay ningún punto del cálculo en el que un monto de una pueda caer en el total de la otra. No
es una comprobación que se hace al final; es una propiedad de cómo está escrita la consulta.

**Y no se toca en esta feature** ([D-04](./research.md)).

---

## Estados y transiciones

Ninguno. Una moneda no tiene ciclo de vida: se agrega y queda. No se da de baja —el `RESTRICT` lo
impide en cuanto tiene movimientos— y cambiar cuál es la predeterminada está fuera de alcance del
PRD como operación de producto, aunque la verificación de AC-02 la ejerza como acto de
administración ([D-02](./research.md)).

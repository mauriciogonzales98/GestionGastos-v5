# Data Model: Nota descriptiva del movimiento

**Feature**: 012-nota-del-movimiento · **Fecha**: 2026-09-10

Dos cambios de esquema en **una sola migración** (D-10). Es la primera migración desde la feature 007.

---

## 1 · `movimiento.nota` — la columna nueva

| Propiedad | Valor | Por qué |
|---|---|---|
| Tipo | `varchar(120)` | El largo es **exactamente** el límite del requisito, no más (D-01). `utf8mb4` cuenta caracteres, no bytes, y el límite se cuenta en caracteres Unicode: la unidad de la columna y la del requisito son la misma |
| Nulabilidad | **Anulable** | Decidido en *Clarifications*: el esquema admite la ausencia de valor y la cadena vacía, y no normaliza al escribir. Es la **primera columna de texto anulable del proyecto** — las nueve que hay son todas `IsRequired()` |
| Valor por omisión | Ninguno | Los movimientos que ya existen quedan sin valor, que es el mismo estado que uno nuevo guardado sin nota. **Sin migración de datos** |
| Índice | **Ninguno** | Un índice sobre la nota sólo sirve para buscar por ella, y buscar por ella es lo que `FR-007` prohíbe. Ponerlo "por si acaso" dejaría servido el camino que la feature evita |
| Restricción de largo en la base | La del tipo | No hace falta un `CHECK`: `varchar(120)` ya rechaza 121 caracteres. El mensaje legible lo da `ValidacionDelMovimiento`, como con el monto |

### Lo que la columna **no** garantiza, y quién lo garantiza entonces

Esto es la consecuencia de la decisión de *Clarifications* y vale tenerlo junto:

| Regla | Quién la hace cumplir |
|---|---|
| Hasta 120 caracteres Unicode | El tipo de la columna **y** `ValidacionDelMovimiento` (el mensaje legible) |
| Sin espacios en los extremos | El servidor, que recorta **antes** de medir (D-03) |
| "Sin nota" es **un solo estado** | **La lectura, no el esquema**: `MovimientoDto` normaliza (D-04, `FR-011`). Es la deuda **D12-08**, aceptada a sabiendas |
| La nota no clasifica ni agrupa | `BarreraDeLaNotaTests` más `verificar-nota.sh` (D-09, `FR-007`) |

---

## 2 · `moneda.codigo` — la restricción que le faltaba (`FR-010`, deuda D11-02)

| Propiedad | Antes | Después |
|---|---|---|
| Tipo | `char(3) NOT NULL` | igual |
| Contenido | **cualquier** carácter | **tres letras** |

La restricción se expresa en la definición de la tabla con una comparación contra una expresión
regular, que MySQL 8.4 evalúa en cada escritura.

**Qué cambia en la práctica**: hoy un `1X2` o un `a1b` metido con SQL puro entra sin protesta, viaja en
el contrato y llega hasta el formateo del monto, que es el cuarto lugar donde esta deuda se podía
cruzar. Después de la migración, la base lo rechaza.

**Qué NO cambia**: no se agrega validación de aplicación. El catálogo se administra como dato
(`PRD:RF-32`) y nadie lo escribe desde la aplicación.

### Compatibilidad con lo que ya existe — verificado, no supuesto

| Quién siembra una moneda | Código | Tres letras |
|---|---|---|
| La semilla de la migración `Inicial` | `ARS`, `USD` | ✅ |
| `verificar-monedas.sh` | `XTS` (ISO 4217 lo reserva para pruebas) | ✅ |
| Los tests (`CatalogoDeMonedas`) | `XCA`, `XCE`, `XCT`, `XED`, `XEL`, `XMV`, `XPF`, `XSC`, `EUR` | ✅ |

**Los doce son tres letras**, así que la migración se aplica sobre datos válidos y ninguna barrera
existente cambia de resultado. Era el único acoplamiento real de este cambio con el resto del
proyecto, y está cerrado antes de empezar.

---

## 3 · El dominio

`Movimiento` suma una propiedad: la nota, anulable. Nada más — no suma relaciones, no suma
comportamiento y no participa de ninguna invariante del modelo.

Lo que **no** cambia y conviene decir, porque es lo que `NFR-002` exige: `MontoAgrupado`, el tipo con
el que se calcula el resumen, **no la menciona**. La consulta que agrupa no la selecciona, no la agrupa
y no la suma. Los totales, el balance y el desglose no pueden moverse por un valor de la nota porque la
nota no entra en esa consulta.

---

## 4 · Las tres formas del contrato

El detalle con ejemplos está en [contracts/api.md](./contracts/api.md). El resumen de la asimetría, que
es la parte que hay que entender:

| Forma | La nota | Ausente significa |
|---|---|---|
| Lo que se manda al **registrar** | Opcional | **Sin nota** |
| Lo que se manda al **modificar** | **Obligatoria** | No es una posibilidad válida |
| Lo que se **devuelve** | Siempre presente, **nunca nula** | — |

Es la misma asimetría que `fecha` ya tiene, por el motivo que `MovimientoDtos.cs` ya escribió:
**ausente nunca puede producir un cambio que nadie pidió** (D-06).

---

## 5 · La migración

**Una sola**, con un nombre que nombra los dos cambios. Contiene:

1. `ALTER TABLE movimiento ADD COLUMN nota varchar(120) NULL`
2. La restricción de tres letras sobre `moneda.codigo`

**Sin migración de datos**: nada hay que rellenar ni corregir. Las filas existentes de `movimiento`
quedan sin valor de nota, y las de `moneda` ya cumplen la restricción.

**Reversible**: la vuelta atrás quita la columna y la restricción. La columna se va con los datos que
tuviera, que es lo esperable de una vuelta atrás y no un problema a resolver.

**Va dentro de `Migrations/`**, que es la carpeta que `verificar-linter.sh` exime de los analizadores a
propósito — el código generado por la herramienta no se formatea a mano. Es la primera vez en cinco
features que esa exención se usa de verdad.

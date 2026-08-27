# Phase 1 — Data model: Límite de intentos fallidos

Una tabla nueva y ninguna modificada. `usuario`, `movimiento`, `categoria` y `moneda` quedan como
están.

---

## `intento_de_acceso` (nueva)

Los fallos consecutivos acumulados por un email presentado en el inicio de sesión. **Una fila por
email, y sólo mientras el email tenga fallos que contar**: sin fila significa "cero fallos", que es
el estado normal de todos los emails del mundo.

| Columna | Tipo | Reglas |
|---------|------|--------|
| `email` | `varchar(254)`, colación `utf8mb4_0900_ai_ci` | **Clave primaria.** El email tal como lo resuelve el inicio de sesión: recortado de espacios. La colación insensible a mayúsculas es la misma de `usuario.email` |
| `fallos_consecutivos` | `tinyint unsigned` | `NOT NULL`. Siempre ≥ 1: una fila con 0 no existe, se borra |
| `ultimo_fallo` | `datetime(6)` | `NOT NULL`, en UTC. La marca del último fallo **contado**; los intentos rechazados por el bloqueo no la mueven ([D-02](./research.md)) |

**Índice**: `ix_intento_de_acceso_ultimo_fallo` sobre `ultimo_fallo`, para que la purga por
inactividad ([D-03](./research.md)) sea un `DELETE` por índice y no un recorrido de la tabla.

### Por qué el email es la clave y no hay `usuario_id`

Porque **esta tabla no habla de cuentas, habla de emails presentados**. FR-001 obliga a contar los
fallos de un email que no existe igual que los de uno registrado, así que no puede haber clave
foránea a `usuario`: la fila tiene que poder existir sin cuenta detrás. Que la fila exista no dice
nada sobre si hay cuenta con ese email, y eso es justamente lo que la vuelve segura de tener.

Es también la razón por la que no son columnas de `usuario` — el motivo completo está en
[D-01](./research.md), y `Usuario.cs` ya lo anticipaba desde `002`.

### Colación: por qué importa acá también

`usuario.email` usa `utf8mb4_0900_ai_ci` para que `Ana@x.com` y `ana@x.com` sean la misma cuenta.
Si esta tabla usara la colación binaria por defecto, serían **dos contadores distintos** y el límite
se esquivaría cambiando una letra de mayúscula: 5 intentos con `ana@`, 5 con `Ana@`, 5 con `aNa@`,
y así hasta agotar las combinaciones. El contador y la búsqueda de la cuenta tienen que usar la
misma clave, o el límite no limita.

---

## Los tres números de la regla

Viven como constantes junto al servicio que las aplica, no repartidas por el código:

| Constante | Valor | De dónde sale |
|-----------|-------|---------------|
| Fallos que bloquean | 5 | RNF-05, FR-002 |
| Ventana de bloqueo | 15 min | RNF-05, FR-002 |
| Inactividad que reinicia el contador | 24 h | Decisión del plan, [D-03](./research.md) |

---

## Transiciones de estado

Todo lo que le puede pasar a la fila de un email. `n` es `fallos_consecutivos`, `t` es
`ultimo_fallo`, y `ahora - t < 15 min` con `n >= 5` es la definición de **bloqueado**.

| Estado | Evento | Estado resultante |
|--------|--------|-------------------|
| Sin fila | Intento fallido | Fila con `n = 1`, `t = ahora` |
| Sin fila | Intento exitoso | Sin fila |
| `n < 5` | Intento fallido | `n = n + 1`, `t = ahora` |
| `n < 5` | Intento **exitoso** | Fila borrada (FR-003, AC-05) |
| `n = 5`, dentro de la ventana | Cualquier intento, correcto o no | **Rechazado.** La fila **no cambia** (AC-02, AC-03) |
| `n >= 5`, ventana vencida | Intento fallido | `n = 1`, `t = ahora` — la ventana vencida se lleva los cinco fallos ([D-03](./research.md)) |
| `n >= 5`, ventana vencida | Intento exitoso | Fila borrada (AC-06) |
| Cualquiera con `t` de más de 24 h | Purga, al escribir el fallo de **otro** email | Fila borrada ([D-03](./research.md)) |

La fila del quinto fallo **no se toca** mientras dure el bloqueo. Es lo que hace que la ventana sea
fija desde el quinto fallo y no deslizante, y por lo tanto lo que impide dejar a alguien afuera para
siempre golpeando su email cada 14 minutos.

---

## Migración

Una sola, y **sólo crea una tabla**: no toca datos existentes, no borra nada y su `Down` la
elimina sin pérdida —lo único que se pierde son contadores en curso, que es exactamente lo que un
`Down` debería llevarse—. Es una migración mucho más tranquila que la de `002`, que borraba la
semilla.

No hay sembrado: la tabla nace vacía y se llena sola con el primer intento fallido.

---

## Relaciones

Ninguna. `intento_de_acceso` no tiene claves foráneas ni nadie la referencia. Es estado operativo
con una vida de 15 minutos —24 h en el peor caso—, y esa ausencia de relaciones es deliberada: si
mañana se decide moverla a otro almacenamiento, no arrastra el modelo.

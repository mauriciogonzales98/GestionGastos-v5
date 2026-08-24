# Phase 1 — Data Model: Identidad y sesión

Cambios sobre el esquema que dejó FEAT-001a. Las decisiones de tipo y su motivo están en
[research.md](./research.md).

Sólo cambia una tabla. La sesión **no es una tabla** — ver [D-01](./research.md).

---

## `usuario` (se modifica)

FEAT-001a la creó con lo mínimo para que `movimiento.usuario_id` fuera una clave foránea real desde
el día uno. Ahora pasa a ser una cuenta de verdad.

| Columna | Tipo | Restricciones | Estado | Motivo |
|---------|------|---------------|--------|--------|
| `id` | `bigint` | PK, autoincremental | ya existe | |
| `email` | `varchar(254)` | `NOT NULL`, `UNIQUE` | ya existe | 254 es el máximo de un email por RFC 5321. El `UNIQUE` es lo que hace imposible la segunda cuenta de FR-002 |
| `contrasena_hash` | `varchar(72)` | `NOT NULL` | **nueva** | El hash bcrypt completo: algoritmo, factor de trabajo y sal incluidos. 60 caracteres en el formato actual; 72 deja aire sin ser un `text` |

**Colación de `email`**: `utf8mb4_0900_ai_ci`, que es insensible a mayúsculas. Es lo que hace que
`Ana@x.com` y `ana@x.com` sean **la misma cuenta**, tanto para el `UNIQUE` como para la búsqueda del
login. Sin esto, el `UNIQUE` dejaría entrar las dos y FR-002 quedaría incumplido por una diferencia
que ninguna persona percibe como distinta.

**Lo que NO se agrega acá**, para que se note que es deliberado:

- Contador de intentos fallidos y ventana de bloqueo → ticket `01b` (RNF-05).
- Nombre, apellido, avatar, preferencias → no están en el PRD.
- Roles o permisos → no hay más de un tipo de usuario.
- Fecha de alta y de último acceso → nadie las lee todavía; agregarlas es contrato que no se decidió.

> **RNF-03 se cumple acá**: `contrasena_hash` guarda un verificador, no la contraseña. Un volcado de
> la base no la revela, que es exactamente lo que `AGENTS.md` fija en *What NOT to do*.

---

## `movimiento` (no cambia su forma, sí sus filas)

La estructura queda igual. Lo que cambia es de quién son las filas: `usuario_id` deja de apuntar
siempre a la semilla y pasa a apuntar a la cuenta que estaba en sesión al registrarlo.

La migración **borra** todos los movimientos cuyo `usuario_id` es el de la semilla ([D-06](./research.md)).

---

## Sesión — por qué no es una tabla

La spec la nombra como entidad porque **conceptualmente** existe: una cuenta autenticada, con un
momento de última actividad. En la implementación es el ticket cifrado dentro de la cookie, y su
"última actividad" es lo que la expiración deslizante renueva en cada petición ([D-01](./research.md),
[D-03](./research.md)).

Consecuencia que conviene tener escrita: **no se puede listar ni revocar sesiones desde el
servidor**. Nadie lo pidió, y el día que haga falta —"cerrar sesión en todos los dispositivos"— la
tabla se agrega ahí, con su caso real.

---

## Migración: orden obligatorio

```text
1. ALTER  usuario  ADD contrasena_hash          -- la columna llega antes que las cuentas
2. DELETE movimiento WHERE usuario_id = semilla -- primero los hijos...
3. DELETE usuario     WHERE id        = semilla -- ...después el padre
```

El orden de 2 y 3 no es preferencia: `movimiento.usuario_id` es una clave foránea `RESTRICT`, así
que borrar el usuario primero **falla**. Es la misma restricción que en FEAT-001a impidió sembrar una
categoría de una cuenta inexistente, funcionando como corresponde.

`contrasena_hash` entra `NOT NULL` sin valor por defecto y la tabla queda vacía después del paso 3,
así que no hay filas que rellenar. El orden importa: al revés, el `ALTER` fallaría contra la fila
semilla.

---

## Relaciones

```text
usuario 1 ──< movimiento          (usuario_id NOT NULL, RESTRICT)
usuario 1 ──< categoria           (usuario_id NULL = predefinida del sistema)
```

Sin cambios respecto de FEAT-001a. Lo que cambia es que ahora hay **más de una** fila en `usuario`,
y por primera vez esas relaciones separan datos de personas distintas — que es lo que el ticket `01c`
va a verificar con dos cuentas reales.

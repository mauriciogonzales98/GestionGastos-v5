# Contrato HTTP — Identidad y sesión

**La fuente de verdad de este contrato sigue siendo `frontend/src/api/tipos.ts`.** Los tests de
`backend/GestionGastos.Api.Tests/Contrato/` leen ese archivo y lo comparan contra el JSON real, en
las dos direcciones. Este documento describe el contrato; no lo reemplaza. El motivo de esa asimetría
está en [ADR-001](../../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md).

Tres endpoints nuevos. Los tres de FEAT-001a no cambian de forma, pero **pasan a exigir sesión**.

---

## `POST /api/cuentas`

Crea una cuenta (FR-001).

**Petición**

```json
{ "email": "ana@ejemplo.com", "contrasena": "una contraseña larga" }
```

**Respuesta `201`** — siempre la misma, exista o no ya esa cuenta:

```json
{ "mensaje": "Si el email no estaba registrado, la cuenta fue creada. Ya podés iniciar sesión." }
```

> **Esto es a propósito y es lo que NFR-03 ordena.** Si el email ya estaba, no se crea nada y la
> cuenta original queda intacta (AC-02), pero la respuesta es idéntica. Responder "ese email ya
> existe" sería mucho más amable y publicaría la lista de emails registrados. El costo —alguien
> recibe un `201` y después no puede entrar— está aceptado en [D-04](../research.md).

**Respuesta `400`** — `ProblemDetails` de validación, con la clave del campo en `errors`:

| Caso | Campo en `errors` |
|------|-------------------|
| Email ausente, vacío o con formato inválido | `email` |
| Contraseña ausente o más corta que el mínimo | `contrasena` |

Acá **sí** se dice qué está mal: no se revela nada sobre qué cuentas existen.

---

## `POST /api/sesion`

Inicia sesión (FR-003). Responde con la cookie de sesión.

**Petición**

```json
{ "email": "ana@ejemplo.com", "contrasena": "una contraseña larga" }
```

**Respuesta `200`**

```json
{ "email": "ana@ejemplo.com" }
```

**Respuesta `401`** — **la misma** para email inexistente y para contraseña incorrecta (AC-04,
NFR-03):

```json
{ "type": "...", "title": "Email o contraseña incorrectos.", "status": 401 }
```

El servidor ejecuta un hash aunque el email no exista, para que las dos respuestas tarden lo mismo
([D-04](../research.md)). Igualar el mensaje sin igualar el tiempo deja el canal abierto.

---

## `DELETE /api/sesion`

Cierra la sesión (FR-005). Responde `204` y borra la cookie.

Es idempotente: cerrar una sesión que ya no existe también responde `204`. No es un error.

---

## `GET /api/sesion`

Devuelve la cuenta en sesión. Es lo que la pantalla consulta al arrancar ([D-09](../research.md)).

**Respuesta `200`**

```json
{ "email": "ana@ejemplo.com" }
```

**Respuesta `401`** si no hay sesión, o si expiró por 24 h sin actividad (AC-12).

---

## Los endpoints de FEAT-001a pasan a exigir sesión

`GET /api/categorias`, `POST /api/movimientos` y `GET /api/movimientos` **no cambian de forma**.
Cambian en dos cosas:

1. Sin sesión responden **`401`** y **no ejecutan su efecto** (AC-05). Que no se ejecute es la mitad
   del criterio: un `POST` que rechaza pero igual inserta cumple el código de estado y falla el
   requisito.
2. El propietario y el recorte de la lectura salen de la sesión, no de la fila semilla (AC-07,
   AC-08).

**Los únicos endpoints sin sesión** son `POST /api/cuentas` y `POST /api/sesion`. Cualquier endpoint
que se agregue en el futuro nace exigiendo sesión: la autorización se aplica global y se exceptúa
explícitamente, nunca al revés. Un endpoint nuevo que alguien olvide proteger es el agujero más
fácil de dejar.

---

## Formato de error

El mismo de FEAT-001a: `ProblemDetails` (RFC 9457), con `errors` indexado por nombre de campo cuando
el error corresponde a un campo. Un `401` no lleva `errors`: no hay un campo al que culpar, y el
frontend usa esa ausencia para mandarlo a la región de error del formulario.

---

## Lo que este contrato NO tiene todavía

Anotado para que se note que es deliberado:

- Bloqueo por intentos fallidos → ticket `01b`
- Recuperación y cambio de contraseña → no están en el PRD
- Verificación del email por correo → exigiría un envío de correo que el proyecto no tiene
- Listar o revocar sesiones activas → no hay tabla de sesiones ([D-01](../research.md))

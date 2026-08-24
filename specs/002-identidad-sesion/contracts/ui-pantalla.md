# Contrato de la pantalla — Identidad y sesión

Fija **estructura y estados**, no apariencia. Las reglas de marcado siguen siendo las tres del
*Contrato de marcado de la UI* de [FEAT-001a](../../001-alta-listado-movimientos/plan.md), que
valen para todo el proyecto: el componente único de campo, dónde vive el error, y `l-*` / `c-*`.

---

## Dos pantallas, un estado

No hay router ([D-08](../research.md)). Lo que se muestra depende de si hay sesión:

```text
¿hay sesión?
├── no  → pantalla de autenticación   (alta | inicio de sesión)
└── sí  → pantalla de movimientos     (la de FEAT-001a, más el cierre de sesión)
```

Mientras se está averiguando —la consulta inicial a `GET /api/sesion`— se muestra un indicador de
carga. **No** se muestra la pantalla de autenticación por defecto: hacerlo haría parpadear el login
en cada recarga de alguien que sí tiene sesión.

---

## Pantalla de autenticación

```text
main
├── h1                                  "Gestión de gastos"
├── conmutador: Iniciar sesión | Crear cuenta
└── form (c-formulario-acceso)          ← el foco arranca en el primer campo
    ├── grupo: email      (email)
    ├── grupo: contraseña (password)
    ├── región de error del formulario  (sólo si hay un error sin campo)
    └── button type=submit              "Entrar" | "Crear cuenta"
```

Cada **grupo** es el mismo `CampoConError` que ya existe. No se arma la tripleta a mano, igual que
en FEAT-001a.

El campo de contraseña es `type="password"`, así que el navegador no lo muestra ni lo autocompleta
en claro. Los dos formularios llevan los `autocomplete` que corresponden (`username`,
`current-password` / `new-password`): es lo que permite a un gestor de contraseñas hacer su trabajo,
y un gestor de contraseñas es la mejor defensa que tiene quien usa la aplicación.

**El envío entero con teclado se mantiene**, igual que AC-55 de FEAT-001a: `<form>` real con
`<button type="submit">`, orden del DOM igual al de tabulación, sin `tabindex` positivo.

---

## Estados

| Estado | Qué se ve | Requerimiento |
|--------|-----------|---------------|
| **Averiguando** | Indicador de carga. Ninguna de las dos pantallas | — |
| **Sin sesión** | Pantalla de autenticación, en "Iniciar sesión" | FR-004 |
| **Enviando** | El botón queda deshabilitado hasta la respuesta | Evita el doble envío |
| **Credenciales rechazadas** | "Email o contraseña incorrectos." en la región de error del formulario | AC-04 |
| **Alta enviada** | El mismo mensaje exista o no la cuenta, y se pasa a "Iniciar sesión" | NFR-03 |
| **Con sesión** | Pantalla de movimientos, con el email y el botón de cerrar sesión | FR-004 |
| **Sesión expirada** | Al primer `401`, se vuelve a la pantalla de autenticación con un aviso | AC-12 |

El error de credenciales va a la **región del formulario** y no al lado de un campo, a propósito:
señalar el campo "email" o el campo "contraseña" diría cuál de los dos estaba bien.

---

## Qué pasa con un `401` en cualquier petición

Cualquier respuesta `401`, en cualquier momento, lleva a la pantalla de autenticación con el aviso
de que la sesión venció ([D-09](../research.md)). No hay un temporizador en el cliente: quien decide
es el servidor, y el cliente reacciona.

**Lo que se estaba haciendo no se pierde en silencio**: si el `401` llega al enviar el formulario de
movimiento, el aviso lo dice. Vaciar la pantalla sin explicación es la peor versión de una sesión
vencida.

---

## Cierre de sesión

Un `<button>` en la pantalla de movimientos. Al confirmarlo el servidor borra la cookie y la
aplicación vuelve a la pantalla de autenticación (AC-06).

Es un `<button>` y no un enlace: cambia estado del servidor, y los enlaces son para navegar.

---

## Lo que esta pantalla NO tiene

- "Recordarme" → no está en el PRD, y con expiración deslizante de 24 h el caso frecuente ya está
  cubierto
- "Olvidé mi contraseña" → no hay recuperación en este ticket
- Medidor de fortaleza de la contraseña → es del ticket de maquetación, si se decide
- Aviso de "te quedan N intentos" → el bloqueo es del ticket `01b`

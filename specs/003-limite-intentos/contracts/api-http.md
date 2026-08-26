# Contrato HTTP — Límite de intentos fallidos

**El contrato no cambia de forma.** Ni un tipo nuevo, ni un campo nuevo, ni un código de estado
nuevo. Esta feature agrega una **tercera causa** para una respuesta que ya existe, y la vuelve
indistinguible de las otras dos.

La fuente de verdad sigue siendo `frontend/src/api/tipos.ts`, comparada contra el JSON real por los
tests de `backend/GestionGastos.Api.Tests/Contrato/` ([ADR-001](../../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md)).
Como no hay tipos nuevos, esos tests **no se tocan**; la barrera del contrato se corre igual en la
puerta de cierre, que para eso está.

---

## `POST /api/sesion`

Único endpoint afectado. Petición y respuesta `200`: sin cambios respecto de
[`002`](../../002-identidad-sesion/contracts/api-http.md).

**Respuesta `401`** — ahora la misma para **tres** causas distintas:

```json
{ "type": "...", "title": "Email o contraseña incorrectos.", "status": 401 }
```

| Causa | Desde |
|-------|-------|
| El email no corresponde a ninguna cuenta | `002` |
| La contraseña no corresponde al email | `002` |
| **El email está dentro de su ventana de bloqueo** | **esta feature** (FR-005, AC-08, AC-09) |

Las tres son **byte por byte la misma respuesta**, y las tres tardan lo mismo: el camino del email
bloqueado ejecuta igual una verificación de hash, aunque descarte el resultado
([D-04](../research.md)). Igualar el mensaje sin igualar el tiempo deja el canal abierto — es la
misma lección que `002` ya había aprendido con el email inexistente.

> **Por qué no un `429 Too Many Requests`**, que es lo que diría el manual para un límite de tasa:
> un código distinto le anuncia al atacante que ese email acumuló cinco fallos, y eso es
> exactamente lo que RNF-05 prohíbe publicar. Acá la práctica habitual y el requisito se
> contradicen, y gana el requisito ([D-09](../research.md)).

**Respuesta `400`** — sin cambios. Un intento sin email o sin contraseña lo rechaza la validación
antes de llegar al límite, y **no cuenta como intento fallido**: no hay contador del email vacío.

---

## Lo que un cliente puede observar

Nada nuevo. Desde afuera, la única diferencia observable es que **a partir del sexto intento
consecutivo fallido sobre un email, la contraseña correcta también devuelve `401` durante 15
minutos**.

Consecuencia directa: **el frontend no se toca**. La pantalla de acceso ya sabe mostrar ese `401`
en la región del formulario, y no tiene forma —ni motivo— de distinguir la causa.

---

## Lo que NO expone esta feature

- **Ningún endpoint para consultar si un email está bloqueado.** Sería un enumerador de cuentas con
  formulario.
- **Ningún endpoint para desbloquear.** FR-004 exige que la ventana se levante sola; adelantarla es
  Out of Scope del PRD.
- **Ningún dato del contador en ninguna respuesta**: ni intentos restantes, ni minutos que faltan,
  ni una cabecera `Retry-After`. Todos ellos dicen lo mismo que un `429`.

# Quickstart — Límite de intentos fallidos

Cómo levantar la feature y verificarla a mano. **No reemplaza la suite**: el Principio III exige la
puerta automatizada, no una pasada a mano.

---

## Prerrequisitos

Los mismos de [`002`](../002-identidad-sesion/quickstart.md): .NET SDK 10.x, Node 22.x con pnpm, y
MySQL 8.4.10 en el puerto 3306. Nada nuevo — esta feature no agrega dependencias
([D-09](./research.md)).

```bash
export ConnectionStrings__Default="Server=127.0.0.1;Port=3306;Database=gestiongastos_test;User Id=...;Password=...;"
```

---

## Levantar

```bash
# Backend — en Development aplica las migraciones al arrancar, incluida la tabla nueva
dotnet run --project backend/GestionGastos.Api

# Frontend, en otra terminal
pnpm --dir frontend dev
```

> **La migración de este ticket sólo crea una tabla.** No borra nada ni toca datos existentes: lo
> que tengas cargado sigue donde estaba. Hace falta una cuenta para probar, así que si venís de una
> base limpia, creala primero.

---

## Validación manual — las tres historias

Todo lo de acá se puede hacer desde la pantalla de acceso. Con `curl` es más rápido, y va indicado
donde ayuda.

**US1 — probar contraseñas deja de ser gratis (P1)**

1. Con una cuenta ya creada, intentá entrar **cinco veces** con la contraseña equivocada. Las cinco
   fallan, como siempre.
2. Intentá una sexta vez, **con la contraseña correcta**. Tiene que fallar igual, con el mismo
   mensaje (AC-01, AC-02).
3. Mirá la tabla: `SELECT * FROM intento_de_acceso;`. Tiene que haber una fila para ese email con
   `fallos_consecutivos = 5`.
4. Volvé a intentar un par de veces más y mirá la fila de nuevo: **no cambió**. Los intentos
   rechazados por el bloqueo no mueven `ultimo_fallo`, que es lo que hace que la ventana sea fija
   ([D-02](./research.md)).
5. Con **otra** cuenta, entrá normalmente. El bloqueo alcanza a un email, no a la aplicación
   (AC-07).
6. Probá los cinco fallos sobre un email **que no existe**. Se comporta igual, y la tabla también
   tiene su fila (AC-09).
7. Desde otro navegador —o una ventana privada—, intentá el email bloqueado. Sigue bloqueado: lo que
   está bloqueado es el email, no quien lo intenta (AC-10).
8. **Reinicio (AC-11)**: cortá el backend con `Ctrl+C` y volvé a levantarlo. El email sigue
   rechazado. Ése es el punto entero de que el contador viva en la base.

**US2 — el bloqueo se levanta solo (P2)**

1. Esperá 15 minutos desde el quinto fallo —esta parte a mano es aburrida a propósito; los tests la
   hacen adelantando el reloj— y entrá con la contraseña correcta. Entra, y sin que nadie haya
   intervenido (AC-06). La fila desaparece.
2. Sin bloquear nada: fallá **cuatro** veces y entrá a la quinta con la contraseña correcta. Entra
   (AC-04), y la fila se borra (AC-05). Volvé a fallar cuatro veces: seguís sin bloquearte, porque
   el contador arrancó de cero otra vez.

Si no querés esperar los 15 minutos, adelantá `ultimo_fallo` en la base:

```sql
UPDATE intento_de_acceso SET ultimo_fallo = ultimo_fallo - INTERVAL 16 MINUTE WHERE email = 'ana@ejemplo.com';
```

**US3 — el bloqueo no delata qué emails existen (P3)**

1. Compará, con `curl -i`, tres respuestas: email inexistente, contraseña incorrecta, y email
   bloqueado. Las tres tienen que ser **idénticas** — mismo código, mismo cuerpo, mismas cabeceras
   (AC-08, AC-09).

   ```bash
   curl -i -X POST localhost:5000/api/sesion -H 'Content-Type: application/json' \
     -d '{"email":"nadie@ejemplo.com","contrasena":"cualquiera1234"}'
   ```

2. Cronometralas: `curl -o /dev/null -s -w '%{time_total}\n' ...`. Las tres tienen que estar en el
   mismo orden de magnitud, ~100 ms. **Si la del email bloqueado vuelve en 2 ms, AC-13 está roto**
   aunque el cuerpo sea idéntico: alguien optimizó el `if` que [D-04](./research.md) pide no
   optimizar.

---

## La puerta

Los comandos son los de la tabla de *Stack* de `AGENTS.md`. Para esta feature, el backend es lo que
cambia; el frontend no se toca ([D-09](./research.md)), pero su puerta se corre igual antes de
cerrar.

```bash
# Backend
dotnet format backend/GestionGastos.slnx --verify-no-changes
dotnet build backend/GestionGastos.slnx -warnaserror
dotnet test backend/                       # en local corren TODOS, incluidos los de Rendimiento
dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings

# Las tres barreras
./backend/verificar-contrato.sh
./backend/verificar-autorizacion.sh
./backend/verificar-linter.sh

# Frontend
pnpm --dir frontend lint && pnpm --dir frontend format
pnpm --dir frontend exec tsc --noEmit
pnpm --dir frontend test
pnpm --dir frontend build
```

> Los tests de AC-12 y AC-13 miden tiempo de pared y **el CI los excluye** con
> `--filter "FullyQualifiedName!~Rendimiento"`. En local corren: si están en rojo acá, miralo, no lo
> ignores ([D-08](./research.md)).

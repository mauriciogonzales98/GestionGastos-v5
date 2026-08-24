# Quickstart — Identidad y sesión

Cómo levantar la feature y verificarla a mano. **No reemplaza la suite**: el Principio III exige la
puerta automatizada, no una pasada a mano.

---

## Prerrequisitos

| Qué | Versión / valor | Cómo comprobarlo |
|-----|-----------------|------------------|
| .NET SDK | 10.x | `dotnet --version` |
| Node + pnpm | Node 22.x | `pnpm --version` |
| MySQL | 8.4.10, puerto 3306 | responde en `127.0.0.1:3306` |

Dos bases, no una:

```bash
# La de la suite de integración
export ConnectionStrings__Default="Server=127.0.0.1;Port=3306;Database=gestiongastos_test;User Id=...;Password=...;"
```

`gestiongastos_migracion_test` la crea y la migra por su cuenta el test de AC-09
([D-07](./research.md)); no hay que prepararla a mano.

---

## Levantar

```bash
# Backend — en Development aplica las migraciones al arrancar, incluida la que borra la semilla
dotnet run --project backend/GestionGastos.Api

# Frontend, en otra terminal
pnpm --dir frontend dev
```

> **La aplicación arranca vacía.** La migración de este ticket borra la fila de usuario semilla y
> todos sus movimientos: lo que hayas cargado probando FEAT-001a se pierde. Está decidido en el PRD
> (2026-08-20) y explicado en [D-06](./research.md).

---

## Validación manual — las tres historias

**US1 — crear una cuenta y entrar (P1)**

1. Abrí la pantalla. Como no hay sesión, aparece la de autenticación.
2. Pasá a "Crear cuenta", poné un email y una contraseña, confirmá.
3. Volvé a "Iniciar sesión" y entrá con esas mismas credenciales. Tenés que llegar a la pantalla de
   movimientos, **vacía** (AC-01).
4. Probá crear una cuenta **con el mismo email**. La respuesta es la misma que la de un email nuevo
   — eso es NFR-03 funcionando, no un error. Verificá en la base que sigue habiendo una sola fila
   con ese email y que su hash no cambió (AC-02).
5. Mirá `usuario.contrasena_hash` en la base: tiene que empezar con `$2` y no parecerse en nada a lo
   que escribiste (AC-10). Creá una segunda cuenta con **la misma** contraseña: los dos hashes tienen
   que ser distintos (AC-11).

**US2 — la frontera (P2)**

1. Con sesión iniciada, cerrala. Volvés a la pantalla de autenticación (AC-06).
2. Sin sesión, pedí un endpoint a mano y comprobá que responde `401` y que **no** ejecuta su efecto
   (AC-05):
   ```bash
   curl -i -X POST http://localhost:5125/api/movimientos \
     -H 'Content-Type: application/json' \
     -d '{"tipo":"gasto","monto":100,"categoriaId":1}'
   # 401, y ningún movimiento nuevo en la base
   ```
3. Intentá entrar con una contraseña incorrecta y con un email que no existe. **Las dos respuestas
   tienen que ser iguales** (AC-04, NFR-03), y tardar parecido — si una vuelve al instante y la otra
   demora, el canal lateral sigue abierto.

**US3 — los movimientos son de quien los cargó (P3)**

1. Con la cuenta A, cargá un gasto. Verificá en la base que su `usuario_id` es el de A y no el de la
   semilla, que ya no existe (AC-07).
2. Cerrá sesión, entrá con la cuenta B y mirá el listado: **no** tiene que aparecer el movimiento de
   A (AC-08, la parte del listado).

> La verificación completa del aislamiento con dos cuentas —y sus criterios propios— es del ticket
> `01c`. Acá se comprueba que el usuario actual sale de la sesión, que es su prerequisito.

**AC-09 — la migración (una sola vez)**

No se valida a mano: es el test de [D-07](./research.md), que arma su propia base con la semilla
adentro, aplica la migración y comprueba que no queda rastro. Una vez borrada, el escenario no se
puede reproducir.

---

## La puerta

Los comandos exactos están en la tabla de `AGENTS.md`. Antes de cerrar la feature se corren todos,
incluidas las dos barreras:

```bash
pnpm --dir frontend lint && pnpm --dir frontend format
pnpm --dir frontend exec tsc --noEmit && pnpm --dir frontend test
pnpm --dir frontend build

dotnet format backend/GestionGastos.slnx --verify-no-changes
dotnet build  backend/GestionGastos.slnx -warnaserror
dotnet test   backend/GestionGastos.slnx
./backend/verificar-contrato.sh
./backend/verificar-linter.sh
```

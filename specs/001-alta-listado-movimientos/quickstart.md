# Quickstart — Alta de movimientos y listado simple

Cómo levantar la feature y validar que hace lo que la [spec](./spec.md) dice. Los comandos salen de
la tabla de *Stack* de `AGENTS.md`; acá está el orden y qué esperar de cada uno.

> Esta feature arranca sobre un repositorio sin código. Hasta que el incremento **A** esté cerrado,
> nada de esto corre: no existen ni `backend/` ni `frontend/`. Ver *Incrementos de entrega* en el
> [plan](./plan.md).

---

## Prerequisitos

| Qué | Versión | Cómo verificar |
|-----|---------|----------------|
| .NET SDK | 10.0.301 | `dotnet --version` |
| Node | 22.x | `node --version` |
| pnpm | 10.x | `pnpm --version` |
| MySQL | 8.4.10, puerto 3306 | `mysqladmin ping` |

Dos bases: `gestiongastos` para desarrollo y `gestiongastos_test` para los tests. El fixture crea y
migra la de test por su cuenta; sólo hace falta que el servidor esté arriba.

---

## Configuración

La cadena de conexión va en **user-secrets**, nunca en `appsettings` (`AGENTS.md`, *Code
conventions*):

```bash
dotnet user-secrets --project backend/GestionGastos.Api \
  set "ConnectionStrings:Default" "Server=127.0.0.1;Port=3306;Database=gestiongastos;User ID=root;Password=TU_PASSWORD;AllowPublicKeyRetrieval=true;"
```

Los tests leen la variable de entorno `ConnectionStrings__Default` apuntando a `gestiongastos_test`.
Sin ella el fixture lanza a propósito, en vez de adivinar contra qué base está escribiendo:

```bash
export ConnectionStrings__Default="Server=127.0.0.1;Port=3306;Database=gestiongastos_test;User ID=root;Password=TU_PASSWORD;AllowPublicKeyRetrieval=true;"
```

---

## Levantar

```bash
# Backend — aplica migraciones y siembra categorías, monedas y el usuario semilla
dotnet run --project backend/GestionGastos.Api

# Frontend, en otra terminal
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend dev
```

---

## Validación manual — los tres escenarios de la spec

Rápido, para ver la feature funcionando. **No reemplaza la suite**: el Principio III de la
constitución exige la puerta automatizada, no una pasada a mano.

**US1 — registrar un gasto (P1)**
1. Abrí la pantalla. El tipo viene en `gasto` y la fecha con el día de hoy (FR-003).
2. Poné monto `1250.50`, elegí `Comida`, guardá.
3. Esperá: el gasto aparece arriba del listado, el formulario queda vacío y el cursor está en el
   primer campo (FR-014).

**US2 — registrar un ingreso (P2)**
1. Cambiá el tipo a `ingreso`. El selector de categoría pasa a ofrecer sólo `Sueldo`, `Ingreso
   extra` y `Otros` — ninguna de gasto (AC-10).
2. Guardá un monto cualquiera. Aparece en el listado marcado como ingreso, distinguible del gasto.

**US3 — el formulario rechaza lo inválido (P3)**
1. Probá monto `0`, `-5`, `10.999` y `1000000000.00`. Cada uno se rechaza **con el mensaje al lado
   del campo de monto**, y nada se agrega al listado (AC-18).
2. Guardá sin elegir categoría: mensaje junto al selector (AC-40).

**AC-55 — sólo con teclado**
Recorré el formulario entero con `Tab`, completalo y enviálo con `Enter`, sin tocar el mouse. Cada
control tiene que mostrar foco visible al pasar por él.

---

## La puerta (VERIFY)

Lo que la constitución exige antes de dar una tarea por terminada. **Verde significa cero fallos y
cero warnings**: el backend compila con `-warnaserror`, así que un hallazgo de los analizadores es
puerta en rojo.

**Por tarea**, según qué se tocó:

```bash
# Frontend
pnpm --dir frontend lint
pnpm --dir frontend format      # prettier --check: verifica sin escribir
pnpm --dir frontend exec tsc --noEmit
pnpm --dir frontend test

# Backend
dotnet format backend/GestionGastos.sln --verify-no-changes
dotnet build backend/GestionGastos.sln -warnaserror
dotnet test backend/
```

**Antes de cerrar la feature**, además:

```bash
dotnet test backend/GestionGastos.sln --settings backend/cobertura.runsettings
./backend/verificar-contrato.sh    # ~90 s: corre dotnet test tres veces
./backend/verificar-linter.sh      # compila con un archivo temporal adentro; va al final
```

Las dos barreras tardan y van igual: el Principio V dice que una barrera que nunca se vio fallar no
es una barrera. `verificar-contrato.sh` comprueba que la verificación del contrato se pone en
**rojo** cuando el contrato se desalinea; `verificar-linter.sh`, que una violación deliberada rompe
el build en código escrito a mano y **no** lo rompe dentro de `Migrations/`.

En local corren todos los tests. El CI excluye los de `Rendimiento` —miden tiempo de pared y en un
runner compartido dan rojos que no dicen nada—, así que **AC-34 sólo se verifica de verdad en
local**:

```bash
dotnet test backend/ --filter "FullyQualifiedName~Rendimiento"
```

---

## Detalles que están en otro lado

- Entidades, columnas, restricciones e índices → [data-model.md](./data-model.md)
- Endpoints, formato de error y su mapeo a los AC → [contracts/api-http.md](./contracts/api-http.md)
- Estructura de la pantalla y sus estados → [contracts/ui-pantalla.md](./contracts/ui-pantalla.md)
- Por qué cada decisión técnica, y qué se descartó → [research.md](./research.md)

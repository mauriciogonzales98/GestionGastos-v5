# AGENTS.md — project context

> Este archivo describe **el proyecto**: qué es, con qué stack se construye, qué convenciones sigue
> y qué no hay que hacer. **El proceso** (fases, specs, planes, tareas, cuándo testear, cuándo
> commitear) no vive acá: lo aporta **Spec Kit** a través de sus comandos y plantillas.
> No mezcles ambas cosas: reglas de proceso escritas acá compiten con las del flujo.
>
> Es **agnóstico de la herramienta** a propósito: Claude Code lo lee vía el import en `CLAUDE.md`;
> Codex CLI, Copilot CLI, Cursor y OpenCode lo leen directamente; Gemini CLI lo recibe a través de
> `GEMINI.md`. El mismo archivo sirve con cualquier agente que abras el repo.

---

## Language

**Always respond in the language the user writes in.** Write every artifact you produce — PRDs,
specs, ADRs, reports, commit messages, status lines — in that same language, regardless of the
language these instructions are written in.

> Working language: Spanish

---

## What this project is

App de registro y gestión de gastos e ingresos personales, cargados por formulario, con un dashboard
para visualizarlos.

**Reference PRD:** `PRD.md`
**Plan de implementación:** `plan-de-implementacion/`

---

## Stack

**Este es el único lugar donde vive el stack.** No hay archivo derivado: si cambia algo acá, cambia
en todos lados.

| Field | Value |
|-------|-------|
| Language | Typescript para frontend, C# para backend |
| Runtime | Node 22.x + pnpm |
| Framework | React 19 + Vite, Node 22.x, .NET 10 (SDK 10.0.301), Entity Framework Core 9.0.18 + Pomelo.MySQL 9.0.0 |
| Database | MySQL 8.4.10 local, puerto 3306, schema `gestiongastos`. Tests: `gestiongastos_test`, y `gestiongastos_migracion_test` para los de migración |
| Test runner | xUnit en backend, Vitest en frontend |
| Linter / formatter | ESLint + Prettier en frontend; analizadores de Roslyn del SDK gobernados por `backend/.editorconfig` en backend |
| Package manager | pnpm |
| Install | `pnpm --dir frontend install --frozen-lockfile` |
| Lint (frontend) | `pnpm --dir frontend lint` |
| Format (frontend) | `pnpm --dir frontend format` — `prettier --check`: **verifica sin modificar archivos**, para que el paso del CI pueda ponerse en rojo. Para formatear de verdad: `pnpm --dir frontend format:fix` |
| Typecheck | `pnpm --dir frontend exec tsc --noEmit` |
| Test (frontend) | `pnpm --dir frontend test` |
| Lint (backend) | `dotnet format backend/GestionGastos.slnx --verify-no-changes` — espejo de `prettier --check`: verifica sin modificar archivos |
| Build (backend) | `dotnet build backend/GestionGastos.slnx -warnaserror` — además de compilar corre los analizadores de Roslyn, así que un hallazgo de calidad rompe el build. Qué reglas se aplican y cuáles se apagan, con su motivo: `backend/.editorconfig` |
| Cobertura (backend) | `dotnet test backend/GestionGastos.slnx --settings backend/cobertura.runsettings` — mide también el código de `Contrato/`, que vive en el proyecto de tests y que coverlet no instrumenta por defecto |
| Barrera del contrato (backend) | `./backend/verificar-contrato.sh` — comprueba que la verificación del contrato frontend↔backend se pone en rojo cuando el contrato se desalinea, no sólo que los tests pasan. Desalinea un caso por **forma** de comparación —una respuesta y una petición fallan por mecanismos distintos—, así que corre `dotnet test` cinco veces y tarda ~2,5 min |
| Barrera de autorización (backend) | `./backend/verificar-autorizacion.sh` — comprueba que la barrera que exige sesión en todo endpoint sabe fallar: agrega uno desprotegido a propósito, verifica el rojo, lo quita y verifica el verde. Sin esto, la barrera pasaría en verde el día que alguien agregue un endpoint abierto, que es el único día que importa |
| Barrera del linter (backend) | `./backend/verificar-linter.sh` — comprueba que la barrera del linter siga en pie: una violación deliberada rompe el build en código escrito a mano y no lo rompe dentro de `Migrations/`. Compila con un archivo temporal adentro, así que va después de los tests |
| Test (backend) | `dotnet test backend/` — requiere `ConnectionStrings__Default` apuntando a `gestiongastos_test`. Los tests de migración usan además una **segunda base**, `gestiongastos_migracion_test`: migran desde cero para verificar el efecto de una migración, y hacer eso sobre la base compartida dejaría a los demás tests sin esquema a mitad de corrida. CI agrega `--filter "FullyQualifiedName!~Rendimiento"`: los tests de rendimiento miden tiempo de pared y en un runner compartido dan rojos que no dicen nada. En local corren todos |

---

## Architecture conventions

- **Folder structure:** frontend y backend separados en sus respectivas carpetas (`backend/` para el
  backend y `frontend/` para el frontend).
- **Folder structure — la única excepción, declarada:** los tests de
  `backend/GestionGastos.Api.Tests/Contrato/` **leen** `frontend/src/api/tipos.ts`. Comparar las dos
  definiciones del contrato exige que algo mire a las dos; el motivo y el alcance quedan
  documentados en
  [`docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md`](docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md). Es lectura, en una sola dirección: el frontend no lee nada
  del backend, y eso no cambia.
- **Error handling:** typed errors; nunca un catch silencioso.
- **Dependencies:** no se agregan librerías nuevas sin justificarlas en la spec.

---

## Testing

Las reglas de testing —TDD obligatorio, un test por AC, la fase VERIFY con su puerta— no viven acá:
son principios no negociables y están en `.specify/memory/constitution.md`, que leen
`/speckit-plan`, `/speckit-tasks` e `/speckit-implement`. Los comandos concretos de esa puerta son
los de la tabla de *Stack*, y `.github/workflows/ci.yml` corre la misma puerta en cada push y PR.

---

## Code conventions

- La cadena de conexión va en user-secrets, nunca en `appsettings`
- No `any`. Si es inevitable, va con un comentario que explique por qué.
- Comentarios sólo cuando el *por qué* no se desprende del código

---

## What NOT to do in this project

Acá van las cicatrices: las cosas que ya salieron mal una vez.

- No guardar contraseñas en texto plano: deben almacenarse con hash seguro (bcrypt/argon2) (RNF-03).
- No commitear credenciales: ni en `appsettings*.json` ni en los `.sql` de `backend/db/`.

---

## Domain glossary

Los términos propios del producto, para que el agente los use bien en lugar de inventar sinónimos.

- **Gasto:** dinero que salió de mi cuenta.
- **Ingreso:** dinero que ingresó a mi cuenta.

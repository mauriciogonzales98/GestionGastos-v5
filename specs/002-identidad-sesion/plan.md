# Implementation Plan: Identidad y sesión

**Branch**: `002-identidad-sesion` | **Date**: 2026-08-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-identidad-sesion/spec.md`

## Summary

Cada persona pasa a tener su cuenta y su sesión, en lugar de compartir la fila semilla que FEAT-001a
dejó detrás de `IUsuarioActual`.

El enfoque técnico, en una línea: **autenticación por cookie del framework, contraseñas con bcrypt,
y `IUsuarioActual` resolviendo al usuario del `ClaimsPrincipal`**. La interfaz no cambia, así que
los endpoints de movimientos no se tocan: siguen asignando el propietario a mano en el `INSERT` y
acotando la lectura por `usuarioActual.Id`. Esa es la costura que FEAT-001a preparó a propósito.

La autorización se aplica **global**, con dos excepciones explícitas (alta e inicio de sesión), para
que un endpoint nuevo nazca protegido en vez de nacer abierto.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.x) en backend; TypeScript 5.9 + React 19 en frontend

**Primary Dependencies**: EF Core 9.0.18 + Pomelo.MySQL 9.0.0; autenticación por cookie del propio
ASP.NET Core; **`BCrypt.Net-Next` 4.2.0** — única dependencia nueva, justificada en
[research.md D-02](./research.md)

**Storage**: MySQL 8.4.10, schema `gestiongastos`. Una columna nueva en `usuario`; la sesión **no**
es una tabla ([D-01](./research.md))

**Testing**: xUnit en backend, Vitest en frontend. El test de AC-09 corre contra
`gestiongastos_test` como el resto de la suite, moviendo el esquema y restaurándolo dentro de la
colección compartida ([D-07](./research.md), con su revisión al implementar)

**Target Platform**: aplicación web servida desde un solo origen (proxy de Vite en desarrollo)

**Project Type**: web application — `backend/` y `frontend/` separados, como fija `AGENTS.md`

**Performance Goals**: sin objetivos nuevos. El hash de bcrypt con factor 11 tarda ~100 ms **a
propósito**: es el costo que lo hace resistente a fuerza bruta, y es lo que iguala el tiempo de
respuesta de D-04

**Constraints**: sesión expirada a las 24 h **sin actividad** (NFR-02); respuestas de alta y login
indistinguibles entre email registrado y no registrado (NFR-03); contraseñas con bcrypt o argon2,
nunca reversibles (NFR-01)

**Scale/Scope**: 4 endpoints nuevos, 3 que pasan a exigir sesión, 1 pantalla nueva, 1 migración

### Decisión que la spec dejó abierta

**Mínimo de contraseña: 12 caracteres, sin reglas de composición.** La spec lo declaró como
pendiente del plan. Doce y no ocho porque una contraseña de 8 con bcrypt sigue siendo atacable, y
sin exigir mayúsculas ni símbolos porque esas reglas empujan a `Password1!` — más corta, más
predecible y más difícil de recordar que una frase larga. Es lo que recomienda NIST SP 800-63B:
longitud sí, composición no.

## Constitution Check

*GATE: verificado antes de la Phase 0 y otra vez después de la Phase 1.*

### Antes del diseño

| Principio | Cómo lo cumple este plan |
|-----------|--------------------------|
| **I. Test-First** | Cada tarea de código va precedida por su tarea de test, con rojo real mostrado. `/speckit-tasks` lo genera en ese orden |
| **II. Cada AC tiene su test que lo nombra** | Los 12 AC del PRD (AC-01..AC-12) se mapean uno a uno. SC-007 de la spec lo exige explícitamente |
| **III. VERIFY con puerta** | Una tarea `[VERIFY]` al cierre de cada historia, con los comandos de `AGENTS.md` y su salida a la vista |
| **IV. Deterministas y aislados** | El reloj de la sesión sale del `TimeProvider` inyectado ([D-03](./research.md)): AC-12 se verifica adelantando el reloj, no esperando 24 h. El test de migración usa base propia para no depender del estado que dejó otro |
| **V. Las barreras se verifican a sí mismas** | La barrera del contrato se extiende a los tipos nuevos, y `verificar-contrato.sh` sigue probando que sabe fallar. **Se agrega una barrera nueva** — ver abajo |

**Barrera nueva que este ticket obliga a crear.** El Principio V dice que aplica "a cualquier barrera
nueva que se agregue", y acá aparece la más importante del proyecto hasta ahora: *ningún endpoint
responde sin sesión*. Un test que liste los endpoints y compruebe que todos menos dos devuelven
`401` sin credenciales **pasa en verde el día que alguien agregue un endpoint desprotegido**, si el
test enumera a mano. Tiene que descubrir los endpoints del `EndpointDataSource` en tiempo de
ejecución, no de una lista escrita al lado. Esa es la diferencia entre una barrera y una lista de
deseos.

**Resultado: PASA.** Sin violaciones que justificar.

### Después del diseño (Phase 1)

Re-evaluado contra `data-model.md`, `contracts/` y `quickstart.md`:

| Principio | Estado tras el diseño |
|-----------|----------------------|
| I | Sin cambios: el diseño no introduce nada que no se pueda testear primero |
| II | Los 12 AC quedan asignados a un artefacto verificable. AC-09 va contra la migración y no contra la API, como la spec anticipó |
| III | Sin cambios |
| IV | **Revisado con atención**: D-04 exige que el login tarde lo mismo exista o no el email. Un test de *tiempo* sería intermitente y el Principio IV lo prohíbe. Se resuelve verificando **la conducta que produce el tiempo** —que se ejecuta un hash aunque el email no exista— y no midiendo milisegundos |
| V | La barrera de autorización queda definida como tarea propia, con descubrimiento dinámico de endpoints |

**Resultado: PASA.** *Complexity Tracking* queda vacío: no hubo que justificar ninguna desviación.

## Project Structure

### Documentation (this feature)

```text
specs/002-identidad-sesion/
├── plan.md              # Este archivo
├── spec.md              # Qué y por qué
├── research.md          # Phase 0: 10 decisiones con sus alternativas descartadas
├── data-model.md        # Phase 1: la columna nueva y el orden de la migración
├── quickstart.md        # Phase 1: cómo levantarla y validarla a mano
├── contracts/
│   ├── api-http.md      # 4 endpoints nuevos + los 3 que pasan a exigir sesión
│   └── ui-pantalla.md   # La pantalla de autenticación y sus estados
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec (16/16)
└── tasks.md             # Lo genera /speckit-tasks, NO este comando
```

### Source Code (repository root)

```text
backend/GestionGastos.Api/
├── Cuentas/                    # NUEVO: alta de cuenta y su DTO
├── Sesion/                     # NUEVO: iniciar, cerrar y consultar sesión
├── Dominio/
│   └── Usuario.cs              # + ContrasenaHash
├── Persistencia/
│   ├── IUsuarioActual.cs       # sin cambios: la interfaz es la costura
│   ├── UsuarioSemilla.cs       # SE ELIMINA
│   └── UsuarioDeLaSesion.cs    # NUEVO: lee el ClaimsPrincipal
├── Migrations/                 # + la que agrega la columna y borra la semilla
└── Program.cs                  # + autenticación, autorización global y sus dos excepciones

backend/GestionGastos.Api.Tests/
├── Autorizacion/               # NUEVO: la barrera de "ningún endpoint sin sesión"
├── Contrato/                   # + los tipos nuevos
├── Integracion/                # + alta, login, logout, expiración
├── Migraciones/                # NUEVO: AC-09, contra su propia base
└── Unitarios/

frontend/src/
├── api/
│   ├── tipos.ts                # + Credenciales, SesionActual y sus respuestas
│   └── cliente.ts              # + credentials: 'include', y el 401 como señal
├── acceso/                     # NUEVO: la pantalla de autenticación
├── movimientos/                # + cerrar sesión
└── App.tsx                     # NUEVO: decide qué pantalla según haya sesión
```

**Estructura**: web application. Es la que `AGENTS.md` fija y la que FEAT-001a ya usa.

## Complexity Tracking

*Vacío a propósito.* No hubo que justificar ninguna desviación de la constitución: la única
dependencia nueva está justificada en [D-02](./research.md), como `AGENTS.md` exige, y el resto sale
del framework.

## Riesgos anotados

Cosas que pueden salir mal y conviene tener escritas antes de empezar, no después:

1. **La migración borra datos y no los devuelve.** `Down` no restituye ([D-06](./research.md)).
   Quien esté probando pierde lo cargado. Está decidido en el PRD, pero es la clase de sorpresa que
   se agradece anticipada.
2. **Las claves de Data Protection.** Si no persisten, todas las sesiones se caen en cada reinicio
   ([D-01](./research.md)). En desarrollo persisten solas; en un contenedor sin volumen, no.
3. **Un endpoint nuevo que nazca sin proteger.** Es el agujero más fácil de dejar y por eso la
   autorización se aplica global con excepciones explícitas, y la barrera descubre los endpoints en
   tiempo de ejecución.
4. **El fixture de tests tiene una lista blanca de bases** para no arrasar el esquema de desarrollo.
   El test de migración necesita una segunda base: se **extiende** la lista con un nombre más, no se
   abre la restricción.

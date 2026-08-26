# Implementation Plan: Límite de intentos fallidos de inicio de sesión

**Branch**: `003-limite-intentos` | **Date**: 2026-08-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-limite-intentos/spec.md`

## Summary

Probar contraseñas contra un email deja de ser gratis: tras 5 fallos consecutivos, todo intento
sobre ese email —incluido el que traiga la contraseña correcta— se rechaza durante 15 minutos, y la
ventana se levanta sola.

El enfoque técnico, en una línea: **una tabla `intento_de_acceso` con una fila por email presentado,
un incremento atómico por UPSERT, y la ventana derivada de la marca del último fallo**. La
comprobación se engancha dentro de `POST /api/sesion`, extraída a un servicio propio.

Lo que no es obvio y decide el éxito de la feature: **el camino del email bloqueado tiene que costar
lo mismo que el de la contraseña incorrecta**. Salir temprano con un `if` responde en ~2 ms contra
los ~100 ms de bcrypt, y esa diferencia convierte al bloqueo en un oráculo que dice qué emails
acumularon fallos. Por eso el rechazo por bloqueo **igual verifica un hash** y descarta el resultado
([D-04](./research.md)). Es la única parte del diseño que un refactor bienintencionado puede romper
sin poner ningún test funcional en rojo.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.x). El frontend no se toca ([D-09](./research.md))

**Primary Dependencies**: EF Core 9.0.18 + Pomelo.MySQL 9.0.0. **Ninguna dependencia nueva**

**Storage**: MySQL 8.4.10, schema `gestiongastos`. Una tabla nueva, `intento_de_acceso`; ninguna
tabla existente se modifica ([data-model.md](./data-model.md))

**Testing**: xUnit. AC-03, AC-06 y AC-11 se verifican adelantando el `RelojFijo` que `002` ya dejó
inyectado, no esperando 15 minutos (Principio IV). AC-12 y AC-13 miden tiempo de pared y viven en
`Rendimiento/`, fuera del CI ([D-08](./research.md))

**Target Platform**: aplicación web servida desde un solo origen

**Project Type**: web application — `backend/` y `frontend/` separados, como fija `AGENTS.md`

**Performance Goals**: comprobar el límite agrega ≤ 50 ms al login en el p95 (NFR-02), y el rechazo
por bloqueo tarda lo mismo que el rechazo por credenciales incorrectas dentro de 50 ms en el p95
(NFR-03). El presupuesto es holgado: una consulta por clave primaria son microsegundos, y el costo
dominante sigue siendo el hash de bcrypt, que es intencional

**Constraints**: 5 fallos, 15 minutos, contado **por email presentado** y no por IP ni por navegador
(FR-006); el estado sobrevive a un reinicio (FR-007); la respuesta del bloqueo es idéntica a la de
credenciales incorrectas en mensaje, código **y tiempo** (FR-005, FR-009)

**Scale/Scope**: 1 tabla nueva, 1 migración, 1 servicio nuevo, 1 endpoint modificado, 0 endpoints
nuevos, 0 cambios en el contrato, 0 cambios en el frontend

### Las dos decisiones que la spec dejó abiertas

1. **Dónde vive el estado**: tabla propia con el email como clave, y no columnas en `usuario`.
   Una columna en `usuario` sólo puede contar los fallos de emails registrados, y ahí AC-09 —"un
   email no registrado se bloquea igual"— se vuelve imposible: el bloqueo pasaría a ser el
   enumerador de cuentas que RNF-05 quiere evitar. El detalle y las alternativas descartadas están
   en [D-01](./research.md). **El PRD pedía un ADR para esta decisión**; queda registrado en
   `research.md`, que es donde este proyecto viene guardando las decisiones de diseño de una
   feature — los ADR de `docs/adr/` se reservan para decisiones que atraviesan features, como
   ADR-001. Si al implementar se ve que ésta trasciende el ticket, se promueve.
2. **Criterio de purga**: un email sin intentos durante 24 h vuelve a foja cero y su fila se borra,
   en el mismo camino que ya escribe. Es una sola regla en vez de dos —no hay tarea de limpieza
   aparte—, y su precio está dicho: permite 4 intentos por día indefinidamente. Frente a un hash
   bcrypt, eso es ruido. El razonamiento completo, en [D-03](./research.md).

## Constitution Check

*GATE: verificado antes de la Phase 0 y otra vez después de la Phase 1.*

### Antes del diseño

| Principio | Cómo lo cumple este plan |
|-----------|--------------------------|
| **I. Test-First** | Cada tarea de código va precedida por su tarea de test con rojo real mostrado. `/speckit-tasks` lo genera en ese orden |
| **II. Cada AC tiene su test que lo nombra** | Los 13 AC del PRD se mapean uno a uno; SC-007 de la spec lo exige. Los dos AC de tiempo también tienen test, aunque el CI los filtre ([D-08](./research.md)) |
| **III. VERIFY con puerta** | Una tarea `[VERIFY]` al cierre de cada historia, con los comandos de `AGENTS.md` y su salida a la vista |
| **IV. Deterministas y aislados** | Ningún test duerme. La ventana se verifica adelantando el `RelojFijo` de `002`; el "reinicio" de AC-11 es una segunda `WebApplicationFactory` sobre la misma base, con su reloj puesto donde estaba ([D-07](./research.md)) |
| **V. Las barreras se verifican a sí mismas** | No se agrega ninguna barrera nueva — ver abajo. Las tres existentes se corren en la puerta de cierre |

**Sobre el Principio V: por qué esta feature no crea una barrera nueva.** El Principio V aplica a
las verificaciones que protegen al proyecto de una regresión silenciosa en *cualquier* código futuro
—como "ningún endpoint responde sin sesión", que tiene que descubrir los endpoints en tiempo de
ejecución para no pasar en verde el día que alguien agregue uno abierto—. El límite de intentos no
es de esa clase: es comportamiento de un endpoint, cubierto por tests funcionales que fallan si se
rompe. Inventar un `verificar-limite.sh` sería ceremonia sin la propiedad que la justifica.

Sí hay **una** regresión silenciosa posible acá, y está identificada: alguien "optimiza" el `if` de
[D-04](./research.md) y AC-13 se rompe sin que ningún test funcional se entere. Se cubre con el test
hermano determinista que verifica que el camino bloqueado ejecuta la verificación de hash, y con un
comentario en el código que dice por qué ese trabajo desperdiciado es el requisito.

**Resultado: PASA.** Sin violaciones que justificar.

### Después del diseño (Phase 1)

Re-evaluado contra `data-model.md`, `contracts/api-http.md` y `quickstart.md`:

| Principio | Estado tras el diseño |
|-----------|----------------------|
| I | Sin cambios: nada del diseño impide escribir el test primero. El servicio extraído ([D-06](./research.md)) hace testeable la lógica de la ventana sin levantar la aplicación |
| II | Los 13 AC quedan asignados a un artefacto verificable. AC-12 y AC-13 quedan con **dos** tests cada uno: el de tiempo, fuera del CI, y el funcional que verifica la conducta que lo produce |
| III | Sin cambios |
| IV | **Revisado con atención en dos puntos**: el incremento atómico ([D-05](./research.md)) evita la carrera que haría intermitente cualquier test concurrente, y el reloj de la segunda factoría de AC-11 se fija a mano para que la ventana no venza por un salto de reloj en vez de por lo que se prueba |
| V | Confirmado: ninguna barrera nueva. La del contrato no cambia porque el contrato no cambia, y se corre igual en la puerta de cierre |

**Resultado: PASA.** *Complexity Tracking* queda vacío.

## Project Structure

### Documentation (this feature)

```text
specs/003-limite-intentos/
├── plan.md              # Este archivo
├── spec.md              # Qué y por qué
├── research.md          # Phase 0: 9 decisiones con sus alternativas descartadas
├── data-model.md        # Phase 1: la tabla nueva y sus transiciones de estado
├── quickstart.md        # Phase 1: cómo levantarla y validarla a mano
├── contracts/
│   └── api-http.md      # El contrato NO cambia: una causa más para un 401 que ya existía
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec (16/16)
└── tasks.md             # Lo genera /speckit-tasks, NO este comando
```

### Source Code (repository root)

```text
backend/GestionGastos.Api/
├── Dominio/
│   └── IntentoDeAcceso.cs          # NUEVO: la fila del contador
├── Sesion/
│   ├── LimiteDeIntentos.cs         # NUEVO: la regla y sus tres constantes (D-06)
│   └── SesionEndpoints.cs          # + la comprobación, y el hash que igual se ejecuta (D-04)
├── Persistencia/
│   └── GestionGastosDbContext.cs   # + el DbSet y el mapeo de intento_de_acceso
├── Migrations/                     # + la que crea la tabla
└── Program.cs                      # + el registro del servicio

backend/GestionGastos.Api.Tests/
├── Integracion/
│   ├── LimiteDeIntentosTests.cs    # NUEVO: AC-01..AC-07, AC-09, AC-10
│   └── BloqueoSobreviveAlReinicioTests.cs  # NUEVO: AC-11 (D-07)
├── Rendimiento/
│   └── RendimientoLimiteTests.cs   # NUEVO: AC-12 y AC-13, fuera del CI (D-08)
└── Unitarios/
    └── LimiteDeIntentosTests.cs    # NUEVO: la ventana y el reinicio del contador, sin base

frontend/                            # SIN CAMBIOS (D-09)
```

**Estructura**: web application, la que `AGENTS.md` fija y la que las features anteriores ya usan.
Los archivos nuevos del backend caen en carpetas que ya existen; ninguna carpeta nueva.

## Complexity Tracking

*Vacío a propósito.* No hubo que justificar ninguna desviación de la constitución. No se agregan
dependencias, ni endpoints, ni tipos del contrato, ni barreras.

## Riesgos anotados

Cosas que pueden salir mal y conviene tener escritas antes de empezar, no después:

1. **La optimización que rompe AC-13.** El `if` que verifica un hash cuyo resultado se descarta
   parece código muerto. Un refactor lo borra, los tests funcionales siguen verdes y el canal lateral
   de tiempo vuelve a abrirse. Mitigación: el comentario en el código, el test hermano determinista,
   y este párrafo.
2. **La colación de la tabla nueva.** Si `intento_de_acceso.email` no queda con la misma colación
   insensible a mayúsculas que `usuario.email`, el límite se esquiva escribiendo el email con otra
   combinación de mayúsculas y **ningún test lo nota** salvo que se escriba uno que lo busque a
   propósito. Va en el mapeo y va con test.
3. **El bloqueo como denegación de servicio dirigida.** Está en el PRD como riesgo aceptado, y esta
   feature lo implementa a propósito: quien conozca un email deja a esa persona afuera 15 minutos.
   No se mitiga acá; si molesta en uso real, se suma un límite por IP **además** del de email.
4. **Los tests de tiempo en una máquina cargada.** AC-12 y AC-13 pueden dar rojo en local si hay una
   compilación corriendo al lado. Están fuera del CI por eso mismo; en local, un rojo se mira dos
   veces antes de creerle, pero no se ignora.
5. **El reloj del servidor.** La ventana se mide con él, así que un cambio de hora la alarga o la
   acorta. Está acotado —15 minutos— y anotado en el PRD; la misma dependencia ya produjo un warning
   con vencimiento en FEAT-001c.

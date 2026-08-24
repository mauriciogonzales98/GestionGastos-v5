---
name: "revision-pr"
description: "Revisión de code review senior sobre los cambios de un PR o rama: seguridad, supuestos no validados, casos borde, errores silenciosos e incoherencias con la spec. Reporta, no edita. Usar cuando se pide revisar un PR, una rama o una feature terminada antes de mergear."
argument-hint: "Número de PR, rama o ruta al diff (por defecto: el PR abierto de la rama actual)"
user-invocable: true
disable-model-invocation: false
---

# Revisión de PR

Actuá como **code reviewer senior**. Revisá **SOLO los cambios de este PR**.

## 1. Armar el material

- **Diff a revisar.** Con el argumento: número de PR → `gh pr diff <n>`; rama → `git diff main...<rama>`;
  ruta a un `.diff` → ese archivo. Sin argumento: el PR abierto de la rama actual, y si no hay,
  `git diff main...HEAD`. Guardalo en el scratchpad de la sesión.
- **Contexto del producto.** La spec de la feature (`specs/<rama>/spec.md`, con sus criterios de
  aceptación) y el PRD que esa spec referencia. Si no hay spec, `PRD.md`.

Leé las dos cosas antes de empezar a reportar.

## 2. Buscar, en este orden

1. **Seguridad:** inyecciones, validación de entrada, secretos, manejo de datos sensibles.
2. **Supuestos no validados:** ¿qué está asumiendo este código que nadie verificó?
3. **Casos borde:** nulos, listas vacías, timeouts, fallas de red, respuestas inesperadas.
4. **Manejo de errores ausente o silencioso.**
5. **Incoherencias con el contexto del producto** (spec, ACs, PRD).

## 3. Reglas

- **NO** comentes estilo ni formato: eso ya lo cubre el linter.
- **NO** edites ningún archivo. Sólo reportá.
- Por cada hallazgo: `archivo:línea` · severidad (**bloqueante** / **debería** / **nit**) ·
  el escenario **CONCRETO** en que falla · la corrección sugerida.
- Al final, listá aparte **"supuestos que hace este código y no están validados"**.

## 4. Formato de salida

Una tabla por severidad, bloqueantes primero:

| # | archivo:línea | Escenario concreto en que falla | Corrección sugerida |
|---|---------------|---------------------------------|---------------------|

Después de las tablas, la lista de supuestos no validados.

## 5. Corregir — sólo si el usuario lo pide

La revisión termina en el reporte. Si el usuario pide aplicar correcciones:

- **Un commit por hallazgo**, en orden de severidad. Nada de un commit que arregla tres cosas.
- **Cada arreglo verificado contra su propio rojo:** demostrá que el test nuevo falla sin el arreglo.
  Un test que pasa en los dos casos no verifica nada.
- Volvé a listar el estado de los hallazgos después de cada tanda.
- La puerta de cierre de *Stack* en `AGENTS.md` tiene que quedar entera en verde antes de dar por
  cerrada la revisión.

## 6. Cerrar — sólo si el usuario lo pide

Comentario en el PR con: qué se buscó, tabla de hallazgos agrupados por severidad con el commit de
cada uno, cómo se verificó cada arreglo contra su rojo, **lo que NO queda cerrado** y por qué, y el
estado final (conteo de tests antes → después, puerta de cierre). Mergear sólo si el usuario lo pide.

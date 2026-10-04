# Quickstart — verificar la identidad visual y la navegación

Cómo comprobar que la feature funciona, de punta a punta. Los pasos automáticos son la puerta; los
manuales son para mirarla con los ojos, que es lo que una feature visual necesita además.

## Paso 0 · Que el navegador de pruebas arranque (una vez por máquina)

```bash
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend exec playwright install chromium
sudo env "PATH=$PATH" pnpm --dir frontend exec playwright install-deps chromium
```

**Los tres comandos hacen falta y el tercero pide `sudo`.** Medido el 2026-10-01, al implementar: los
dos primeros corren sin privilegios y dejan Chromium descargado en `~/.cache/ms-playwright/`, pero al
lanzarlo falla con

```text
chrome-headless-shell: error while loading shared libraries:
libnspr4.so: cannot open shared object file: No such file or directory
```

`libnspr4` no está instalada en este WSL (`apt-cache policy libnspr4` → `Installed: (none)`), y es
una biblioteca del sistema: no hay forma de resolverlo desde el proyecto. En el CI lo hace
`playwright install --with-deps chromium`, que ya está en `ci.yml`.

**Resultado esperado**: `pnpm --dir frontend test` corre los dos proyectos —`dom` y `navegador`— y el
segundo no falla al lanzar el navegador.

**Y el caso contrario, que es el que importa, ya está comprobado** (2026-10-01): sin las bibliotecas,
`pnpm test` termina en **rojo** —`Test Files 26 passed (27)`, código de salida 1— y no en verde con
el proyecto `navegador` salteado. La barrera del principio V se cumple por construcción, sin
configuración extra. Para volver a verla sin desinstalar nada, apuntar `PLAYWRIGHT_BROWSERS_PATH` a
un directorio vacío.

## Paso 1 · La puerta

```bash
pnpm --dir frontend lint
pnpm --dir frontend format
pnpm --dir frontend typecheck
pnpm --dir frontend test
pnpm --dir frontend build
```

`typecheck` y no `tsc --noEmit`: son **dos** programas de TypeScript desde esta feature
([research D-12](./research.md#d-12--dos-programas-de-typescript-no-uno)).

Todo en verde. El backend no se toca, pero la puerta de cierre de la feature lo corre igual
(`AGENTS.md`).

## Paso 2 · Mirarla en el navegador, claro y oscuro

Levantar la app contra la base de **desarrollo**. El `.bashrc` apunta `ConnectionStrings__Default` a
la de tests, así que hay que pasarla explícita:

```bash
ConnectionStrings__Default="<la cadena de user-secrets>" \
  dotnet run --project backend/GestionGastos.Api --launch-profile http
pnpm --dir frontend dev
```

Abrir `http://127.0.0.1:5173/` y recorrer, primero con el sistema en modo claro y después en oscuro:

| # | Dónde | Qué mirar | Requisito |
|---|-------|-----------|-----------|
| 1 | Acceso | marca, tarjeta centrada, conmutador con una opción elegida, un solo botón principal | US1 |
| 2 | Acceso | cambiar de modo: la tarjeta no salta ni cambia de ancho | `FR-012` |
| 3 | Acceso | email inválido, credenciales malas, alta enviada: tres aspectos distintos | US3 |
| 4 | Acceso | cambiar el modo del sistema con la pantalla abierta y un email escrito: cambia el color, el email sigue ahí | `FR-018` |
| 5 | Con sesión | barra lateral, sección actual marcada, ir de dashboard a categorías sin pasar por movimientos | US5 |
| 6 | Movimientos | formulario: tipo arriba; monto y moneda juntos; categoría y fecha juntas | US6 |
| 7 | Movimientos | resumen: un renglón por moneda, sin desglose; una moneda sin movimientos en una línea | US7 |
| 8 | Dashboard | el desglose sigue ahí | `FR-036` |
| 9 | Categorías y listado | lápiz y tacho en cada fila; al pasar el mouse dicen qué hacen | US8 |
| 10 | Todo | Tab de punta a punta: el foco siempre se ve | `FR-014` |

## Paso 3 · En un teléfono de verdad

Con las herramientas del navegador en 360 px se ve casi todo, pero la zona de gestos y el zoom al
tocar un campo sólo aparecen en un teléfono. Con el teléfono en la misma red:

```bash
pnpm --dir frontend dev --host 0.0.0.0
```

y abrir `http://<IP de la máquina>:5173/`. Mirar: la barra inferior no queda debajo de la zona de
gestos, tocar el campo de monto no agranda la página, y el formulario de movimiento entra con, como
mucho, una desplazada.

Este paso es **manual y opcional**: lo que puede medirse ya lo mide el paso 1. Si no se corre, se dice
en el PR; no se da por hecho.

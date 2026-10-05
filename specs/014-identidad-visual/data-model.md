# Data Model — Identidad visual, navegación y pantallas más compactas

**Esta feature no toca datos persistidos.** No hay migración, no cambia ninguna tabla, y la API
devuelve exactamente lo mismo que hoy. El backend no se modifica.

Lo que sí tiene forma y reglas son las piezas del sistema visual y dos estados de la pantalla.

## Token de diseño

Una variable CSS con nombre y rol, declarada en `frontend/src/estilos/base.css`.

| Campo | Regla |
|-------|-------|
| nombre | `--color-*`, `--texto-*`, `--espacio-*`, `--radio*`, `--alto-tactil` |
| valor por modo | los `--color-*` tienen **exactamente** un valor en `:root` y uno en el bloque `prefers-color-scheme: dark`; los demás tokens, sólo en `:root` |
| rol | uno solo, el que dice su nombre. Un color no se reutiliza fuera de su rol aunque el valor coincida (`--color-barra` vale lo mismo que `--color-acento` y sigue siendo otro token) |

El catálogo completo, con sus valores, está en
[contracts/sistema-visual.md](./contracts/sistema-visual.md).

## Par de contraste

| Campo | Regla |
|-------|-------|
| frente | un token `--color-*` |
| fondo | un token `--color-*` |
| umbral | 4,5 (texto normal) o 3 (texto grande, bordes, foco, marcas) |
| modos | se mide en los dos. Un par que no llega a su umbral en uno de los modos es rojo |

Todo `--color-*` pertenece al menos a un par, como exige hoy `Paleta.test.ts`.

## Sección

| Valor | Pantalla | Rótulo |
|-------|----------|--------|
| `movimientos` | `PantallaMovimientos` | Movimientos |
| `dashboard` | `PantallaDashboard` | Dashboard |
| `categorias` | `PantallaCategorias` | Categorías |

Es el tipo `Vista` que ya existe en `App.tsx`; no cambia. La sección inicial al iniciar sesión sigue
siendo `movimientos` (`FR-028`). Transiciones: desde cualquier sección a cualquier otra, con un toque
(`SC-009`); hoy sólo existen `movimientos ⇄ dashboard` y `movimientos ⇄ categorías`.

## Moneda del resumen: con o sin movimientos

No es un campo nuevo: se deduce de lo que ya manda el servidor.

| Condición | Se muestra |
|-----------|-----------|
| `totalIngresado == 0` y `totalGastado == 0` | una línea: "USD — sin movimientos en el período" |
| cualquier otro caso, incluido balance en cero | el renglón con las tres cifras |

La deducción vale porque `PRD:RF-13` exige montos mayores a cero: con un solo movimiento en esa
moneda, alguno de los dos totales deja de ser cero.

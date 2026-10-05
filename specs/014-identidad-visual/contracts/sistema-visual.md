# Contrato — el sistema visual

Esta feature no expone endpoints nuevos. Su contrato es el que usan las pantallas: los tokens que
pueden nombrar y los componentes compartidos que pueden usar. Una pantalla que declare un color, un
tamaño de letra o un espaciado propio, en lugar de un token, rompe este contrato.

## Tokens de color

| Token | Claro | Oscuro | Sobre qué fondos se mide |
|-------|-------|--------|--------------------------|
| `--color-fondo` | `#f8f7fb` | `#16141b` | — (es fondo) |
| `--color-superficie` | `#ffffff` | `#211e29` | — (es fondo) |
| `--color-texto` | `#1d1a24` | `#ece9f2` | fondo, superficie (4,5) |
| `--color-texto-secundario` | `#5c5668` | `#b1abbd` | fondo, superficie (4,5) |
| `--color-acento` | `#6b46b0` | `#b89cf2` | fondo, superficie (4,5) |
| `--color-sobre-acento` | `#ffffff` | `#1b1328` | acento (4,5) |
| `--color-foco` | `#4a2b86` | `#d6c5fa` | fondo, superficie (3) |
| `--color-error` | `#b3261e` | `#f28b82` | fondo, superficie (4,5) |
| `--color-exito` | `#1d6b40` | `#7fd3a2` | fondo, superficie (4,5) |
| `--color-borde` | `#817a8d` | `#7f788b` | fondo, superficie (3) |
| `--color-barra` | `#6b46b0` | `#b89cf2` | riel (3) |
| `--color-riel` | `#e9e4f2` | `#34303d` | — (es fondo de la barra) |

Mediciones: [research.md, D-02](../research.md#d-02--la-paleta-violeta-sobrio-dos-modos-los-mismos-roles).

**Invariante del foco**: `outline-offset` de al menos 2px en `:focus-visible`. Con esa separación, lo
que tiene al lado el anillo es el fondo o la superficie, que son los pares que se miden. Sin ella, el
anillo quedaría pegado al botón principal y contra el acento no llega a 3:1.

## Tokens de tipografía, espaciado y forma

| Token | Valor |
|-------|-------|
| `--texto-chico` | 0.875rem |
| `--texto` | 1rem |
| `--texto-grande` | 1.25rem |
| `--texto-titulo` | 1.5rem |
| `--texto-marca` | 1.75rem |
| `--espacio-minimo` | 0.25rem |
| `--espacio-chico` | 0.5rem |
| `--espacio` | 0.75rem |
| `--espacio-grande` | 1.5rem |
| `--espacio-enorme` | 2.5rem |
| `--radio` | 0.5rem |
| `--radio-grande` | 0.75rem |
| `--alto-tactil` | 2.75rem (44 px) |
| `--ancho-barra-lateral` | 14rem |

Corte de disposición: **48rem**. Por debajo, barra inferior; desde ahí, barra lateral.

## Botones

| Variante | Cómo se marca | Aspecto |
|----------|---------------|---------|
| principal | `className="c-boton c-boton--principal"` | relleno de acento, texto sobre acento |
| secundaria | ningún modificador (todo `<button>`) | superficie, borde, texto en acento |
| sólo ícono | el componente `BotonIcono` | sin borde, ícono en texto secundario, acento al pasar |

Todo botón mide al menos `--alto-tactil` de alto; los de sólo ícono, también de ancho. Como máximo un
botón principal a la vista por formulario (`FR-003`).

## `BotonIcono`

```ts
interface PropsBotonIcono {
  /** Nombre accesible completo: "Renombrar Supermercado", "Eliminar gasto de $ 1.500 del 3/9". */
  nombre: string;
  /** Rótulo corto visible al pasar el puntero o recibir foco: "Renombrar", "Eliminar". */
  accion: string;
  icono: ComponentType;
  onClick: () => void;
}
```

Reglas (`FR-045`): `aria-label={nombre}`; el ícono y el rótulo corto son `aria-hidden`; el rótulo se
muestra en `:hover` y en `:focus-visible`.

## `MarcoDeLaApp`

```ts
interface PropsMarcoDeLaApp {
  seccion: Vista;                      // la actual: lleva aria-current="page"
  email: string;                       // la 015 lo reemplaza por el nombre (FR-027)
  onIrA: (seccion: Vista) => void;
  onCerrarSesion: () => void;
  children: ReactNode;                 // la pantalla; se renderiza dentro de <main>
}
```

Estructura, en este orden en el DOM: marca · `<nav aria-label="Secciones">` con tres botones ·
bloque de cuenta (email y "Cerrar sesión") · `<main>`. La ubicación visual la decide CSS con el corte
de 48rem ([research.md, D-05](../research.md#d-05--la-navegación-un-solo-nav-reubicado-por-css)).

## `ResumenDelPeriodo`

Se agrega una prop:

```ts
/** false en movimientos (FR-035). Por defecto true: el dashboard no cambia su llamada. */
conDesglose?: boolean;
```

## Clases de disposición nuevas

| Clase | Qué hace |
|-------|----------|
| `l-marco` | la grilla de la app con sesión: barra y contenido |
| `l-par` | dos controles en un renglón, que se apilan si no entran con su mínimo |
| `l-centrado` | centra un bloque en la pantalla (acceso) |

Como hoy, **toda clase que el código nombra tiene su regla** y `ClasesConRegla.test.ts` lo comprueba.

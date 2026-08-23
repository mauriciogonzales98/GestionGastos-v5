# Contrato de la pantalla — Alta de movimientos y listado simple

Una sola pantalla (FR-013). Este documento fija **estructura y estados**, no apariencia: las reglas
de marcado están en el *Contrato de marcado de la UI* del [plan](../plan.md), y las decisiones
visuales son del ticket 6.

---

## Estructura

```text
main
├── h1                                   "Mis movimientos"
├── form  (c-formulario-movimiento)      ← el foco arranca acá
│   ├── grupo: tipo         (radio: gasto | ingreso)
│   ├── grupo: monto        (number)
│   ├── grupo: categoría    (select, opciones según el tipo elegido)
│   ├── grupo: fecha        (date, con el día de hoy puesto)
│   ├── región de error del formulario   (sólo si hay un error sin campo)
│   └── button type=submit               "Registrar"
└── section (c-listado-movimientos)
    └── table | mensaje de vacío
```

Cada **grupo** es el componente único de campo que fija el plan: `label` + control + contenedor de
error, con `aria-describedby` y `aria-invalid`. Ninguna pantalla arma esa tripleta a mano.

El orden del DOM es el orden de tabulación, y es el orden en que se completa el formulario. Eso es
lo que verifica **AC-55**.

---

## El campo `tipo` manda sobre `categoría`

Al cambiar `tipo`, el `select` de categoría se repuebla con las del tipo elegido y **se limpia la
selección anterior**. Dejarla puesta permitiría enviar un gasto con categoría de ingreso, que el
servidor rechaza por FR-011: es mejor que la combinación imposible no sea alcanzable.

`gasto` viene marcado de entrada. Es el caso mayoritario y el que el PRD nombra primero.

---

## Estados

| Estado | Qué se ve | Requerimiento |
|--------|-----------|---------------|
| **Inicial** | Formulario con `tipo = gasto` y la fecha de hoy. Listado con los movimientos del mes | FR-003, FR-007 |
| **Cargando el listado** | Indicador de carga; el formulario ya es usable, no espera al listado | — |
| **Listado vacío** | Mensaje explícito de que no hay movimientos este mes. No es un error | FR-012 |
| **Enviando** | El botón queda deshabilitado hasta que responda el servidor | Evita el doble envío |
| **Error de validación** | Cada mensaje junto a su campo; el formulario conserva lo cargado | FR-004, FR-004b, FR-005, FR-011 |
| **Error al persistir** | Mensaje en la región de error del formulario; conserva lo cargado | *Edge Cases* de la spec |
| **Guardado con éxito** | Formulario vacío, foco en el primer campo, movimiento insertado en el listado | FR-014 |

---

## Qué pasa al guardar con éxito (FR-014)

En este orden:

1. El servidor devuelve el movimiento creado (`201`).
2. Si su fecha cae dentro del mes actual, se inserta en el listado **en su posición** según el
   orden `fecha DESC, id DESC` — no se agrega al final ni se recarga la lista entera.
3. El formulario se vacía y vuelve a su estado inicial: `tipo = gasto`, fecha de hoy.
4. El foco vuelve al primer campo.

**Si la fecha cae fuera del mes actual**, el movimiento se guardó igual pero no aparece en el
listado. La confirmación tiene que decirlo, porque si no la persona cree que se perdió — está en
*Edge Cases* de la spec.

---

## Columnas del listado

| Columna | De dónde sale |
|---------|---------------|
| Fecha | `fecha` |
| Tipo | `tipo`, como texto — no sólo por color: el color solo no es accesible |
| Categoría | `categoriaNombre` |
| Monto | `monto`, con el símbolo de `monedaCodigo` |

Es una `<table>` con `<th scope="col">`, no una grilla de `<div>`: son datos tabulares y la tabla
es lo que los lectores de pantalla saben recorrer.

La columna de moneda propiamente dicha (RF-27) llega en el ticket 4b, cuando haya más de una. Acá
el símbolo acompaña al monto porque el dato ya existe y no cuesta nada.

---

## Lo que esta pantalla NO tiene

- Controles de filtro → FEAT-001b
- Botones de editar y eliminar en cada fila → FEAT-001b
- Totales o resumen → FEAT-001c
- Selector de moneda → ticket 4b
- Campo de nota → ticket 2
- Navegación, menú o segunda pantalla → no hay adónde ir todavía ([research.md D-11](../research.md))

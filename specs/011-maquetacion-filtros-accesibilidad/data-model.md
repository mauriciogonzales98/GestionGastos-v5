# Data Model: Maquetación, filtros del listado y accesibilidad

**Sin migración, sin entidad nueva, sin columna nueva.** Esta feature toca el modelo en un solo
punto y es un campo que ya existe en la base desde la migración `Inicial`: empieza a viajar, nada
más.

## Lo que no cambia

| Entidad | Estado |
|---|---|
| `Movimiento` | Sin cambios. Se le agrega un camino de borrado **en la pantalla**; el `DELETE` del servidor existe desde FEAT-001b |
| `Categoria` | Sin cambios. El acotado del listado consume el catálogo que `GET /api/categorias` ya devuelve |
| `Cuenta`, `Sesion` | Sin cambios |
| `Moneda` | **La entidad no cambia.** `Decimales` es `byte` con valor por omisión 2 y está en la tabla desde `20260823220228_Inicial`. Lo que cambia es su DTO |

## El único cambio: `Decimales` empieza a salir

```text
Moneda (tabla `moneda`)
├── id            short       — sin cambios
├── codigo        char(3)     — sin cambios. Sigue sin CHECK de tres letras (deuda D11-02)
├── nombre        varchar     — sin cambios
├── simbolo       varchar     — sin cambios
├── decimales     tinyint u.  — EXISTE desde FEAT-001a. Empieza a viajar en MonedaDto  ← el cambio
└── es_predeterminada bool    — sin cambios
```

**Por qué no viajaba.** `tipos.ts` lo dice con todas las letras y con su motivo: *"`decimales` NO
viaja, aunque la columna existe: hoy no lo consume nadie. El formato regional del monto es el ticket
6, y un campo que nadie usa es un dato que salió a la red sin que nadie lo decidiera."* Este es el
ticket 6. El consumidor existe: `FR-019`.

**Por qué gana sobre `Intl`.** `Intl.NumberFormat` con `style: 'currency'` deduce la escala del
código ISO. Si esa deducción mandara, agregar al catálogo una moneda cuya escala no coincida con la
que su código tiene asignada produciría montos redondeados a una escala que nadie eligió, en
silencio. `PRD:RF-32` dice que la moneda es un dato; el dato es `decimales` (D-08).

**Rango de valores**: `tinyint unsigned`, así que 0 a 255. En la práctica 0, 2 o 3. No se agrega
validación nueva: la semilla pone 2 y nadie escribe en esta tabla desde la aplicación — el catálogo
se administra como dato, que es toda la promesa de `RF-32`.

## Lo que se lee y no se escribe

Esta feature no escribe nada nuevo en la base. El único camino de escritura que estrena es un
**borrado**, y ya está implementado del lado del servidor:

- `DELETE /api/movimientos/{id}` pasa por `MovimientosConsulta.PropioPorId`, que acota por cuenta, y
  responde el 404 uniforme que no distingue lo ajeno de lo inexistente. La barrera
  `verificar-aislamiento.sh` ya lo cubre; esta feature no lo toca.
- **Es un borrado real, no lógico.** No hay columna de baja en `movimiento` y no se agrega: la
  confirmación previa (`FR-011`) es la mitigación elegida, y es la misma forma que la baja de una
  categoría, que también es un camino de ida.

## El acotado del listado, que tampoco es modelo

`AcotadoDelListado` en `frontend/src/api/cliente.ts` pasa de un campo a cuatro:

```text
AcotadoDelListado
├── monedaId?     number | null   — ya existía (feature 009)
├── categoriaId?  number | null   — NUEVO del lado del cliente; el servidor lo entiende desde FEAT-001b
├── desde?        string | null   — NUEVO del lado del cliente; ídem
└── hasta?        string | null   — NUEVO del lado del cliente; ídem
```

Los cuatro son parámetros de consulta, no estado persistido. **Ninguno es nuevo para la API**: el
tipo estaba escrito con el comentario *"este tipo es donde va a crecer cuando se salde"* la deuda
D9-01, y esto es saldarla.

Las reglas del período —los dos extremos juntos o ninguno, el rango invertido rechazado, el mes en
curso por omisión— **no se replican acá**. Viven en `Dominio/PeriodoPedido.cs` desde FEAT-001c, que
es el único intérprete, y el listado las recibe por el mismo camino que el resumen (D-05).

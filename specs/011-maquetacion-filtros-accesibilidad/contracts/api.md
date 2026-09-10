# Contrato: Maquetación, filtros del listado y accesibilidad

Qué cambia del contrato HTTP y qué no. La fuente de verdad del lado del cliente es
`frontend/src/api/tipos.ts`; los tests de `backend/GestionGastos.Api.Tests/Contrato/` lo leen y lo
comparan contra el JSON que la API emite de verdad, en las dos direcciones
([ADR-001](../../../docs/adr/ADR-001-tests-de-contrato-leen-tipos-del-frontend.md)).

## El único cambio: `GET /api/monedas` suma `decimales`

**Antes**

```json
[
  { "id": 1, "codigo": "ARS", "nombre": "Peso argentino", "simbolo": "$", "esPredeterminada": true }
]
```

**Después**

```json
[
  { "id": 1, "codigo": "ARS", "nombre": "Peso argentino", "simbolo": "$", "esPredeterminada": true, "decimales": 2 }
]
```

`decimales` va **último**, después de `esPredeterminada`, en las dos definiciones: es el orden del
record `MonedaDto` y el de `interface Moneda`. Si los tests de contrato comparan sólo nombres el
orden da igual, pero mantenerlo alineado cuesta nada y evita una discusión sobre cuál de las dos
cosas hacen.

- `decimales`: entero, 0 a 255, cuántos decimales usa esa moneda. Lo consume `formatearMonto`
  (`FR-019`), y **prevalece sobre lo que `Intl` deduzca del código ISO** (D-08).
- Del lado del servidor es `MonedaDto`, en `Monedas/MonedasEndpoints.cs`. Es el **único cambio de
  código de producción del backend** en toda la feature.
- Del lado del cliente es un campo más en `interface Moneda`, y el comentario que hoy explica por
  qué no viajaba se reemplaza por el que explica qué hace.

**Es un campo agregado, así que el contrato es compatible hacia atrás**: un cliente que no lo lea
sigue funcionando. Se agrega igual en las dos pilas a la vez porque la barrera del contrato exige
que las dos definiciones coincidan — y el rojo intermedio, con el campo de un solo lado, es
exactamente lo que hay que ver antes de completarlo.

## Lo que NO cambia, y hay que decirlo

### `GET /api/movimientos` — los cuatro acotados ya existen

```text
GET /api/movimientos?desde=YYYY-MM-DD&hasta=YYYY-MM-DD&categoriaId=N&monedaId=N
```

Los cuatro parámetros están implementados desde FEAT-001b y sólo `monedaId` tenía control en
pantalla. **Esta feature no agrega ni un parámetro**: enchufa los otros dos.

Lo que hay que saber para consumirlos, y que ya está resuelto del lado del servidor:

- **Los dos extremos van juntos o no va ninguno.** Medio rango se rechaza con `400` y un
  `ValidationProblem` bajo la clave `rango`. Esa clave existe, según su propio comentario, *"porque
  el frontend la usa para poner el mensaje al lado del control"*.
- **El rango invertido se rechaza** por la misma vía.
- **Sin período, el servidor decide el mes en curso**, con su propio "hoy". El cliente no manda
  `desde=&hasta=` vacíos: la ausencia es el mensaje (`FR-015` y el `AC-03` de la Historia 4 —el
  control refleja el mes actual sin haberlo elegido— se cumplen sin mandar nada).
- **`categoriaId` y `monedaId` no se validan contra su catálogo**: una que no existe simplemente no
  deja pasar nada. Rechazarlas confirmaría cuáles existen, que es la misma fuga que el 404 uniforme
  cierra.

### `DELETE /api/movimientos/{id}` — existe y nunca tuvo cliente

```text
DELETE /api/movimientos/{id}
→ 204 No Content                 eliminado
→ 404 Problem "No encontrado"    no existe, es de otra cuenta, o ya se había eliminado
→ 401                            sin sesión
```

**El 404 es uno solo para las tres situaciones y eso no es comodidad**: un 403 sobre lo ajeno
confirmaría que ese identificador existe, y como son autoincrementales y contiguos, confirmarlo
permite contar los movimientos de otra cuenta sin ver ninguno.

Para el cliente eso significa que **`FR-013` no puede distinguir "ya no existe" de "nunca fue tuyo",
y no debe intentarlo**: el mensaje es uno solo, y la reacción es la misma — dejar de mostrar la fila.

La función nueva del cliente es `eliminarMovimiento(id): Promise<void>`, con la forma de
`darDeBajaCategoria`: pasa por `pedirSinCuerpo`, que ya sabe tratar un `204` y convertir un `401` en
`ErrorDeSesion`.

### `GET /api/resumen`, `GET /api/categorias`, todo lo demás

Sin cambios. El resumen se recalcula después de un borrado pidiéndolo otra vez, que es lo que la
pantalla ya hace después de cada alta y de cada edición (D-11).

## Qué se pone en rojo, y cuándo

| Momento | Qué falla | Es lo esperado |
|---|---|---|
| `decimales` en `tipos.ts`, todavía no en `MonedaDto` | Los tests de `Contrato/` | Sí. Es el rojo del Principio I para el campo |
| `decimales` en `MonedaDto`, todavía no en `tipos.ts` | Los tests de `Contrato/`, por la otra dirección | Sí. La barrera compara en las dos |
| `formatearMonto` con los decimales del catálogo | `ListadoMovimientos.test.tsx`, `ResumenDelPeriodo.test.tsx`, los fixtures | Sí, y está en el presupuesto de D-12 |

`verificar-contrato.sh` no se toca. Se corre al cierre, como siempre, y tarda ~2,5 min.

# Quickstart: el resumen en pantalla y el dashboard

**Feature**: `010-dashboard-con-graficos` · **Fecha**: 2026-09-05

Cómo comprobar a mano lo que esta feature construye. Un quickstart que nadie ejecutó es
documentación que envejece sin avisar, así que la tarea de cierre es recorrerlo entero y anotar
cualquier línea que no haya salido como dice.

> **Recorrido el 2026-09-05.** Los pasos **1 a 5 y el 12** se ejecutaron contra la API y salieron
> como dice acá; la medición también, y sus números están más abajo. Los pasos **6 a 11 y el 13**
> son de navegador y quedaron **sin ejecutar a mano**: lo que verifican está cubierto por los tests
> del frontend que cada uno cita. Lo que se encontró al recorrerlo está al final.

**Prerrequisitos**: MySQL 8.4.10 en `127.0.0.1:3306` con `gestiongastos` migrado,
`ConnectionStrings__Default` apuntando ahí, el backend con
`dotnet run --project backend/GestionGastos.Api` y el frontend con `pnpm --dir frontend dev`.

**El puerto del backend es el 5125**, que es el que fija `launchSettings.json`.

Las llamadas usan cookie de sesión: entrá primero con
`curl -c /tmp/sesion.txt -X POST localhost:5125/api/sesion -H 'content-type: application/json' -d '{"email":"...","contrasena":"..."}'`
y pasá `-b /tmp/sesion.txt` en todo lo que sigue.

---

## 1 · El resumen del mes en curso responde, y el servidor decide el mes

```bash
curl -b /tmp/sesion.txt -s localhost:5125/api/resumen | jq
```

**Esperado**: `desde` es el día 1 del mes en curso y `hasta` el último; `monedas` trae **una entrada
por cada fila de la tabla `moneda`**, tenga o no movimientos. Es lo que `FR-011` pinta en la pantalla
principal, y `desde`/`hasta` es lo que le permite titular el período sin calcularlo por su cuenta.

## 2 · Una moneda sin movimientos aparece igual, en cero

En la respuesta del paso 1, buscá una moneda del catálogo en la que no hayas registrado nada.

**Esperado**: aparece con `totalIngresado`, `totalGastado` y `balance` en `0`, y
`gastosPorCategoria: []`. **Sin error de ningún tipo.** Es `FR-009` y el `AC-31` de la feature 006:
la pantalla no tiene que inventar los ceros porque ya vienen.

## 3 · El período se acota, con los extremos incluidos

```bash
curl -b /tmp/sesion.txt -s 'localhost:5125/api/resumen?desde=2026-08-01&hasta=2026-08-31' | jq '.desde, .hasta'
```

**Esperado**: eco exacto del rango pedido, y totales calculados sólo con ese mes. Registrá un
movimiento fechado **exactamente** el `desde` y otro el `hasta` y comprobá que los dos suman: los
extremos van incluidos (`FR-004`).

## 4 · Los dos rechazos del período, con su clave

```bash
curl -b /tmp/sesion.txt -s -o /dev/null -w '%{http_code}\n' 'localhost:5125/api/resumen?desde=2026-08-01'
curl -b /tmp/sesion.txt -s 'localhost:5125/api/resumen?desde=2026-09-30&hasta=2026-09-01' | jq '.errors'
```

**Esperado**: `400` en los dos, y el segundo con la clave **`rango`** y el texto *"La fecha de inicio
no puede ser posterior a la de fin."*. Esa clave es la que la pantalla usa para poner el mensaje al
lado del control (`FR-005`), y por eso hay que verla con los ojos y no suponerla.

## 5 · El resumen no hereda el acotado por moneda

```bash
curl -b /tmp/sesion.txt -s 'localhost:5125/api/resumen?desde=2026-09-01&hasta=2026-09-30' | jq '.monedas | length'
curl -b /tmp/sesion.txt -s 'localhost:5125/api/movimientos?monedaId=2' | jq 'length'
```

**Esperado**: el resumen sigue trayendo **todas** las monedas del catálogo aunque el listado se
acote. Es la garantía D-05 de la feature 009, y es la razón por la que el filtro de moneda del
dashboard es de presentación (D-05 de esta feature).

## 6 · La pantalla principal muestra el resumen arriba

En el navegador, con sesión abierta.

**Esperado**: de arriba abajo — el resumen del mes en curso, el formulario de registro, el listado.
Por cada moneda: lo ingresado, lo gastado, el balance y el desglose por categoría. **No hay ningún
control de período en esta pantalla**: su resumen es siempre el del mes en curso (`FR-011b`).

## 7 · Registrar un movimiento mueve el resumen, sin recargar

Registrá un gasto y mirá el resumen sin tocar el navegador.

**Esperado**: el total gastado y el desglose de esa moneda incorporan el movimiento recién
registrado. Es el escenario 2 de la historia 1.

## 8 · El dashboard, con su gráfico y su texto

Andá al dashboard desde la pantalla principal.

**Esperado**: por cada moneda, una fila por categoría con **su nombre, su total y una barra**
proporcional. Comprobá las dos mitades de la decisión D-03: que el nombre y el total se leen como
texto, y que la barra más larga es la de la categoría con el total más alto.

**Comprobá también que la barra no lleva información que el texto no tenga**: es decorativa, y todas
las barras son del mismo color a propósito (D-04). Ninguna categoría se distingue únicamente por su
color porque **ninguna se distingue por su color**.

## 9 · El rango del dashboard, y la principal quieta

Anotá los números del resumen de la pantalla principal. Andá al dashboard, elegí el mes anterior,
volvé.

**Esperado**: en el dashboard, los totales del mes anterior. En la principal, **exactamente los
mismos números que anotaste**. Es `FR-012` y `PRD:AC-08`, y es el requisito de esta feature cuya
violación sería invisible en la pantalla donde se produce.

## 10 · Un rango inválido se dice, y no borra lo que estaba

En el dashboard, poné una fecha de inicio posterior a la de fin.

**Esperado**: el mensaje del servidor junto al control, y **los totales que estaban a la vista siguen
ahí**. Un vacío se leería como *"no hay nada"* y escondería que la pregunta estaba mal formada.

## 11 · El filtro de moneda recorta la vista, no el cálculo

Con movimientos en dos monedas, elegí una en el dashboard.

**Esperado**: se ve sólo esa moneda, con **los mismos números** que tenía cuando se veían las dos
(`FR-006`). Y no se dispara ninguna petición: mirá la pestaña de red del navegador — cambiar de
moneda no pide nada, porque los datos ya llegaron (D-05).

Volvé a la principal: su resumen sigue mostrando **todas** las monedas. El filtro es del dashboard.

## 12 · Una moneda agregada como dato aparece en el selector

```sql
INSERT INTO moneda (codigo, nombre, simbolo, decimales, es_predeterminada)
VALUES ('BRL', 'Real', 'R$', 2, 0);
```

**Esperado**: recargá el dashboard y la moneda está en el selector, sin haber tocado una línea de
código ni recompilado nada. Es `FR-007`, y `verificar-monedas.sh` lo protege desde la 009 **en las
dos pilas** — así que si alguien escribiera la lista a mano, la barrera se pondría en rojo sola.

Borrala después: `DELETE FROM moneda WHERE codigo = 'BRL';`

## 13 · Un servidor caído se dice como fallo, no como ceros

Bajá el backend y recargá el dashboard.

**Esperado**: dice que no se pudo cargar. **No muestra ceros.** Ceros y "no se pudo cargar" son la
misma pantalla diciendo dos cosas opuestas, y confundirlas haría que alguien creyera que no gastó
nada (`FR-010`).

Volvé a levantarlo y recargá: el cartel de error **se va**. Es la cicatriz `10a2e6d` de la feature
009 — un cartel de fallo que sobrevive a una carga que salió bien miente.

---

## La medición, que sale de la puerta local

`RendimientoResumenTests` está excluida del CI por medir tiempo de pared, así que los números de
`PRD:AC-11` y `PRD:AC-12` salen de correrla en local:

```bash
dotnet test backend/ --filter "FullyQualifiedName~RendimientoResumen"
```

**Esperado**: verde en los dos escalones —1000 movimientos bajo 2 s y 10000 bajo 4 s, p95 sobre 100
ejecuciones— más el caso repartido en dos monedas.

**Medido el 2026-09-05**, en la máquina de desarrollo:

| Caso | p95 | Mediana | Máximo | Techo | Margen |
|---|---|---|---|---|---|
| 1000 movimientos, 1 moneda | **6 ms** | 5 ms | 8 ms | 2000 ms | ×333 |
| 1000 movimientos, 2 monedas | **10 ms** | 8 ms | 22 ms | 2000 ms | ×200 |
| 10000 movimientos, 1 moneda | **33 ms** | 30 ms | 43 ms | 4000 ms | ×121 |

Tres cosas que estos números dicen y conviene no perder:

1. **`PRD:AC-12` no era el riesgo que el PRD creía.** Ese PRD pide atacar temprano el volumen de
   10000 porque *"nunca se midió"*; se venía midiendo desde la feature 006 y da 33 ms contra un techo
   de 4 s. Hay dos órdenes de magnitud de margen.
2. **El costo crece menos que las filas.** Diez veces más movimientos cuestan cinco veces y media
   más tiempo: el índice `(usuario_id, fecha DESC, id DESC)` acota bien y el `GROUP BY` opera sobre
   un conjunto ya recortado.
3. **La segunda moneda sigue costando alrededor de un 60 %** (6 → 10 ms), que es lo mismo que la
   feature 008 anotó (6 → 9 ms). El índice por `categoria_id` de la deuda **D6-05** sigue sin
   justificarse, y ahora con el número de 10000 en la mano.

> Los tres números salen del propio test desde esta feature: hasta ahora el p95 vivía únicamente en
> el mensaje del `Assert`, o sea que sólo se podía leer haciendo fallar la medición — justo el caso
> en que el número no importa.

---

## Las barreras, al cerrar la feature

Ninguna es nueva y ninguna hay que tocarla (D-13), pero las seis se corren:

```bash
./backend/verificar-contrato.sh        # ~2,5 min · el contrato no cambia, se corre igual
./backend/verificar-autorizacion.sh    # ningún endpoint nuevo, pero es puerta de cierre
./backend/verificar-desglose.sh        # protege FR-015: el desglose no filtra por categoria.activa
./backend/verificar-linter.sh
./backend/verificar-monedas.sh         # ~1 min · protege FR-007 en las dos pilas, sin tocarla
./backend/verificar-aislamiento.sh     # ~7 min
```

Y la puerta completa de las dos pilas, con cobertura, como fija la constitución.

---

## Lo que se encontró al recorrerlo

**Recorrido el 2026-09-05**, contra `gestiongastos_test` con el backend levantado en el 5125.

**Los pasos 1 a 5 salieron exactamente como están escritos.** Vale la pena anotar los tres que más
se pueden romper sin avisar:

- **Paso 3, los extremos incluidos**: dos gastos de 7 y 11 en el primer y el último día del mes, y
  el rango `desde=<día 1>&hasta=<último>` devolvió **18** en Transporte. Los dos bordes adentro.
- **Paso 4, los dos rechazos**: medio rango dio `400`, y el rango invertido devolvió
  `{'rango': ['La fecha de inicio no puede ser posterior a la de fin.']}` — la clave `rango` con el
  texto exacto que la pantalla muestra al lado del control.
- **Paso 5**: con el listado acotado a `monedaId=2`, el resumen siguió devolviendo **las dos**
  monedas del catálogo. La garantía de la 009, viva.

**El paso 12 es el que más dice, y salió mejor de lo que el quickstart pedía.** Un
`INSERT INTO moneda` de `BRL` con SQL puro y, sin recompilar ni reiniciar nada:

- `GET /api/monedas` devolvió `['ARS', 'USD', 'BRL']`;
- y **`GET /api/resumen` la informó en cero**, `('BRL', 0)`, junto a las otras dos.

Esa segunda mitad no estaba escrita en el paso y es la que cierra el círculo entre `FR-007` y
`FR-009`: la moneda nueva no sólo aparece en el selector, aparece en el resumen con la forma
completa que la pantalla sabe pintar. Se borró después.

**Dos cosas que no se pudieron ejecutar**, y por qué:

1. **Los pasos de navegador (6 a 11 y 13).** No hay navegador en este entorno. Cada uno cita los
   tests que cubren lo mismo —el orden del DOM en `PantallaMovimientos.test.tsx`, la proporción de
   la barra en `GastosPorCategoria.test.tsx`, la carrera y el rango en `PantallaDashboard.test.tsx`,
   y la no-contaminación entre pantallas en `App.test.tsx`—, así que lo que queda sin comprobar no
   es el comportamiento sino **cómo se ve**.
2. **La contraseña del paso de alta de cuenta**: el quickstart no dice cuál usar y la primera que se
   probó fue rechazada con *"La contraseña tiene que tener al menos 12 caracteres."*. No es un
   hallazgo del dashboard —es `RNF-03` haciendo su trabajo— pero cuesta un intento a quien recorra
   esto por primera vez. Queda dicho acá.

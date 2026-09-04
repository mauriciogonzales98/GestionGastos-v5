# Quickstart: elegir, ver, acotar y corregir la moneda

**Feature**: `009-elegir-y-filtrar-moneda` · **Fecha**: 2026-09-04

Cómo comprobar a mano lo que esta feature construye. Son ocho pasos: los cinco primeros contra la
API, los tres últimos en la pantalla. Un quickstart que nadie ejecutó es documentación que envejece
sin avisar, así que la tarea de cierre es recorrerlo entero y anotar cualquier línea que no haya
salido como dice.

> **Recorrido el 2026-09-04.** Los pasos 1 a 5 y la mitad de API del 8 se ejecutaron y salieron
> como dice acá; lo que no coincidió está anotado al final, en *Lo que se encontró al recorrerlo*.
> Los pasos 6, 7 y la parte de pantalla del 8 son de navegador y quedaron **sin ejecutar a mano**:
> lo que verifican está cubierto por los tests del frontend que cada uno cita.

**Prerrequisitos**: MySQL 8.4.10 en `127.0.0.1:3306` con `gestiongastos` migrado,
`ConnectionStrings__Default` apuntando ahí, el backend levantado con
`dotnet run --project backend/GestionGastos.Api`, y el frontend con `pnpm --dir frontend dev`.

**El puerto es el 5125**, que es el que `launchSettings.json` fija: `ASPNETCORE_URLS` no lo pisa
cuando se arranca con `dotnet run`.

Las llamadas usan una cookie de sesión: entrá primero con
`curl -c /tmp/sesion.txt -X POST localhost:5125/api/sesion -H 'content-type: application/json' -d '{"email":"...","contrasena":"..."}'`
y pasá `-b /tmp/sesion.txt` en todo lo que sigue.

---

## 1 · El catálogo existe y se puede pedir

```bash
curl -b /tmp/sesion.txt -s localhost:5125/api/monedas | jq
```

**Esperado**: una entrada por fila de `moneda`, con `codigo`, `nombre`, `simbolo` y exactamente una
con `esPredeterminada: true`. Es FR-004 y FR-006.

**Comprobá también que sin sesión no se puede**: la misma llamada sin `-b` tiene que dar `401`. Un
catálogo no es un secreto, pero todo endpoint del proyecto exige sesión y
`verificar-autorizacion.sh` se pone en rojo si alguno nace abierto.

---

## 2 · Registrar sin elegir moneda sigue funcionando igual

```bash
curl -b /tmp/sesion.txt -s -X POST localhost:5125/api/movimientos \
  -H 'content-type: application/json' \
  -d '{"tipo":"gasto","monto":100,"categoriaId":1}' | jq '.monedaCodigo'
```

**Esperado**: `"ARS"`, o el código que el catálogo tenga como predeterminada. **Este paso es el más
importante de todos y es el que no cambió nada**: es `PRD:NFR-01` —cero interacciones adicionales— y
la compatibilidad hacia atrás del contrato. Si acá hace falta mandar `monedaId`, la feature rompió
a todo cliente que ya existía.

---

## 3 · Registrar eligiendo otra moneda

```bash
curl -b /tmp/sesion.txt -s -X POST localhost:5125/api/movimientos \
  -H 'content-type: application/json' \
  -d '{"tipo":"gasto","monto":100,"categoriaId":1,"monedaId":2}' | jq '.monedaCodigo'
```

**Esperado**: `"USD"`. Es FR-001. Y en `GET /api/resumen` ahora tienen que verse los dos montos de
100 **en entradas distintas**, sin sumarse entre sí: es lo que la 008 dejó verificado y lo que este
paso empieza a ejercitar con datos de verdad en las dos monedas.

## 4 · Una moneda que no existe se rechaza

```bash
curl -b /tmp/sesion.txt -s -o /dev/stderr -w '%{http_code}\n' \
  -X POST localhost:5125/api/movimientos -H 'content-type: application/json' \
  -d '{"tipo":"gasto","monto":100,"categoriaId":1,"monedaId":9999}'
```

**Esperado**: `400`, con `errors.monedaId` explicando el motivo, y **ningún movimiento creado** —
comprobalo con `GET /api/movimientos`. Es FR-003, `PRD:AC-11`, y **la deuda D8-01 de la feature 008
saldándose**: es la primera vez que hay una entrada de moneda que validar.

## 5 · Acotar el listado, y combinarlo

```bash
curl -b /tmp/sesion.txt -s "localhost:5125/api/movimientos?monedaId=2"            | jq 'map(.monedaCodigo) | unique'
curl -b /tmp/sesion.txt -s "localhost:5125/api/movimientos"                        | jq 'map(.monedaCodigo) | unique'
curl -b /tmp/sesion.txt -s "localhost:5125/api/movimientos?monedaId=2&categoriaId=1&desde=2026-09-01&hasta=2026-09-30" | jq 'length'
curl -b /tmp/sesion.txt -s "localhost:5125/api/movimientos?monedaId=9999"          | jq 'length'
```

**Esperado**, en orden: `["USD"]` · las dos monedas · sólo los que cumplen las tres condiciones ·
`0` **sin error** (FR-015). Es FR-008 y FR-009.

---

## 6 · El selector, en la pantalla

Abrí el formulario. **Esperado**: un selector de moneda con exactamente las monedas del catálogo,
con la predeterminada ya elegida. Registrá un movimiento **sin tocarlo**: tiene que guardarse igual
que siempre, sin un paso de más (`PRD:NFR-01`).

## 7 · Cada fila dice su moneda

Con un gasto de 100 en pesos y otro de 100 en dólares, el listado tiene que mostrar **el código** de
cada uno —`ARS` y `USD`— además del monto formateado con su símbolo. Es FR-007, y el código va
explícito a propósito: el símbolo lo elige `Intl` según el locale y no es garantía de que dos
monedas se distingan (ver *Clarifications* en la spec).

Y probá el acotado por moneda desde la pantalla: sólo ese control, que es lo que esta feature
construye. Los de categoría y fecha siguen sin interfaz — es la deuda **D9-01**.

## 8 · Corregir la moneda sin perder nada

Abrí la ventana de edición de un movimiento en pesos. **Esperado**: se abre encima del listado con
el monto, la categoría y la fecha ya cargados; `Escape` la cierra; el foco no se escapa al fondo.
Cambiale **sólo la moneda** a dólares y guardá.

**Esperado**: el movimiento conserva su monto, su categoría y su fecha (FR-012), y el listado lo
muestra en la moneda nueva.

**Y ahora el paso que no se ve en la pantalla**, porque la vista de totales es del ticket 5 (D9-06):

```bash
curl -b /tmp/sesion.txt -s localhost:5125/api/resumen | jq '.monedas[] | {monedaCodigo, totalGastado}'
```

**Esperado**: los 100 **dejaron** de sumar en el total de pesos y **pasaron** a sumar en el de
dólares. Las dos direcciones, no sólo el destino — que es lo que FR-012b pide y lo que un test que
mire sólo el destino dejaría pasar.

---

## Medición

```bash
dotnet test backend/GestionGastos.slnx --filter "FullyQualifiedName~RendimientoAlta"
```

**Esperado**: los dos casos en verde, el de siempre y el nuevo con la moneda elegida, los dos con
p95 < 1 s sobre 100 ejecuciones. **Anotá los dos números.** El caso viejo queda intacto justamente
para eso: si el p95 empeora, tener la referencia al lado es lo que permite atribuirlo al `SELECT` de
la moneda y no a que la máquina estaba ocupada ([research.md D-09](./research.md)).

Corre en local y no en CI: mide tiempo de pared, y el CI lo excluye con
`--filter "FullyQualifiedName!~Rendimiento"`.

---

## Las seis barreras

```bash
./backend/verificar-contrato.sh      # el contrato cambió en cuatro lugares: es la que más trabaja
./backend/verificar-autorizacion.sh  # GET /api/monedas es un endpoint nuevo
./backend/verificar-desglose.sh
./backend/verificar-monedas.sh       # ahora también vigila que frontend/src/ quede limpio (D-11)
./backend/verificar-aislamiento.sh   # ~7 min
./backend/verificar-linter.sh
```

---

## Lo que se encontró al recorrerlo

Un quickstart que nadie ejecutó es documentación que envejece sin avisar. Esto es lo que el
recorrido del 2026-09-04 encontró:

1. **El puerto no era el 5000.** Estaba escrito de memoria; la API escucha en **5125**, que es lo
   que fija `launchSettings.json`, y `ASPNETCORE_URLS` no lo pisa con `dotnet run`. Corregido
   arriba. Es el tipo de línea que sólo se descubre ejecutando: leyendo el documento es correcta.
2. **El nombre del dólar en el catálogo es "Dólar estadounidense", no "Dólar".**
   [`contracts/api.md`](./contracts/api.md) lo abreviaba en su ejemplo. No afecta a nada —ningún
   test compara ese nombre, justamente por D-10— pero un ejemplo que no coincide con el dato real
   es un ejemplo que confunde a quien lo copia.
3. **Los pasos 6, 7 y la parte de pantalla del 8 no se ejecutaron**, porque piden un navegador. Lo
   que verifican está cubierto por tests automatizados: el selector con la predeterminada y la
   moneda inesperada por `FormularioMovimiento.test.tsx`, el código en cada fila por
   `ListadoMovimientos.test.tsx`, y la ventana emergente por `VentanaDeEdicion.test.tsx`. **La
   única afirmación del paso 8 que ningún test cubre es que `Escape` cierre la ventana**: es
   conducta del navegador y happy-dom no la simula (D9-07). Esa línea sigue necesitando a alguien
   con un teclado.

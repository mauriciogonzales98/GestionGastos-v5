# Quickstart — Resumen del mes con desglose por categoría

Cómo comprobar a mano que la feature hace lo que dice. No reemplaza a los tests: sirve para ver el
comportamiento con los ojos.

Y acá eso importa más que en las features anteriores. **Un total contaminado se ve idéntico a uno
correcto**: no hay fila de más que saltar a la vista, sólo un número más grande. Por eso todos los
pasos comparan contra un valor calculado a mano, y por eso el paso 3 es el que no se puede saltear.

**Los pasos 3, 4 y 6 son los que importan.** Los dos primeros son andamio.

---

## Prerrequisitos

- MySQL 8.4.10 en `localhost:3306`, con el esquema `gestiongastos` migrado.
- `ConnectionStrings__Default` apuntando al esquema de **desarrollo**, no al de tests.
- La API corriendo: `dotnet run --project backend/GestionGastos.Api`.
- `curl` y `jq`.

```bash
API=http://localhost:5xxx   # el puerto que imprime dotnet run
```

> **No uses `date +%F` para armar fechas.** Ésa es la fecha de tu máquina, y el resumen sin
> parámetros recorta al mes en curso **del servidor**, que puede estar en otro día — en Argentina
> (UTC−3) difieren todas las noches a partir de las 21:00. El paso 2 saca el "hoy" del servidor, que
> es el único que manda acá. Es la misma trampa que documentó el quickstart de la feature 005, y en
> ésta es peor: un movimiento que cae afuera del mes no desaparece de una lista, sino que hace que
> un total no cierre, y eso se lee como un error de cálculo.

---

## 1. Dos cuentas, dos frascos de cookies

El aislamiento sólo se puede ver con dos.

```bash
A=/tmp/cuenta-a.txt; B=/tmp/cuenta-b.txt; rm -f $A $B

for par in "ana@ejemplo.com:$A" "beto@ejemplo.com:$B"; do
  email=${par%%:*}; frasco=${par##*:}
  curl -s -X POST $API/api/cuentas -H 'Content-Type: application/json' \
    -d "{\"email\":\"$email\",\"contrasena\":\"unaContrasenaLarga1\"}" > /dev/null
  curl -s -c $frasco -X POST $API/api/sesion -H 'Content-Type: application/json' \
    -d "{\"email\":\"$email\",\"contrasena\":\"unaContrasenaLarga1\"}" > /dev/null
done
```

## 2. Números que se puedan verificar de cabeza

Montos redondos y distintos entre sí, para que un total mal armado no dé por casualidad lo mismo que
uno bien armado.

```bash
G1=$(curl -s -b $A $API/api/categorias | jq '[.[] | select(.tipo=="gasto")][0].id')
G2=$(curl -s -b $A $API/api/categorias | jq '[.[] | select(.tipo=="gasto")][1].id')
ING=$(curl -s -b $A $API/api/categorias | jq '[.[] | select(.tipo=="ingreso")][0].id')

cargar() { # cuenta tipo monto categoria
  curl -s -b $1 -X POST $API/api/movimientos -H 'Content-Type: application/json' \
    -d "{\"tipo\":\"$2\",\"monto\":$3,\"categoriaId\":$4}"
}

# El "hoy" del servidor sale del alta sin fecha, que es la única forma de averiguarlo.
HOY=$(cargar $A gasto 1000 $G1 | jq -r .fecha)
cargar $A gasto   2000 $G1 > /dev/null
cargar $A gasto    500 $G2 > /dev/null
cargar $A ingreso 8000 $ING > /dev/null

# Beto carga MUCHO más que Ana, para que su contaminación sea imposible de no ver.
cargar $B gasto 999999 $G1 > /dev/null
```

**Lo que Ana tiene que ver**: gastado `3500` (1000 + 2000 + 500), ingresado `8000`, balance `4500`.
En el desglose, `3000` en la primera categoría y `500` en la segunda.

## 3. Los totales son los de Ana y sólo los de Ana

```bash
curl -s -b $A $API/api/resumen | jq '.monedas[0] | {totalIngresado, totalGastado, balance}'
```

**Esperado**: `8000`, `3500`, `4500`.

**Lo que hay que mirar de verdad**: que `totalGastado` **no** sea `1003499`. Los `999999` de Beto no
aparecen como una fila ajena — aparecen adentro de un número que, sin este cálculo hecho a mano, se
vería perfectamente razonable. Ése es el AC-02 que la feature 004 dejó pendiente.

## 4. El desglose suma exactamente el total

```bash
curl -s -b $A $API/api/resumen | jq '.monedas[0] |
  { total: .totalGastado, suma: ([.gastosPorCategoria[].total] | add) }'
```

**Esperado**: los dos campos iguales, en `3500`.

Si difieren, el desglose y el total dejaron de salir de la misma consulta
([D-04](./research.md#d-04--una-sola-consulta-agregada-y-la-composición-en-memoria)) — que es
justamente lo que ese diseño tiene que hacer imposible.

También: ninguna categoría de **ingreso** puede aparecer acá (INV-07).

```bash
curl -s -b $A $API/api/resumen \
  | jq --argjson ing "$ING" '[.monedas[].gastosPorCategoria[].categoriaId] | index($ing)'
```

**Esperado**: `null` — la categoría de ingreso no está en ningún desglose, de ninguna moneda.

## 5. El resumen y el listado hablan del mismo conjunto

```bash
curl -s -b $A "$API/api/movimientos?desde=$HOY&hasta=$HOY" \
  | jq '[.[] | select(.tipo=="gasto") | .monto] | add'
curl -s -b $A "$API/api/resumen?desde=$HOY&hasta=$HOY" | jq '.monedas[0].totalGastado'
```

**Esperado**: el mismo número las dos veces (INV-03). Si el resumen y el listado difieren, uno de
los dos miente y quien mira la pantalla no tiene cómo saber cuál.

## 6. El período vacío devuelve ceros, no una respuesta vacía

```bash
curl -s -b $A "$API/api/resumen?desde=1999-01-01&hasta=1999-01-31" \
  | jq '{ monedas: (.monedas | length), primera: .monedas[0] }'
```

**Esperado**: `monedas` con tantas entradas como filas tenga el catálogo —**no cero**—, y la primera
con `totalIngresado`, `totalGastado` y `balance` en `0` y `gastosPorCategoria` en `[]`.

Es AC-31, y es el paso que separa las dos respuestas posibles a la pregunta que se cerró antes de
planificar: el backend trae los ceros, el cliente no los inventa.

## 7. El período que decidió el servidor viaja en la respuesta

```bash
curl -s -b $A $API/api/resumen | jq '{desde, hasta}'
```

**Esperado**: el primero y el último día del mes en curso **del servidor**, aunque no hayas mandado
nada. Comparalo con `$HOY`: tienen que ser el mismo mes.

## 8. Los rangos mal formados se rechazan

```bash
curl -s -o /dev/null -w '%{http_code}\n' -b $A "$API/api/resumen?desde=2026-08-31&hasta=2026-08-01"
curl -s -o /dev/null -w '%{http_code}\n' -b $A "$API/api/resumen?desde=2026-08-01"
curl -s -o /dev/null -w '%{http_code}\n'    "$API/api/resumen"
```

**Esperado**: `400`, `400`, `401`. El último es el que confirma que el resumen no es una filtración
de datos agregados a un anónimo.

---

## Limpieza

```bash
rm -f $A $B
```

Las cuentas y los movimientos quedan en el esquema de desarrollo. Si molestan, se borran a mano: los
tests no los tocan, corren contra `gestiongastos_test`.

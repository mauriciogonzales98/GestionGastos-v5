# Quickstart — Categorías propias del usuario

Cómo comprobar a mano que la feature hace lo que dice. Ocho pasos, en orden, sobre una base de
desarrollo. La verificación automática vive en la suite; esto es para verlo con los ojos.

> Las líneas usan `jq`. Si no está instalado, `python3 -m json.tool` sirve para mirar la respuesta,
> aunque no para las líneas que extraen un campo.

## Antes de empezar

```bash
# La API, contra la base de DESARROLLO (no la de tests: se limpia sola)
dotnet run --project backend/GestionGastos.Api

# En otra terminal, el frontend
pnpm --dir frontend dev
```

Registrá dos cuentas desde la aplicación —`una@ejemplo.local` y `otra@ejemplo.local`— y guardá la
cookie de sesión de cada una. Los pasos 1 a 6 son con la primera.

```bash
API=http://localhost:5000
UNA=/tmp/una.cookies
OTRA=/tmp/otra.cookies
```

---

## 1 · El catálogo arranca con las diez predefinidas y ninguna propia

```bash
curl -s -b $UNA $API/api/categorias | jq '[.[] | select(.esPropia)] | length'   # 0
curl -s -b $UNA $API/api/categorias | jq 'length'                              # 10
```

Las diez tienen `esPropia: false`. Es AC-02 y es el punto de partida de todo lo demás.

## 2 · Crear una propia, y que aparezca

```bash
curl -s -b $UNA -X POST $API/api/categorias \
  -H 'Content-Type: application/json' \
  -d '{"nombre":"Gimnasio","tipo":"gasto"}' | jq
```

`201`, con `esPropia: true`. Anotá el `id`; en los pasos siguientes es `$GIM`.

```bash
curl -s -b $UNA $API/api/categorias | jq '.[] | select(.nombre=="Gimnasio")'
```

Ahí está, junto a las predefinidas. **En la aplicación**: entrá a la pantalla de gestión, creala
desde ahí, volvé a la principal y comprobá que el selector del formulario ya la ofrece **sin
recargar**. Eso es AC-13 y FR-019, y es lo que más fácil se rompe.

## 3 · El nombre repetido se rechaza, contra propias y contra predefinidas

```bash
curl -s -b $UNA -X POST $API/api/categorias -H 'Content-Type: application/json' \
  -d '{"nombre":"Gimnasio","tipo":"gasto"}' | jq '.errors.nombre'

curl -s -b $UNA -X POST $API/api/categorias -H 'Content-Type: application/json' \
  -d '{"nombre":"  supermercado  ","tipo":"gasto"}' | jq '.errors.nombre'
```

Los dos `400`. El segundo es el que importa: choca contra una **predefinida**, con otras mayúsculas
y con espacios de sobra. Si ése pasa, la comprobación de ámbito de D-02 no está o no recorta.

```bash
curl -s -b $UNA -X POST $API/api/categorias -H 'Content-Type: application/json' \
  -d '{"nombre":"Gimnasio","tipo":"ingreso"}' | jq '.id'
```

Éste **sí** se acepta: mismo nombre, otro tipo. Borralo después o dejalo, da igual.

## 4 · Renombrar, y que los movimientos ya cargados lo vean

Registrá un gasto con "Gimnasio" desde la aplicación. Después:

```bash
curl -s -b $UNA -X PUT $API/api/categorias/$GIM -H 'Content-Type: application/json' \
  -d '{"nombre":"Gimnasio y pileta"}' | jq '.nombre'

curl -s -b $UNA "$API/api/movimientos" | jq '.[0].categoriaNombre'
curl -s -b $UNA "$API/api/resumen" | jq '.monedas[0].gastosPorCategoria'
```

El listado y el desglose muestran el nombre nuevo **sin que nadie haya tocado los movimientos**. Es
AC-04, y sale gratis porque el movimiento guarda el identificador, no el nombre.

## 5 · Las predefinidas no se tocan

```bash
curl -s -o /dev/null -w '%{http_code}\n' -b $UNA -X PUT $API/api/categorias/1 \
  -H 'Content-Type: application/json' -d '{"nombre":"Otra cosa"}'      # 403
curl -s -o /dev/null -w '%{http_code}\n' -b $UNA -X DELETE $API/api/categorias/1  # 403
curl -s -b $UNA $API/api/categorias | jq '.[] | select(.id==1) | .nombre'
```

`403` las dos veces, y la categoría 1 sigue llamándose como antes. Es AC-03. **En la aplicación**:
la pantalla de gestión no ofrece renombrar ni dar de baja las que tienen `esPropia: false`.

## 6 · Dar de baja: desaparece del selector y no mueve ni un número

**Este es el paso que más importa.** Anotá el resumen ANTES:

```bash
curl -s -b $UNA "$API/api/resumen" > /tmp/antes.json
cat /tmp/antes.json | jq '.monedas[0] | {totalGastado, balance, gastosPorCategoria}'

curl -s -o /dev/null -w '%{http_code}\n' -b $UNA -X DELETE $API/api/categorias/$GIM   # 204
curl -s -o /dev/null -w '%{http_code}\n' -b $UNA -X DELETE $API/api/categorias/$GIM   # 204 otra vez

curl -s -b $UNA "$API/api/categorias" | jq '[.[] | select(.id=='$GIM')] | length'     # 0
curl -s -b $UNA "$API/api/movimientos" | jq '.[0].categoriaNombre'   # "Gimnasio y pileta"

curl -s -b $UNA "$API/api/resumen" > /tmp/despues.json
diff <(jq -S . /tmp/antes.json) <(jq -S . /tmp/despues.json) && echo "IDÉNTICOS ✅"
```

El `diff` tiene que salir **vacío**. Si aparece una diferencia, el desglose empezó a filtrar por
`activa` y acabás de romper la deuda D6-04 de la feature 006 (AC-05, AC-06, FR-011).

El segundo `DELETE` devuelve `204` igual que el primero: es idempotente.

## 7 · Volver a crear el mismo nombre

```bash
curl -s -b $UNA -X POST $API/api/categorias -H 'Content-Type: application/json' \
  -d '{"nombre":"Gimnasio y pileta","tipo":"gasto"}' | jq '{id, nombre}'

curl -s -b $UNA "$API/api/movimientos" | jq '.[0].categoriaId'
```

`201`, con un `id` **distinto** del que se dio de baja. Y el movimiento viejo sigue apuntando al
viejo: son dos categorías homónimas, una activa y una archivada, y cada una nombra lo suyo. Es
AC-09, y es lo que la columna `discriminador` hace posible.

Si esto devuelve un `500` o un choque de índice, la migración de D-01 no se aplicó.

## 8 · Lo de la otra cuenta no existe

Con la cookie de la **segunda** cuenta:

```bash
curl -s -b $OTRA $API/api/categorias | jq '[.[] | select(.esPropia)] | length'   # 0

curl -s -o /dev/null -w '%{http_code}\n' -b $OTRA -X PUT $API/api/categorias/$GIM \
  -H 'Content-Type: application/json' -d '{"nombre":"Mía ahora"}'      # 404
curl -s -o /dev/null -w '%{http_code}\n' -b $OTRA -X DELETE $API/api/categorias/$GIM   # 404
curl -s -o /dev/null -w '%{http_code}\n' -b $OTRA -X DELETE $API/api/categorias/999999 # 404

curl -s -b $OTRA -X POST $API/api/movimientos -H 'Content-Type: application/json' \
  -d '{"tipo":"gasto","monto":100,"categoriaId":'$GIM',"fecha":"2026-09-02"}' | jq '.errors.categoriaId'
```

`404` en los tres, **con el mismo cuerpo**: los dos primeros son la categoría real de la otra cuenta
y el tercero es un identificador inventado. Si difieren, la respuesta confirma que la categoría
ajena existe (AC-11, FR-013).

> El tercero usa `DELETE` y no `GET` a propósito: **no hay** `GET /api/categorias/{id}` en el
> contrato, así que un `GET` devolvería `404` por ruta inexistente —el motivo equivocado— y el paso
> pasaría sin probar nada. Y el alta del movimiento con una
categoría ajena se rechaza (FR-021).

---

## Lo que este recorrido no cubre

- **FR-015** (una sola petición del catálogo por carga): se mira en la pestaña Red del navegador,
  no con `curl`. Cargá la pantalla principal y contá las peticiones a `/api/categorias`: tiene que
  haber **una**. Después navegá a gestión y volvé: no tiene que haber una segunda.
- **FR-023** (editar un movimiento cuya categoría se dio de baja): se puede probar acá —editale el
  monto al movimiento del paso 6 sin tocarle la categoría, tiene que aceptar; movelo a otra
  categoría dada de baja, tiene que rechazar— pero es más cómodo verlo en los tests, que arman las
  dos categorías archivadas sin trabajo manual.

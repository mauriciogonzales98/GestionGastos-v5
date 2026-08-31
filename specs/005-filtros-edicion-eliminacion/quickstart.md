# Quickstart — Filtros del listado, edición y eliminación

Cómo comprobar a mano que la feature hace lo que dice. No reemplaza a los tests: sirve para ver el
comportamiento con los ojos, que es lo que atrapa las cosas que un test escrito por la misma persona
que el código no atrapa.

**Los pasos 4 y 5 son los que importan.** Los tres primeros son andamio.

---

## Prerrequisitos

- MySQL 8.4.10 en `localhost:3306`, con el esquema `gestiongastos` migrado.
- `ConnectionStrings__Default` apuntando al esquema de **desarrollo**, no al de tests.
- La API corriendo: `dotnet run --project backend/GestionGastos.Api`.
- `curl` y `jq`.

```bash
API=http://localhost:5xxx   # el puerto que imprime dotnet run
```

---

## 1. Dos cuentas, dos frascos de cookies

El aislamiento sólo se puede ver con dos. Un solo frasco de cookies deja pasar en verde todo lo que
esta feature tiene que impedir.

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

## 2. Un movimiento de cada una

```bash
CAT=$(curl -s -b $A $API/api/categorias | jq '[.[] | select(.tipo=="gasto")][0].id')

MOV_A=$(curl -s -b $A -X POST $API/api/movimientos -H 'Content-Type: application/json' \
  -d "{\"tipo\":\"gasto\",\"monto\":1500,\"categoriaId\":$CAT}" | jq .id)
MOV_B=$(curl -s -b $B -X POST $API/api/movimientos -H 'Content-Type: application/json' \
  -d "{\"tipo\":\"gasto\",\"monto\":2500,\"categoriaId\":$CAT}" | jq .id)

echo "A=$MOV_A  B=$MOV_B"
```

## 3. El `Location` volvió

```bash
curl -s -D - -o /dev/null -b $A -X POST $API/api/movimientos \
  -H 'Content-Type: application/json' \
  -d "{\"tipo\":\"gasto\",\"monto\":100,\"categoriaId\":$CAT}" | grep -i '^location:'
```

**Esperado**: una línea `location: /api/movimientos/<id>`, y esa URL responde `200`. Hasta esta
feature el encabezado no venía, porque habría apuntado a un `404`.

---

## 4. Corregir, y que el listado lo refleje

```bash
curl -s -b $A -X PUT $API/api/movimientos/$MOV_A -H 'Content-Type: application/json' \
  -d "{\"tipo\":\"gasto\",\"monto\":15000,\"categoriaId\":$CAT,\"fecha\":\"$(date +%F)\"}" | jq .monto

curl -s -b $A $API/api/movimientos | jq "[.[] | select(.id==$MOV_A)][0].monto"
```

**Esperado**: `15000` las dos veces. El `1500` no aparece en ningún lado — AC-01.

```bash
curl -s -b $A -X DELETE $API/api/movimientos/$MOV_A -o /dev/null -w '%{http_code}\n'
curl -s -b $A $API/api/movimientos | jq "[.[] | select(.id==$MOV_A)] | length"
```

**Esperado**: `204`, y después `0` — AC-08.

---

## 5. Lo ajeno responde igual que lo inexistente

**Éste es el paso que justifica el quickstart entero.** Los tests lo comprueban, pero verlo con los
ojos es lo que convence.

```bash
INEXISTENTE=999999

for id in $MOV_B $INEXISTENTE; do
  echo "--- id=$id"
  curl -s -b $A -o /tmp/cuerpo-$id.json -w 'GET    %{http_code} %{content_type}\n' \
    $API/api/movimientos/$id
  curl -s -b $A -X PUT $API/api/movimientos/$id -H 'Content-Type: application/json' \
    -d "{\"tipo\":\"gasto\",\"monto\":1,\"categoriaId\":$CAT,\"fecha\":\"$(date +%F)\"}" \
    -o /dev/null -w 'PUT    %{http_code}\n'
  curl -s -b $A -X DELETE $API/api/movimientos/$id -o /dev/null -w 'DELETE %{http_code}\n'
done

echo "--- ¿los cuerpos son idénticos?"
diff /tmp/cuerpo-$MOV_B.json /tmp/cuerpo-$INEXISTENTE.json && echo "IDÉNTICOS, como tiene que ser"
```

**Esperado**: `404` en las seis líneas, mismo `content_type`, y el `diff` **vacío**. Cualquier
diferencia entre los dos cuerpos —una palabra, un campo de más— es la fuga que AC-05 prohíbe: los
identificadores son contiguos, así que distinguir "ajeno" de "inexistente" permite contar los
movimientos de otra cuenta sin ver ninguno.

```bash
curl -s -b $B $API/api/movimientos | jq "[.[] | select(.id==$MOV_B)][0].monto"
```

**Esperado**: `2500`. Después de que A intentara modificarlo y eliminarlo, el movimiento de B sigue
intacto — AC-06 y AC-09.

---

## 6. Los filtros

```bash
# Sin filtros: el mes en curso del servidor
curl -s -b $B $API/api/movimientos | jq length

# Rango con los extremos incluidos: un solo día, el de hoy
HOY=$(date +%F)
curl -s -b $B "$API/api/movimientos?desde=$HOY&hasta=$HOY" | jq length

# Un mes sin nada
curl -s -b $B "$API/api/movimientos?desde=2020-01-01&hasta=2020-01-31" | jq

# Rango invertido
curl -s -b $B "$API/api/movimientos?desde=2026-12-31&hasta=2026-01-01" \
  -o /dev/null -w '%{http_code}\n'

# Medio rango
curl -s -b $B "$API/api/movimientos?desde=$HOY" -o /dev/null -w '%{http_code}\n'

# Por categoría, y por una que no existe
curl -s -b $B "$API/api/movimientos?categoriaId=$CAT" | jq length
curl -s -b $B "$API/api/movimientos?categoriaId=999999" | jq
```

**Esperado**, en orden:

| Paso | Resultado |
|---|---|
| Sin filtros | La cantidad de movimientos de B en el mes en curso — AC-13 |
| Un solo día, hoy | Los de hoy. **Que no sea `0`** es lo que prueba que los extremos se incluyen — AC-14 |
| Enero de 2020 | `[]`, **no** un `404` — AC-16 |
| Rango invertido | `400` — FR-015 |
| Medio rango | `400`. No se supone un extremo abierto que nadie declaró |
| Por categoría | Sólo los de esa categoría — AC-11 |
| Categoría inexistente | `[]`, no un `400`: rechazarla confirmaría cuáles existen — AC-17 |

---

## 7. Limpiar

```bash
rm -f $A $B /tmp/cuerpo-*.json
```

Las dos cuentas quedan en el esquema de desarrollo. Si molestan, se borran a mano: esta feature no
agrega forma de dar de baja una cuenta.

#!/usr/bin/env bash
#
# La barrera de la restricción de ámbito (Principio V de la constitución). La octava del proyecto.
#
# `AmbitoDeCategoriaEsquemaTests` afirma que la base rechaza, por su cuenta, un movimiento
# clasificado con la categoría de otra cuenta (`FR-006`, `SC-002`). Que pase sólo prueba que hoy
# rechaza: no prueba que el test sepa darse cuenta el día que la restricción no esté.
#
# Y ésa es justamente la forma en la que este test puede mentir. Afirma que **algo falla**: informa
# verde cuando la restricción está puesta y cuando la comprobación dejó de verificarla —un `INSERT`
# que ya fallaba por otro motivo, un `Assert.ThrowsAny` demasiado ancho, una limpieza que borra la
# fila antes de mirarla—. Las dos cosas se ven igual desde afuera.
#
# Lo que protege es la deuda **D7-07**, abierta desde la feature 007 y saldada por la 013: la regla
# de que un movimiento no puede apuntar a la categoría de otra cuenta vivía sólo en dos
# comprobaciones de la aplicación, así que valía para lo que pasa por la API y no para un script de
# mantenimiento, una importación o un arreglo a mano en la base.
#
# **Se desarma con SQL directo y no tocando código**, a diferencia de las barreras del desglose y de
# la nota. La restricción no vive en una consulta de C#: la puso una migración y vive en el catálogo
# de MySQL. Bajarla con `ALTER TABLE` es desarmar exactamente lo que se está midiendo, y no una
# representación suya.
#
# No recompila nada, así que puede correr antes o después de los tests. Tarda unos segundos.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILTRO='FullyQualifiedName~AmbitoDeCategoriaEsquema'
FORANEA='fk_movimiento_categoria_del_ambito'

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. La barrera necesita hablarle a la base para poder" >&2
  echo "       bajar y volver a poner la restricción." >&2
  exit 1
fi

command -v mysql > /dev/null 2>&1 || {
  echo "ERROR: falta el cliente \`mysql\`. Es lo que permite bajar la restricción SIN pasar por la" >&2
  echo "       aplicación, que es la mitad del punto de esta barrera." >&2
  exit 1
}

# **Lee una clave de la cadena de conexión sin asumir cómo está escrita.** Es el mismo lector que
# usa `verificar-monedas.sh`, y por el mismo motivo: ADO.NET no fija ni la capitalización ni los
# espacios alrededor del `=`, y las dos cadenas que este repo usa de hecho difieren —en local dice
# `User Id=` y en `ci.yml` dice `User ID=`.
leer() {
  local clave
  for clave in "$@"; do
    local valor
    valor="$(sed -nE "s/.*(^|;)[[:space:]]*$clave[[:space:]]*=[[:space:]]*([^;]*).*/\2/Ip" \
             <<< "$ConnectionStrings__Default")"
    if [[ -n "$valor" ]]; then
      echo "$valor"
      return
    fi
  done
}

HOST="$(leer 'Server' 'Data Source' 'Host')"; BASE="$(leer 'Database' 'Initial Catalog')"
USUARIO="$(leer 'User Id' 'User ID' 'Uid' 'UserName')"; CLAVE="$(leer 'Password' 'Pwd')"

if [[ -z "$USUARIO" || -z "$HOST" || -z "$BASE" ]]; then
  echo "ERROR: no se pudo leer la cadena de conexión. Faltan servidor, base o usuario." >&2
  echo "       servidor=[$HOST] base=[$BASE] usuario=[$USUARIO]" >&2
  exit 1
fi

# `-p` sólo cuando hay contraseña: con el argumento vacío, mysql la pide POR TECLADO y en CI —que
# corre con `MYSQL_ALLOW_EMPTY_PASSWORD`— eso muere leyendo EOF. La contraseña viaja por `MYSQL_PWD`
# para que no aparezca en `ps`.
ARGS=(-h "$HOST" -u "$USUARIO" -N -B)
[[ -n "$CLAVE" ]] && export MYSQL_PWD="$CLAVE"

sql() { mysql "${ARGS[@]}" "$BASE" -e "$1"; }

if ! sql "SELECT 1;" > /dev/null; then
  echo "ERROR: no se pudo conectar a la base." >&2
  echo "       servidor=[$HOST] base=[$BASE] usuario=[$USUARIO]" >&2
  exit 1
fi

existe_la_foranea() {
  local cuantas
  cuantas="$(sql "
    SELECT COUNT(*)
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'movimiento'
      AND CONSTRAINT_NAME = '$FORANEA';
  ")"

  [[ "$cuantas" == "1" ]]
}

# La restauración va en un `trap`: si el script muere a la mitad —una aserción, un Ctrl-C—, la base
# NO puede quedarse sin la restricción. Sería el peor resultado posible de una barrera: dejar
# desarmado justamente lo que vino a comprobar.
#
# **Y si la restauración falla, se dice.** La primera versión terminaba en `|| true`, que es un catch
# silencioso escrito en bash — lo que `AGENTS.md` prohíbe y lo que la barrera de monedas ya tiene
# documentado. En el camino feliz no se notaba, porque el paso 4 comprueba aparte que la foránea
# volvió; el problema era el otro camino: si el script moría entre el DROP y el final, el `trap`
# intentaba restaurar, fallaba, y **nadie se enteraba**. La base quedaba sin la restricción y el rojo
# aparecía dos corridas después, en otro test y sin ninguna pista.
#
# El `if` distingue los dos motivos de fallo que tiene este ALTER, que no son lo mismo: que la
# foránea YA esté puesta —el caso normal al salir bien, porque el paso 4 ya la restauró— y que no se
# haya podido poner. Sólo el segundo es un problema.
restaurar() {
  if existe_la_foranea; then
    return 0
  fi

  if ! sql "
    ALTER TABLE movimiento
      ADD CONSTRAINT $FORANEA
      FOREIGN KEY (categoria_id, usuario_id)
      REFERENCES categoria (id, usuario_id);
  " > /dev/null; then
    echo "ERROR: no se pudo restaurar \`$FORANEA\`. **La base quedó SIN la restricción**: hasta" >&2
    echo "       que se reponga, un movimiento puede quedar clasificado con la categoría de otra" >&2
    echo "       cuenta, y los tests de esquema van a dar rojo sin que la causa esté a la vista." >&2
    echo "       El error de mysql está arriba de estas líneas." >&2
    return 1
  fi
}
trap restaurar EXIT

correr_tests() {
  dotnet test "$RAIZ/backend/GestionGastos.slnx" --filter "$FILTRO" --nologo --verbosity quiet
}

echo "== 1/4 · la restricción tiene que estar puesta"
if ! existe_la_foranea; then
  echo "ERROR: \`$FORANEA\` no existe en la base. O la migración no se aplicó, o una corrida" >&2
  echo "       anterior de esta barrera murió sin restaurarla. Corré los tests una vez para que el" >&2
  echo "       fixture migre, y volvé a intentar." >&2
  exit 1
fi
echo "   puesta, como se esperaba"

echo "== 2/4 · con la restricción puesta, la barrera tiene que estar en verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: la barrera ya falla sin tocar nada. Arreglá eso antes de medirla." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 3/4 · sin la restricción tiene que ponerse en ROJO"
sql "ALTER TABLE movimiento DROP FOREIGN KEY $FORANEA;" > /dev/null

if existe_la_foranea; then
  echo "ERROR: no se pudo bajar \`$FORANEA\`. El script quedó mirando un esquema que ya no existe:" >&2
  echo "       actualizá verificar-ambito-de-categoria.sh." >&2
  exit 1
fi

if correr_tests > /dev/null 2>&1; then
  echo "ERROR: la restricción no está y la barrera pasó igual." >&2
  echo "       Ese test no está verificando nada, y lo que deja pasar es la deuda D7-07 entera: un" >&2
  echo "       movimiento puede quedar clasificado con la categoría de otra cuenta desde cualquier" >&2
  echo "       escritura que no pase por la aplicación." >&2
  exit 1
fi
echo "   rojo, como se esperaba"

echo "== 4/4 · restaurada tiene que volver al verde"
restaurar

if ! existe_la_foranea; then
  echo "ERROR: no se pudo volver a poner \`$FORANEA\`. **La base quedó sin la restricción**: revisá" >&2
  echo "       el esquema de \`movimiento\` antes de seguir." >&2
  exit 1
fi

if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: se restauró la restricción y la barrera sigue en rojo." >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera del ámbito de categoría: EN PIE. Sabe detectar que la base dejó de rechazar un"
echo "movimiento clasificado con la categoría de otra cuenta. Es la deuda D7-07, y lo que la"
echo "sostiene ya no son dos comprobaciones de la aplicación sino una clave foránea."

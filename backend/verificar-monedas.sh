#!/usr/bin/env bash
#
# La barrera de las monedas como dato. La SEXTA del proyecto, y la única que no verifica una
# barrera: verifica una promesa de producto.
#
# `PRD:RF-32` promete que se puede sumar una moneda al catálogo "con 0 líneas de código modificadas
# y 0 recompilaciones". Eso NO es una afirmación sobre el comportamiento del sistema, es una
# afirmación sobre el PROCESO — y por eso `MonedaComoDatoTests` no la puede sostener solo. Ese test
# corre dentro de un proceso que ya se compiló, así que su verde no distingue "no hizo falta
# recompilar" de "recompilamos y no nos dimos cuenta".
#
# El script agrega la moneda con SQL puro, corre los tests con `--no-build`, y al terminar exige las
# DOS mitades de la afirmación, con un mecanismo cada una:
#
#   · 0 recompilaciones      → la FECHA DE MODIFICACIÓN del ensamblado, antes y después, más su
#                              hash. **La fecha es la que detecta el rebuild; el hash solo no
#                              puede.** .NET compila de forma determinista: recompilar el mismo
#                              fuente produce un binario byte a byte idéntico, así que un hash igual
#                              NO prueba que nadie compiló. Comprobado corriéndolo (T012), que es la
#                              única razón por la que esto no quedó siendo un agujero silencioso.
#                              El hash igual sigue valiendo, pero por otra cosa: dice que el binario
#                              que corrió los tests es el mismo que se midió.
#   · 0 líneas modificadas   → `git status --porcelain` sobre GestionGastos.Api/, vacío.
#
# **Por qué la segunda mitad NO usa un hash del árbol de fuentes**, que es lo primero que se le
# ocurre a cualquiera: un hash tomado antes y después sólo ve lo que cambió DURANTE la ventana del
# script. Un archivo tocado antes de arrancar entra en los dos hashes, que quedan iguales, y el
# script pasaría en verde con el árbol sucio. `git status` compara contra el commit, no contra sí
# mismo hace un minuto, así que no tiene ese punto ciego.
#
# NO modifica ningún archivo, así que no invalida el `--no-build` de nadie. Sí escribe en la base
# —agrega una moneda— y la restaura con un `trap`, porque una moneda de más se lleva puesta la
# corrida siguiente.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API="$RAIZ/backend/GestionGastos.Api"
ENSAMBLADO="$API/bin/Debug/net10.0/GestionGastos.Api.dll"
FILTRO='FullyQualifiedName~MonedaComoDato'
CODIGO='XTS'   # ISO 4217 reserva XTS para pruebas: no colisiona con ninguna moneda real.

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default." >&2
  exit 1
fi

# El cliente `mysql` es el que hace que "sólo como dato" sea cierto: si la moneda se agregara con
# EF, se estaría usando el código de la aplicación para probar que no hace falta el código.
command -v mysql > /dev/null 2>&1 || {
  echo "ERROR: falta el cliente \`mysql\`. Es lo que permite agregar la moneda SIN pasar por la" >&2
  echo "       aplicación, que es la mitad del punto de esta barrera." >&2
  exit 1
}

# **Lee una clave de la cadena de conexión sin asumir cómo está escrita.**
#
# ADO.NET no fija ni la capitalización ni los espacios alrededor del `=`, y las dos cadenas que este
# repo usa de hecho difieren: en local dice `User Id=` y en `ci.yml` dice `User ID=`. Un `sed`
# sensible a mayúsculas devolvía vacío contra la de CI, y `mysql -u ""` no falla limpio — cae al
# usuario del sistema operativo, así que el error que aparecía era un `Access denied` para un
# usuario que nadie nombró.
#
# Acepta varios nombres para la misma clave —`User Id`, `User ID`, `Uid`— porque la cadena de
# conexión es un formato que nadie de este lado define.
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

# **`-p` sólo cuando hay contraseña, y ésa es la diferencia entre andar y colgarse.**
#
# `mysql -p""` NO significa "sin contraseña": con el argumento vacío, mysql la pide **por teclado**.
# Sin TTY —o sea, en CI— eso imprime `Enter password:` y muere leyendo EOF. La base de CI corre con
# `MYSQL_ALLOW_EMPTY_PASSWORD`, así que la cadena no trae `Password` y éste es exactamente el caso
# que se da ahí.
#
# Los argumentos van en un array para que la ausencia de `-p` sea la ausencia de un elemento, y no
# una cadena vacía que el shell igual le pasa a mysql.
ARGS=(-h "$HOST" -u "$USUARIO" -N -B)

# La contraseña viaja por `MYSQL_PWD` y no por `-p`: así no aparece en `ps` —que es lo que el
# warning de mysql viene a decir— y, sobre todo, **el warning desaparece por el motivo correcto**.
# Antes se lo tapaba mandando todo `stderr` a /dev/null, y con él se iban también los errores de
# conexión: `AGENTS.md` prohíbe el catch silencioso y ése lo era, escrito en bash.
[[ -n "$CLAVE" ]] && export MYSQL_PWD="$CLAVE"

sql() { mysql "${ARGS[@]}" "$BASE" -e "$1"; }

# **Se comprueba la conexión ANTES de usarla.** Sin esto, el primer fallo aparecía en medio del
# paso 2, con `set -e` matando el script sin imprimir una sola línea sobre la causa: quien mirara
# CI veía la barrera cortada a la mitad y ninguna pista de que el problema era la cadena de
# conexión.
if ! sql "SELECT 1;" > /dev/null; then
  echo "ERROR: no se pudo conectar a la base." >&2
  echo "       servidor=[$HOST] base=[$BASE] usuario=[$USUARIO] contraseña=[$([[ -n "$CLAVE" ]] && echo "sí" || echo "no")]" >&2
  echo "       El error de mysql está arriba de estas líneas." >&2
  exit 1
fi

limpiar() { sql "DELETE FROM moneda WHERE codigo = '$CODIGO';" || true; }
trap limpiar EXIT

echo "== 1/4 · compilar UNA vez y tomar la línea de base"
dotnet build "$RAIZ/backend/GestionGastos.slnx" -warnaserror --nologo --verbosity quiet > /dev/null
HASH_ANTES="$(sha256sum "$ENSAMBLADO" | cut -d' ' -f1)"
MTIME_ANTES="$(stat -c %Y "$ENSAMBLADO")"

# El árbol tiene que estar limpio ANTES de empezar, o la comprobación del final no distingue lo que
# ensució el script de lo que ya estaba sucio.
if [[ -n "$(git -C "$RAIZ" status --porcelain -- backend/GestionGastos.Api/)" ]]; then
  echo "ERROR: hay cambios sin commitear en backend/GestionGastos.Api/." >&2
  echo "       Esta barrera mide que el script no modifique código, y con el árbol ya sucio no" >&2
  echo "       puede distinguir una cosa de la otra. Commiteá o descartá y volvé a correr." >&2
  exit 1
fi
echo "   ensamblado en ${HASH_ANTES:0:12}… (mtime $MTIME_ANTES) y árbol limpio"

echo "== 2/4 · agregar la moneda al catálogo, sólo con SQL"
limpiar
sql "INSERT INTO moneda (codigo, nombre, simbolo, decimales, es_predeterminada)
     VALUES ('$CODIGO', 'Moneda de prueba', '¤', 2, 0);"
[[ "$(sql "SELECT COUNT(*) FROM moneda WHERE codigo = '$CODIGO';")" == "1" ]] || {
  echo "ERROR: no se pudo agregar la moneda. Sin eso no hay nada que verificar." >&2
  exit 1
}
echo "   $CODIGO agregada sin tocar un archivo"

echo "== 3/4 · los tests tienen que pasar SIN recompilar"
if ! dotnet test "$RAIZ/backend/GestionGastos.slnx" --no-build --filter "$FILTRO" \
     --nologo --verbosity quiet > /dev/null 2>&1; then
  echo "ERROR: los tests de MonedaComoDato fallaron con --no-build." >&2
  echo "       O la moneda nueva no llegó al resumen, o hizo falta recompilar para que llegara." >&2
  echo "       Las dos cosas son \`PRD:RF-32\` incumplido." >&2
  exit 1
fi
echo "   verde, y sin build"

echo "== 4/4 · ni una recompilación, ni una línea tocada"
HASH_DESPUES="$(sha256sum "$ENSAMBLADO" | cut -d' ' -f1)"
MTIME_DESPUES="$(stat -c %Y "$ENSAMBLADO")"

if [[ "$MTIME_ANTES" != "$MTIME_DESPUES" ]]; then
  echo "ERROR: el ensamblado se volvió a escribir. Algo recompiló." >&2
  echo "       mtime antes:   $MTIME_ANTES" >&2
  echo "       mtime después: $MTIME_DESPUES" >&2
  echo "       Que el hash coincida NO lo desmiente: con compilación determinista, recompilar el" >&2
  echo "       mismo fuente da un binario idéntico." >&2
  exit 1
fi

if [[ "$HASH_ANTES" != "$HASH_DESPUES" ]]; then
  echo "ERROR: el ensamblado cambió de contenido." >&2
  echo "       antes:   $HASH_ANTES" >&2
  echo "       después: $HASH_DESPUES" >&2
  exit 1
fi

SUCIO="$(git -C "$RAIZ" status --porcelain -- backend/GestionGastos.Api/)"
if [[ -n "$SUCIO" ]]; then
  echo "ERROR: quedaron archivos de producción modificados:" >&2
  echo "$SUCIO" >&2
  echo "       Agregar una moneda no puede exigir tocar código." >&2
  exit 1
fi
echo "   mismo ensamblado (mtime y contenido), mismo árbol"

echo
echo "Monedas como dato: VERIFICADO. Se agregó una moneda al catálogo y la aplicación la tomó con"
echo "0 recompilaciones y 0 líneas de código modificadas."

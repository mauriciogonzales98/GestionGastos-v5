#!/usr/bin/env bash
#
# La barrera del contrato frontend↔backend (Principio V de la constitución).
#
# Que los tests de Contrato/ pasen sólo prueba que HOY las dos definiciones están alineadas. No
# prueba que la comparación sirva: un test que compara mal, o que dejó de leer el archivo, pasa
# igual de verde. Lo único que distingue una barrera viva de una decorativa es verla ponerse en
# rojo cuando el contrato se rompe.
#
# Este script desalinea el contrato a propósito, exige el rojo, restaura y exige el verde.
#
# Hay un caso por FORMA de comparación, no uno por tipo: una respuesta que se compara en las dos
# direcciones y una petición que se arma con los nombres del contrato fallan por mecanismos
# distintos, y un solo caso dejaría al otro mecanismo sin probar que sabe fallar.
#
# Corre `dotnet test` una vez por caso más dos, así que tarda ~2,5 min. Va al final de la puerta.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTRATO="$RAIZ/frontend/src/api/tipos.ts"
RESPALDO="$(mktemp)"
FILTRO='FullyQualifiedName~GestionGastos.Api.Tests.Contrato'

# Cada caso: descripción | expresión de sed que desalinea | texto que tiene que aparecer después.
# El texto esperado es lo que detecta que el archivo cambió de forma y el sed ya no muerde: sin él,
# un sed que no hace nada daría "verde con el contrato roto" y este script culparía a la barrera.
#
# El separador es `|`, así que ninguno de los tres campos puede contener uno: un `sed` con una
# alternancia parte el caso en pedazos y `esperado` se queda con un trozo de la expresión, en
# silencio. Escaparla no alcanza —`\|` también tiene el carácter—: si un caso la necesita, hay que
# cambiar el separador de acá y el `IFS` del bucle.
#
# La expresión va anclada a la interfaz que nombra la descripción. Sin el rango, `^  nombre:
# string;` muerde en cualquier interfaz que tenga ese campo: hoy hay una sola y el caso parece
# correcto de casualidad, pero el día que otra gane un `nombre` el caso renombra dos y deja de ser
# el que dice ser.
CASOS=(
  'un rename coherente en una respuesta (Categoria.nombre)|/export interface Categoria/,/^}/ s/^  nombre: string;/  nombreCategoria: string;/|nombreCategoria'
  'un rename en las dos peticiones (NuevaCuenta y Credenciales)|s/^  contrasena: string;/  clave: string;/|clave: string;'
  'un rename en la respuesta de sesión (SesionActual.email)|/export interface SesionActual/,/^}/ s/^  email: string;/  correo: string;/|correo: string;'
)

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. Los tests de contrato levantan la API de verdad." >&2
  exit 1
fi

# El respaldo se hace ANTES de armar el trap, y el trap se niega a restaurar desde un respaldo
# vacío. Al revés —que es como estaba— un `cp` que falle acá dispara la salida por `set -e`, el
# trap corre igual y copia el `mktemp` recién creado, todavía vacío, encima del contrato: la fuente
# de verdad del contrato queda en 0 bytes en el árbol de trabajo.
cp "$CONTRATO" "$RESPALDO"

# El archivo se restaura pase lo que pase: un fallo a mitad de camino no puede dejar el contrato
# desalineado en el árbol de trabajo.
restaurar() {
  if [[ -s "$RESPALDO" ]]; then
    cp "$RESPALDO" "$CONTRATO"
  else
    echo "ERROR: el respaldo de $CONTRATO está vacío; no se restaura nada para no pisarlo." >&2
  fi
  rm -f "$RESPALDO"
}
trap restaurar EXIT

# Sin `--verbosity quiet`: `exigir_rojo` necesita leer el mensaje y el stack del fallo, y en quiet
# no se imprimen. Los pasos que sólo miran verde/rojo la mandan a /dev/null igual.
correr_tests() {
  dotnet test "$RAIZ/backend/GestionGastos.slnx" --filter "$FILTRO" --nologo
}

exigir_rojo() {
  local que="$1"
  local salida

  if salida="$(correr_tests 2>&1)"; then
    echo "ERROR: el contrato está desalineado ($que) y los tests pasaron igual." >&2
    echo "       La barrera no sirve: no detecta lo único que tiene que detectar." >&2
    exit 1
  fi

  # Mira DE DÓNDE viene el rojo, y no es celo de más.
  #
  # `TiposDelFrontend` lanza cuando no encuentra la interfaz o cuando las llaves no cierran, y eso
  # sale como test en rojo igual que una desalineación de verdad. Sin esta distinción, un caso que
  # rompe el archivo en vez de desalinearlo se cuenta como rojo válido: el paso imprime "rojo, como
  # se esperaba", el script termina en EN PIE, y ese caso no volvió a probar nada.
  #
  # El discriminador es el frame, no el tipo de excepción: el caso de las peticiones también falla
  # con `InvalidOperationException`, pero de un guard del propio test —el contrato declara un campo
  # que el test no sabe ejercitar—, que es un rojo legítimo. `TiposDelFrontend` en el stack sólo
  # aparece cuando lanzó el parser.
  if grep -q 'TiposDelFrontend' <<< "$salida"; then
    echo "ERROR: el contrato quedó en rojo ($que), pero lo tiró el PARSER, no la comparación." >&2
    echo "       El caso rompió la forma del archivo en vez de desalinear el contrato: así no" >&2
    echo "       prueba nada. Ajustá la sustitución en CASOS de verificar-contrato.sh." >&2
    exit 1
  fi
  echo "   rojo, como se esperaba"
}

PASOS=$(( ${#CASOS[@]} + 2 ))

echo "== 1/$PASOS · el contrato alineado tiene que estar en verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: los tests de contrato ya fallan sin tocar nada. Arreglá eso antes de medir la barrera." >&2
  exit 1
fi
echo "   verde, como se esperaba"

paso=1
for caso in "${CASOS[@]}"; do
  paso=$(( paso + 1 ))
  IFS='|' read -r descripcion expresion esperado <<< "$caso"

  echo "== $paso/$PASOS · $descripcion: tiene que ponerse en ROJO"
  sed -i "$expresion" "$CONTRATO"

  if ! grep -q "$esperado" "$CONTRATO"; then
    echo "ERROR: no se pudo desalinear el contrato ($descripcion)." >&2
    echo "       El archivo cambió de forma y este script quedó viejo." >&2
    exit 1
  fi

  exigir_rojo "$descripcion"

  cp "$RESPALDO" "$CONTRATO"
done

echo "== $PASOS/$PASOS · restaurado tiene que volver al verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: el contrato quedó restaurado pero los tests siguen en rojo." >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera del contrato: EN PIE. Sabe fallar cuando el contrato se desalinea."

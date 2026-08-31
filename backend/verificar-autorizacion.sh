#!/usr/bin/env bash
#
# La barrera de autorización (Principio V de la constitución).
#
# `BarreraDeAutorizacionTests` comprueba que ningún endpoint responde sin sesión salvo los
# declarados. Que pase sólo prueba que HOY están todos protegidos: no prueba que la comparación
# sirva. Una barrera que descubre mal los endpoints, o que se quedó mirando una lista vieja, pasa
# igual de verde.
#
# Este script agrega un endpoint desprotegido a propósito, exige el ROJO, lo quita y exige el verde.
#
# Es la barrera más importante del proyecto: un endpoint sin proteger no rompe nada visible, no da
# error, y expone los datos de todas las cuentas.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM="$RAIZ/backend/GestionGastos.Api/Program.cs"
FILTRO='FullyQualifiedName~BarreraDeAutorizacion'

# El endpoint colado se mapea inline y en el mismo lugar que los de verdad, no en un archivo aparte.
#
# Antes venía en un `EndpointDesprotegido.temporal.cs` propio, con su `namespace GestionGastos.Api`
# — que `Program.cs` no importa: importa los subnamespaces uno por uno. El intruso no compilaba, y
# como `dotnet test` devuelve 1 igual, el paso 2 contaba ese error del compilador como su rojo. La
# barrera decía EN PIE sin haber levantado nunca un endpoint desprotegido.
#
# Inline no hay namespace que casar ni `using` que agregar: es la misma forma que `app.MapGet("/")`
# de dos líneas más arriba, que ya se sabe que compila y que pasa los analizadores.
MARCA='// intruso de verificar-autorizacion.sh'
COLADO="app.MapGet(\"/api/colado\", () => Results.Ok(\"no debería poder leerme sin sesión\")).AllowAnonymous(); $MARCA"

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. La barrera levanta la aplicación de verdad." >&2
  exit 1
fi

limpiar() {
  sed -i "\\|$MARCA|d" "$PROGRAM"
}
trap limpiar EXIT

correr_tests() {
  dotnet test "$RAIZ/backend/GestionGastos.slnx" --filter "$FILTRO" --nologo --verbosity quiet
}

exigir_rojo() {
  local que="$1"

  # Compila ANTES de mirar los tests, y no es celo de más.
  #
  # `dotnet test` devuelve 1 tanto si los tests fallaron como si el proyecto no compiló, así que sin
  # esta distinción un intruso que no compila se cuenta como rojo válido: el paso imprime "rojo,
  # como se esperaba", el script termina diciendo EN PIE, y desde ese momento no verifica nada. Es
  # la falla que esta barrera existe para prevenir, ocurriendo adentro de ella.
  #
  # Ya pasó una vez, y por eso está: el intruso no compilaba y nadie se enteró. Vuelve a pasar el
  # día que alguien retoque el endpoint colado de acá arriba y lo deje inválido.
  if ! dotnet build "$RAIZ/backend/GestionGastos.slnx" --nologo --verbosity quiet > /dev/null 2>&1; then
    echo "ERROR: $que y el proyecto dejó de COMPILAR." >&2
    echo "       El rojo tiene que venir de los tests, no del compilador. El endpoint colado que" >&2
    echo "       inyecta el script es código inválido: actualizá verificar-autorizacion.sh." >&2
    exit 1
  fi

  if correr_tests > /dev/null 2>&1; then
    echo "ERROR: $que y la barrera pasó igual." >&2
    echo "       No está descubriendo los endpoints: revisá BarreraDeAutorizacionTests." >&2
    exit 1
  fi
  echo "   rojo, como se esperaba"
}

echo "== 1/3 · con todo protegido, la barrera tiene que estar en verde"
limpiar
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: la barrera ya falla sin tocar nada. Arreglá eso antes de medirla." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 2/3 · con un endpoint desprotegido tiene que ponerse en ROJO"
sed -i "s|^app\\.MapCuentas();|$COLADO\\napp.MapCuentas();|" "$PROGRAM"

# Que el `sed` haya mordido. Sin esto, el día que `Program.cs` deje de mapear así, el colado no
# entra: la barrera pasa en verde con razón y el script culpa a BarreraDeAutorizacionTests por un
# endpoint que nunca llegó a existir.
if ! grep -qF "$MARCA" "$PROGRAM"; then
  echo "ERROR: no se pudo enganchar el endpoint desprotegido en Program.cs." >&2
  echo "       El archivo cambió de forma y este script quedó viejo: actualizá la sustitución." >&2
  exit 1
fi

exigir_rojo "hay un endpoint anónimo sin declarar"

echo "== 3/3 · quitado el intruso tiene que volver al verde"
limpiar

if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: se quitó el endpoint desprotegido y la barrera sigue en rojo." >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera de autorización: EN PIE. Sabe detectar un endpoint que responde sin sesión."

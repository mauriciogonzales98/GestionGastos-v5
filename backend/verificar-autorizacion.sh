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
INTRUSO="$RAIZ/backend/GestionGastos.Api/EndpointDesprotegido.temporal.cs"
FILTRO='FullyQualifiedName~BarreraDeAutorizacion'

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. La barrera levanta la aplicación de verdad." >&2
  exit 1
fi

limpiar() {
  rm -f "$INTRUSO"
}
trap limpiar EXIT

correr_tests() {
  dotnet test "$RAIZ/backend/GestionGastos.slnx" --filter "$FILTRO" --nologo --verbosity quiet
}

echo "== 1/3 · con todo protegido, la barrera tiene que estar en verde"
limpiar
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: la barrera ya falla sin tocar nada. Arreglá eso antes de medirla." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 2/3 · con un endpoint desprotegido tiene que ponerse en ROJO"
cat > "$INTRUSO" <<'CS'
namespace GestionGastos.Api;

/// <summary>
/// Archivo temporal de verificar-autorizacion.sh. Si lo encontrás commiteado, el script murió a
/// mitad de camino: borralo. Expone un endpoint sin sesión a propósito.
/// </summary>
public static class EndpointDesprotegidoTemporal
{
    public static void MapEndpointDesprotegido(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/colado", () => Results.Ok("no debería poder leerme sin sesión"))
            .AllowAnonymous();
    }
}
CS

# Se engancha en Program.cs junto a los demás mapeos.
sed -i 's|^app\.MapCuentas();|app.MapEndpointDesprotegido();\napp.MapCuentas();|' "$RAIZ/backend/GestionGastos.Api/Program.cs"

restaurar_program() {
  sed -i '/^app\.MapEndpointDesprotegido();$/d' "$RAIZ/backend/GestionGastos.Api/Program.cs"
}
trap 'limpiar; restaurar_program' EXIT

if correr_tests > /dev/null 2>&1; then
  echo "ERROR: hay un endpoint anónimo sin declarar y la barrera pasó igual." >&2
  echo "       No está descubriendo los endpoints: revisá BarreraDeAutorizacionTests." >&2
  exit 1
fi
echo "   rojo, como se esperaba"

echo "== 3/3 · quitado el intruso tiene que volver al verde"
limpiar
restaurar_program
trap limpiar EXIT

if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: se quitó el endpoint desprotegido y la barrera sigue en rojo." >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera de autorización: EN PIE. Sabe detectar un endpoint que responde sin sesión."

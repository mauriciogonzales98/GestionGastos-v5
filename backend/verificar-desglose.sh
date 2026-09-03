#!/usr/bin/env bash
#
# La barrera del desglose del resumen (Principio V de la constitución). La quinta del proyecto.
#
# `BarreraDelDesgloseTests` exige que la consulta del resumen NO filtre por `categoria.activa`. Que
# pase sólo prueba que hoy el filtro no está: no prueba que sepa detectarlo el día que aparezca.
#
# Y esta barrera necesita el trato más que ninguna, porque el agujero que tapa ya estuvo abierto y
# nadie lo vio. Es la deuda D6-04 de la feature 006: hasta la 007 todas las categorías tenían
# `activa = true`, así que agregarle el filtro al desglose no cambiaba ningún número y la suite
# entera quedaba en VERDE con él puesto — 195 de 195, medido antes de escribir la barrera. Un test
# de resultado no podía verlo: no había ninguna categoría inactiva con la que producir la
# diferencia.
#
# El daño no es un error visible. Es que los totales históricos cambien solos: los movimientos de
# una categoría dada de baja dejan de sumar, y el resumen de un mes cerrado hace dos años pasa a
# dar otro número sin que nadie haya tocado un movimiento. La baja lógica existe para que una
# categoría desaparezca del SELECTOR (FR-010) y siga nombrando lo que ya nombró (FR-011, AC-06).
#
# El script le agrega el filtro a la consulta del resumen, exige el ROJO, restaura y exige el verde.
#
# Recompila con el archivo modificado, así que va DESPUÉS de los tests: invalidaría su --no-build.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONSULTA="$RAIZ/backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs"
FILTRO='FullyQualifiedName~BarreraDelDesglose'

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. La barrera necesita el proveedor de MySQL para" >&2
  echo "       poder generar el SQL que inspecciona." >&2
  exit 1
fi

RESPALDO="$(mktemp -d)"
cp "$CONSULTA" "$RESPALDO/consulta.cs"

restaurar() {
  cp "$RESPALDO/consulta.cs" "$CONSULTA"
}
trap 'restaurar; rm -rf "$RESPALDO"' EXIT

correr_tests() {
  dotnet test "$RAIZ/backend/GestionGastos.slnx" --filter "$FILTRO" --nologo --verbosity quiet
}

echo "== 1/3 · sin el filtro, la barrera tiene que estar en verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: la barrera ya falla sin tocar nada. Arreglá eso antes de medirla." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 2/3 · con el filtro por \`activa\` en el desglose tiene que ponerse en ROJO"
# Se le cuela el filtro justo antes del GroupBy, que es exactamente donde lo escribiría alguien que
# acaba de agregar la baja lógica al modelo y está recorriendo las consultas que tocan categorías.
perl -0pi -e 's/(\n\s*)(\.GroupBy\(m => new)/$1.Where(m => m.Categoria!.Activa)$1$2/' "$CONSULTA"
grep -q 'm.Categoria!.Activa' "$CONSULTA" || {
  echo "ERROR: no se pudo colar el filtro por \`activa\` en la consulta del resumen." >&2
  echo "       El script quedó mirando un código que ya no existe: actualizá verificar-desglose.sh." >&2
  exit 1
}

# Compila ANTES de mirar los tests, y no es celo de más: `dotnet test` devuelve 1 tanto si los
# tests fallaron como si el proyecto no compiló, así que sin esta distinción un desarme que genera
# código inválido se cuenta como rojo válido y la barrera termina diciendo EN PIE sin verificar
# nada. Es la misma guarda que tiene verificar-aislamiento.sh, y por el mismo motivo.
if ! dotnet build "$RAIZ/backend/GestionGastos.slnx" --nologo --verbosity quiet > /dev/null 2>&1; then
  echo "ERROR: se puso el filtro y el proyecto dejó de COMPILAR." >&2
  echo "       El rojo tiene que venir del test, no del compilador: actualizá la sustitución." >&2
  exit 1
fi

if correr_tests > /dev/null 2>&1; then
  echo "ERROR: el desglose filtra por \`activa\` y la barrera pasó igual." >&2
  echo "       Ese test no está verificando nada. Es exactamente la deuda D6-04 volviendo:" >&2
  echo "       los totales históricos pueden cambiar solos y nadie se entera." >&2
  exit 1
fi
echo "   rojo, como se esperaba"
restaurar

echo "== 3/3 · restaurado tiene que volver al verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: se restauró la consulta y la barrera sigue en rojo." >&2
  echo "       Fijate cómo quedó $CONSULTA" >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera del desglose: EN PIE. Sabe detectar que la consulta del resumen empiece a filtrar por"
echo "\`categoria.activa\`, que es lo que haría cambiar solos los totales de meses ya cerrados."

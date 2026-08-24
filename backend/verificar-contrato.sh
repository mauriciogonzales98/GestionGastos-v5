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
# Corre `dotnet test` tres veces, así que tarda ~90 s. Va al final de la puerta.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTRATO="$RAIZ/frontend/src/api/tipos.ts"
RESPALDO="$(mktemp)"
FILTRO='FullyQualifiedName~GestionGastos.Api.Tests.Contrato'

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. Los tests de contrato levantan la API de verdad." >&2
  exit 1
fi

# El archivo se restaura pase lo que pase: un fallo a mitad de camino no puede dejar el contrato
# desalineado en el árbol de trabajo.
restaurar() {
  cp "$RESPALDO" "$CONTRATO"
  rm -f "$RESPALDO"
}
trap restaurar EXIT

cp "$CONTRATO" "$RESPALDO"

correr_tests() {
  dotnet test "$RAIZ/backend/GestionGastos.slnx" --filter "$FILTRO" --nologo --verbosity quiet
}

echo "== 1/3 · el contrato alineado tiene que estar en verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: los tests de contrato ya fallan sin tocar nada. Arreglá eso antes de medir la barrera." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 2/3 · con el contrato desalineado tiene que ponerse en ROJO"
# Un rename coherente, que es exactamente el caso que D-09 describe: el frontend seguiría
# compilando y `tsc` no diría nada, pero la pantalla leería undefined.
sed -i 's/^  nombre: string;/  nombreCategoria: string;/' "$CONTRATO"

if ! grep -q 'nombreCategoria' "$CONTRATO"; then
  echo "ERROR: no se pudo desalinear el contrato. El archivo cambió de forma y este script quedó viejo." >&2
  exit 1
fi

if correr_tests > /dev/null 2>&1; then
  echo "ERROR: el contrato está desalineado y los tests pasaron igual." >&2
  echo "       La barrera no sirve: no detecta lo único que tiene que detectar." >&2
  exit 1
fi
echo "   rojo, como se esperaba"

echo "== 3/3 · restaurado tiene que volver al verde"
cp "$RESPALDO" "$CONTRATO"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: el contrato quedó restaurado pero los tests siguen en rojo." >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera del contrato: EN PIE. Sabe fallar cuando el contrato se desalinea."

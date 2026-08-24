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
CASOS=(
  'un rename coherente en una respuesta (Categoria.nombre)|s/^  nombre: string;/  nombreCategoria: string;/|nombreCategoria'
  'un rename en las dos peticiones (NuevaCuenta y Credenciales)|s/^  contrasena: string;/  clave: string;/|clave: string;'
  'un rename en la respuesta de sesión (SesionActual.email)|/export interface SesionActual/,/^}/ s/^  email: string;/  correo: string;/|correo: string;'
)

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

  if correr_tests > /dev/null 2>&1; then
    echo "ERROR: el contrato está desalineado ($descripcion) y los tests pasaron igual." >&2
    echo "       La barrera no sirve: no detecta lo único que tiene que detectar." >&2
    exit 1
  fi
  echo "   rojo, como se esperaba"

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

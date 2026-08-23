#!/usr/bin/env bash
#
# La barrera del linter del backend (Principio V de la constitución).
#
# El linter es configuración, y una barrera de configuración se puede desarmar en silencio: alguien
# pone EnforceCodeStyleInBuild en false, o borra una línea de .editorconfig, y el build sigue verde.
# Nada avisa. Este script es lo único que se pone en rojo cuando eso pasa.
#
# Verifica las DOS direcciones, porque las dos se pueden romper:
#   1. una violación deliberada en código escrito a mano TIENE que romper el build
#   2. la misma violación dentro de Migrations/ NO tiene que romperlo — es código generado por EF,
#      y analizarlo produce hallazgos que se regeneran solos. Si esta exclusión se extendiera de
#      más, dejaría de analizarse código que sí es nuestro.
#
# Compila con un archivo temporal adentro, así que va DESPUÉS de los tests: invalidaría su
# --no-build.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUCION="$RAIZ/backend/GestionGastos.slnx"
A_MANO="$RAIZ/backend/GestionGastos.Api/BarreraDelLinter.temporal.cs"
GENERADO="$RAIZ/backend/GestionGastos.Api/Migrations/BarreraDelLinter.temporal.cs"

# CA1707: los identificadores no llevan guión bajo. Está activa en el código de producción y
# apagada sólo en el proyecto de tests, así que sirve como violación de prueba.
violacion() {
  cat <<'CS'
namespace GestionGastos.Api;

/// <summary>
/// Archivo temporal de verificar-linter.sh. Si lo encontrás commiteado, el script murió a mitad de
/// camino: borralo. El guión bajo del nombre viola CA1707 a propósito.
/// </summary>
public class Clase_Con_Guion_Bajo
{
    public int Valor { get; set; }
}
CS
}

limpiar() {
  rm -f "$A_MANO" "$GENERADO"
}
trap limpiar EXIT

compila() {
  dotnet build "$SOLUCION" -warnaserror --nologo --verbosity quiet > /dev/null 2>&1
}

echo "== 1/3 · el árbol limpio tiene que compilar"
limpiar
if ! compila; then
  echo "ERROR: el build ya falla sin tocar nada. Arreglá eso antes de medir la barrera." >&2
  exit 1
fi
echo "   compila, como se esperaba"

echo "== 2/3 · una violación en código escrito a mano tiene que ROMPER el build"
violacion > "$A_MANO"
if compila; then
  echo "ERROR: hay una violación de CA1707 en código escrito a mano y el build pasó igual." >&2
  echo "       El linter está apagado. Revisá Directory.Build.props y .editorconfig." >&2
  exit 1
fi
rm -f "$A_MANO"
echo "   rompió, como se esperaba"

echo "== 3/3 · la misma violación dentro de Migrations/ NO tiene que romperlo"
violacion > "$GENERADO"
if ! compila; then
  echo "ERROR: una violación dentro de Migrations/ rompió el build." >&2
  echo "       La exclusión de código generado dejó de aplicar: cada migración nueva va a traer" >&2
  echo "       hallazgos que no se pueden arreglar sin editar código que EF regenera." >&2
  exit 1
fi
rm -f "$GENERADO"
echo "   no rompió, como se esperaba"

echo
echo "Barrera del linter: EN PIE. Rompe donde tiene que romper y calla donde tiene que callar."

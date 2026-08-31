#!/usr/bin/env bash
#
# La barrera del aislamiento entre cuentas (Principio V de la constitución).
#
# `AislamientoEntreCuentasTests` y `BarreraDeAislamientoTests` comprueban que ninguna cuenta ve ni
# toca los datos de otra. Que pasen sólo prueba que HOY el aislamiento está puesto: no prueba que
# esos tests sepan detectar que se caiga. Un test de aislamiento roto se ve exactamente igual que
# uno que funciona — devuelve verde, y sigue devolviendo verde el día que deja de verificar nada.
#
# Este script desarma el aislamiento a propósito de las cuatro formas en que se puede desarmar,
# exige el ROJO en cada una, restaura y exige el verde.
#
# Las cuatro formas no son intercambiables:
#   · la consulta deja de acotar por cuenta   → una cuenta ve los movimientos de todas
#   · una lectura nace fuera del canal único  → nadie la está mirando, y nace sin acotar
#   · una lectura nace DENTRO del archivo que  → el mismo olvido, en el único lugar donde la
#     la barrera exime por escribir              barrera no estaba mirando (FEAT-001b, D-01)
#   · el alta asigna un propietario ajeno     → lo que escribo cae en la cuenta de otro
#
# Va después del paso de Tests en el CI, como las otras tres barreras: recompila con archivos
# modificados, así que correrlo antes invalidaría su `--no-build`.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONSULTA="$RAIZ/backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs"
ALTA="$RAIZ/backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs"
COLADO="$RAIZ/backend/GestionGastos.Api/LecturaColada.temporal.cs"
FILTRO='FullyQualifiedName~Aislamiento'

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. La barrera levanta la aplicación de verdad." >&2
  exit 1
fi

RESPALDO="$(mktemp -d)"
cp "$CONSULTA" "$RESPALDO/consulta.cs"
cp "$ALTA" "$RESPALDO/alta.cs"

restaurar() {
  cp "$RESPALDO/consulta.cs" "$CONSULTA"
  cp "$RESPALDO/alta.cs" "$ALTA"
  rm -f "$COLADO"
}
trap 'restaurar; rm -rf "$RESPALDO"' EXIT

correr_tests() {
  dotnet test "$RAIZ/backend/GestionGastos.slnx" --filter "$FILTRO" --nologo --verbosity quiet
}

exigir_rojo() {
  local que="$1"

  # Compila ANTES de mirar los tests, y no es celo de más.
  #
  # `dotnet test` devuelve 1 tanto si los tests fallaron como si el proyecto no compiló, así que sin
  # esta distinción un desarme que genera código inválido se cuenta como rojo válido: la barrera
  # imprime "rojo, como se esperaba" en los tres pasos, termina diciendo EN PIE, y desde ese momento
  # no verifica nada. Es la falla que esta barrera existe para prevenir, ocurriendo adentro de ella.
  #
  # Pasa el día que alguien retoque una de las sustituciones `perl` de acá abajo — al agregar el
  # `PUT` de FEAT-001b, por ejemplo — y la deje generando algo que no compila.
  if ! dotnet build "$RAIZ/backend/GestionGastos.slnx" --nologo --verbosity quiet > /dev/null 2>&1; then
    echo "ERROR: $que y el proyecto dejó de COMPILAR." >&2
    echo "       El rojo tiene que venir de los tests, no del compilador. La sustitución del script" >&2
    echo "       generó código inválido: actualizá verificar-aislamiento.sh." >&2
    exit 1
  fi

  if correr_tests > /dev/null 2>&1; then
    echo "ERROR: $que y la suite de aislamiento pasó igual." >&2
    echo "       Esos tests no están verificando el aislamiento: revisalos antes de confiar en ellos." >&2
    exit 1
  fi
  echo "   rojo, como se esperaba"
}

echo "== 1/6 · con el aislamiento puesto, la barrera tiene que estar en verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: la barrera ya falla sin tocar nada. Arreglá eso antes de medirla." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 2/6 · sin el acotado por cuenta tiene que ponerse en ROJO"
# Se le quita `m.UsuarioId == usuarioId` al WHERE del listado.
perl -0pi -e 's/\.Where\(m => m\.UsuarioId == usuarioId && /.Where(m => /' "$CONSULTA"
grep -q 'm.UsuarioId == usuarioId' "$CONSULTA" && {
  echo "ERROR: no se pudo quitar el acotado; el script quedó mirando un código que ya no existe." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "se quitó el acotado por cuenta de la consulta del listado"
restaurar

echo "== 3/6 · con una lectura fuera del canal tiene que ponerse en ROJO"
cat > "$COLADO" <<'CS'
using GestionGastos.Api.Persistencia;

namespace GestionGastos.Api;

/// <summary>
/// Archivo temporal de verificar-aislamiento.sh. Si lo encontrás commiteado, el script murió a
/// mitad de camino: borralo. Lee movimientos fuera del canal único a propósito.
/// </summary>
public static class LecturaColadaTemporal
{
    public static IQueryable<Dominio.Movimiento> TodosSinAcotar(GestionGastosDbContext contexto) =>
        contexto.Movimientos;
}
CS
exigir_rojo "apareció una lectura de movimientos fuera del canal único"
restaurar

echo "== 4/6 · con una lectura sin acotar DENTRO del archivo exento tiene que ponerse en ROJO"
# La barrera exime a MovimientosEndpoints.cs por ser la escritura declarada. Hasta FEAT-001b esa
# exención era por archivo entero, y el archivo sólo hacía un INSERT — que no tiene a quién dejar
# de acotar. La edición trae leer-modificar-guardar, y ese "encontrar primero" es justo la lectura
# que puede nacer sin acotar, en el único lugar donde la barrera no estaba mirando (D-01).
perl -0pi -e 's/(rutas\.MapPost\("\/api\/movimientos", async \()/rutas.MapGet("\/api\/movimientos\/coladas", async (GestionGastosDbContext ctx) =>\n            await ctx.Movimientos.ToListAsync());\n\n        $1/' "$ALTA"
grep -q 'api/movimientos/coladas' "$ALTA" || {
  echo "ERROR: no se pudo colar la lectura sin acotar en el archivo exento." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "apareció una lectura sin acotar dentro del archivo que la barrera exime por escribir"
restaurar

echo "== 5/6 · con el alta asignando un propietario ajeno tiene que ponerse en ROJO"
# El alta deja de tomar el propietario de la sesión y usa cualquier otra cuenta.
perl -0pi -e 's/UsuarioId = usuarioActual\.Id,/UsuarioId = await contexto.Usuarios.Where(u => u.Id != usuarioActual.Id).Select(u => u.Id).FirstOrDefaultAsync() is var otro \&\& otro != 0 ? otro : usuarioActual.Id,/' "$ALTA"
grep -q 'UsuarioId = usuarioActual.Id,' "$ALTA" && {
  echo "ERROR: no se pudo desarmar la asignación del propietario en el alta." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "el alta dejó de tomar el propietario de la sesión"
restaurar

echo "== 6/6 · restaurado tiene que volver al verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: se restauró todo y la barrera sigue en rojo." >&2
  echo "       Fijate si quedó algún archivo temporal: $COLADO" >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera de aislamiento: EN PIE. Sabe detectar una consulta que no acota por cuenta, una"
echo "lectura fuera del canal, una lectura sin acotar dentro del archivo exento, y un alta que le"
echo "pone dueño ajeno a lo que escribe."

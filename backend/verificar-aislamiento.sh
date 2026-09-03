#!/usr/bin/env bash
#
# La barrera del aislamiento entre cuentas (Principio V de la constitución).
#
# `AislamientoEntreCuentasTests` y `BarreraDeAislamientoTests` comprueban que ninguna cuenta ve ni
# toca los datos de otra. Que pasen sólo prueba que HOY el aislamiento está puesto: no prueba que
# esos tests sepan detectar que se caiga. Un test de aislamiento roto se ve exactamente igual que
# uno que funciona — devuelve verde, y sigue devolviendo verde el día que deja de verificar nada.
#
# Este script desarma el aislamiento a propósito de las siete formas en que se puede desarmar,
# exige el ROJO en cada una, restaura y exige el verde.
#
# Las siete formas no son intercambiables:
#   · la consulta deja de acotar por cuenta   → una cuenta ve los movimientos de todas
#   · una lectura nace fuera del canal único  → nadie la está mirando, y nace sin acotar
#   · una lectura nace DENTRO del archivo que  → el mismo olvido, en el único lugar donde la
#     la barrera exime por escribir              barrera no estaba mirando (FEAT-001b, D-01)
#   · una lectura del canal NO devuelve       → la barrera enumeraba por forma de retorno, así que
#     movimientos sino sumas                    a ésta ni la miraba (FEAT-001c, D-01)
#   · una lectura del canal EJECUTA adentro   → enumerar por `IQueryable` a secas corría la misma
#     y devuelve el resultado                   condición un casillero: ésta tampoco aparecía
#   · una consulta del canal de CATEGORÍAS    → una cuenta ve las categorías privadas de las
#     deja de acotar por ámbito                  demás (FEAT-007, D-03)
#   · el alta asigna un propietario ajeno     → lo que escribo cae en la cuenta de otro
#
# La de categorías llega con la feature 007 y es de otra clase que las anteriores: no es una
# condición que caducó, es una tabla que hasta entonces no tenía nada que aislar. Las diez
# categorías eran de todo el mundo, así que una consulta sin acotar no devolvía nada de nadie. El
# día que cada cuenta tuvo las suyas, la barrera seguía mirando sólo movimientos — comprobado: un
# `TodasSinAcotar(contexto)` en el canal de categorías la dejaba en 4/4 verde.
#
# Las dos del medio son la MISMA caducidad encontrada dos veces: mientras el descubrimiento filtre
# por la forma del retorno, siempre va a haber una forma más que no está en la lista. Por eso la
# barrera dejó de filtrar y ahora enumera todo, y por eso estos dos pasos se quedan: son los que
# prueban que no volvió a filtrar.
#
# Va después del paso de Tests en el CI, como las otras tres barreras: recompila con archivos
# modificados, así que correrlo antes invalidaría su `--no-build`.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONSULTA="$RAIZ/backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs"
ALTA="$RAIZ/backend/GestionGastos.Api/Movimientos/MovimientosEndpoints.cs"
CATEGORIAS="$RAIZ/backend/GestionGastos.Api/Categorias/CategoriasConsulta.cs"
COLADO="$RAIZ/backend/GestionGastos.Api/LecturaColada.temporal.cs"
FILTRO='FullyQualifiedName~Aislamiento'

if [[ -z "${ConnectionStrings__Default:-}" ]]; then
  echo "ERROR: falta ConnectionStrings__Default. La barrera levanta la aplicación de verdad." >&2
  exit 1
fi

RESPALDO="$(mktemp -d)"
cp "$CONSULTA" "$RESPALDO/consulta.cs"
cp "$ALTA" "$RESPALDO/alta.cs"
cp "$CATEGORIAS" "$RESPALDO/categorias.cs"

restaurar() {
  cp "$RESPALDO/consulta.cs" "$CONSULTA"
  cp "$RESPALDO/alta.cs" "$ALTA"
  cp "$RESPALDO/categorias.cs" "$CATEGORIAS"
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

echo "== 1/9 · con el aislamiento puesto, la barrera tiene que estar en verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: la barrera ya falla sin tocar nada. Arreglá eso antes de medirla." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 2/9 · sin el acotado por cuenta tiene que ponerse en ROJO"
# Se le quita `m.UsuarioId == usuarioId` al WHERE de TODAS las consultas del canal.
#
# El /g no es cosmético: desde FEAT-001b el canal tiene dos consultas acotadas —el listado y la
# lectura por identificador— y desarmar sólo la primera dejaría la otra en pie. Además la guarda de
# abajo exige que no quede ninguna, así que sin el /g el script se acusa a sí mismo de no haber
# podido desarmar.
perl -0pi -e 's/\.Where\(m => m\.UsuarioId == usuarioId && /.Where(m => /g' "$CONSULTA"
grep -q 'm.UsuarioId == usuarioId' "$CONSULTA" && {
  echo "ERROR: no se pudo quitar el acotado; el script quedó mirando un código que ya no existe." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "se quitó el acotado por cuenta de la consulta del listado"
restaurar

echo "== 3/9 · con una lectura fuera del canal tiene que ponerse en ROJO"
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

echo "== 4/9 · con una lectura sin acotar DENTRO del archivo exento tiene que ponerse en ROJO"
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

echo "== 5/9 · con una agregación del canal que no acota tiene que ponerse en ROJO"
# La barrera enumera las consultas del canal por reflexión, y hasta FEAT-001c las enumeraba por su
# forma de retorno: `IQueryable<Movimiento>`. Eso cubría el canal entero mientras toda lectura
# devolviera movimientos. El resumen es la primera que devuelve SUMAS, y una agregación sin acotar
# no era una consulta que la barrera aprobara mal — era una que ni siquiera enumeraba (D-01).
#
# Es la misma clase de caducidad que el paso 4/8, por otra vía: allá era una exención por archivo
# que dejó de alcanzar, acá una condición de tipo. Por eso son dos pasos y no uno.
perl -0pi -e 's/(    public static IQueryable<Movimiento> PropioPorId\()/    public static IQueryable<decimal> TotalSinAcotar(GestionGastosDbContext contexto) =>\n        contexto.Movimientos.GroupBy(m => m.MonedaId).Select(g => g.Sum(m => m.Monto));\n\n$1/' "$CONSULTA"
grep -q 'TotalSinAcotar' "$CONSULTA" || {
  echo "ERROR: no se pudo colar la agregación sin acotar en el canal." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "apareció en el canal una agregación que no acota por cuenta"
restaurar

echo "== 6/9 · con una consulta del canal que EJECUTA adentro tiene que ponerse en ROJO"
# El paso 5/8 cubre la agregación que sale del canal SIN ejecutar. Ésta ejecuta adentro y devuelve
# el resultado ya calculado: `Task<decimal>` con un `SumAsync`, que es el paso siguiente natural de
# quien escribe una agregación.
#
# Es la tercera vez que la misma condición caduca, y por eso ahora la barrera no filtra por el
# retorno: enumera TODOS los métodos públicos estáticos y hace fallar al que no sepa inspeccionar.
# Sin este paso, ensanchar de `IQueryable<Movimiento>` a `IQueryable` se vería igual de bien que
# sacar la condición, y no es lo mismo — la versión ensanchada dejaba pasar esta consulta en verde,
# y la otra mitad de la barrera tampoco la veía porque el canal está exento del escaneo por archivo.
perl -0pi -e 's/(    public static IQueryable<Movimiento> PropioPorId\()/    public static Task<decimal> TotalEjecutadoSinAcotar(GestionGastosDbContext contexto, RangoDeFechas rango) =>\n        contexto.Movimientos.Where(m => m.Fecha >= rango.Desde).SumAsync(m => m.Monto);\n\n$1/' "$CONSULTA"
perl -0pi -e 's/(using GestionGastos\.Api\.Persistencia;\n)/$1using Microsoft.EntityFrameworkCore;\n/' "$CONSULTA"
grep -q 'TotalEjecutadoSinAcotar' "$CONSULTA" || {
  echo "ERROR: no se pudo colar en el canal la consulta que ejecuta adentro." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "apareció en el canal una consulta que ejecuta adentro y no acota por cuenta"
restaurar

echo "== 7/9 · con el canal de CATEGORÍAS sin acotar por ámbito tiene que ponerse en ROJO"
# La feature 007 le dio categorías propias a cada cuenta, y con eso `contexto.Categorias` sin
# condición pasó a devolver las privadas de todas. Se le vacía el WHERE a `DelAmbito`, que es el
# único lugar donde el acotado de categorías se escribe.
#
# La sustitución reemplaza el CUERPO entero del método en vez de recortarle un predicado literal:
# el predicado del ámbito no es `usuario_id = @yo` a secas —una categoría puede ser de nadie— y va
# a seguir cambiando de forma. Un recorte literal caducaría en silencio, que es justo lo que estos
# pasos existen para no dejar pasar.
perl -0pi -e 's/(private static IQueryable<Categoria> DelAmbito\([^)]*\) =>\s*\n)\s*contexto\.Categorias[^;]*;/$1        contexto.Categorias;/s' "$CATEGORIAS"
grep -qE '^\s*contexto\.Categorias;\s*$' "$CATEGORIAS" || {
  echo "ERROR: no se pudo quitar el acotado por ámbito del canal de categorías." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "una consulta del canal de categorías dejó de acotar por ámbito"
restaurar

echo "== 8/9 · con el alta asignando un propietario ajeno tiene que ponerse en ROJO"
# El alta deja de tomar el propietario de la sesión y usa cualquier otra cuenta.
perl -0pi -e 's/UsuarioId = usuarioActual\.Id,/UsuarioId = await contexto.Usuarios.Where(u => u.Id != usuarioActual.Id).Select(u => u.Id).FirstOrDefaultAsync() is var otro \&\& otro != 0 ? otro : usuarioActual.Id,/' "$ALTA"
grep -q 'UsuarioId = usuarioActual.Id,' "$ALTA" && {
  echo "ERROR: no se pudo desarmar la asignación del propietario en el alta." >&2
  echo "       Actualizá la sustitución de verificar-aislamiento.sh." >&2
  exit 1
}
exigir_rojo "el alta dejó de tomar el propietario de la sesión"
restaurar

echo "== 9/9 · restaurado tiene que volver al verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: se restauró todo y la barrera sigue en rojo." >&2
  echo "       Fijate si quedó algún archivo temporal: $COLADO" >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera de aislamiento: EN PIE. Sabe detectar una consulta que no acota por cuenta, una"
echo "lectura fuera del canal, una lectura sin acotar dentro del archivo exento, una agregación del"
echo "canal que no acota aunque no devuelva movimientos, una consulta del canal que ejecuta adentro"
echo "y devuelve el resultado ya calculado, una consulta del canal de categorías que deja de acotar"
echo "por ámbito, y un alta que le pone dueño ajeno a lo que escribe."

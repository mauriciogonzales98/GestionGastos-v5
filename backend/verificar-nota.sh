#!/usr/bin/env bash
#
# La barrera de la nota (Principio V de la constitución). La séptima del proyecto.
#
# `BarreraDeLaNotaTests` exige dos cosas: que el listado SELECCIONE la nota y NO acote por ella, y
# que la consulta del resumen no la mencione en absoluto. Que pase sólo prueba que hoy no se filtra:
# no prueba que sepa detectarlo el día que alguien lo agregue.
#
# Y esta barrera necesita el trato tanto como la del desglose, por dos razones que se suman:
#
#   1. Es una afirmación de AUSENCIA hecha inspeccionando texto. Informa verde cuando la ausencia es
#      real y cuando la inspección dejó de encontrar nada — un cambio de forma en el SQL, un recorte
#      del WHERE que deja de ubicarlo, un nombre de columna distinto. Las dos cosas se ven igual.
#   2. El cambio que impide es un REFLEJO, no un descuido. "Obvio que uno querría buscar en las
#      notas" es lo primero que piensa cualquiera que lea el listado, igual que filtrar por
#      `categoria.activa` era el reflejo de quien acababa de sumar la baja lógica al modelo. Los
#      daños silenciosos que este proyecto ya se comió vinieron todos de un reflejo razonable.
#
# Lo que protege es `PRD:RF-33` y `FR-007`: la nota es DESCRIPTIVA, no clasificatoria. Una nota libre
# que se puede filtrar se vuelve una segunda taxonomía informal —"alquiler", "Alquiler", "alq"— que
# el sistema no entiende y que da una falsa sensación de estar clasificando. La categoría es el único
# eje de análisis.
#
# **Es más fina que la del desglose, y de ahí que se la desarme de dos formas.** Aquella puede exigir
# que una palabra no aparezca en ningún lugar del SQL; acá la nota TIENE que aparecer, porque el
# listado la muestra, y lo que no puede es aparecer en el WHERE. Una comprobación que distingue dónde
# aparece algo tiene una manera más de romperse en silencio que una que sólo pregunta si aparece.
#
# El script desarma la protección de TRES formas —el acotado en el listado, el orden del listado y el
# agrupamiento del resumen—, exige el ROJO en cada una, restaura y exige el verde.
#
# La del orden se agregó tras la revisión del PR #29, y es la que prueba el punto de este script mejor
# que ninguna: hasta entonces la barrera cubría dos tercios de FR-007 afirmando cubrirlo entero, porque
# el recorte que aísla el WHERE corta justamente en el ORDER BY. Ordenar por la nota caía en el
# fragmento descartado y el test informaba verde. Se descubrió desarmándolo.
#
# Recompila con el archivo modificado, así que va DESPUÉS de los tests: invalidaría su --no-build.

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONSULTA="$RAIZ/backend/GestionGastos.Api/Movimientos/MovimientosConsulta.cs"
FILTRO='FullyQualifiedName~BarreraDeLaNota'

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

# Compila antes de mirar los tests, y no es celo de más: `dotnet test` devuelve 1 tanto si los tests
# fallaron como si el proyecto no compiló, así que sin esta distinción un desarme que genera código
# inválido se cuenta como rojo válido y la barrera termina diciendo EN PIE sin verificar nada. Es la
# misma guarda que tienen verificar-desglose.sh y verificar-aislamiento.sh.
exigir_que_compile() {
  if ! dotnet build "$RAIZ/backend/GestionGastos.slnx" --nologo --verbosity quiet > /dev/null 2>&1; then
    echo "ERROR: se desarmó la protección y el proyecto dejó de COMPILAR." >&2
    echo "       El rojo tiene que venir del test, no del compilador: actualizá la sustitución." >&2
    exit 1
  fi
}

echo "== 1/5 · sin desarmar nada, la barrera tiene que estar en verde"
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: la barrera ya falla sin tocar nada. Arreglá eso antes de medirla." >&2
  exit 1
fi
echo "   verde, como se esperaba"

echo "== 2/5 · con el listado acotando por la nota tiene que ponerse en ROJO"
# Se le cuela el acotado en `DeLaCuenta`, al lado de los que ya están, que es exactamente donde lo
# escribiría alguien que viene de agregar el acotado por moneda y sigue el patrón de la línea de
# arriba. No hace falta que el parámetro exista: se filtra contra un literal, que es lo que alguien
# probaría primero y basta para que la columna aparezca en el WHERE.
perl -0pi -e 's/(== monedaId\));/$1\n            .Where(m => m.Nota != null);/' "$CONSULTA"
grep -q 'm.Nota != null' "$CONSULTA" || {
  echo "ERROR: no se pudo colar el acotado por la nota en la consulta del listado." >&2
  echo "       El script quedó mirando un código que ya no existe: actualizá verificar-nota.sh." >&2
  exit 1
}
exigir_que_compile

if correr_tests > /dev/null 2>&1; then
  echo "ERROR: el listado acota por la nota y la barrera pasó igual." >&2
  echo "       Ese test no está verificando nada, y lo que deja pasar es que la nota se vuelva un" >&2
  echo "       eje de clasificación — la segunda taxonomía informal que PRD:RF-33 evita." >&2
  exit 1
fi
echo "   rojo, como se esperaba"
restaurar

echo "== 3/5 · con el listado ORDENANDO por la nota tiene que ponerse en ROJO"
# El desarme que faltaba. Se le agrega un desempate por nota al orden del listado, que es donde lo
# escribiría alguien que quiere "agrupar visualmente las notas parecidas" sin darse cuenta de que eso
# es clasificar. FR-007 prohíbe el orden además del acotado, y hasta el PR #29 nadie lo verificaba.
perl -0pi -e 's/(\.OrderByDescending\(m => m\.Fecha\))/$1\n            .ThenBy(m => m.Nota)/' "$CONSULTA"
grep -q 'ThenBy(m => m.Nota)' "$CONSULTA" || {
  echo "ERROR: no se pudo colar el orden por la nota en la consulta del listado." >&2
  echo "       El script quedó mirando un código que ya no existe: actualizá verificar-nota.sh." >&2
  exit 1
}
exigir_que_compile

if correr_tests > /dev/null 2>&1; then
  echo "ERROR: el listado ordena por la nota y la barrera pasó igual." >&2
  echo "       Es el agujero que la revisión del PR #29 encontró: el recorte del WHERE corta en el" >&2
  echo "       ORDER BY, así que ordenar por la nota queda en el fragmento que nadie mira." >&2
  exit 1
fi
echo "   rojo, como se esperaba"
restaurar

echo "== 4/5 · con el resumen agrupando por la nota tiene que ponerse en ROJO"
# El otro desarme, y no es el mismo caso: acá la nota entra en el GROUP BY del resumen. El daño es
# silencioso —dos movimientos de la misma categoría con notas distintas dejan de sumar juntos, y el
# resumen de un mes ya cerrado da otro número— y lo detecta la otra mitad de la barrera, la que
# exige la ausencia TOTAL de la nota en esa consulta.
perl -0pi -e 's/(\n\s*)(m\.CategoriaId,\n\s*CategoriaNombre = m\.Categoria!\.Nombre,)/$1$2$1                m.Nota,/' "$CONSULTA"
grep -q 'm.Nota,' "$CONSULTA" || {
  echo "ERROR: no se pudo colar la nota en el agrupamiento del resumen." >&2
  echo "       El script quedó mirando un código que ya no existe: actualizá verificar-nota.sh." >&2
  exit 1
}
exigir_que_compile

if correr_tests > /dev/null 2>&1; then
  echo "ERROR: el resumen agrupa por la nota y la barrera pasó igual." >&2
  echo "       Es el daño silencioso de NFR-002: los totales de un mes ya cerrado pasan a dar otro" >&2
  echo "       número sin que nadie haya tocado un movimiento." >&2
  exit 1
fi
echo "   rojo, como se esperaba"
restaurar

echo "== 5/5 · restaurado tiene que volver al verde"
exigir_que_compile
if ! correr_tests > /dev/null 2>&1; then
  echo "ERROR: se restauró la consulta y la barrera sigue en rojo." >&2
  echo "       Fijate cómo quedó $CONSULTA" >&2
  exit 1
fi
echo "   verde de nuevo"

echo
echo "Barrera de la nota: EN PIE. Sabe detectar las TRES formas de que la nota empiece a clasificar:"
echo "que el listado acote por ella, que el listado la ordene y que el resumen la agrupe. La nota"
echo "describe; la categoría clasifica (PRD:RF-33, FR-007)."

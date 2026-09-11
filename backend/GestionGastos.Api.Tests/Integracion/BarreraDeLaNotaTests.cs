using GestionGastos.Api.Dominio;
using GestionGastos.Api.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// **La nota no clasifica: no se filtra ni se agrupa por ella, y nunca puede empezar a hacerse**
/// (`FR-007`, `SC-007` de la feature 012).
///
/// Es la decisión de producto central del ticket 2 y viene de `PRD:RF-33`, que la explica: una nota
/// libre que se pudiera filtrar se convierte en una segunda taxonomía informal —"alquiler",
/// "Alquiler", "alq"— que el sistema no entiende y que da una falsa sensación de estar clasificando.
/// La categoría sigue siendo el único eje de análisis.
///
/// **Por qué hace falta una barrera y no alcanza con no escribirlo.** Agregar un acotado por nota es
/// el primer reflejo de cualquiera que lea el listado —"obvio que uno querría buscar en las notas"—,
/// igual que filtrar por `categoria.activa` era el reflejo de quien acababa de sumar la baja lógica.
/// Los daños silenciosos que este proyecto ya se comió vinieron todos de un reflejo razonable.
///
/// **Y la comprobación tiene la forma peligrosa**: es una afirmación de AUSENCIA hecha inspeccionando
/// texto, así que informa verde de las dos maneras — cuando la ausencia es real y cuando la
/// inspección dejó de encontrar nada. Ésa es exactamente la razón por la que existen las barreras de
/// este proyecto, y por la que `backend/verificar-nota.sh` le prueba a ésta que sabe ponerse en rojo.
///
/// **Es más fina que la del desglose, y de ahí su dificultad.** `BarreraDelDesgloseTests` puede
/// exigir que la palabra `activa` no aparezca en ningún lugar del SQL. Acá no: la nota **tiene** que
/// aparecer, porque el listado la muestra. Lo que no puede es aparecer en el `WHERE` ni en el
/// `ORDER BY`. Así que la afirmación es "está en la proyección, y no en el filtro ni en el orden", y
/// las tres partes importan.
///
/// **La del orden faltaba hasta la revisión del PR #29**, y es la mejor ilustración de por qué una
/// comprobación así necesita que se la vea fallar: el recorte que aísla el `WHERE` corta justamente en
/// el `ORDER BY`, así que ordenar por la nota caía en el fragmento descartado y la barrera informaba
/// verde. Cubría dos tercios de `FR-007` afirmando cubrirlo entero.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class BarreraDeLaNotaTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// El listado **selecciona** la nota y **no la filtra**.
    ///
    /// La primera mitad no es decorativa: si la consulta dejara de traer la nota, el SQL no la
    /// nombraría y la segunda comprobación pasaría en verde sin vigilar nada — el mismo mecanismo por
    /// el que la barrera del desglose exige que el `JOIN` contra `categoria` siga estando.
    /// </summary>
    [Fact]
    public void El_Listado_Selecciona_La_Nota_Y_No_Filtra_Por_Ella_FR007()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var sql = ConsultaDelListado(contexto);

        Assert.True(
            NombraLaNota(sql),
            "El SQL del listado ya ni siquiera nombra `nota`: la consulta cambió de forma y esta " +
            "barrera quedó mirando algo que no existe. La comprobación de abajo pasaría en verde sin " +
            "vigilar nada.\n\nSQL:\n" + sql);

        Assert.False(
            NombraLaNota(DondeDe(sql)),
            "El listado empezó a acotar por la nota.\n\n" +
            "Es el primer reflejo de cualquiera que lea esta pantalla, y es exactamente lo que " +
            "`PRD:RF-33` decide no hacer: la nota es DESCRIPTIVA, no clasificatoria. Una nota libre " +
            "que se puede filtrar se vuelve una segunda taxonomía informal —\"alquiler\", " +
            "\"Alquiler\", \"alq\"— que el sistema no entiende y que da una falsa sensación de estar " +
            "clasificando. La categoría es el único eje de análisis (`FR-007`).\n\n" +
            "Si aparece la necesidad real de agrupar por algo más fino que la categoría, se resuelve " +
            "con un catálogo de etiquetas y su decisión de producto, no estirando la nota.\n\nSQL:\n" +
            sql);

        // **La tercera mitad, y faltaba.** `FR-007` dice que la nota no participa de ningún acotado,
        // ORDEN ni total, y esta barrera sólo miraba el acotado: el recorte de `DondeDe` corta
        // justamente en el `ORDER BY`, así que un `.ThenBy(m => m.Nota)` dejaba la columna en el
        // fragmento descartado y la barrera pasaba en verde. Lo encontró la revisión del PR #29, y se
        // comprobó desarmándolo: el listado ordenaba por la nota y los dos casos de arriba pasaban.
        //
        // Ordenar por la nota no crea una taxonomía como la crea filtrar, pero la convierte en un eje
        // de navegación, que es el primer paso hacia lo mismo.
        Assert.False(
            NombraLaNota(OrdenDe(sql)),
            "El listado empezó a ordenar por la nota.\n\n" +
            "`FR-007` no prohíbe sólo filtrar: dice que la nota no participa de ningún acotado, ORDEN " +
            "ni total. El orden del listado es por fecha y, para desempatar, por id — eso es `D-04` de " +
            "la feature 001 y no tiene nada que ver con lo que la nota dice.\n\nSQL:\n" + sql);
    }

    /// <summary>
    /// El resumen **no menciona la nota en absoluto**.
    ///
    /// Acá sí se puede exigir la ausencia total, y es la diferencia con el listado: el desglose no
    /// tiene ningún motivo legítimo para tocar la nota. Ni la selecciona, ni la agrupa, ni la suma.
    ///
    /// Es la otra mitad de `NFR-002`, y la que protege contra el daño silencioso: si la nota entrara
    /// en el `GROUP BY`, dos movimientos de la misma categoría con notas distintas dejarían de sumar
    /// juntos y el resumen de un mes ya cerrado pasaría a dar otro número sin que nadie tocara un
    /// movimiento. Es el mismo mecanismo que `verificar-desglose.sh` vigila para `categoria.activa`.
    /// </summary>
    [Fact]
    public void El_Resumen_No_Menciona_La_Nota_NFR002()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var sql = MovimientosConsulta
            .Agrupado(contexto, usuarioId: 1, RangoDelMes.De(new DateOnly(2026, 9, 10)))
            .ToQueryString();

        Assert.False(
            NombraLaNota(sql),
            "La consulta del resumen empezó a mencionar la nota.\n\n" +
            "No tiene ningún motivo legítimo para tocarla: no la muestra, no la suma y no la agrupa. " +
            "Si entrara en el GROUP BY, dos movimientos de la misma categoría con notas distintas " +
            "dejarían de sumar juntos, y el resumen de un mes ya cerrado pasaría a dar otro número " +
            "sin que nadie hubiera tocado un movimiento (`NFR-002`, `PRD:AC-07`).\n\nSQL:\n" + sql);
    }

    /// <summary>
    /// El SQL del listado, con los cuatro acotados pedidos.
    ///
    /// **Se piden los cuatro a propósito**: con los acotados en `null` el `WHERE` que EF genera es
    /// mínimo, y la barrera estaría inspeccionando una consulta más pobre que la que corre de verdad.
    /// Los valores no importan — se mira el SQL, no filas.
    /// </summary>
    private static string ConsultaDelListado(Persistencia.GestionGastosDbContext contexto) =>
        MovimientosConsulta
            .Filtrado(
                contexto,
                usuarioId: 1,
                RangoDelMes.De(new DateOnly(2026, 9, 10)),
                categoriaId: 1,
                monedaId: 1)
            .ToQueryString();

    /// <summary>
    /// La parte del SQL que **acota**: desde el `WHERE` hasta el `ORDER BY`.
    ///
    /// Recortar así es lo que permite distinguir "la nota se muestra" de "la nota se filtra", que es
    /// la única distinción que importa acá y la que hace a esta barrera más delicada que la del
    /// desglose. Si el SQL dejara de tener `WHERE` —o el recorte dejara de encontrarlo—, esto
    /// devolvería una cadena vacía y la comprobación pasaría en verde sin mirar nada: por eso la
    /// primera mitad del test exige que el SQL completo sí nombre la nota, y por eso existe
    /// `verificar-nota.sh`.
    /// </summary>
    private static string DondeDe(string sql)
    {
        var inicio = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);

        Assert.True(
            inicio >= 0,
            "El SQL del listado no tiene `WHERE`. O la consulta dejó de acotar —lo que sería un " +
            "problema mucho peor que el que esta barrera vigila, porque el acotado por cuenta vive " +
            "ahí— o este recorte dejó de saber leerla.\n\nSQL:\n" + sql);

        var fin = sql.IndexOf("ORDER BY", inicio, StringComparison.OrdinalIgnoreCase);
        return fin < 0 ? sql[inicio..] : sql[inicio..fin];
    }

    /// <summary>
    /// La parte del SQL que **ordena**: desde el `ORDER BY` hasta el final.
    ///
    /// Es el fragmento que <see cref="DondeDe"/> descarta, y por eso existe: mientras nadie lo
    /// miraba, ordenar por la nota era invisible para esta barrera.
    ///
    /// A diferencia del `WHERE`, el `ORDER BY` **puede no estar** —una consulta sin orden es legítima,
    /// aunque el listado siempre lo pida (D-04 de la feature 001)—, así que su ausencia devuelve vacío
    /// en vez de fallar. Lo que sí fallaría ruidosamente es que el listado dejara de ordenar, y eso lo
    /// cubren los tests del listado, no esta barrera.
    /// </summary>
    private static string OrdenDe(string sql)
    {
        var inicio = sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        return inicio < 0 ? string.Empty : sql[inicio..];
    }

    /// <summary>
    /// Si ese fragmento de SQL nombra la columna de la nota.
    ///
    /// Busca el nombre de la COLUMNA y no la palabra suelta: `nota` aparecería también dentro de un
    /// literal o de un alias, y esta barrera tiene que hablar de la columna.
    /// </summary>
    private static bool NombraLaNota(string sql) =>
        sql.Contains("`nota`", StringComparison.OrdinalIgnoreCase);
}

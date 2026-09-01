using System.Reflection;
using System.Text.RegularExpressions;
using GestionGastos.Api.Dominio;
using GestionGastos.Api.Movimientos;
using GestionGastos.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// La barrera del aislamiento (FR-004 del ticket 01c, reformulación de su AC-10).
///
/// Los tests cruzados de <see cref="AislamientoEntreCuentasTests"/> ya detectan que el acotado
/// **actual** desaparezca: si se borra, el listado de una cuenta devuelve los movimientos de la
/// otra y esos tests caen solos. Lo que no detectan es una consulta **nueva** que nadie acote —
/// no saben que existe—, y ése es el descuido que va a pasar el día que alguien agregue el séptimo
/// endpoint.
///
/// Por eso hay dos tests y no uno: el primero vigila la condición, el segundo vigila el canal.
/// Y por eso existe `backend/verificar-aislamiento.sh`, que le prueba a esta barrera que sabe
/// ponerse en rojo — una barrera que nunca se vio fallar no es una barrera (Principio V).
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class BarreraDeAislamientoTests(BaseDeDatosFixture baseDeDatos)
{
    /// <summary>
    /// El único archivo de producción que puede LEER <c>contexto.Movimientos</c>.
    ///
    /// Se nombra, no se descubre: una barrera que deduce sus propias excepciones aprende a
    /// aceptarlas.
    /// </summary>
    private const string CanalDeLectura = "Movimientos/MovimientosConsulta.cs";

    /// <summary>
    /// El archivo donde las ESCRITURAS de movimientos son legítimas: agregar, modificar y borrar
    /// filas. El acotado por cuenta no aplica a un INSERT, y el UPDATE y el DELETE trabajan sobre
    /// una entidad que ya vino acotada del canal.
    ///
    /// **La exención es por operación, no por archivo, y eso cambió en FEAT-001b.** Antes era por
    /// archivo entero, y era segura porque acá adentro sólo había un INSERT: un INSERT no tiene a
    /// quién dejar de acotar. La edición trajo leer-modificar-guardar, y ese "encontrar primero" es
    /// justo la lectura que puede nacer sin acotar — en el único lugar donde esta barrera no estaba
    /// mirando.
    ///
    /// Comprobado antes de estrecharla: un <c>MapGet</c> que devolvía
    /// <c>contexto.Movimientos.ToListAsync()</c> —los movimientos de TODAS las cuentas— escrito acá
    /// adentro compilaba y dejaba la barrera en 4/4 verde. No era un error de quien la escribió:
    /// era una condición que caducó cuando cambió lo que este archivo hace.
    ///
    /// `verificar-aislamiento.sh` tiene el paso 4/7 que le prueba el rojo por esta vía.
    /// </summary>
    private const string EscrituraDeclarada = "Movimientos/MovimientosEndpoints.cs";

    /// <summary>
    /// Lo único que <see cref="EscrituraDeclarada"/> puede hacer con <c>Movimientos</c>.
    ///
    /// Se nombran las tres operaciones y no se acepta cualquier cosa: la diferencia entre
    /// "este archivo escribe movimientos" y "este archivo hace lo que quiera con movimientos" es
    /// exactamente el agujero que se cerró.
    /// </summary>
    private static readonly string[] EscriturasPermitidas = ["Add", "Update", "Remove"];

    /// <summary>
    /// Donde el <c>DbSet</c> se DECLARA, que no es lo mismo que leerlo.
    ///
    /// `GestionGastosDbContext` contiene `DbSet&lt;Movimiento&gt; Movimientos =&gt;
    /// Set&lt;Movimiento&gt;()`: es la definición del conjunto, no una consulta sobre él, y no tiene
    /// dónde acotar por cuenta. Excluirlo no abre ningún hueco — una consulta escrita ahí adentro
    /// sería tan visible como rara.
    /// </summary>
    private const string DeclaracionDelDbSet = "Persistencia/GestionGastosDbContext.cs";

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// La consulta del listado acota por <c>usuario_id</c>, y se comprueba sobre el SQL que genera.
    ///
    /// Mirar el resultado no alcanza: el índice <c>(usuario_id, fecha, id)</c> hace que MySQL
    /// devuelva filas que parecen correctas por motivos que no son la consulta. Es el mismo motivo
    /// por el que `ListadoMovimientosTests` verifica el orden en dos capas.
    /// </summary>
    [Fact]
    public void La_Consulta_Del_Listado_Acota_Por_Cuenta_En_El_Sql()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var sql = MovimientosConsulta
            // El id no importa: este test sólo mira el SQL que se genera, no filas.
            .Filtrado(contexto, usuarioId: 1, RangoDelMes.De(new DateOnly(2026, 8, 15)))
            .ToQueryString();

        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);

        var donde = sql[sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..];

        Assert.True(
            donde.Contains("usuario_id", StringComparison.OrdinalIgnoreCase),
            "La consulta del listado dejó de acotar por cuenta: su WHERE no nombra `usuario_id`. " +
            "Con eso, el listado de una cuenta devuelve los movimientos de todas.\n\nSQL:\n" + sql);
    }

    /// <summary>
    /// **Todas** las consultas del canal acotan por cuenta, no sólo la del listado.
    ///
    /// El test de arriba mira `Filtrado` y nada más, y el del canal mira quién lee movimientos
    /// **afuera**. Entre los dos quedaba un hueco justo en el peor lugar: adentro. El mensaje de
    /// error de la barrera del canal empuja a agregar las consultas nuevas acá —"la salida es
    /// agregar el método a `MovimientosConsulta`"—, y hasta ahora eso las metía al único sitio
    /// donde nadie las miraba. Comprobado: un `TodosLosDelMes(contexto, rango)` sin `usuario_id`
    /// pasaba la suite en verde.
    ///
    /// Se descubren por reflexión y no por una lista: una lista hay que acordarse de actualizarla,
    /// que es la misma clase de olvido que esta barrera existe para atrapar.
    ///
    /// **Qué se descubre cambió en la feature 006, y el motivo importa.** Hasta acá el filtro era
    /// `IQueryable&lt;Movimiento&gt;`, y cubría el canal entero porque toda lectura escrita hasta
    /// entonces devolvía movimientos. El resumen es la primera que devuelve **sumas**: una
    /// agregación sin acotar no era una consulta que la barrera aprobara mal, era una que ni
    /// siquiera enumeraba. Comprobado antes de ensancharlo, igual que en FEAT-001b: un
    /// `TotalDeTodasLasCuentas(contexto)` que agrupa `contexto.Movimientos` sin `usuario_id` dejaba
    /// la barrera en 4/4 verde.
    ///
    /// Es la segunda vez que una condición de esta barrera caduca en silencio al cambiar lo que
    /// tiene que cubrir —la primera fue la exención por archivo de FEAT-001b—, así que conviene
    /// decirlo acá: **lo que se vigila es el canal, no una forma de retorno.** Cualquier
    /// `IQueryable`, devuelva lo que devuelva, sale de leer movimientos de alguien.
    ///
    /// `verificar-aislamiento.sh` tiene el paso 5/7 que le prueba el rojo por esta vía.
    /// </summary>
    [Fact]
    public void Todas_Las_Consultas_Del_Canal_Acotan_Por_Cuenta()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var consultas = typeof(MovimientosConsulta)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => typeof(IQueryable).IsAssignableFrom(m.ReturnType))
            .ToList();

        Assert.NotEmpty(consultas);

        foreach (var consulta in consultas)
        {
            var sql = ((IQueryable)consulta.Invoke(null, ArgumentosDePrueba(consulta, contexto))!)
                .ToQueryString();

            var donde = sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase)
                ? sql[sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..]
                : string.Empty;

            Assert.True(
                donde.Contains("usuario_id", StringComparison.OrdinalIgnoreCase),
                $"`MovimientosConsulta.{consulta.Name}` no acota por cuenta: su SQL no nombra " +
                "`usuario_id` en el WHERE. Una consulta de movimientos sin acotar devuelve los de " +
                $"todas las cuentas.\n\nSQL:\n{sql}");
        }
    }

    /// <summary>
    /// Los argumentos con los que invocar una consulta del canal, por tipo.
    ///
    /// Los valores no importan —sólo se mira el SQL que se genera, no filas—, pero el método tiene
    /// que poder invocarse. Si aparece un parámetro de un tipo que no está acá, el test falla
    /// diciéndolo en vez de pasar de largo: una consulta que la barrera no puede invocar es una
    /// consulta que la barrera no está mirando.
    /// </summary>
    private static object?[] ArgumentosDePrueba(MethodInfo consulta, GestionGastosDbContext contexto) =>
        [.. consulta.GetParameters().Select(object? (p) => p.ParameterType switch
        {
            var t when t == typeof(GestionGastosDbContext) => contexto,
            var t when t == typeof(long) => 1L,
            var t when t == typeof(RangoDeFechas) => RangoDelMes.De(new DateOnly(2026, 8, 15)),
            var t when t == typeof(DateOnly) => new DateOnly(2026, 8, 15),
            var t when t == typeof(int) => 1,

            // Un filtro opcional se ejercita CON valor, no con null: con null el predicado se
            // simplifica y el SQL que se inspecciona deja de ser el que corre en producción cuando
            // alguien filtra de verdad.
            var t when t == typeof(int?) => (int?)1,
            var t when t == typeof(string) => "x",
            _ => throw new InvalidOperationException(
                $"`MovimientosConsulta.{consulta.Name}` tiene un parámetro de tipo " +
                $"{p.ParameterType.Name} que esta barrera no sabe construir. Agregalo acá: sin eso, " +
                "esa consulta queda sin vigilar y el aislamiento depende de que alguien se acuerde."),
        })];

    /// <summary>
    /// Ningún archivo de producción lee <c>contexto.Movimientos</c> fuera del canal único.
    ///
    /// Es la mitad de la barrera que protege lo que todavía no se escribió. Vigilar la condición
    /// cuida la consulta que existe; vigilar el canal cuida la que alguien agregue el mes que viene
    /// sin acordarse de acotarla, que es la que va a fallar porque nadie la va a estar mirando.
    ///
    /// **Si este test te frena al agregar una consulta legítima, la salida es agregar el método al
    /// canal, no ampliar la lista de excepciones.**
    /// </summary>
    [Fact]
    public void Ninguna_Lectura_De_Movimientos_Vive_Fuera_Del_Canal()
    {
        var raiz = RaizDelProyectoDeProduccion();

        var infractores = Directory
            .EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(archivo => !EstaEnMigraciones(raiz, archivo))
            .Where(archivo => !EsRutaDeclarada(raiz, archivo))
            .Where(archivo => UsaElDbSetDeMovimientos(
                EsLaEscrituraDeclarada(raiz, archivo)
                    ? SinLasEscriturasPermitidas(File.ReadAllText(archivo))
                    : File.ReadAllText(archivo)))
            .Select(archivo => Path.GetRelativePath(raiz, archivo).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            infractores.Count == 0,
            "Estos archivos LEEN `contexto.Movimientos` fuera del canal único " +
            $"(`{CanalDeLectura}`):\n  " +
            string.Join("\n  ", infractores) +
            "\n\nUna lectura de movimientos que no pase por el canal es una que nadie está " +
            "mirando, y el acotado por cuenta se olvida escribiéndola. La salida es agregar el " +
            "método a `MovimientosConsulta`, no sumar una excepción acá.\n\n" +
            $"`{EscrituraDeclarada}` puede ESCRIBIR movimientos —" +
            string.Join(", ", EscriturasPermitidas.Select(o => $"`.Movimientos.{o}(`")) +
            "— y nada más. Si aparece ahí, es porque lee.");
    }

    /// <summary>
    /// El canal sigue existiendo y sigue siendo el que la otra mitad de la barrera vigila.
    ///
    /// Sin esto, borrar `MovimientosConsulta` y esparcir las consultas dejaría el test de arriba en
    /// verde por vacuidad: sin archivos que lo usen, no hay infractores.
    /// </summary>
    [Fact]
    public void El_Canal_De_Lectura_Existe_Y_Se_Usa()
    {
        var canal = Path.Combine(RaizDelProyectoDeProduccion(), CanalDeLectura);

        Assert.True(
            File.Exists(canal),
            $"No existe `{CanalDeLectura}`. La barrera del canal quedaría en verde sin vigilar nada.");

        Assert.True(
            UsaElDbSetDeMovimientos(File.ReadAllText(canal)),
            $"`{CanalDeLectura}` ya no lee `contexto.Movimientos`: el canal se vació y las " +
            "consultas se mudaron a algún lado que esta barrera no está mirando.");
    }

    /// <summary>
    /// <c>true</c> si el texto usa el <c>DbSet</c> de movimientos.
    ///
    /// **El receptor no se nombra a propósito.** La primera versión buscaba
    /// <c>(contexto|context|db)\.Movimientos</c> y era ciega a cualquier prefijo: un campo privado
    /// <c>_contexto</c> —que es la convención de este repositorio, la misma de <c>_baseDeDatos</c>—
    /// pasaba de largo, y con él pasaba toda la barrera. También se le escapaba
    /// <c>contexto.Set&lt;Movimiento&gt;()</c>, que llega al mismo DbSet por otra puerta.
    ///
    /// Lo que sí hay que excluir es el namespace <c>GestionGastos.Api.Movimientos</c>, que aparece
    /// en un <c>using</c> o un <c>namespace</c> de casi todos estos archivos: sin el
    /// <c>(?&lt;!Api)</c>, la barrera se dispararía en todos lados y se terminaría apagando.
    /// <c>MovimientosConsulta</c> y <c>MapMovimientos()</c> no matchean por el <c>\b</c> y por el
    /// punto, respectivamente.
    /// </summary>
    private static bool UsaElDbSetDeMovimientos(string codigo) =>
        Regex.IsMatch(
            codigo,
            @"(?<!Api)\.\s*Movimientos\b|\bSet\s*<\s*Movimiento\s*>",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

    /// <summary><c>true</c> si el archivo es aquel donde las escrituras son legítimas.</summary>
    private static bool EsLaEscrituraDeclarada(string raiz, string archivo) =>
        Path.GetRelativePath(raiz, archivo).Replace('\\', '/') == EscrituraDeclarada;

    /// <summary>
    /// El código sin sus escrituras permitidas, para que lo que quede se pueda mirar como se mira
    /// cualquier otro archivo.
    ///
    /// Se borran los usos de la forma <c>.Movimientos.Add(</c> —y `Update` y `Remove`— y se deja
    /// todo lo demás intacto. Si después de sacarlos sigue habiendo un <c>.Movimientos</c>, ese uso
    /// no es una escritura declarada: es una lectura, y tiene que ir al canal.
    ///
    /// Se recorta la operación y no la línea entera: borrar la línea escondería una lectura escrita
    /// al lado de una escritura legítima.
    /// </summary>
    private static string SinLasEscriturasPermitidas(string codigo) =>
        EscriturasPermitidas.Aggregate(codigo, (texto, operacion) => Regex.Replace(
            texto,
            @"\.\s*Movimientos\s*\.\s*" + operacion + @"\s*\(",
            "(",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)));

    private static bool EsRutaDeclarada(string raiz, string archivo)
    {
        var relativa = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
        return relativa == CanalDeLectura
            || relativa == DeclaracionDelDbSet;
    }

    /// <summary>
    /// `Migrations/` queda fuera: lo genera EF, nadie lo escribe a mano, y no consulta movimientos
    /// en nombre de ninguna cuenta. Es la misma excepción que hace la barrera del linter.
    /// </summary>
    private static bool EstaEnMigraciones(string raiz, string archivo) =>
        Path.GetRelativePath(raiz, archivo).Replace('\\', '/').StartsWith("Migrations/", StringComparison.Ordinal);

    /// <summary>
    /// La carpeta de `GestionGastos.Api`, encontrada subiendo desde el binario de los tests.
    ///
    /// Se busca por el `.csproj` y no con una ristra de `..`: así el test no se rompe si cambia la
    /// profundidad de la carpeta de salida.
    /// </summary>
    private static string RaizDelProyectoDeProduccion()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null &&
               !File.Exists(Path.Combine(directorio.FullName, "GestionGastos.slnx")))
        {
            directorio = directorio.Parent;
        }

        Assert.NotNull(directorio);

        var api = Path.Combine(directorio.FullName, "GestionGastos.Api");

        Assert.True(Directory.Exists(api), $"No se encontró el proyecto de producción en {api}.");
        return api;
    }
}

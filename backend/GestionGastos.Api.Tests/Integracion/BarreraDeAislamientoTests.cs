using System.Reflection;
using System.Text.RegularExpressions;
using GestionGastos.Api.Categorias;
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
/// Por eso no hay un test sino dos por tabla: uno vigila la condición —que las consultas del canal
/// acoten— y otro vigila el canal —que nadie lea por afuera—. Las categorías tuvieron sólo el
/// primero desde la feature 007 hasta que se saldó D7-05, y esa mitad que faltaba es la que
/// protege lo que todavía no se escribió.
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
    /// `verificar-aislamiento.sh` tiene el paso 4/10 que le prueba el rojo por esta vía.
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
    /// El único archivo de producción que puede LEER <c>contexto.Categorias</c>.
    ///
    /// Llega con la deuda D7-05 de la feature 007, que dejó a las categorías con **media** barrera:
    /// se vigilaba que las consultas del canal acotaran, y no que alguien leyera por fuera de él.
    /// </summary>
    private const string CanalDeCategorias = "Categorias/CategoriasConsulta.cs";

    /// <summary>
    /// El archivo donde las ESCRITURAS de categorías son legítimas.
    ///
    /// Sólo <c>Add</c>, y no por prudencia: el renombre y la baja trabajan sobre una entidad que ya
    /// vino acotada del canal y se guardan con <c>SaveChangesAsync</c>, así que no tocan el
    /// <c>DbSet</c>. Nombrar las tres operaciones acá cuando sólo se usa una sería declarar un
    /// permiso que nadie pidió.
    /// </summary>
    private const string EscrituraDeCategoriasDeclarada = "Categorias/CategoriasEndpoints.cs";

    /// <summary>Lo único que <see cref="EscrituraDeCategoriasDeclarada"/> puede hacer con el DbSet.</summary>
    private static readonly string[] EscriturasDeCategoriasPermitidas = ["Add"];

    /// <summary>
    /// El SEGUNDO archivo que puede escribir categorías: el catálogo inicial de una cuenta nueva.
    ///
    /// Llega con la feature 013, que le entrega a cada cuenta sus diez categorías al registrarse.
    /// Ese alta tiene que escribir diez filas, y hasta acá el único autorizado era el endpoint de
    /// categorías.
    ///
    /// **Es un archivo propio y no una autorización a <c>CuentasEndpoints</c>**, que es quien lo
    /// llama. Declarar a `CuentasEndpoints` le abriría el <c>DbSet</c> de categorías a un archivo
    /// que además valida el email, hashea la contraseña y atrapa el 1062 del alta duplicada — y esa
    /// autorización quedaría vigente para todo lo que ese archivo haga en el futuro. Un archivo de
    /// una sola responsabilidad mantiene la excepción del tamaño de lo que efectivamente hace falta
    /// (D-06 de la feature 013).
    ///
    /// Agregar un archivo nombrado, con su motivo escrito, es mantenimiento de la barrera: sigue
    /// saltando ante el archivo siguiente. Lo que la desarmaría es una excepción genérica o un
    /// patrón ensanchado para que el código nuevo entre solo.
    /// </summary>
    private const string CatalogoInicialDeclarado = "Categorias/CatalogoInicial.cs";

    /// <summary>
    /// Lo que <see cref="CatalogoInicialDeclarado"/> puede hacer con el DbSet.
    ///
    /// <c>AddRange</c> además de <c>Add</c>: entrega las diez de una vez, en una sola operación y
    /// dentro del mismo <c>SaveChanges</c> que crea la cuenta (FR-003).
    /// </summary>
    private static readonly string[] EscriturasDelCatalogoInicialPermitidas = ["Add", "AddRange"];

    /// <summary>Los archivos que pueden ESCRIBIR categorías, cada uno con lo que puede hacer.</summary>
    private static readonly (string Archivo, string[] Operaciones)[] EscritoresDeCategorias =
    [
        (EscrituraDeCategoriasDeclarada, EscriturasDeCategoriasPermitidas),
        (CatalogoInicialDeclarado, EscriturasDelCatalogoInicialPermitidas),
    ];

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
    /// decirlo acá: **lo que se vigila es el canal, no una forma de retorno.**
    ///
    /// **Y por eso el descubrimiento dejó de filtrar por el retorno.** Ensanchar de
    /// `IQueryable&lt;Movimiento&gt;` a `IQueryable` corría la misma condición un casillero en vez de
    /// sacarla: un método que **ejecuta adentro** —`Task&lt;decimal&gt;` con un `SumAsync`, que es el
    /// paso siguiente natural de una agregación— no devuelve `IQueryable`, así que tampoco lo
    /// enumeraba. Comprobado igual que las otras dos veces: un
    /// `TotalDeTodasLasCuentas(contexto, rango)` que suma `contexto.Movimientos` sin `usuario_id`
    /// dejaba la barrera en 4/4 verde, y la otra mitad tampoco lo veía porque este archivo está
    /// exento del escaneo por ser el canal.
    ///
    /// Ahora se enumeran **todos** los métodos públicos estáticos y el que no devuelva un
    /// `IQueryable` hace fallar el test diciéndolo, en lugar de saltearse. Es la misma forma que ya
    /// tenía <see cref="ArgumentosDePrueba"/> para un parámetro de tipo desconocido: una consulta
    /// que la barrera no sabe inspeccionar es una consulta que la barrera no está mirando, y eso se
    /// grita, no se omite.
    ///
    /// `verificar-aislamiento.sh` tiene los pasos 5/10 y 6/10 que le prueban el rojo por estas dos
    /// vías: la que sale sin ejecutar y la que ejecuta adentro.
    /// </summary>
    [Fact]
    public void Todas_Las_Consultas_Del_Canal_Acotan_Por_Cuenta()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var consultas = typeof(MovimientosConsulta)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .ToList();

        Assert.NotEmpty(consultas);

        foreach (var consulta in consultas)
        {
            Assert.True(
                typeof(IQueryable).IsAssignableFrom(consulta.ReturnType),
                $"`MovimientosConsulta.{consulta.Name}` devuelve `{consulta.ReturnType.Name}`, que " +
                "no es un `IQueryable`: esta barrera inspecciona el SQL de la consulta ANTES de que " +
                "se ejecute, y de un resultado ya ejecutado no puede leer nada. Una consulta que la " +
                "barrera no sabe inspeccionar es una que no está mirando, y ahí el acotado por " +
                "cuenta vuelve a depender de que alguien se acuerde.\n\nLa salida es devolver el " +
                "`IQueryable` y ejecutarlo en quien lo pide, no agregar una excepción acá.");

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
    /// **Todas** las consultas del canal de categorías acotan por ámbito.
    ///
    /// Es la misma vigilancia que la de movimientos, sobre otra tabla y con otro predicado, y llega
    /// con la feature 007 porque hasta ella no había nada que aislar: las diez categorías eran de
    /// todo el mundo, así que una consulta sin acotar no devolvía nada de nadie. Desde que cada
    /// cuenta tiene las suyas, `contexto.Categorias` sin condición devuelve las privadas de todas.
    ///
    /// **El predicado NO se comparte con el de movimientos, y eso es deliberado** (D-03): una
    /// categoría puede ser de nadie —`usuario_id IS NULL` son las predefinidas del sistema, que se
    /// ven desde todas las cuentas— así que su acotado no es `usuario_id = @yo` a secas. Lo que se
    /// comparte es la vigilancia: el SQL tiene que nombrar `usuario_id` en el WHERE, y cómo lo
    /// nombra lo decide el canal.
    ///
    /// Comprobado antes de escribirla, igual que las tres veces anteriores: un
    /// `TodasSinAcotar(contexto)` que devuelve `contexto.Categorias` entero dejaba esta barrera en
    /// 4/4 verde. No era un descuido de quien la escribió — era una condición que nunca había
    /// tenido que existir.
    ///
    /// `verificar-aislamiento.sh` tiene el paso 7/10 que le prueba el rojo por esta vía.
    /// </summary>
    [Fact]
    public void Todas_Las_Consultas_Del_Canal_De_Categorias_Acotan_Por_Ambito()
    {
        using var contexto = _baseDeDatos.CrearContexto();

        var consultas = typeof(CategoriasConsulta)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .ToList();

        Assert.NotEmpty(consultas);

        foreach (var consulta in consultas)
        {
            Assert.True(
                typeof(IQueryable).IsAssignableFrom(consulta.ReturnType),
                $"`CategoriasConsulta.{consulta.Name}` devuelve `{consulta.ReturnType.Name}`, que " +
                "no es un `IQueryable`: esta barrera inspecciona el SQL de la consulta ANTES de que " +
                "se ejecute, y de un resultado ya ejecutado no puede leer nada. Una consulta que la " +
                "barrera no sabe inspeccionar es una que no está mirando, y ahí el acotado por " +
                "ámbito vuelve a depender de que alguien se acuerde.\n\nLa salida es devolver el " +
                "`IQueryable` y ejecutarlo en quien lo pide, no agregar una excepción acá.");

            var sql = ((IQueryable)consulta.Invoke(null, ArgumentosDePrueba(consulta, contexto))!)
                .ToQueryString();

            var donde = sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase)
                ? sql[sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..]
                : string.Empty;

            Assert.True(
                donde.Contains("usuario_id", StringComparison.OrdinalIgnoreCase),
                $"`CategoriasConsulta.{consulta.Name}` no acota por ámbito: su SQL no nombra " +
                "`usuario_id` en el WHERE. Una consulta de categorías sin acotar devuelve también " +
                $"las privadas de las demás cuentas.\n\nSQL:\n{sql}");
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
            var t when t == typeof(TipoMovimiento) => TipoMovimiento.Gasto,
            _ => throw new InvalidOperationException(
                $"`{consulta.DeclaringType!.Name}.{consulta.Name}` tiene un parámetro de tipo " +
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
        var infractores = LecturasFueraDelCanal(
            "Movimientos",
            "Movimiento",
            CanalDeLectura,
            (EscrituraDeclarada, EscriturasPermitidas));

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
    public void El_Canal_De_Lectura_Existe_Y_Se_Usa() =>
        ExigirQueElCanalExistaYSeUse("Movimientos", "Movimiento", CanalDeLectura);

    /// <summary>
    /// Ningún archivo de producción lee <c>contexto.Categorias</c> fuera del canal único.
    ///
    /// **Es la mitad que faltaba, y la deuda D7-05 la daba por inofensiva.** Su texto decía que hoy
    /// no había ninguna lectura de categorías fuera del canal, así que lo único que faltaba era
    /// proteger lo que alguien escribiera el mes que viene. Había **dos**, las dos en
    /// <c>Movimientos/MovimientosEndpoints.cs</c>: la que busca la categoría al dar de alta un
    /// movimiento y la que la busca al editarlo. Estaban bien acotadas —las dos llevaban
    /// <c>usuario_id</c> escrito a mano— así que no había ningún dato expuesto; lo que no había era
    /// nada que las obligara a seguir estándolo. Se mudaron al canal antes de que este test pasara
    /// a verde, que es la salida que la barrera de movimientos viene predicando desde FEAT-001b:
    /// se agrega el método al canal, no la excepción a la barrera.
    ///
    /// **Lo que este escaneo NO ve, dicho para que nadie le confíe de más** (hallazgo 2 de la
    /// revisión del PR #35): las lecturas por **propiedad de navegación**.
    /// <c>MovimientosConsulta</c> proyecta <c>m.Categoria!.Nombre</c>, que es un JOIN contra
    /// <c>categoria</c> y llega a filas de esa tabla sin pasar por <c>CategoriasConsulta</c>; el
    /// regex mira <c>.Categorias</c> en plural y no puede verlo. **Hoy es seguro y no por
    /// casualidad**: esa consulta ya viene acotada por <c>usuario_id</c> sobre movimientos, así que
    /// sólo alcanza las categorías que los movimientos propios referencian. Pero eso vale mientras
    /// ningún movimiento pueda apuntar a una categoría ajena, y esa invariante la sostienen las dos
    /// comprobaciones de <c>MovimientosEndpoints</c> —con su test cruzado
    /// <c>Un_Movimiento_No_Puede_Apuntar_A_Una_Categoria_Ajena_FR021_SC009</c>— y **ninguna
    /// restricción de esquema**. Ensanchar el escaneo a las navegaciones sería perseguir la vía
    /// equivocada: lo que protege ese flanco es la invariante, no el canal.
    /// </summary>
    [Fact]
    public void Ninguna_Lectura_De_Categorias_Vive_Fuera_Del_Canal()
    {
        var infractores = LecturasFueraDelCanal(
            "Categorias",
            "Categoria",
            CanalDeCategorias,
            EscritoresDeCategorias);

        Assert.True(
            infractores.Count == 0,
            "Estos archivos LEEN `contexto.Categorias` fuera del canal único " +
            $"(`{CanalDeCategorias}`):\n  " +
            string.Join("\n  ", infractores) +
            "\n\nUna lectura de categorías que no pase por el canal es una que nadie está " +
            "mirando, y el acotado por ámbito se olvida escribiéndola: `contexto.Categorias` sin " +
            "condición devuelve también las privadas de las demás cuentas. La salida es agregar el " +
            "método a `CategoriasConsulta`, no sumar una excepción acá.\n\n" +
            "Los archivos que pueden ESCRIBIR categorías son:\n  " +
            string.Join(
                "\n  ",
                EscritoresDeCategorias.Select(e =>
                    $"`{e.Archivo}` — " +
                    string.Join(", ", e.Operaciones.Select(o => $"`.Categorias.{o}(`")))) +
            "\n\ny nada más. Si uno de ésos aparece en la lista de arriba, es porque además LEE.");
    }

    /// <summary>El canal de categorías sigue existiendo y sigue siendo el que se vigila.</summary>
    [Fact]
    public void El_Canal_De_Categorias_Existe_Y_Se_Usa() =>
        ExigirQueElCanalExistaYSeUse("Categorias", "Categoria", CanalDeCategorias);

    /// <summary>
    /// Los archivos de producción que usan el <c>DbSet</c> indicado fuera de su canal.
    ///
    /// **Está parametrizado porque son dos vigilancias con la misma forma y distinto predicado**,
    /// que es exactamente lo que ya pasaba con las dos comprobaciones por reflexión de más arriba.
    /// Lo que cambia entre movimientos y categorías es qué DbSet se mira, cuál es su canal y qué
    /// escrituras se le permiten al archivo que escribe; el recorrido y las exenciones
    /// estructurales —`Migrations/`, la declaración del DbSet— son los mismos, y duplicarlos
    /// dejaría dos copias que hay que acordarse de arreglar juntas.
    /// </summary>
    private static List<string> LecturasFueraDelCanal(
        string dbSet,
        string entidad,
        string canal,
        params (string Archivo, string[] Operaciones)[] escritores)
    {
        var raiz = RaizDelProyectoDeProduccion();

        // Cada escritor declara SUS operaciones, no las de todos. Con una sola lista compartida,
        // autorizar `AddRange` para el catálogo inicial se lo habría autorizado de paso al endpoint
        // de categorías, que nunca lo pidió — y una barrera que reparte permisos que nadie pidió es
        // una barrera que se afloja sola.
        string SinSusEscrituras(string relativa, string codigo) =>
            escritores.FirstOrDefault(e => e.Archivo == relativa) is { Operaciones: not null } escritor
                ? SinLasEscriturasPermitidas(codigo, dbSet, escritor.Operaciones)
                : codigo;

        return [.. Directory
            .EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(archivo => !EsCodigoGenerado(raiz, archivo))
            .Where(archivo => Relativa(raiz, archivo) != canal)
            .Where(archivo => Relativa(raiz, archivo) != DeclaracionDelDbSet)
            .Where(archivo => UsaElDbSet(
                SinSusEscrituras(Relativa(raiz, archivo), File.ReadAllText(archivo)),
                dbSet,
                entidad))
            .Select(archivo => Relativa(raiz, archivo))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// El canal existe y sigue leyendo su <c>DbSet</c>.
    ///
    /// Sin esto, borrar el canal y esparcir las consultas dejaría el escaneo en verde por vacuidad:
    /// sin archivos que lo usen, no hay infractores.
    /// </summary>
    private static void ExigirQueElCanalExistaYSeUse(string dbSet, string entidad, string canal)
    {
        var ruta = Path.Combine(RaizDelProyectoDeProduccion(), canal);

        Assert.True(
            File.Exists(ruta),
            $"No existe `{canal}`. La barrera del canal quedaría en verde sin vigilar nada.");

        Assert.True(
            UsaElDbSet(File.ReadAllText(ruta), dbSet, entidad),
            $"`{canal}` ya no lee `contexto.{dbSet}`: el canal se vació y las consultas se " +
            "mudaron a algún lado que esta barrera no está mirando.");
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
    private static bool UsaElDbSet(string codigo, string dbSet, string entidad) =>
        Regex.IsMatch(
            codigo,
            @"(?<!Api)\.\s*" + dbSet + @"\b|\bSet\s*<\s*" + entidad + @"\s*>",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

    /// <summary>La ruta del archivo relativa a la raíz, con barras normales.</summary>
    private static string Relativa(string raiz, string archivo) =>
        Path.GetRelativePath(raiz, archivo).Replace('\\', '/');

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
    private static string SinLasEscriturasPermitidas(
        string codigo,
        string dbSet,
        string[] escriturasPermitidas) =>
        escriturasPermitidas.Aggregate(codigo, (texto, operacion) => Regex.Replace(
            texto,
            @"\.\s*" + dbSet + @"\s*\.\s*" + operacion + @"\s*\(",
            "(",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)));

    /// <summary>
    /// Las carpetas de código GENERADO, que quedan fuera del escaneo.
    ///
    /// `Migrations/` lo genera EF, nadie lo escribe a mano, y no consulta en nombre de ninguna
    /// cuenta. Es la misma excepción que hace la barrera del linter.
    ///
    /// `obj/` y `bin/` son la misma clase de cosa y estaban adentro por descuido: el recorrido es
    /// sobre la carpeta del proyecto entera, así que barría también los `.cs` que deja la
    /// compilación. Hoy son tres y ninguno nombra un `DbSet`, así que la barrera estaba verde por
    /// suerte y no por construcción. **Comprobado**: un archivo con `contexto.Categorias` adentro
    /// de `obj/Debug/` la ponía en rojo, y el mensaje pedía mover al canal código que nadie
    /// escribió y nadie puede mover. El escenario que lo vuelve real es
    /// `dotnet ef dbcontext optimize` —el modelo compilado, que es el paso de rendimiento estándar
    /// de EF— y ese día la única salida visible sería apagar la barrera.
    /// </summary>
    private static bool EsCodigoGenerado(string raiz, string archivo)
    {
        var relativa = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');

        return relativa.StartsWith("Migrations/", StringComparison.Ordinal)
            || relativa.StartsWith("obj/", StringComparison.Ordinal)
            || relativa.StartsWith("bin/", StringComparison.Ordinal);
    }

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

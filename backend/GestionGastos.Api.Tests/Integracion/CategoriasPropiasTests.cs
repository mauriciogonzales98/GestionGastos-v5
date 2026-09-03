using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Las categorías propias de una cuenta: crearlas, verlas, renombrarlas y darlas de baja
/// (US1, US2 y US3 de la feature 007).
///
/// **Todo pasa por la API y nada por la base.** Es el mismo criterio de `CuentaDePrueba`: el camino
/// que ejercitan los tests tiene que ser el que recorre una persona. Un test que inserta la fila a
/// mano verifica el esquema y no la regla — y la regla es lo que puede nacer mal.
///
/// La base la comparte toda la suite, así que cada test limpia con `LimpiarCuentasAsync`, que desde
/// esta feature se lleva también las categorías propias.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class CategoriasPropiasTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateOnly Hoy = new(2026, 8, 24);

    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-02: una cuenta recién registrada ve las diez predefinidas y ninguna propia, porque no
    /// tiene.
    ///
    /// Se verifica el catálogo COMPLETO contra la lista literal, y no sólo que "haya categorías de
    /// gasto": lo que AC-02 promete es que el punto de partida de toda cuenta nueva es el mismo, y
    /// eso se rompe tanto por una de menos como por una de más — por ejemplo la propia de otra
    /// cuenta colándose por un ámbito mal escrito.
    ///
    /// **Y el orden, que hasta hoy no lo verificaba nadie.** El contrato dice "por tipo y después
    /// por identificador" y el catálogo salía ordenado igual sin que ningún test lo mirara: si
    /// alguien borraba el `OrderBy`, MySQL seguía devolviendo las filas por clave primaria y nadie
    /// se enteraba hasta que una propia con id alto apareciera en el medio.
    /// </summary>
    [Fact]
    public async Task Una_Cuenta_Nueva_Ve_Las_Diez_Predefinidas_Y_Ninguna_Propia_AC02()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var catalogo = await CatalogoAsync(cuenta);

        Assert.Equal(
            [
                (1, "Comida", "gasto"),
                (2, "Transporte", "gasto"),
                (3, "Vivienda", "gasto"),
                (4, "Servicios", "gasto"),
                (5, "Salud", "gasto"),
                (6, "Ocio", "gasto"),
                (7, "Otros", "gasto"),
                (8, "Sueldo", "ingreso"),
                (9, "Ingreso extra", "ingreso"),
                (10, "Otros", "ingreso"),
            ],
            catalogo.Select(c => (c.Id, c.Nombre, c.Tipo)));

        Assert.All(catalogo, c => Assert.False(
            c.EsPropia,
            $"La predefinida {c.Id} ({c.Nombre}) vino marcada como propia."));

        // El selector de gasto del formulario no ofrece categorías de ingreso, y eso sale de que
        // cada categoría trae su tipo: el filtro es del cliente, pero el dato con el que filtra
        // tiene que estar bien de este lado.
        Assert.Equal(7, catalogo.Count(c => c.Tipo == "gasto"));
        Assert.Equal(3, catalogo.Count(c => c.Tipo == "ingreso"));
    }

    /// <summary>
    /// AC-01: una categoría propia aparece en el catálogo de su cuenta —marcada como propia— y no
    /// en el de ninguna otra (FR-012).
    ///
    /// Las dos mitades van juntas en un test y no en dos: "aparece en la mía" y "no aparece en la
    /// ajena" son la misma promesa mirada desde los dos lados, y separarlas deja pasar el día en
    /// que la primera pase por un motivo que rompe la segunda.
    /// </summary>
    [Fact]
    public async Task Una_Categoria_Propia_Aparece_Solo_En_Su_Cuenta_AC01()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var mia = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var ajena = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var creada = await CrearYLeerAsync(mia, "Gimnasio", "gasto");

        Assert.True(creada.EsPropia, "La categoría recién creada no vino marcada como propia.");
        Assert.Equal("Gimnasio", creada.Nombre);
        Assert.Equal("gasto", creada.Tipo);

        var catalogoPropio = await CatalogoAsync(mia);
        Assert.Contains(catalogoPropio, c => c.Id == creada.Id && c.EsPropia);

        var catalogoAjeno = await CatalogoAsync(ajena);
        Assert.DoesNotContain(catalogoAjeno, c => c.Id == creada.Id);
        Assert.DoesNotContain(catalogoAjeno, c => c.Nombre == "Gimnasio");
        Assert.All(catalogoAjeno, c => Assert.False(c.EsPropia));
    }

    /// <summary>
    /// AC-07 y FR-007: el nombre repetido se rechaza, contra una propia y contra una predefinida, y
    /// la comparación ignora mayúsculas, acentos y espacios al borde.
    ///
    /// Las mayúsculas y los acentos los resuelve la collation `utf8mb4_0900_ai_ci` sin que nadie
    /// escriba nada, y por eso justamente hacen falta estos casos: una regla que se cumple sola es
    /// una regla que nadie nota cuando deja de cumplirse. Los espacios al borde sí se recortan a
    /// mano, y son la única mitad que puede romperse por un cambio de código.
    ///
    /// El mensaje **no** dice si la que choca es propia o predefinida (D-06): no hace falta, y es
    /// una fuga menos.
    /// </summary>
    [Theory]
    [InlineData("Gimnasio", "contra una propia, idéntico")]
    [InlineData("gimnasio", "contra una propia, en minúsculas")]
    [InlineData("GIMNASIO", "contra una propia, en mayúsculas")]
    [InlineData("  Gimnasio  ", "contra una propia, con espacios al borde")]
    public async Task Rechaza_El_Nombre_Repetido_Contra_Una_Propia_AC07(string nombre, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");

        using var repetida = await CrearAsync(cuenta, nombre, "gasto");
        await AssertRechazadoAsync(repetida, "nombre", caso);

        // Y no quedó una segunda fila: un 400 que igual inserta cumple el código y falla la regla.
        var catalogo = await CatalogoAsync(cuenta);
        Assert.Equal(1, catalogo.Count(c => c.EsPropia));
    }

    /// <summary>
    /// La otra mitad de AC-07: choca también contra las predefinidas, que la cuenta ve pero no
    /// posee. Es la razón por la que la unicidad la comprueba la aplicación y no puede quedar en el
    /// índice: para MySQL, `usuario_id NULL` y `usuario_id 7` son claves distintas (D-02).
    /// </summary>
    [Theory]
    [InlineData("Comida", "idéntico a una predefinida")]
    [InlineData("comida", "una predefinida, en minúsculas")]
    [InlineData("  Comida ", "una predefinida, con espacios al borde")]
    public async Task Rechaza_El_Nombre_Repetido_Contra_Una_Predefinida_AC07(string nombre, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var respuesta = await CrearAsync(cuenta, nombre, "gasto");
        await AssertRechazadoAsync(respuesta, "nombre", caso);

        var catalogo = await CatalogoAsync(cuenta);
        Assert.DoesNotContain(catalogo, c => c.EsPropia);
    }

    /// <summary>
    /// AC-10: el largo del nombre, con sus dos bordes.
    ///
    /// El de 50 se acepta y por eso está: sin él, una validación de más —rechazar el borde exacto—
    /// quedaría indistinguible de una correcta. El límite es el de la columna, no el que el PRD
    /// citó de memoria.
    /// </summary>
    [Theory]
    [InlineData("", "nombre vacío")]
    [InlineData("   ", "sólo espacios")]
    [InlineData("x51", "51 caracteres")]
    public async Task Rechaza_El_Nombre_De_Largo_Invalido_AC10(string nombre, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var respuesta = await CrearAsync(cuenta, nombre == "x51" ? new string('x', 51) : nombre, "gasto");
        await AssertRechazadoAsync(respuesta, "nombre", caso);
    }

    [Fact]
    public async Task Acepta_El_Nombre_De_Cincuenta_Caracteres_AC10()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var nombre = new string('x', 50);
        var creada = await CrearYLeerAsync(cuenta, nombre, "gasto");

        Assert.Equal(nombre, creada.Nombre);
    }

    /// <summary>
    /// AC-08: dos cuentas crean la misma categoría y las dos se aceptan. La unicidad es por ámbito,
    /// no global — y ya funcionaba, porque el índice siempre incluyó `usuario_id`.
    /// </summary>
    [Fact]
    public async Task Dos_Cuentas_Pueden_Crear_La_Misma_Categoria_AC08()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var una = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var otra = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deUna = await CrearYLeerAsync(una, "Gimnasio", "gasto");
        var deOtra = await CrearYLeerAsync(otra, "Gimnasio", "gasto");

        Assert.NotEqual(deUna.Id, deOtra.Id);

        Assert.Equal([deUna.Id], (await CatalogoAsync(una)).Where(c => c.EsPropia).Select(c => c.Id));
        Assert.Equal([deOtra.Id], (await CatalogoAsync(otra)).Where(c => c.EsPropia).Select(c => c.Id));
    }

    /// <summary>
    /// El mismo nombre con OTRO tipo se acepta: la unicidad es por `(nombre, tipo)`, igual que
    /// "Otros" existe en gasto y en ingreso desde la primera migración.
    /// </summary>
    [Fact]
    public async Task Acepta_El_Mismo_Nombre_Con_Otro_Tipo()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var gasto = await CrearYLeerAsync(cuenta, "Viajes", "gasto");
        var ingreso = await CrearYLeerAsync(cuenta, "Viajes", "ingreso");

        Assert.NotEqual(gasto.Id, ingreso.Id);
        Assert.Equal("gasto", gasto.Tipo);
        Assert.Equal("ingreso", ingreso.Tipo);
    }

    /// <summary>
    /// AC-04: renombrar una categoría con movimientos cambia el nombre **en el listado y en el
    /// desglose del resumen**, y no toca ningún movimiento.
    ///
    /// Es la prueba de que el movimiento guarda el identificador y no una copia del nombre. Los dos
    /// lugares se miran porque son dos consultas distintas: el listado cruza contra `categoria` y
    /// el desglose la agrupa. Que una de las dos se hubiera quedado con el nombre copiado no se
    /// vería mirando la otra.
    ///
    /// Y se comprueba que los identificadores de los movimientos no cambiaron: si el renombre
    /// estuviera implementado como "dar de baja y crear otra", el nombre nuevo aparecería igual —
    /// y la historia habría quedado partida en dos categorías.
    /// </summary>
    [Fact]
    public async Task Renombrar_Cambia_El_Nombre_En_El_Listado_Y_En_El_Desglose_AC04()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var categoria = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");
        var movimiento = await RegistrarAsync(cuenta, categoria.Id, 1500m);

        var renombrada = await RenombrarYLeerAsync(cuenta, categoria.Id, "Gimnasio y pileta");

        Assert.Equal(categoria.Id, renombrada.Id);
        Assert.Equal("Gimnasio y pileta", renombrada.Nombre);
        Assert.Equal("gasto", renombrada.Tipo);
        Assert.True(renombrada.EsPropia);

        var listado = await ListadoAsync(cuenta);
        var fila = listado.Single(m => m.GetProperty("id").GetInt64() == movimiento);
        Assert.Equal("Gimnasio y pileta", fila.GetProperty("categoriaNombre").GetString());
        Assert.Equal(categoria.Id, fila.GetProperty("categoriaId").GetInt32());

        var desglose = await DesgloseAsync(cuenta);
        var entrada = desglose.Single(c => c.GetProperty("categoriaId").GetInt32() == categoria.Id);
        Assert.Equal("Gimnasio y pileta", entrada.GetProperty("categoriaNombre").GetString());
        Assert.Equal(1500m, entrada.GetProperty("total").GetDecimal());
    }

    /// <summary>
    /// Clarificación 1: el renombre valida la MISMA unicidad que el alta, contra propias y contra
    /// predefinidas. Reusar la validación entera es lo que lo garantiza; este test es lo que se
    /// pondría en rojo si alguien la duplicara y las dos copias divergieran.
    /// </summary>
    [Theory]
    [InlineData("Pilates", "contra otra propia")]
    [InlineData("pilates", "contra otra propia, en minúsculas")]
    [InlineData("Comida", "contra una predefinida")]
    [InlineData("  Comida  ", "contra una predefinida, con espacios al borde")]
    public async Task El_Renombre_Valida_La_Misma_Unicidad_Que_El_Alta(string nombre, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var gimnasio = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");
        await CrearYLeerAsync(cuenta, "Pilates", "gasto");

        using var respuesta = await RenombrarAsync(cuenta, gimnasio.Id, nombre);
        await AssertRechazadoAsync(respuesta, "nombre", caso);

        // Y quedó como estaba: un 400 que igual guarda cumple el código y falla la regla.
        Assert.Contains(await CatalogoAsync(cuenta), c => c.Id == gimnasio.Id && c.Nombre == "Gimnasio");
    }

    /// <summary>
    /// La otra mitad de la Clarificación 1, y la que un `AnyAsync` ingenuo rompe: **una categoría
    /// no choca consigo misma**. Renombrar "Gimnasio" a "Gimnasio" no es un error.
    ///
    /// Parece un caso de laboratorio y no lo es: es lo que pasa cuando alguien abre el renombre,
    /// cambia de idea y guarda igual. Rechazarlo sería incomprensible.
    /// </summary>
    [Theory]
    [InlineData("Gimnasio", "el mismo nombre exacto")]
    [InlineData("GIMNASIO", "el mismo nombre en mayúsculas")]
    [InlineData("  Gimnasio  ", "el mismo nombre con espacios al borde")]
    public async Task Renombrar_Una_Categoria_A_Su_Propio_Nombre_No_Es_Un_Error(string nombre, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var categoria = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");

        var renombrada = await RenombrarYLeerAsync(cuenta, categoria.Id, nombre);

        // Se guarda lo que se mandó, ya recortado: corregirle las mayúsculas a una categoría es un
        // renombre legítimo, no un no-op. Lo que este test fija es que NO se rechace.
        Assert.Equal(nombre.Trim(), renombrada.Nombre);
        Assert.Equal(categoria.Id, renombrada.Id);
        Assert.True(renombrada.EsPropia, caso);
    }

    /// <summary>
    /// AC-03: renombrar una **predefinida** responde `403` y la deja intacta (FR-008).
    ///
    /// **No es `404`**, y la diferencia con el caso de abajo es deliberada (D-06): la persona la
    /// está viendo en su selector, y decirle que no existe es mentirle sobre algo que tiene a la
    /// vista. No hay nada que ocultar — el catálogo predefinido es igual para todas las cuentas.
    /// </summary>
    [Fact]
    public async Task Renombrar_Una_Predefinida_Responde_403_Y_No_La_Toca_AC03()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var respuesta = await RenombrarAsync(cuenta, 1, "Comida casera");
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        var comida = (await CatalogoAsync(cuenta)).Single(c => c.Id == 1);
        Assert.Equal("Comida", comida.Nombre);
        Assert.Equal("gasto", comida.Tipo);
        Assert.False(comida.EsPropia);
    }

    /// <summary>
    /// AC-11 y FR-013: renombrar una categoría propia de OTRA cuenta responde `404` **con el mismo
    /// cuerpo** que un identificador inexistente, y la deja sin cambios.
    ///
    /// El cuerpo se compara entero y no sólo el código: cualquier diferencia —un título, un
    /// mensaje— confirma que esa fila existe, y los ids son autoincrementales y contiguos. Con eso
    /// se pueden contar las categorías de otra cuenta sin ver ninguna.
    /// </summary>
    [Fact]
    public async Task Renombrar_Una_Categoria_Ajena_Responde_Como_Una_Inexistente_AC11()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var mia = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var ajena = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deLaOtra = await CrearYLeerAsync(ajena, "Privada", "gasto");

        using var sobreLaAjena = await RenombrarAsync(mia, deLaOtra.Id, "Renombrada por la vecina");
        using var sobreUnaInexistente = await RenombrarAsync(mia, 999_999, "Renombrada por la vecina");

        RespuestasIndistinguibles.Exigir(
            await ObservarAsync(sobreLaAjena),
            await ObservarAsync(sobreUnaInexistente),
            "renombre de una categoría ajena");

        // Y la de la otra cuenta quedó como estaba.
        Assert.Contains(await CatalogoAsync(ajena), c => c.Id == deLaOtra.Id && c.Nombre == "Privada");
    }

    /// <summary>AC-10 en el renombre: el largo se valida igual que en el alta y la deja como estaba.</summary>
    [Theory]
    [InlineData("", "nombre vacío")]
    [InlineData("   ", "sólo espacios")]
    [InlineData("x51", "51 caracteres")]
    public async Task El_Renombre_Valida_El_Largo_Del_Nombre_AC10(string nombre, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var categoria = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");

        using var respuesta = await RenombrarAsync(
            cuenta, categoria.Id, nombre == "x51" ? new string('x', 51) : nombre);

        await AssertRechazadoAsync(respuesta, "nombre", caso);

        Assert.Contains(await CatalogoAsync(cuenta), c => c.Id == categoria.Id && c.Nombre == "Gimnasio");
    }

    /// <summary>
    /// AC-05: después de la baja, la categoría **no** está en el catálogo y **sí** sigue nombrando
    /// sus movimientos en el listado (FR-010).
    ///
    /// Las dos mitades son opuestas y por eso van juntas: una baja que sólo cumpliera la primera
    /// sería un `DELETE` disfrazado, y el listado quedaría con una categoría sin nombre o sin fila.
    /// </summary>
    [Fact]
    public async Task Despues_De_La_Baja_Desaparece_Del_Catalogo_Y_Sigue_Nombrando_AC05()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var categoria = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");
        var movimiento = await RegistrarAsync(cuenta, categoria.Id, 1500m);

        await DarDeBajaAsync(cuenta);

        Assert.DoesNotContain(await CatalogoAsync(cuenta), c => c.Id == categoria.Id);

        var fila = (await ListadoAsync(cuenta)).Single(m => m.GetProperty("id").GetInt64() == movimiento);
        Assert.Equal("Gimnasio", fila.GetProperty("categoriaNombre").GetString());
        Assert.Equal(categoria.Id, fila.GetProperty("categoriaId").GetInt32());

        async Task DarDeBajaAsync(CuentaDePrueba quien)
        {
            using var respuesta = await BajaAsync(quien, categoria.Id);
            Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        }
    }

    /// <summary>
    /// **AC-06 y FR-011: el test que sostiene la feature.**
    ///
    /// Se guarda el resumen ENTERO antes de la baja, se da de baja una categoría con movimientos, y
    /// el resumen de después tiene que ser **idéntico** — totales, balance y el monto de esa
    /// categoría en el desglose.
    ///
    /// **Se compara el documento completo, no campo por campo.** Un campo que nadie mira es
    /// exactamente por donde se escapa la diferencia: si mañana el resumen gana una sección, esta
    /// comparación la cubre sin que nadie se acuerde de agregarla acá.
    ///
    /// El escenario tiene dos categorías y no una a propósito. Con una sola, un desglose que se
    /// vaciara entero y un total que se fuera a cero darían "todo cambió" y el test fallaría igual
    /// — pero también fallaría un resumen que se rompiera por cualquier otro motivo. Con dos, lo
    /// que se verifica es que la categoría dada de baja siga contando **junto a** la que quedó, que
    /// es la promesa real.
    /// </summary>
    [Fact]
    public async Task Dar_De_Baja_Una_Categoria_No_Cambia_Ni_Un_Numero_Del_Resumen_AC06()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var gimnasio = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");
        await RegistrarAsync(cuenta, gimnasio.Id, 1500m);
        await RegistrarAsync(cuenta, gimnasio.Id, 300m);

        // Una predefinida que se queda activa, y un ingreso: así el balance no es una resta de un
        // solo número y el desglose tiene con qué convivir.
        await RegistrarAsync(cuenta, categoriaId: 1, monto: 900m);
        await RegistrarAsync(cuenta, categoriaId: 8, monto: 5000m, tipo: "ingreso");

        var antes = await ResumenCrudoAsync(cuenta);

        using (var baja = await BajaAsync(cuenta, gimnasio.Id))
        {
            Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        }

        var despues = await ResumenCrudoAsync(cuenta);

        Assert.Equal(antes, despues);

        // Y que la comparación no pasó por vacuidad: el desglose tiene que seguir nombrando la
        // categoría dada de baja con su monto. Sin esto, un resumen que devolviera `[]` en los dos
        // momentos también daría "idéntico".
        var entrada = (await DesgloseAsync(cuenta))
            .Single(c => c.GetProperty("categoriaId").GetInt32() == gimnasio.Id);
        Assert.Equal("Gimnasio", entrada.GetProperty("categoriaNombre").GetString());
        Assert.Equal(1800m, entrada.GetProperty("total").GetDecimal());
    }

    /// <summary>
    /// AC-09: se puede crear una categoría con el mismo nombre y tipo que una dada de baja. La
    /// nueva tiene otro `id`, y el movimiento viejo sigue apuntando al viejo.
    ///
    /// **Es la razón de existir de `discriminador`** (FR-009, D-01): sin él, la fila de baja seguiría
    /// ocupando su casillero en el índice único y este alta chocaría con un error de la base.
    ///
    /// La segunda mitad —el movimiento viejo no se muda— es la que impide confundir esto con una
    /// reactivación. Son dos categorías distintas que se llaman igual, y la historia se queda con
    /// la que ya tenía.
    /// </summary>
    [Fact]
    public async Task Se_Puede_Recrear_Una_Categoria_Dada_De_Baja_AC09()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var vieja = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");
        var movimiento = await RegistrarAsync(cuenta, vieja.Id, 1500m);

        using (var baja = await BajaAsync(cuenta, vieja.Id))
        {
            Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        }

        var nueva = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");

        Assert.NotEqual(vieja.Id, nueva.Id);
        Assert.Equal("Gimnasio", nueva.Nombre);

        var fila = (await ListadoAsync(cuenta)).Single(m => m.GetProperty("id").GetInt64() == movimiento);
        Assert.Equal(vieja.Id, fila.GetProperty("categoriaId").GetInt32());

        // Sólo la nueva se ofrece: la vieja sigue dada de baja.
        var propias = (await CatalogoAsync(cuenta)).Where(c => c.EsPropia).Select(c => c.Id);
        Assert.Equal([nueva.Id], propias);
    }

    /// <summary>
    /// La baja es idempotente: dos `DELETE` seguidos devuelven `204` los dos (D-06).
    ///
    /// El estado final es el mismo en los dos casos, y obligar al cliente a distinguir dos
    /// situaciones idénticas no le sirve a nadie — menos todavía cuando la segunda petición suele
    /// ser un doble clic o un reintento.
    /// </summary>
    [Fact]
    public async Task La_Baja_Es_Idempotente()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var categoria = await CrearYLeerAsync(cuenta, "Gimnasio", "gasto");

        using (var primera = await BajaAsync(cuenta, categoria.Id))
        {
            Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        }

        using var segunda = await BajaAsync(cuenta, categoria.Id);
        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);

        Assert.DoesNotContain(await CatalogoAsync(cuenta), c => c.Id == categoria.Id);
    }

    /// <summary>AC-03 en el `DELETE`: una predefinida responde `403` y sigue en el catálogo.</summary>
    [Fact]
    public async Task Dar_De_Baja_Una_Predefinida_Responde_403_AC03()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var respuesta = await BajaAsync(cuenta, 1);
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        Assert.Contains(await CatalogoAsync(cuenta), c => c.Id == 1 && c.Nombre == "Comida");
    }

    /// <summary>
    /// AC-11 en el `DELETE`: una categoría de otra cuenta responde igual que una inexistente, y
    /// sigue en el catálogo de su dueña.
    /// </summary>
    [Fact]
    public async Task Dar_De_Baja_Una_Categoria_Ajena_Responde_Como_Una_Inexistente_AC11()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var factoria = new FactoriaConReloj(Hoy);
        using var mia = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var ajena = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        var deLaOtra = await CrearYLeerAsync(ajena, "Privada", "gasto");

        using var sobreLaAjena = await BajaAsync(mia, deLaOtra.Id);
        using var sobreUnaInexistente = await BajaAsync(mia, 999_999);

        RespuestasIndistinguibles.Exigir(
            await ObservarAsync(sobreLaAjena),
            await ObservarAsync(sobreUnaInexistente),
            "baja de una categoría ajena");

        Assert.Contains(await CatalogoAsync(ajena), c => c.Id == deLaOtra.Id);
    }

    /// <summary>Da de baja por la API. Devuelve la respuesta cruda: hay tests que la esperan en rojo.</summary>
    private static Task<HttpResponseMessage> BajaAsync(CuentaDePrueba cuenta, int id) =>
        cuenta.Cliente.DeleteAsync(new Uri($"/api/categorias/{id}", UriKind.Relative));

    /// <summary>El resumen como texto, para poder compararlo entero sin elegir qué mirar.</summary>
    private static async Task<string> ResumenCrudoAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/resumen", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        return await respuesta.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// La respuesta reducida a lo que un tercero puede observar de ella.
    ///
    /// El cuerpo se compara con <see cref="RespuestasIndistinguibles"/>, que normaliza el
    /// `traceId`: `ProblemDetails` lleva uno por petición, así que dos respuestas nunca son iguales
    /// byte a byte ni siquiera pidiendo dos veces lo mismo. Lo que se verifica no es que los
    /// cuerpos sean idénticos, sino que nada que dependa de la EXISTENCIA difiera.
    /// </summary>
    private static async Task<RespuestaObservable> ObservarAsync(HttpResponseMessage respuesta) => new(
        respuesta.StatusCode,
        await respuesta.Content.ReadAsStringAsync(),
        respuesta.Content.Headers.ContentType?.ToString());

    /// <summary>Renombra por la API. Devuelve la respuesta cruda: hay tests que la esperan en rojo.</summary>
    private static Task<HttpResponseMessage> RenombrarAsync(CuentaDePrueba cuenta, int id, string nombre) =>
        cuenta.Cliente.PutAsJsonAsync(new Uri($"/api/categorias/{id}", UriKind.Relative), new { nombre });

    private static async Task<CategoriaVista> RenombrarYLeerAsync(CuentaDePrueba cuenta, int id, string nombre)
    {
        using var respuesta = await RenombrarAsync(cuenta, id, nombre);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        return await respuesta.Content.ReadFromJsonAsync<CategoriaVista>()
            ?? throw new InvalidOperationException("El renombre respondió 200 con un cuerpo nulo.");
    }

    /// <summary>Registra un movimiento en el día del reloj fijo. Devuelve su identificador.</summary>
    private static async Task<long> RegistrarAsync(
        CuentaDePrueba cuenta, int categoriaId, decimal monto, string tipo = "gasto")
    {
        using var respuesta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new
            {
                tipo,
                monto,
                categoriaId,
                fecha = Hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt64();
    }

    private static async Task<IReadOnlyList<JsonElement>> ListadoAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return [.. json.RootElement.EnumerateArray().Select(m => m.Clone())];
    }

    /// <summary>El desglose de la moneda predeterminada, con el nombre de cada categoría.</summary>
    private static async Task<IReadOnlyList<JsonElement>> DesgloseAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/resumen", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return
        [
            .. json.RootElement.GetProperty("monedas").EnumerateArray()
                .Single(m => m.GetProperty("monedaCodigo").GetString() == "ARS")
                .GetProperty("gastosPorCategoria").EnumerateArray()
                .Select(c => c.Clone()),
        ];
    }

    /// <summary>Crea una categoría por la API. Devuelve la respuesta cruda: hay tests que la esperan en rojo.</summary>
    private static Task<HttpResponseMessage> CrearAsync(CuentaDePrueba cuenta, string nombre, string tipo) =>
        cuenta.Cliente.PostAsJsonAsync(new Uri("/api/categorias", UriKind.Relative), new { nombre, tipo });

    /// <summary>Crea una categoría y devuelve la que respondió el 201, ya deserializada.</summary>
    private static async Task<CategoriaVista> CrearYLeerAsync(CuentaDePrueba cuenta, string nombre, string tipo)
    {
        using var respuesta = await CrearAsync(cuenta, nombre, tipo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        // El Location tiene que apuntar a algo alcanzable: un encabezado que promete un recurso
        // inexistente es peor que no ponerlo.
        Assert.NotNull(respuesta.Headers.Location);

        return await respuesta.Content.ReadFromJsonAsync<CategoriaVista>()
            ?? throw new InvalidOperationException("El alta respondió 201 con un cuerpo nulo.");
    }

    /// <summary>Un 400 de validación con la clave esperada dentro de `errors`.</summary>
    private static async Task AssertRechazadoAsync(HttpResponseMessage respuesta, string clave, string caso)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.True(
            json.RootElement.TryGetProperty("errors", out var errores),
            $"El rechazo de {caso} no trae `errors`: la pantalla no tiene dónde poner el mensaje.");

        Assert.True(
            errores.TryGetProperty(clave, out _),
            $"El rechazo de {caso} no usa la clave `{clave}`: el mensaje no cae al lado de su control.");
    }

    /// <summary>El catálogo tal como lo ve una cuenta, ya deserializado.</summary>
    private static async Task<IReadOnlyList<CategoriaVista>> CatalogoAsync(CuentaDePrueba cuenta)
    {
        using var respuesta = await cuenta.Cliente.GetAsync(new Uri("/api/categorias", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        return await respuesta.Content.ReadFromJsonAsync<List<CategoriaVista>>()
            ?? throw new InvalidOperationException("El catálogo vino nulo.");
    }

}

/// <summary>
/// La categoría como la ve el cliente.
///
/// Se declara acá y no se reusa `CategoriaDto` a propósito: un test que deserializa con el tipo del
/// backend no verifica el contrato, lo asume. Que las dos formas coincidan es lo que comprueban los
/// tests de `Contrato/`.
///
/// Va al nivel del archivo y no anidada en la clase por dos reglas del analizador que se cruzan:
/// anidada y privada dispara CA1812 —sólo la instancia el deserializador, así que para el
/// analizador es código muerto— y anidada y pública dispara CA1034.
/// </summary>
public sealed record CategoriaVista(int Id, string Nombre, string Tipo, bool EsPropia);

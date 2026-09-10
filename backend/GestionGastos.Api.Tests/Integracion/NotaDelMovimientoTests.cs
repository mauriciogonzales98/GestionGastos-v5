using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// La nota descriptiva del movimiento (RF-33, feature 012).
///
/// La decisión de producto que estos tests protegen y que no se discute: **la nota es descriptiva, no
/// clasificatoria**. No se busca, no se filtra, no se agrupa y no entra en ningún total. Que eso siga
/// siendo cierto lo vigila `BarreraDeLaNotaTests`; acá se verifica lo que la nota **sí** hace.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class NotaDelMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>PRD:AC-01 — el alta guarda la nota y el listado la devuelve (FR-002, FR-006).</summary>
    [Fact]
    public async Task El_Alta_Guarda_La_Nota_Y_El_Listado_La_Devuelve_AC01()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 10));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        const string Nota = "viaje al aeropuerto";

        using var alta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 8500m, categoriaId = 1, fecha = "2026-09-10", nota = Nota });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        using var json = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        Assert.Equal(Nota, json.RootElement.GetProperty("nota").GetString());

        using var listado = await cuenta.Cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative));
        using var jsonListado = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.Equal(
            Nota,
            jsonListado.RootElement.EnumerateArray().Single().GetProperty("nota").GetString());

        // Y la fila la tiene. La respuesta podría ser un eco de lo que llegó.
        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Equal(Nota, (await contexto.Movimientos.SingleAsync()).Nota);
    }

    /// <summary>
    /// PRD:AC-02 — el alta sin nota deja el movimiento sin nota, sin ningún error (FR-005).
    ///
    /// Los cuatro casos son **el mismo estado** dicho de cuatro formas: sin el campo, con null, con la
    /// cadena vacía y con sólo espacios. El último es D-03: una nota de sólo espacios queda vacía
    /// después de recortar, porque el valor guardado tiene que ser el que se ve — si no, dos
    /// movimientos indistinguibles en la pantalla tendrían contenido distinto en la base.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public async Task El_Alta_Sin_Nota_Deja_El_Movimiento_Sin_Nota_AC02(string? nota)
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 10));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var alta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 1200m, categoriaId = 1, fecha = "2026-09-10", nota });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        using var json = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, json.RootElement.GetProperty("nota").GetString());
    }

    /// <summary>
    /// El alta **sin el campo** también deja el movimiento sin nota (FR-001, PRD:AC-09).
    ///
    /// Va aparte de los cuatro casos de arriba porque lo que verifica es distinto: que el campo sea
    /// **omitible**. Es la mitad del valor del ticket que se pierde más fácil — quien no usa la nota
    /// no paga nada por que exista.
    /// </summary>
    [Fact]
    public async Task El_Alta_Que_Omite_El_Campo_Registra_Igual_AC09()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 10));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var alta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 1200m, categoriaId = 1, fecha = "2026-09-10" });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        using var json = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, json.RootElement.GetProperty("nota").GetString());
    }

    /// <summary>
    /// PRD:AC-04 y PRD:AC-03 — 120 se acepta, 121 se rechaza (FR-003).
    ///
    /// **Y el límite se cuenta en CARACTERES UNICODE, no en unidades UTF-16** (D-02, FR-013). El caso
    /// de los 120 emoji es el que separa esta decisión de la fácil: `string.Length` en C# y `.length`
    /// en JavaScript cuentan unidades UTF-16, donde un emoji fuera del BMP vale 2, así que esos mismos
    /// 120 emoji se rechazarían por "superar los 120 caracteres" cuando la persona escribió
    /// exactamente 120 — un mensaje que no se puede entender ni corregir. `varchar(120)` en utf8mb4
    /// cuenta caracteres, así que es además la unidad del esquema: las tres capas acuerdan qué
    /// significa 120.
    ///
    /// El caso de los espacios alrededor es D-03: se recorta **antes** de medir. Midiendo primero se
    /// rechazaría algo que, una vez guardado, entra exacto en la columna.
    /// </summary>
    [Theory]
    [InlineData(120, false)]
    [InlineData(121, true)]
    public async Task El_Limite_Son_120_Caracteres_AC03_AC04(int cuantos, bool rechazado)
    {
        var nota = new string('a', cuantos);
        Assert.Equal(rechazado, await RechazaLaNotaAsync(nota));
    }

    [Fact]
    public async Task Ciento_Veinte_Emoji_Se_Aceptan_Porque_El_Limite_Es_En_Caracteres_FR013()
    {
        // Un emoji fuera del BMP: 1 carácter Unicode, 2 unidades UTF-16.
        var nota = string.Concat(Enumerable.Repeat("😀", 120));
        Assert.Equal(240, nota.Length);
        Assert.Equal(120, nota.EnumerateRunes().Count());

        Assert.False(await RechazaLaNotaAsync(nota));
    }

    [Fact]
    public async Task Ciento_Veintiuno_Emoji_Se_Rechazan_FR013()
    {
        var nota = string.Concat(Enumerable.Repeat("😀", 121));
        Assert.True(await RechazaLaNotaAsync(nota));
    }

    [Fact]
    public async Task Ciento_Veinte_Caracteres_Con_Espacios_Alrededor_Se_Aceptan_FR003()
    {
        var nota = "  " + new string('a', 120) + "  ";
        Assert.False(await RechazaLaNotaAsync(nota));
    }

    /// <summary>
    /// **FR-011** — las cuatro rutas que devuelven un movimiento no distinguen las dos formas de
    /// "sin nota".
    ///
    /// Éste es el test delicado de la feature, y el motivo está escrito para que nadie lo ablande: el
    /// almacenamiento admite **dos** representaciones de la ausencia —sin valor y la cadena vacía— y
    /// no normaliza al escribir. Si este test usara una fila cualquiera sin nota, pasaría en verde
    /// desde antes de que exista la normalización y no verificaría nada. Por eso **escribe las dos
    /// representaciones explícitamente con SQL**, que es la única forma de producir la fila sin valor:
    /// por la API no se puede, porque la API ya normaliza.
    ///
    /// Las cuatro rutas se recorren de verdad y no por muestreo: `MovimientoDto` se construye en
    /// cuatro lugares distintos de los endpoints, y la normalización vive en el tipo precisamente para
    /// que los cuatro la hereden (D-04). Si alguna devolviera null, este test lo dice y nombra cuál.
    /// </summary>
    [Fact]
    public async Task Las_Cuatro_Rutas_No_Distinguen_Las_Dos_Formas_De_Sin_Nota_FR011()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 10));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        var cliente = cuenta.Cliente;

        // Ruta 1 — el alta. Se registran dos movimientos y después se fuerza cada representación
        // con SQL: `nota = NULL` en uno y `nota = ''` en el otro.
        var sinValor = await RegistrarAsync(cliente);
        var cadenaVacia = await RegistrarAsync(cliente);

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE movimiento SET nota = NULL WHERE id = {sinValor}");
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE movimiento SET nota = '' WHERE id = {cadenaVacia}");

            // Las dos formas están de verdad en la base: si el alta hubiera normalizado al escribir,
            // este test no estaría probando lo que dice.
            var nulos = await contexto.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM movimiento WHERE nota IS NULL")
                .SingleAsync();
            Assert.Equal(1, nulos);
        }

        // Ruta 2 — el listado. Las dos filas salen iguales.
        using (var listado = await cliente.GetAsync(new Uri("/api/movimientos", UriKind.Relative)))
        {
            using var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
            foreach (var fila in json.RootElement.EnumerateArray())
            {
                Assert.Equal(
                    string.Empty,
                    fila.GetProperty("nota").GetString());
            }
        }

        // Ruta 3 — la consulta individual, sobre la fila guardada SIN VALOR.
        using (var individual = await cliente.GetAsync(
            new Uri($"/api/movimientos/{sinValor}", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, individual.StatusCode);
            using var json = JsonDocument.Parse(await individual.Content.ReadAsStringAsync());
            Assert.Equal(string.Empty, json.RootElement.GetProperty("nota").GetString());
        }

        // Ruta 4 — la edición, que devuelve el movimiento ya modificado. Se manda la nota vacía, y lo
        // que se verifica es que la respuesta no la devuelva como null.
        using (var edicion = await cliente.PutAsJsonAsync(
            new Uri($"/api/movimientos/{sinValor}", UriKind.Relative),
            new { tipo = "gasto", monto = 1200m, categoriaId = 1, fecha = "2026-09-10", nota = (string?)null }))
        {
            Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
            using var json = JsonDocument.Parse(await edicion.Content.ReadAsStringAsync());
            Assert.Equal(string.Empty, json.RootElement.GetProperty("nota").GetString());
        }
    }

    /// <summary>
    /// El mensaje de rechazo **no repite la nota**.
    ///
    /// Es la única entrada de texto libre de la aplicación: devolver el valor lo haría viajar de vuelta
    /// y aparecer en cualquier lugar donde el mensaje termine —un log, una traza, una captura de
    /// pantalla de soporte—. El mensaje dice que se pasó del límite y nada más.
    /// </summary>
    [Fact]
    public async Task El_Mensaje_De_Rechazo_No_Repite_La_Nota_NFR001()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 10));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        const string Secreto = "zzzsecretozzz";
        var nota = Secreto + new string('a', 120);

        using var alta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 1200m, categoriaId = 1, fecha = "2026-09-10", nota });

        Assert.Equal(HttpStatusCode.BadRequest, alta.StatusCode);

        var cuerpo = await alta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secreto, cuerpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// Manda un alta con esa nota y dice si fue rechazada. Si se rechaza, comprueba además que el
    /// error venga con la clave `nota` —es lo que permite a la pantalla poner el mensaje al lado de su
    /// control— y que **no se haya creado nada** (PRD:AC-03).
    /// </summary>
    private async Task<bool> RechazaLaNotaAsync(string nota)
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 9, 10));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);

        using var alta = await cuenta.Cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 1200m, categoriaId = 1, fecha = "2026-09-10", nota });

        if (alta.StatusCode == HttpStatusCode.Created)
        {
            using var json = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());

            // Lo aceptado se guarda RECORTADO: el valor guardado es el que se ve (D-03).
            Assert.Equal(nota.Trim(), json.RootElement.GetProperty("nota").GetString());
            return false;
        }

        Assert.Equal(HttpStatusCode.BadRequest, alta.StatusCode);

        using (var json = JsonDocument.Parse(await alta.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("errors").TryGetProperty("nota", out _),
                "El rechazo por largo tiene que venir con la clave `nota`, o la pantalla no puede " +
                "poner el mensaje al lado de su control y cae en la región general del formulario.");
        }

        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Empty(await contexto.Movimientos.ToListAsync());
        return true;
    }

    /// <summary>Registra un gasto sin nota y devuelve su id.</summary>
    private static async Task<long> RegistrarAsync(HttpClient cliente)
    {
        using var alta = await cliente.PostAsJsonAsync(
            new Uri("/api/movimientos", UriKind.Relative),
            new { tipo = "gasto", monto = 1200m, categoriaId = 1, fecha = "2026-09-10" });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        using var json = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt64();
    }
}

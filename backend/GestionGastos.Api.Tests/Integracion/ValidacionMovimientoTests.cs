using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// Ningún movimiento inválido llega a la base, y la respuesta siempre dice por qué y en qué campo
/// (FR-004, FR-004b, FR-005, FR-011).
///
/// Cada test verifica DOS cosas: que la respuesta es un 400 con la clave correcta en `errors`, y
/// que la cantidad de filas no cambió. Sin lo segundo, un endpoint que devuelve 400 y guarda igual
/// pasaría en verde.
/// </summary>
[Collection(BaseDeDatosSuite.Nombre)]
public class ValidacionMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    private readonly BaseDeDatosFixture _baseDeDatos = baseDeDatos;

    /// <summary>
    /// AC-18 (RF-13): un monto inválido se rechaza con un motivo visible y no queda registrado.
    /// </summary>
    [Theory]
    [InlineData("null", "monto ausente")]
    [InlineData("0", "cero")]
    [InlineData("-5", "negativo")]
    [InlineData("10.999", "tres decimales")]
    [InlineData("1000000000.00", "por encima del techo de FR-004b")]
    public async Task Rechaza_Monto_Invalido_Sin_Registrar_Nada_AC18(string monto, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var respuesta = await EnviarCrudoAsync(
            $$"""{"tipo":"gasto","monto":{{monto}},"categoriaId":1,"fecha":"2026-08-23"}""");

        await AssertRechazadoAsync(respuesta, "monto", caso);
    }

    /// <summary>
    /// Los bordes que SÍ pasan. Sin ellos, una validación de más —por ejemplo rechazar el techo
    /// exacto— quedaría indistinguible de una correcta.
    /// </summary>
    [Theory]
    [InlineData("0.01")]
    [InlineData("999999999.99")]
    public async Task Acepta_Los_Bordes_Validos_Del_Monto_AC18(string monto)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var respuesta = await EnviarCrudoAsync(
            $$"""{"tipo":"gasto","monto":{{monto}},"categoriaId":1,"fecha":"2026-08-23"}""");

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Equal(
            decimal.Parse(monto, CultureInfo.InvariantCulture),
            (await contexto.Movimientos.SingleAsync()).Monto);
    }

    /// <summary>AC-40 (RF-14) y FR-011: la categoría es obligatoria, tiene que existir y tiene que
    /// ser del mismo tipo que el movimiento.</summary>
    [Theory]
    [InlineData("\"gasto\"", "null", "sin categoría")]
    [InlineData("\"gasto\"", "9999", "categoría inexistente")]
    [InlineData("\"gasto\"", "8", "categoría de ingreso en un gasto")]
    [InlineData("\"ingreso\"", "1", "categoría de gasto en un ingreso")]
    public async Task Rechaza_Categoria_Invalida_Sin_Registrar_Nada_AC40_FR011(
        string tipo, string categoriaId, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var respuesta = await EnviarCrudoAsync(
            $$"""{"tipo":{{tipo}},"monto":100,"categoriaId":{{categoriaId}},"fecha":"2026-08-23"}""");

        await AssertRechazadoAsync(respuesta, "categoriaId", caso);
    }

    [Theory]
    [InlineData("null", "tipo ausente")]
    [InlineData("\"\"", "tipo vacío")]
    [InlineData("\"transferencia\"", "tipo desconocido")]
    [InlineData("\"Gasto\"", "tipo con mayúscula")]
    public async Task Rechaza_Tipo_Invalido_Sin_Registrar_Nada(string tipo, string caso)
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        using var respuesta = await EnviarCrudoAsync(
            $$"""{"tipo":{{tipo}},"monto":100,"categoriaId":1,"fecha":"2026-08-23"}""");

        await AssertRechazadoAsync(respuesta, "tipo", caso);
    }

    /// <summary>
    /// D-07: un solo formato de error para las cuatro familias. Si cada una respondiera distinto,
    /// el frontend necesitaría un camino por familia y alguno quedaría sin cubrir.
    /// </summary>
    [Fact]
    public async Task Todas_Las_Familias_Responden_El_Mismo_ProblemDetails()
    {
        await _baseDeDatos.LimpiarCuentasAsync();

        string[] peticiones =
        [
            """{"tipo":"gasto","monto":0,"categoriaId":1}""",
            """{"tipo":"gasto","monto":100,"categoriaId":9999}""",
            """{"tipo":"transferencia","monto":100,"categoriaId":1}""",
            """{"tipo":"gasto","monto":10.999,"categoriaId":1}""",
        ];

        foreach (var peticion in peticiones)
        {
            using var respuesta = await EnviarCrudoAsync(peticion);
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.TryGetProperty("type", out _), peticion);
            Assert.True(json.RootElement.TryGetProperty("title", out _), peticion);
            Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
            Assert.True(json.RootElement.TryGetProperty("errors", out _), peticion);
        }
    }

    /// <summary>
    /// El alta sólo acepta categorías que el catálogo ofrece: predefinidas del sistema y activas.
    ///
    /// Hoy todas las filas lo son, así que estos dos casos hay que fabricarlos. Se escriben igual
    /// porque el ticket 3 introduce categorías propias y bajas lógicas sin volver a tocar esta
    /// validación: si nadie la fija ahora, el aislamiento nace roto y nadie se entera hasta que
    /// haya dos cuentas.
    /// </summary>
    [Fact]
    public async Task Rechaza_Una_Categoria_De_Otra_Cuenta()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        const int IdAjena = 901;

        const long OtraCuenta = 999;

        await BorrarCategoriaAsync(IdAjena, OtraCuenta);

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            // La cuenta ajena tiene que existir de verdad: categoria.usuario_id es una clave
            // foránea, y el esquema rechaza la fila si apunta a la nada.
            contexto.Usuarios.Add(new Usuario { Id = OtraCuenta, Email = "otra@gestiongastos.local" });
            contexto.Categorias.Add(new Categoria
            {
                Id = IdAjena,
                Nombre = "Privada de otra cuenta",
                Tipo = TipoMovimiento.Gasto,
                UsuarioId = OtraCuenta,
                Activa = true,
            });
            await contexto.SaveChangesAsync();
        }

        try
        {
            using var respuesta = await EnviarCrudoAsync(
                $$"""{"tipo":"gasto","monto":100,"categoriaId":{{IdAjena}},"fecha":"2026-08-23"}""");

            await AssertRechazadoAsync(respuesta, "categoriaId", "categoría de otra cuenta");
        }
        finally
        {
            await BorrarCategoriaAsync(IdAjena, OtraCuenta);
        }
    }

    [Fact]
    public async Task Rechaza_Una_Categoria_Dada_De_Baja()
    {
        await _baseDeDatos.LimpiarCuentasAsync();
        const int IdInactiva = 902;

        await BorrarCategoriaAsync(IdInactiva);

        await using (var contexto = _baseDeDatos.CrearContexto())
        {
            contexto.Categorias.Add(new Categoria
            {
                Id = IdInactiva,
                Nombre = "Dada de baja",
                Tipo = TipoMovimiento.Gasto,
                UsuarioId = null,
                Activa = false,
            });
            await contexto.SaveChangesAsync();
        }

        try
        {
            using var respuesta = await EnviarCrudoAsync(
                $$"""{"tipo":"gasto","monto":100,"categoriaId":{{IdInactiva}},"fecha":"2026-08-23"}""");

            // El catálogo no la ofrece (GET /api/categorias filtra por activa). El alta tampoco
            // puede aceptarla, o la baja lógica sería puramente cosmética.
            await AssertRechazadoAsync(respuesta, "categoriaId", "categoría inactiva");
        }
        finally
        {
            await BorrarCategoriaAsync(IdInactiva);
        }
    }

    /// <summary>
    /// Borra la categoría de prueba y, si se indica, la cuenta ajena que la sostiene.
    ///
    /// Se llama antes Y después: la base es compartida por toda la suite, y una corrida
    /// interrumpida a mitad de camino deja la fila puesta. Un test que asume una base impecable
    /// falla por lo que hizo la corrida anterior, no por el código.
    /// </summary>
    private async Task BorrarCategoriaAsync(int categoriaId, long? usuarioId = null)
    {
        await using var contexto = _baseDeDatos.CrearContexto();
        await contexto.Categorias.Where(c => c.Id == categoriaId).ExecuteDeleteAsync();

        if (usuarioId is { } id)
        {
            await contexto.Usuarios.Where(u => u.Id == id).ExecuteDeleteAsync();
        }
    }

    private async Task<HttpResponseMessage> EnviarCrudoAsync(string cuerpo)
    {
        // JSON crudo y no un objeto anónimo: hace falta poder mandar `null`, cadenas vacías y
        // números que ningún tipo de C# admitiría, que es exactamente lo que un cliente puede
        // mandar de verdad.
        //
        // Con sesión iniciada: desde el ticket 01a el propietario sale de la sesión, así que sin
        // ella la petición ni siquiera llega a la validación que este test quiere ejercitar.
        using var factoria = new FactoriaConReloj(new DateOnly(2026, 8, 23));
        using var cuenta = await CuentaDePrueba.CrearYEntrarAsync(factoria, _baseDeDatos);
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");

        return await cuenta.Cliente.PostAsync(new Uri("/api/movimientos", UriKind.Relative), contenido);
    }

    private async Task AssertRechazadoAsync(HttpResponseMessage respuesta, string campo, string caso)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var errores = json.RootElement.GetProperty("errors");

        Assert.True(
            errores.TryGetProperty(campo, out var mensajes),
            $"[{caso}] se esperaba la clave `{campo}` en errors. La clave es lo que permite poner " +
            "el mensaje al lado de su control en vez de volcar un texto suelto.");

        Assert.NotEmpty(mensajes.EnumerateArray());
        Assert.False(
            string.IsNullOrWhiteSpace(mensajes[0].GetString()),
            $"[{caso}] el mensaje está vacío: la persona tiene que saber por qué se rechazó.");

        // Y nada quedó registrado.
        await using var contexto = _baseDeDatos.CrearContexto();
        Assert.Equal(0, await contexto.Movimientos.CountAsync());
    }
}

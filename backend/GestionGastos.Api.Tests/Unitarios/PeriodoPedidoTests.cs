using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// El único intérprete de `desde` y `hasta` (D-03 de la feature 006).
///
/// Antes estas tres reglas vivían escritas a mano adentro del endpoint del listado. El resumen
/// necesita exactamente las mismas —FR-005 exige que las dos vistas describan el mismo conjunto
/// ante el mismo período—, y dos copias de una regla son dos copias que divergen el día que alguien
/// arregla una sola.
///
/// Las fechas son fijas: el Principio IV prohíbe tests que dependan del día en que corren, y el
/// "hoy" acá entra por parámetro justamente para eso.
/// </summary>
public class PeriodoPedidoTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 15);

    /// <summary>
    /// FR-002: sin parámetros, el mes en curso **del servidor**.
    ///
    /// Que el valor por omisión lo ponga el servidor no es un detalle de implementación: es lo que
    /// impide que el navegador, con su propia zona horaria, decida qué mes se está mirando.
    /// </summary>
    [Fact]
    public void Sin_Parametros_Devuelve_El_Mes_En_Curso_Del_Servidor_FR002()
    {
        var errores = PeriodoPedido.Interpretar(desde: null, hasta: null, Hoy, out var rango);

        Assert.Empty(errores);
        Assert.Equal(new DateOnly(2026, 8, 1), rango.Desde);
        Assert.Equal(new DateOnly(2026, 8, 31), rango.Hasta);
    }

    /// <summary>FR-003: los dos extremos, y los dos incluidos.</summary>
    [Fact]
    public void Con_Los_Dos_Extremos_Devuelve_Ese_Rango_FR003()
    {
        var errores = PeriodoPedido.Interpretar(
            new DateOnly(2026, 3, 10), new DateOnly(2026, 4, 20), Hoy, out var rango);

        Assert.Empty(errores);
        Assert.Equal(new DateOnly(2026, 3, 10), rango.Desde);
        Assert.Equal(new DateOnly(2026, 4, 20), rango.Hasta);
    }

    /// <summary>Un rango de un solo día es válido: `desde == hasta` no está invertido.</summary>
    [Fact]
    public void Un_Rango_De_Un_Solo_Dia_Es_Valido_FR003()
    {
        var errores = PeriodoPedido.Interpretar(
            new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10), Hoy, out var rango);

        Assert.Empty(errores);
        Assert.Equal(rango.Desde, rango.Hasta);
    }

    /// <summary>
    /// FR-004: el rango invertido se rechaza en lugar de devolver un resultado vacío.
    ///
    /// Un resultado vacío se lee como "no hay nada" y esconde que la pregunta estaba mal formada.
    /// </summary>
    [Fact]
    public void Un_Rango_Invertido_Se_Rechaza_FR004()
    {
        var errores = PeriodoPedido.Interpretar(
            new DateOnly(2026, 4, 20), new DateOnly(2026, 3, 10), Hoy, out _);

        Assert.Equal([PeriodoPedido.Clave], errores.Keys);
    }

    /// <summary>
    /// FR-004: medio rango también se rechaza, venga el extremo que venga.
    ///
    /// Suponer el que falta es inventar un extremo abierto que nadie declaró, y ese supuesto es
    /// distinto para cada quien.
    /// </summary>
    [Theory]
    [InlineData("2026-03-10", null)]
    [InlineData(null, "2026-04-20")]
    public void Medio_Rango_Se_Rechaza_FR004(string? desde, string? hasta)
    {
        var errores = PeriodoPedido.Interpretar(
            desde is null ? null : DateOnly.Parse(desde, null),
            hasta is null ? null : DateOnly.Parse(hasta, null),
            Hoy,
            out _);

        Assert.Equal([PeriodoPedido.Clave], errores.Keys);
    }

    /// <summary>
    /// Los dos rechazos NO comparten mensaje, aunque compartan clave.
    ///
    /// Son dos errores distintos —uno es un rango imposible, el otro una petición incompleta— y
    /// quien los lea tiene que poder corregir el que le tocó. Un mensaje único obligaría a adivinar.
    /// </summary>
    [Fact]
    public void El_Rango_Invertido_Y_El_Medio_Rango_Dicen_Cosas_Distintas_FR004()
    {
        var invertido = PeriodoPedido.Interpretar(
            new DateOnly(2026, 4, 20), new DateOnly(2026, 3, 10), Hoy, out _);
        var incompleto = PeriodoPedido.Interpretar(
            new DateOnly(2026, 3, 10), null, Hoy, out _);

        Assert.NotEqual(invertido[PeriodoPedido.Clave], incompleto[PeriodoPedido.Clave]);
    }
}

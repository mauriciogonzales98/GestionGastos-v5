namespace GestionGastos.Api.Tests.Rendimiento;

/// <summary>
/// El guardarraíl tiene su propio test. Una función de sembrado rota es lo que convierte una
/// medición en un verde vacío, así que no puede depender de que alguien la mire.
/// </summary>
public class SembradoDeRendimientoTests
{
    [Theory]
    [InlineData("2026-08-15")]
    [InlineData("2027-02-03")]
    [InlineData("2028-02-29")]
    [InlineData("2030-12-31")]
    public void Todas_Las_Fechas_Caen_En_El_Mes_De_La_Fecha_Dada(string hoy)
    {
        var fecha = DateOnly.Parse(hoy);

        var fechas = SembradoDeRendimiento.GenerarFechasSembradas(fecha, 500);

        Assert.Equal(500, fechas.Count);
        Assert.All(fechas, f => Assert.Equal(fecha.Year, f.Year));
        Assert.All(fechas, f => Assert.Equal(fecha.Month, f.Month));
    }

    [Fact]
    public void No_Esta_Anclada_A_Ningun_Año_Escrito_A_Mano()
    {
        // El mismo pedido en dos años distintos da dos meses distintos. Si estuviera anclada, el
        // segundo caería en el año del código y no en el pedido — que es exactamente cómo vence.
        var deEsteAño = SembradoDeRendimiento.GenerarFechasSembradas(new DateOnly(2026, 5, 10), 10);
        var deDentroDeDiezAños = SembradoDeRendimiento.GenerarFechasSembradas(new DateOnly(2036, 5, 10), 10);

        Assert.All(deEsteAño, f => Assert.Equal(2026, f.Year));
        Assert.All(deDentroDeDiezAños, f => Assert.Equal(2036, f.Year));
    }
}

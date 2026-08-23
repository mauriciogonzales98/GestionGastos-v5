using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// AC-25 (RF-18): sin filtro de fecha, el listado muestra únicamente los movimientos cuya fecha
/// cae dentro del mes actual. Este tipo es lo que define ese "mes actual", así que sus bordes son
/// los que deciden si un movimiento entra o queda afuera.
///
/// Las fechas son fijas a propósito: el Principio IV prohíbe tests que dependan del día en que
/// corren. Un test que esperara a marzo para verificar febrero no verifica nada hoy.
/// </summary>
public class RangoDelMesTests
{
    [Theory]
    // Mes de 31 días.
    [InlineData("2026-01-15", "2026-01-01", "2026-01-31")]
    // Mes de 30 días.
    [InlineData("2026-04-10", "2026-04-01", "2026-04-30")]
    // Febrero común.
    [InlineData("2026-02-05", "2026-02-01", "2026-02-28")]
    // Febrero bisiesto: el caso que un rango calculado a mano suele errar.
    [InlineData("2024-02-05", "2024-02-01", "2024-02-29")]
    // Diciembre, donde el mes siguiente cambia de año.
    [InlineData("2026-12-20", "2026-12-01", "2026-12-31")]
    public void Devuelve_Primer_Y_Ultimo_Dia_Del_Mes_AC25(string hoy, string primero, string ultimo)
    {
        var rango = RangoDelMes.De(DateOnly.Parse(hoy));

        Assert.Equal(DateOnly.Parse(primero), rango.Primero);
        Assert.Equal(DateOnly.Parse(ultimo), rango.Ultimo);
    }

    [Theory]
    // Estar parado en el primer día del mes no corre el rango hacia atrás...
    [InlineData("2026-03-01")]
    // ...ni estar parado en el último lo corre hacia adelante.
    [InlineData("2026-03-31")]
    public void Los_Extremos_Del_Mes_Caen_Dentro_De_Su_Propio_Rango_AC25(string hoy)
    {
        var fecha = DateOnly.Parse(hoy);

        var rango = RangoDelMes.De(fecha);

        Assert.Equal(new DateOnly(2026, 3, 1), rango.Primero);
        Assert.Equal(new DateOnly(2026, 3, 31), rango.Ultimo);
        Assert.True(fecha >= rango.Primero && fecha <= rango.Ultimo);
    }
}

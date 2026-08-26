using GestionGastos.Api.Sesion;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// La regla del bloqueo, sin base de datos y sin servidor: 5 fallos consecutivos bloquean durante
/// 15 minutos contados desde el quinto (FR-02, RNF-05).
///
/// Que la decisión sea una función pura es lo que permite verificar los bordes de la ventana acá,
/// en milisegundos, en vez de levantar la aplicación para cada caso.
/// </summary>
public class LimiteDeIntentosTests
{
    private static readonly DateTime QuintoFallo = new(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Con_Cuatro_Fallos_No_Bloquea()
    {
        Assert.False(LimiteDeIntentos.EstaBloqueado(4, QuintoFallo, QuintoFallo.AddSeconds(1)));
    }

    [Fact]
    public void Con_Cinco_Fallos_Dentro_De_La_Ventana_Bloquea()
    {
        Assert.True(LimiteDeIntentos.EstaBloqueado(5, QuintoFallo, QuintoFallo.AddMinutes(14)));
    }

    [Fact]
    public void Con_La_Ventana_Vencida_No_Bloquea()
    {
        Assert.False(LimiteDeIntentos.EstaBloqueado(5, QuintoFallo, QuintoFallo.AddMinutes(16)));
    }

    /// <summary>
    /// El borde exacto cae del lado de "ya no bloquea": el PRD pide **al menos** 15 minutos, y a los
    /// 15 minutos clavados ya se cumplieron. Fijarlo con un test es lo que evita que el borde se
    /// mueva sin que nadie lo decida.
    /// </summary>
    [Fact]
    public void A_Los_Quince_Minutos_Clavados_Ya_No_Bloquea()
    {
        Assert.False(LimiteDeIntentos.EstaBloqueado(5, QuintoFallo, QuintoFallo.Add(LimiteDeIntentos.Ventana)));
    }

    /// <summary>Más de cinco fallos siguen bloqueando: el contador no se "pasa" del límite.</summary>
    [Fact]
    public void Con_Mas_De_Cinco_Fallos_Sigue_Bloqueando()
    {
        Assert.True(LimiteDeIntentos.EstaBloqueado(9, QuintoFallo, QuintoFallo.AddMinutes(1)));
    }
}

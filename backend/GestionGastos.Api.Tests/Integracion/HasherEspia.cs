using GestionGastos.Api.Cuentas;

namespace GestionGastos.Api.Tests.Integracion;

/// <summary>
/// El hasher de verdad, contando cuántas veces se lo llamó a verificar.
///
/// Existe para que AC-13 —"rechazar un email bloqueado tarda lo mismo que rechazar una contraseña
/// incorrecta"— tenga un test **determinista**: en vez de medir milisegundos, se verifica la
/// conducta que produce ese tiempo, que es ejecutar la verificación del hash igual. El test de
/// tiempo existe aparte, en `Rendimiento/`, y el CI lo excluye.
/// </summary>
public sealed class HasherEspia : HasherDeContrasenas
{
    private int _verificaciones;

    public int Verificaciones => _verificaciones;

    public override bool Verificar(string contrasena, string hash)
    {
        Interlocked.Increment(ref _verificaciones);
        return base.Verificar(contrasena, hash);
    }

    public void Reiniciar() => Interlocked.Exchange(ref _verificaciones, 0);
}

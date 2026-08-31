using System.Reflection;
using System.Runtime.CompilerServices;
using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// La barrera del filtro de cobertura.
///
/// El problema que esto cierra: <c>backend/cobertura.runsettings</c> excluye del reporte el patrón
/// <c>[*]*d__*</c>. Está ahí porque desde coverlet 10 el <c>ExcludeByAttribute</c> dejó de alcanzar
/// a las máquinas de estado async —las clases <c>&lt;Metodo&gt;d__N</c> que escribe el compilador—,
/// y sin excluirlas el total subía de 97,20 % a 98,60 % por dilución, con más líneas sin cubrir que
/// antes. El patrón preciso no sirve: coverlet no compara contra los <c>&lt;&gt;</c> del nombre.
///
/// El costo de ese patrón es que es más ancho de lo que se quiere. Excluye por subcadena, así que
/// una clase escrita a mano que se llamara <c>Cuentad__Vieja</c> desaparecería del reporte sin
/// avisar: la cobertura seguiría en verde y nadie vería que ese código dejó de medirse.
///
/// Este test convierte esa condición tácita —"ningún tipo propio se llama así"— en una regla
/// vigilada. Es el mismo patrón que el resto de las barreras del repositorio: lo que hoy se cumple
/// de casualidad, se escribe, para que el día que deje de cumplirse haga ruido.
/// </summary>
public class BarreraDeCoberturaTests
{
    /// <summary>
    /// La subcadena que el filtro de <c>cobertura.runsettings</c> usa para descartar las máquinas
    /// de estado. Si allá se cambia el patrón, acá se cambia la constante: son la misma decisión.
    /// </summary>
    private const string SubcadenaExcluida = "d__";

    [Fact]
    public void Ningun_Tipo_Escrito_A_Mano_Se_Llama_Como_Lo_Que_La_Cobertura_Descarta()
    {
        Assembly[] ensamblados =
        [
            typeof(Movimiento).Assembly,
            typeof(BarreraDeCoberturaTests).Assembly,
        ];

        var colados = ensamblados
            .SelectMany(ensamblado => ensamblado.GetTypes())
            .Where(tipo => tipo.Name.Contains(SubcadenaExcluida, StringComparison.Ordinal))
            .Where(tipo => !EscritoPorElCompilador(tipo))
            .Select(tipo => tipo.FullName ?? tipo.Name)
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            colados.Count == 0,
            $"""
            Hay {colados.Count} tipo(s) escritos a mano cuyo nombre contiene "{SubcadenaExcluida}",
            que es la subcadena que backend/cobertura.runsettings descarta con [*]*d__*. Ese código
            no se está midiendo, y la cobertura no lo dice:

              {string.Join("\n  ", colados)}

            La salida es renombrar el tipo. Aflojar el filtro no sirve: el patrón preciso
            [*]*<*>d__* no matchea, porque coverlet no compara contra los <> del nombre.
            """
        );
    }

    /// <summary>
    /// Las máquinas de estado async y demás plomería llevan <see cref="CompilerGeneratedAttribute"/>.
    /// Son justamente las que el filtro existe para descartar, así que no cuentan como coladas.
    /// </summary>
    private static bool EscritoPorElCompilador(Type tipo) =>
        tipo.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
}

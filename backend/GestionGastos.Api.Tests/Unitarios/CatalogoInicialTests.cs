using GestionGastos.Api.Categorias;
using GestionGastos.Api.Dominio;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// FR-002: cada cuenta nueva recibe su propio juego de las diez categorías iniciales.
///
/// El catálogo se verifica acá, sin base de datos, porque lo que se afirma es **cuáles son los diez
/// nombres** — un hecho del código y no del esquema. Que las diez lleguen efectivamente a la base al
/// registrarse es otra cosa, y la verifica <c>AltaDeCuentaTests</c> contra MySQL de verdad.
///
/// Los nombres se escriben acá enteros y a mano, y eso es deliberado: un test que los leyera de la
/// misma fuente que el código pasaría en verde con la lista entera cambiada. Son los diez de FR-006
/// de la feature 001, que esta feature no cambia — cambia a quién pertenecen.
/// </summary>
public class CatalogoInicialTests
{
    private static readonly string[] Gastos =
        ["Comida", "Transporte", "Vivienda", "Servicios", "Salud", "Ocio", "Otros"];

    private static readonly string[] Ingresos =
        ["Sueldo", "Ingreso extra", "Otros"];

    [Fact]
    public void Entrega_Diez_Categorias_FR002()
    {
        var nuevas = CatalogoInicial.Nuevas(new Usuario());

        Assert.Equal(10, nuevas.Count);
    }

    [Fact]
    public void Siete_Son_De_Gasto_Y_Tres_De_Ingreso_FR002()
    {
        var nuevas = CatalogoInicial.Nuevas(new Usuario());

        Assert.Equal(7, nuevas.Count(c => c.Tipo == TipoMovimiento.Gasto));
        Assert.Equal(3, nuevas.Count(c => c.Tipo == TipoMovimiento.Ingreso));
    }

    [Fact]
    public void Son_Los_Nombres_Del_Catalogo_Original_FR002()
    {
        var nuevas = CatalogoInicial.Nuevas(new Usuario());

        Assert.Equal(
            Gastos,
            nuevas.Where(c => c.Tipo == TipoMovimiento.Gasto).Select(c => c.Nombre).ToArray());

        Assert.Equal(
            Ingresos,
            nuevas.Where(c => c.Tipo == TipoMovimiento.Ingreso).Select(c => c.Nombre).ToArray());
    }

    [Fact]
    public void Todas_Nacen_Activas_FR002()
    {
        var nuevas = CatalogoInicial.Nuevas(new Usuario());

        Assert.All(nuevas, c => Assert.True(c.Activa));
    }

    /// <summary>
    /// Todas quedan enlazadas al usuario que se le pasó, y ninguna trae dueño puesto a mano.
    ///
    /// El enlace va por la propiedad de navegación y no por <c>UsuarioId</c>, y ése es justamente el
    /// punto: la cuenta todavía no tiene identificador —lo genera el <c>INSERT</c>— y es EF quien lo
    /// propaga a las diez filas dentro del mismo <c>SaveChanges</c>. Es lo que hace que FR-003 sea
    /// una sola transacción y no dos.
    /// </summary>
    [Fact]
    public void Quedan_Enlazadas_Al_Usuario_Que_Las_Recibe_FR003()
    {
        var dueno = new Usuario { Email = "alguien@ejemplo.com" };

        var nuevas = CatalogoInicial.Nuevas(dueno);

        Assert.All(nuevas, c => Assert.Same(dueno, c.Usuario));
    }

    /// <summary>
    /// Dos llamadas devuelven filas **distintas**: son los diez de esa cuenta, no diez compartidas.
    ///
    /// Devolver la misma instancia dos veces haría que la segunda cuenta reclamara las filas de la
    /// primera, que es exactamente el modelo que esta feature viene a terminar.
    /// </summary>
    [Fact]
    public void Cada_Cuenta_Recibe_Filas_Propias_SC006()
    {
        var unas = CatalogoInicial.Nuevas(new Usuario());
        var otras = CatalogoInicial.Nuevas(new Usuario());

        Assert.All(unas, c => Assert.DoesNotContain(c, otras));
    }
}

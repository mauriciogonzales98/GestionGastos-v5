using GestionGastos.Api.Cuentas;

namespace GestionGastos.Api.Tests.Unitarios;

/// <summary>
/// AC-10 (NFR-01): lo que queda guardado tiene formato de hash bcrypt o argon2 y no la contraseña.
/// AC-11 (NFR-01): dos cuentas con la misma contraseña guardan valores distintos.
///
/// Un volcado de la base no puede revelar contraseñas — `AGENTS.md`, *What NOT to do*.
/// </summary>
public class HasherDeContrasenasTests
{
    private readonly HasherDeContrasenas _hasher = new();

    [Fact]
    public void Lo_Guardado_No_Es_La_Contrasena_Y_Tiene_Formato_Bcrypt_AC10()
    {
        const string Contrasena = "una frase larga de prueba";

        var hash = _hasher.Hashear(Contrasena);

        Assert.DoesNotContain(Contrasena, hash, StringComparison.Ordinal);
        // El prefijo `$2` identifica a bcrypt; el resto lleva el factor de trabajo y la sal.
        Assert.StartsWith("$2", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Dos_Hashes_De_La_Misma_Contrasena_Son_Distintos_AC11()
    {
        const string Contrasena = "la misma para las dos cuentas";

        var primero = _hasher.Hashear(Contrasena);
        var segundo = _hasher.Hashear(Contrasena);

        // La sal va adentro del hash y la genera la librería. Si estos dos fueran iguales, una
        // tabla precalculada rompería las dos cuentas de una vez.
        Assert.NotEqual(primero, segundo);

        // Y los dos siguen verificando: distintos no significa que uno esté mal.
        Assert.True(_hasher.Verificar(Contrasena, primero));
        Assert.True(_hasher.Verificar(Contrasena, segundo));
    }

    [Fact]
    public void Verificar_Acepta_La_Correcta_Y_Rechaza_La_Incorrecta()
    {
        var hash = _hasher.Hashear("la correcta");

        Assert.True(_hasher.Verificar("la correcta", hash));
        Assert.False(_hasher.Verificar("la incorrecta", hash));
        Assert.False(_hasher.Verificar("La Correcta", hash));
        Assert.False(_hasher.Verificar(string.Empty, hash));
    }

    /// <summary>
    /// Las tres formas de estar ilegible, no una sola.
    ///
    /// La librería no lanza siempre lo mismo: un texto cualquiera da `SaltParseException`, el hash
    /// vacío da `ArgumentException` y uno truncado da `ArgumentOutOfRangeException`. Cubrir sólo la
    /// primera dejaba que las otras dos salieran como un 500 — y un 500 en el login convierte una
    /// fila corrupta en una caída y hace que esa cuenta se comporte distinto de todas las demás,
    /// que es exactamente lo que NFR-03 no quiere.
    /// </summary>
    [Theory]
    [InlineData("esto no es un hash", "texto cualquiera")]
    [InlineData("", "hash vacío")]
    [InlineData("   ", "sólo espacios")]
    [InlineData("$2a$11$corto", "hash bcrypt truncado")]
    public void Verificar_Contra_Un_Hash_Ilegible_Devuelve_False_En_Vez_De_Lanzar(
        string hash, string caso)
    {
        // Una fila corrupta o migrada a mano no puede tumbar el login con una excepción: eso
        // convertiría un dato malo en una caída, y encima distinguiría esa cuenta de las demás.
        Assert.False(_hasher.Verificar("cualquier cosa", hash), caso);
    }
}

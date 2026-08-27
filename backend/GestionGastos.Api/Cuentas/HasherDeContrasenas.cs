namespace GestionGastos.Api.Cuentas;

/// <summary>
/// Convierte una contraseña en un verificador del que no se puede volver atrás, y comprueba una
/// contraseña contra ese verificador.
///
/// Es bcrypt y no el <c>PasswordHasher&lt;T&gt;</c> del framework —que usa PBKDF2— porque NFR-01
/// nombra bcrypt o argon2 con todas las letras. Ver research.md D-02.
/// </summary>
public class HasherDeContrasenas
{
    /// <summary>
    /// Devuelve el verificador de <paramref name="contrasena"/>.
    ///
    /// La sal la genera la librería y viaja **dentro** del resultado, así que dos llamadas con la
    /// misma contraseña dan valores distintos sin que este código administre sales. AC-11 sale por
    /// construcción y no por disciplina.
    /// </summary>
    public string Hashear(string contrasena) => BCrypt.Net.BCrypt.HashPassword(contrasena);

    /// <summary>
    /// <c>true</c> si <paramref name="contrasena"/> es la que produjo <paramref name="hash"/>.
    ///
    /// Un hash ilegible devuelve <c>false</c> en vez de lanzar: una fila corrupta o migrada a mano
    /// no puede tumbar el login con una excepción. Eso convertiría un dato malo en una caída y,
    /// peor, haría que esa cuenta se comportara distinto de todas las demás — que es exactamente
    /// la clase de diferencia observable que NFR-03 quiere evitar.
    /// </summary>
    /// <remarks>
    /// <c>virtual</c> a propósito: es la costura que permite verificar, sin cronómetro, que el
    /// rechazo de un email bloqueado **también** paga el costo del hash (AC-13). Sin esa costura,
    /// esa propiedad sólo se puede medir en milisegundos, y un test así es intermitente.
    /// </remarks>
    public virtual bool Verificar(string contrasena, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(contrasena, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // No es un catch silencioso: el resultado —credenciales inválidas— es la respuesta
            // correcta para un verificador que no se puede leer.
            return false;
        }
        catch (ArgumentException)
        {
            // `SaltParseException` sola no alcanza, y creer que sí dejaba el agujero abierto justo
            // en el caso que este método dice cubrir: contra un hash vacío la librería lanza
            // `ArgumentException`, y contra uno truncado —`$2a$11$` y nada más—
            // `ArgumentOutOfRangeException`, que deriva de ésta. Las dos escapaban y salían por
            // arriba como un 500.
            return false;
        }
    }
}

using System.Net.Mail;
using System.Text;

namespace GestionGastos.Api.Cuentas;

/// <summary>
/// Las reglas del alta. Acá **sí** se dice qué campo está mal: un email con formato inválido o una
/// contraseña corta no revelan nada sobre qué cuentas existen, así que la indistinguibilidad de
/// NFR-03 no aplica.
/// </summary>
public static class ValidacionDeLaCuenta
{
    /// <summary>
    /// Doce caracteres y ninguna regla de composición.
    ///
    /// Doce y no ocho porque una de 8 sigue siendo atacable aun con bcrypt. Sin exigir mayúsculas
    /// ni símbolos porque esas reglas empujan a `Password1!` — más corta, más predecible y más
    /// difícil de recordar que una frase larga. Es lo que recomienda NIST SP 800-63B: longitud sí,
    /// composición no.
    /// </summary>
    public const int LargoMinimoDeContrasena = 12;

    /// <summary>
    /// Lo que entra en la columna `usuario.email`, que es `varchar(254)` — el máximo que fija el
    /// RFC 5321 para una dirección.
    ///
    /// Sin este tope, un email más largo pasaba la validación, llegaba a la base y volvía como un
    /// 500 "Data too long" donde correspondía un 400 diciendo qué campo estaba mal.
    /// </summary>
    public const int LargoMaximoDeEmail = 254;

    /// <summary>
    /// El límite del algoritmo: bcrypt sólo mira los primeros 72 bytes y descarta el resto **en
    /// silencio**. Sin tope, una frase de 100 caracteres se aceptaba y quedaba valiendo por sus
    /// primeros 72 sin que nadie lo dijera, y dos contraseñas distintas que compartieran ese
    /// prefijo abrían la misma cuenta.
    ///
    /// Se mide en bytes y no en caracteres porque es lo que el algoritmo corta: una frase con
    /// acentos o emojis ocupa más de un byte por caracter.
    /// </summary>
    public const int MaximoDeBytesDeContrasena = 72;

    public static Dictionary<string, string[]> Validar(string? email, string? contrasena)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(email))
        {
            errores["email"] = ["Ingresá tu email."];
        }
        else if (email.Length > LargoMaximoDeEmail)
        {
            errores["email"] = ["Ese email es demasiado largo."];
        }
        else if (!EsUnEmail(email))
        {
            errores["email"] = ["Ese email no parece válido."];
        }

        if (string.IsNullOrEmpty(contrasena))
        {
            errores["contrasena"] = ["Ingresá una contraseña."];
        }
        else if (contrasena.Length < LargoMinimoDeContrasena)
        {
            errores["contrasena"] =
                [$"La contraseña tiene que tener al menos {LargoMinimoDeContrasena} caracteres."];
        }
        else if (Encoding.UTF8.GetByteCount(contrasena) > MaximoDeBytesDeContrasena)
        {
            errores["contrasena"] =
                ["La contraseña es demasiado larga. Probá con una de hasta 72 caracteres."];
        }

        return errores;
    }

    /// <summary>
    /// Validación deliberadamente laxa: se rechaza lo que claramente no es un email, y nada más.
    /// Nadie valida un email de verdad sin mandarle un correo, y una expresión regular estricta
    /// rechaza direcciones legítimas — que es un error peor que aceptar una que no existe.
    /// </summary>
    private static bool EsUnEmail(string valor)
    {
        if (valor.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        return MailAddress.TryCreate(valor, out var direccion)
            && direccion.Host.Contains('.', StringComparison.Ordinal);
    }
}

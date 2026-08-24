using System.Net.Mail;

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

    public static Dictionary<string, string[]> Validar(string? email, string? contrasena)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(email))
        {
            errores["email"] = ["Ingresá tu email."];
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

namespace GestionGastos.Api.Dominio;

/// <summary>
/// Los fallos de inicio de sesión que un email lleva acumulados sin acertar (RNF-05).
///
/// **No habla de cuentas, habla de emails presentados.** No tiene clave foránea a
/// <see cref="Usuario"/> y no la puede tener: FR-01 obliga a contar los fallos de un email que no
/// existe igual que los de uno registrado. Si el contador sólo existiera para las cuentas
/// registradas, un email inexistente nunca llegaría a bloquearse y bastaría con fallar seis veces y
/// mirar si la sexta respuesta cambia para saber qué emails tienen cuenta — que es exactamente lo
/// que el bloqueo tiene que no delatar.
///
/// Que la fila exista no dice nada sobre si hay una cuenta con ese email, y eso es lo que la vuelve
/// segura de guardar.
/// </summary>
public class IntentoDeAcceso
{
    /// <summary>
    /// El email tal como lo resuelve el inicio de sesión: recortado de espacios. Es la clave
    /// primaria, con la misma colación insensible a mayúsculas que <see cref="Usuario.Email"/>.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Siempre ≥ 1: una fila en cero no existe, se borra.</summary>
    public byte FallosConsecutivos { get; set; }

    /// <summary>
    /// La marca, en UTC, del último fallo **contado**. Los intentos rechazados por el bloqueo no la
    /// mueven: es lo que hace que la ventana sea fija desde el quinto fallo y no deslizante. Si se
    /// moviera, cualquiera dejaría a otra persona afuera para siempre golpeando su email cada 14
    /// minutos.
    /// </summary>
    public DateTime UltimoFallo { get; set; }
}

namespace GestionGastos.Api.Dominio;

/// <summary>
/// De qué día habla el servidor cuando dice "hoy".
///
/// La zona horaria es explícita y no la del proceso. En un contenedor la del proceso es UTC, y con
/// la persona usuaria en Argentina (UTC-3) los dos reloj es dejan de coincidir a partir de las
/// 21:00: el servidor ya está en el día siguiente mientras el navegador todavía no. Ahí el recorte
/// del mes del listado se corre un día antes que la fecha que el formulario propone, y un
/// movimiento del 31 se guarda bien pero no aparece en un listado que ya recorta el mes siguiente.
/// </summary>
public static class DiaActual
{
    public static DateOnly De(TimeProvider reloj, TimeZoneInfo zona) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), zona).DateTime);
}

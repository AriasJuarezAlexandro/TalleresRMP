namespace TalleresRMP.Services;

/// <summary>
/// Hora local de Perú (UTC-5), independiente de la zona horaria del servidor.
/// En Render los contenedores corren en UTC, por lo que <c>DateTime.Now</c>
/// devolvería UTC y, de noche, la fecha del día siguiente. Este helper parte
/// siempre de <see cref="DateTime.UtcNow"/> y lo convierte a la zona de Perú.
/// </summary>
public static class FechaHelper
{
    private static readonly TimeZoneInfo ZonaPeru = ObtenerZonaPeru();

    /// <summary>Fecha y hora actual en Perú.</summary>
    public static DateTime Ahora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaPeru);

    private static TimeZoneInfo ObtenerZonaPeru()
    {
        // "America/Lima" (IANA, Linux/Render) y "SA Pacific Standard Time" (Windows).
        foreach (var id in new[] { "America/Lima", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Último recurso: Perú es UTC-5 fijo (sin horario de verano).
        return TimeZoneInfo.CreateCustomTimeZone("PET", TimeSpan.FromHours(-5), "Hora de Perú", "PET");
    }
}

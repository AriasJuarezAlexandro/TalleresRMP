using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using TalleresRMP.Models;

namespace TalleresRMP.Services;

/// <summary>
/// Cachea en memoria el listado de la página de inicio (mantenimientos cuya
/// FechaCreacion cae entre hace 125 y 120 días). Patrón cache-aside con
/// expiración absoluta configurable ("Cache:MantenimientoExpiracionDias").
/// Consulta los datos con ADO.NET puro a través de TursoService.
/// </summary>
public class MantenimientoCacheService
{
    private const string CacheKey = "mantenimiento_home";
    private const string FechaFormato = "yyyy-MM-dd HH:mm:ss";

    private readonly IMemoryCache _cache;
    private readonly TursoService _turso;
    private readonly int _expiracionDias;

    public MantenimientoCacheService(
        IMemoryCache cache,
        TursoService turso,
        IConfiguration configuration)
    {
        _cache = cache;
        _turso = turso;
        _expiracionDias = configuration.GetValue<int?>("Cache:MantenimientoExpiracionDias") ?? 5;
    }

    public async Task<IEnumerable<Mantenimiento>> GetCachedAsync()
    {
        if (_cache.TryGetValue(CacheKey, out IEnumerable<Mantenimiento>? cached) && cached is not null)
            return cached;

        var datos = await ConsultarUltimos120a125DiasAsync();

        _cache.Set(CacheKey, datos, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_expiracionDias)
        });

        return datos;
    }

    /// <summary>Invalida la caché del listado de inicio.</summary>
    public void Invalidate() => _cache.Remove(CacheKey);

    private async Task<List<Mantenimiento>> ConsultarUltimos120a125DiasAsync()
    {
        var ahora = DateTime.Now;
        var desde = ahora.AddDays(-125).ToString(FechaFormato);
        var hasta = ahora.AddDays(-120).ToString(FechaFormato);

        var lista = new List<Mantenimiento>();

        using var conn = _turso.GetConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT IdMantenimiento, Cliente, Numero, Telefono, FechaCreacion " +
            "FROM Mantenimiento " +
            "WHERE FechaCreacion BETWEEN @Desde AND @Hasta " +
            "ORDER BY FechaCreacion ASC";
        cmd.Parameters.AddWithValue("@Desde", desde);
        cmd.Parameters.AddWithValue("@Hasta", hasta);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new Mantenimiento
            {
                IdMantenimiento = reader.GetInt32(0),
                Cliente = reader.GetString(1),
                Numero = reader.GetString(2),
                Telefono = reader.GetString(3),
                FechaCreacion = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture)
            });
        }
        return lista;
    }
}

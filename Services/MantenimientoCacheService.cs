using Microsoft.Extensions.Caching.Memory;
using TalleresRMP.Data;
using TalleresRMP.Models;

namespace TalleresRMP.Services;

/// <summary>
/// Cachea en memoria el listado de la página de inicio. Si la caché está
/// vacía, consulta el repositorio y guarda el resultado con expiración
/// absoluta configurable (sección "Cache:MantenimientoExpiracionDias").
/// </summary>
public class MantenimientoCacheService
{
    private const string CacheKey = "mantenimiento_home";

    private readonly IMemoryCache _cache;
    private readonly MantenimientoRepository _repository;
    private readonly int _expiracionDias;

    public MantenimientoCacheService(
        IMemoryCache cache,
        MantenimientoRepository repository,
        IConfiguration configuration)
    {
        _cache = cache;
        _repository = repository;
        _expiracionDias = configuration.GetValue<int?>("Cache:MantenimientoExpiracionDias") ?? 5;
    }

    public async Task<IEnumerable<Mantenimiento>> GetCachedAsync()
    {
        if (_cache.TryGetValue(CacheKey, out IEnumerable<Mantenimiento>? cached) && cached is not null)
            return cached;

        var datos = (await _repository.GetUltimos120a125DiasAsync()).ToList();

        var opciones = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_expiracionDias)
        };

        _cache.Set(CacheKey, datos, opciones);
        return datos;
    }

    /// <summary>Invalida la caché del listado de inicio.</summary>
    public void Invalidate() => _cache.Remove(CacheKey);
}

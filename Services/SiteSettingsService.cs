using CryptoDashboard.Data;
using CryptoDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CryptoDashboard.Services;

public class SiteSettingsService
{
    private readonly AppDbContext _ctx;
    private readonly IMemoryCache _cache;

    private const string CacheKey = "site_settings_all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SiteSettingsService(AppDbContext ctx, IMemoryCache cache)
    {
        _ctx = ctx;
        _cache = cache;
    }

    public async Task<string> GetAsync(string key, string fallback = "")
    {
        var all = await GetAllAsync();
        return all.TryGetValue(key, out var v) ? v : fallback;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        if (_cache.TryGetValue<Dictionary<string, string>>(CacheKey, out var cached) && cached is not null)
            return cached;

        var data = await _ctx.SiteSettings
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        _cache.Set(CacheKey, data, CacheDuration);
        return data;
    }

    public async Task SetAsync(string key, string value)
    {
        var entity = await _ctx.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (entity is null)
        {
            entity = new SiteSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow };
            _ctx.SiteSettings.Add(entity);
        }
        else
        {
            entity.Value = value;
            entity.UpdatedAt = DateTime.UtcNow;
            _ctx.SiteSettings.Update(entity);
        }
        await _ctx.SaveChangesAsync();
        _cache.Remove(CacheKey);
    }
}

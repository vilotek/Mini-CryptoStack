using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoDashboard.Data;
using CryptoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Services;

public class CoinGeckoService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _ctx;
    private readonly ILogger<CoinGeckoService> _logger;

    public CoinGeckoService(HttpClient http, AppDbContext ctx, ILogger<CoinGeckoService> logger)
    {
        _http = http;
        _ctx = ctx;
        _logger = logger;
    }

    public async Task<int> RefreshPricesAsync(CancellationToken ct = default)
    {
        var coins = await _ctx.Coins.ToListAsync(ct);
        if (coins.Count == 0) return 0;

        var ids = string.Join(',', coins.Select(c => c.CoinGeckoId));
        var url = $"simple/price?ids={Uri.EscapeDataString(ids)}" +
                  "&vs_currencies=usd&include_24hr_change=true";

        try
        {
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            var stream = await resp.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<
                Dictionary<string, CoinGeckoRaw>>(stream, cancellationToken: ct);

            if (data is null) return 0;

            var now = DateTime.UtcNow;
            var added = 0;

            foreach (var coin in coins)
            {
                if (!data.TryGetValue(coin.CoinGeckoId, out var raw)) continue;

                _ctx.PricePoints.Add(new PricePoint
                {
                    CoinId = coin.Id,
                    Price = (decimal)raw.Usd,
                    Change24h = raw.Usd24hChange.HasValue ? (decimal)raw.Usd24hChange.Value : null,
                    Timestamp = now
                });
                added++;
            }

            await _ctx.SaveChangesAsync(ct);
            _logger.LogInformation("CoinGecko: zapisano {Count} punktów cenowych.", added);
            return added;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych z CoinGecko.");
            return 0;
        }
    }

    private sealed class CoinGeckoRaw
    {
        [JsonPropertyName("usd")] public double Usd { get; set; }
        [JsonPropertyName("usd_24h_change")] public double? Usd24hChange { get; set; }
    }
}

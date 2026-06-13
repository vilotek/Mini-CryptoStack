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

    /// <summary>
    /// Dosypuje historyczne ceny sprzed uruchomienia aplikacji z endpointu market_chart.
    /// Dodaje tylko punkty starsze niż najwcześniejszy już zapisany, więc nie dubluje danych.
    /// </summary>
    public async Task<int> BackfillHistoryAsync(int coinId, int days, CancellationToken ct = default)
    {
        var coin = await _ctx.Coins.FirstOrDefaultAsync(c => c.Id == coinId, ct);
        if (coin is null) return 0;

        var earliestExisting = await _ctx.PricePoints
            .Where(p => p.CoinId == coinId)
            .OrderBy(p => p.Timestamp)
            .Select(p => (DateTime?)p.Timestamp)
            .FirstOrDefaultAsync(ct);

        // Mamy już wystarczająco głęboką historię - nie wołamy ponownie zewnętrznego API.
        if (earliestExisting is not null && earliestExisting.Value <= DateTime.UtcNow.AddDays(-(days - 2)))
            return 0;

        var url = $"coins/{Uri.EscapeDataString(coin.CoinGeckoId)}/market_chart" +
                  $"?vs_currency=usd&days={days}";

        try
        {
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            var stream = await resp.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<MarketChartRaw>(stream, cancellationToken: ct);

            if (data?.Prices is null || data.Prices.Count == 0) return 0;

            var added = 0;
            foreach (var pair in data.Prices)
            {
                if (pair.Count < 2) continue;
                var ts = DateTimeOffset.FromUnixTimeMilliseconds((long)pair[0]).UtcDateTime;

                // Pomijamy punkty z okresu już pokrytego przez dane zbierane na żywo.
                if (earliestExisting is not null && ts >= earliestExisting.Value) continue;

                _ctx.PricePoints.Add(new PricePoint
                {
                    CoinId = coinId,
                    Price = (decimal)pair[1],
                    Change24h = null,
                    Timestamp = ts
                });
                added++;
            }

            if (added > 0) await _ctx.SaveChangesAsync(ct);
            _logger.LogInformation("CoinGecko: dosypano {Count} historycznych punktów dla {Symbol}.", added, coin.Symbol);
            return added;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania historii z CoinGecko dla {Symbol}.", coin.Symbol);
            return 0;
        }
    }

    private sealed class CoinGeckoRaw
    {
        [JsonPropertyName("usd")] public double Usd { get; set; }
        [JsonPropertyName("usd_24h_change")] public double? Usd24hChange { get; set; }
    }

    private sealed class MarketChartRaw
    {
        [JsonPropertyName("prices")] public List<List<double>> Prices { get; set; } = new();
    }
}

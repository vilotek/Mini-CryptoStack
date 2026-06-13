using CryptoDashboard.Data;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Services;

public class PriceUpdateService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PriceUpdateService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _backfillDays;

    public PriceUpdateService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<PriceUpdateService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = config.GetValue<int?>("Game:PriceRefreshMinutes") ?? 10;
        _interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
        _backfillDays = Math.Max(0, config.GetValue<int?>("Game:BackfillDays") ?? 90);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceUpdateService start, interwał = {Interval}", _interval);

        // pierwsze pobranie wkrótce po starcie aplikacji
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Jednorazowe dosypanie historii sprzed uruchomienia (pomijane, gdy już mamy dane).
        if (_backfillDays > 0)
            await BackfillHistoryAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<CoinGeckoService>();
                await svc.RefreshPricesAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd w PriceUpdateService.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task BackfillHistoryAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<CoinGeckoService>();

            var coinIds = await ctx.Coins.Select(c => c.Id).ToListAsync(stoppingToken);
            foreach (var coinId in coinIds)
            {
                await svc.BackfillHistoryAsync(coinId, _backfillDays, stoppingToken);
                // łagodnie dla darmowego rate-limitu CoinGecko
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas dosypywania historii cen.");
        }
    }
}

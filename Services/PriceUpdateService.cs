namespace CryptoDashboard.Services;

public class PriceUpdateService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PriceUpdateService> _logger;
    private readonly TimeSpan _interval;

    public PriceUpdateService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<PriceUpdateService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = config.GetValue<int?>("Game:PriceRefreshMinutes") ?? 10;
        _interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceUpdateService start, interwał = {Interval}", _interval);

        // pierwsze pobranie wkrótce po starcie aplikacji
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

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
}

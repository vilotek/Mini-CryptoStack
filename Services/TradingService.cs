using CryptoDashboard.Data;
using CryptoDashboard.Models;
using CryptoDashboard.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Services;

public record TradeResult(bool Success, string Message);

public class TradingService
{
    private readonly AppDbContext _ctx;
    private readonly ILogger<TradingService> _logger;

    public TradingService(AppDbContext ctx, ILogger<TradingService> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    /// <summary>Kup za podaną kwotę USD.</summary>
    public async Task<TradeResult> BuyAsync(string userId, int coinId, decimal usdAmount)
    {
        if (usdAmount <= 0) return new(false, "Kwota musi być większa od zera.");

        await using var tx = await _ctx.Database.BeginTransactionAsync();

        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return new(false, "Użytkownik nie znaleziony.");

        if (user.VirtualBalance < usdAmount)
            return new(false, $"Za mało środków. Saldo: ${user.VirtualBalance:N2}");

        var price = await GetLatestPriceAsync(coinId);
        if (price is null) return new(false, "Brak aktualnej ceny dla tego coina.");

        var quantity = usdAmount / price.Value;

        // Aktualizuj holding
        var holding = await _ctx.Holdings
            .FirstOrDefaultAsync(h => h.UserId == userId && h.CoinId == coinId);

        if (holding is null)
        {
            holding = new Holding
            {
                UserId = userId,
                CoinId = coinId,
                Quantity = quantity,
                AvgBuyPrice = price.Value
            };
            _ctx.Holdings.Add(holding);
        }
        else
        {
            // średnia ważona
            var totalQty = holding.Quantity + quantity;
            holding.AvgBuyPrice = (holding.Quantity * holding.AvgBuyPrice + quantity * price.Value) / totalQty;
            holding.Quantity = totalQty;
        }

        user.VirtualBalance -= usdAmount;

        _ctx.Transactions.Add(new Transaction
        {
            UserId = userId,
            CoinId = coinId,
            Type = TransactionType.Buy,
            Quantity = quantity,
            PricePerUnit = price.Value,
            TotalUsd = usdAmount,
            Timestamp = DateTime.UtcNow
        });

        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        return new(true, $"Kupiono {quantity:N8} po cenie ${price.Value:N4}.");
    }

    /// <summary>Sprzedaj określoną ilość (np. 0.05 BTC).</summary>
    public async Task<TradeResult> SellAsync(string userId, int coinId, decimal quantity)
    {
        if (quantity <= 0) return new(false, "Ilość musi być większa od zera.");

        await using var tx = await _ctx.Database.BeginTransactionAsync();

        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return new(false, "Użytkownik nie znaleziony.");

        var holding = await _ctx.Holdings
            .FirstOrDefaultAsync(h => h.UserId == userId && h.CoinId == coinId);

        if (holding is null || holding.Quantity < quantity)
            return new(false, $"Za mało posiadanej ilości. Masz: {holding?.Quantity ?? 0:N8}");

        var price = await GetLatestPriceAsync(coinId);
        if (price is null) return new(false, "Brak aktualnej ceny dla tego coina.");

        var usdReceived = quantity * price.Value;

        holding.Quantity -= quantity;
        if (holding.Quantity < 0.00000001m)
            _ctx.Holdings.Remove(holding);
        // AvgBuyPrice nie zmieniamy przy sprzedaży - sprzedajemy część po pierwotnej średniej

        user.VirtualBalance += usdReceived;

        _ctx.Transactions.Add(new Transaction
        {
            UserId = userId,
            CoinId = coinId,
            Type = TransactionType.Sell,
            Quantity = quantity,
            PricePerUnit = price.Value,
            TotalUsd = usdReceived,
            Timestamp = DateTime.UtcNow
        });

        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        return new(true, $"Sprzedano {quantity:N8} za ${usdReceived:N2}.");
    }

    private async Task<decimal?> GetLatestPriceAsync(int coinId)
    {
        var pp = await _ctx.PricePoints
            .Where(p => p.CoinId == coinId)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync();
        return pp?.Price;
    }
}

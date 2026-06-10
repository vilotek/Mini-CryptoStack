using CryptoDashboard.Data;
using CryptoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Services;

public record HoldingValue(
    Coin Coin,
    decimal Quantity,
    decimal AvgBuyPrice,
    decimal CurrentPrice,
    decimal CurrentValue,
    decimal CostBasis,
    decimal PnlUsd,
    decimal PnlPercent);

public record PortfolioSnapshot(
    decimal CashBalance,
    decimal HoldingsValue,
    decimal TotalValue,
    decimal StartingBalance,
    decimal TotalPnlUsd,
    decimal TotalPnlPercent,
    List<HoldingValue> Holdings);

public record RankingRow(string UserName, decimal TotalValue, decimal PnlPercent);

public class PortfolioService
{
    private readonly AppDbContext _ctx;

    public PortfolioService(AppDbContext ctx) => _ctx = ctx;

    public async Task<PortfolioSnapshot> GetSnapshotAsync(string userId)
    {
        var user = await _ctx.Users.FirstAsync(u => u.Id == userId);
        var holdings = await _ctx.Holdings
            .Include(h => h.Coin)
            .Where(h => h.UserId == userId)
            .ToListAsync();

        var latestPrices = await GetLatestPricesAsync(holdings.Select(h => h.CoinId));

        var rows = new List<HoldingValue>();
        decimal holdingsValue = 0m;

        foreach (var h in holdings)
        {
            latestPrices.TryGetValue(h.CoinId, out var current);
            var costBasis = h.Quantity * h.AvgBuyPrice;
            var currentValue = h.Quantity * current;
            var pnl = currentValue - costBasis;
            var pnlPct = costBasis > 0 ? pnl / costBasis * 100m : 0m;

            rows.Add(new HoldingValue(h.Coin!, h.Quantity, h.AvgBuyPrice, current,
                                       currentValue, costBasis, pnl, pnlPct));
            holdingsValue += currentValue;
        }

        var total = user.VirtualBalance + holdingsValue;
        var totalPnl = total - user.StartingBalance;
        var totalPnlPct = user.StartingBalance > 0 ? totalPnl / user.StartingBalance * 100m : 0m;

        return new PortfolioSnapshot(
            user.VirtualBalance, holdingsValue, total,
            user.StartingBalance, totalPnl, totalPnlPct,
            rows.OrderByDescending(r => r.CurrentValue).ToList());
    }

    public async Task<List<RankingRow>> GetRankingAsync(int top = 50)
    {
        var users = await _ctx.Users.ToListAsync();
        var holdings = await _ctx.Holdings.ToListAsync();
        var coinIds = holdings.Select(h => h.CoinId).Distinct();
        var prices = await GetLatestPricesAsync(coinIds);

        var rows = users.Select(u =>
        {
            var hold = holdings.Where(h => h.UserId == u.Id);
            var holdValue = hold.Sum(h => h.Quantity * (prices.TryGetValue(h.CoinId, out var p) ? p : 0m));
            var total = u.VirtualBalance + holdValue;
            var pnlPct = u.StartingBalance > 0 ? (total - u.StartingBalance) / u.StartingBalance * 100m : 0m;
            return new RankingRow(u.UserName ?? u.Email ?? "(brak)", total, pnlPct);
        }).OrderByDescending(r => r.TotalValue).Take(top).ToList();

        return rows;
    }

    private async Task<Dictionary<int, decimal>> GetLatestPricesAsync(IEnumerable<int> coinIds)
    {
        var ids = coinIds.Distinct().ToList();
        var result = new Dictionary<int, decimal>();
        foreach (var id in ids)
        {
            var pp = await _ctx.PricePoints
                .Where(p => p.CoinId == id)
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync();
            result[id] = pp?.Price ?? 0m;
        }
        return result;
    }
}

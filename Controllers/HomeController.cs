using CryptoDashboard.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _ctx;

    public HomeController(AppDbContext ctx) => _ctx = ctx;

    public async Task<IActionResult> Index()
    {
        var coins = await _ctx.Coins.OrderBy(c => c.Symbol).ToListAsync();

        var latestPrices = new Dictionary<int, Models.PricePoint?>();
        foreach (var c in coins)
        {
            latestPrices[c.Id] = await _ctx.PricePoints
                .Where(p => p.CoinId == c.Id)
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync();
        }
        ViewBag.LatestPrices = latestPrices;
        ViewBag.LastUpdate = await _ctx.PricePoints
            .OrderByDescending(p => p.Timestamp)
            .Select(p => (DateTime?)p.Timestamp)
            .FirstOrDefaultAsync();
        return View(coins);
    }
}

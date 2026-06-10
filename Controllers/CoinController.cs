using CryptoDashboard.Data;
using CryptoDashboard.Models;
using CryptoDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Controllers;

public class CoinController : Controller
{
    private readonly AppDbContext _ctx;
    private readonly TradingService _trading;
    private readonly UserManager<ApplicationUser> _userMgr;

    public CoinController(AppDbContext ctx, TradingService trading, UserManager<ApplicationUser> userMgr)
    {
        _ctx = ctx;
        _trading = trading;
        _userMgr = userMgr;
    }

    public async Task<IActionResult> Details(int id)
    {
        var coin = await _ctx.Coins.FirstOrDefaultAsync(c => c.Id == id);
        if (coin is null) return NotFound();

        var latest = await _ctx.PricePoints
            .Where(p => p.CoinId == id)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync();
        var pointsCount = await _ctx.PricePoints.CountAsync(p => p.CoinId == id);

        ViewBag.Latest = latest;
        ViewBag.PointsCount = pointsCount;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = _userMgr.GetUserId(User);
            var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var holding = await _ctx.Holdings
                .FirstOrDefaultAsync(h => h.UserId == userId && h.CoinId == id);
            ViewBag.Balance = user?.VirtualBalance ?? 0m;
            ViewBag.Holding = holding;
        }

        return View(coin);
    }

    [HttpGet]
    public async Task<IActionResult> ChartData(int id)
    {
        var points = await _ctx.PricePoints
            .Where(p => p.CoinId == id)
            .OrderBy(p => p.Timestamp)
            .Select(p => new
            {
                t = new DateTimeOffset(p.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                p = p.Price
            })
            .ToListAsync();
        return Json(points);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(int id, decimal usdAmount)
    {
        var userId = _userMgr.GetUserId(User)!;
        var res = await _trading.BuyAsync(userId, id, usdAmount);
        TempData[res.Success ? "Msg" : "Err"] = res.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sell(int id, decimal quantity)
    {
        var userId = _userMgr.GetUserId(User)!;
        var res = await _trading.SellAsync(userId, id, quantity);
        TempData[res.Success ? "Msg" : "Err"] = res.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
}

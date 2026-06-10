using CryptoDashboard.Data;
using CryptoDashboard.Models;
using CryptoDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Controllers;

[Authorize]
public class PortfolioController : Controller
{
    private readonly AppDbContext _ctx;
    private readonly PortfolioService _portfolio;
    private readonly UserManager<ApplicationUser> _userMgr;

    public PortfolioController(AppDbContext ctx, PortfolioService portfolio, UserManager<ApplicationUser> userMgr)
    {
        _ctx = ctx;
        _portfolio = portfolio;
        _userMgr = userMgr;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userMgr.GetUserId(User)!;
        var snapshot = await _portfolio.GetSnapshotAsync(userId);
        var recent = await _ctx.Transactions
            .Include(t => t.Coin)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Timestamp)
            .Take(20)
            .ToListAsync();
        ViewBag.RecentTx = recent;
        return View(snapshot);
    }
}

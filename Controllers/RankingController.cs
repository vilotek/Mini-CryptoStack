using CryptoDashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace CryptoDashboard.Controllers;

public class RankingController : Controller
{
    private readonly PortfolioService _portfolio;

    public RankingController(PortfolioService portfolio) => _portfolio = portfolio;

    public async Task<IActionResult> Index()
    {
        var rows = await _portfolio.GetRankingAsync(50);
        return View(rows);
    }
}

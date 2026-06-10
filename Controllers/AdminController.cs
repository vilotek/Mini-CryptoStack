using CryptoDashboard.Data;
using CryptoDashboard.Models;
using CryptoDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _ctx;
    private readonly CoinGeckoService _coinGecko;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly SiteSettingsService _settings;

    private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
    private const long MaxImageSizeBytes = 2 * 1024 * 1024; // 2 MB

    public AdminController(
        AppDbContext ctx,
        CoinGeckoService coinGecko,
        IConfiguration config,
        IWebHostEnvironment env,
        SiteSettingsService settings)
    {
        _ctx = ctx;
        _coinGecko = coinGecko;
        _config = config;
        _env = env;
        _settings = settings;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.UsersCount = await _ctx.Users.CountAsync();
        ViewBag.CoinsCount = await _ctx.Coins.CountAsync();
        ViewBag.TxCount = await _ctx.Transactions.CountAsync();
        ViewBag.LastUpdate = await _ctx.PricePoints
            .OrderByDescending(p => p.Timestamp)
            .Select(p => (DateTime?)p.Timestamp)
            .FirstOrDefaultAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshPrices()
    {
        var n = await _coinGecko.RefreshPricesAsync();
        TempData["Msg"] = n > 0
            ? $"Zapisano {n} nowych punktów cenowych."
            : "Nie udało się pobrać danych.";
        return RedirectToAction(nameof(Index));
    }

    // === Ustawienia strony ===

    public async Task<IActionResult> Settings()
    {
        var all = await _settings.GetAllAsync();
        ViewBag.AppName = all.GetValueOrDefault(SiteSetting.KeyAppName, "CryptoDashboard");
        ViewBag.Tagline = all.GetValueOrDefault(SiteSetting.KeyTagline, "");
        ViewBag.FooterText = all.GetValueOrDefault(SiteSetting.KeyFooterText, "");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(string appName, string tagline, string footerText)
    {
        await _settings.SetAsync(SiteSetting.KeyAppName, (appName ?? "").Trim());
        await _settings.SetAsync(SiteSetting.KeyTagline, (tagline ?? "").Trim());
        await _settings.SetAsync(SiteSetting.KeyFooterText, (footerText ?? "").Trim());
        TempData["Msg"] = "Ustawienia zapisane.";
        return RedirectToAction(nameof(Settings));
    }

    // === Użytkownicy ===

    public async Task<IActionResult> Users()
    {
        var users = await _ctx.Users.OrderBy(u => u.Email).ToListAsync();
        return View(users);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUser(string id)
    {
        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var startBalance = _config.GetValue<decimal?>("Game:StartingBalanceUsd") ?? 10000m;

        await using var tx = await _ctx.Database.BeginTransactionAsync();

        var holdings = _ctx.Holdings.Where(h => h.UserId == id);
        var txs = _ctx.Transactions.Where(t => t.UserId == id);
        _ctx.Holdings.RemoveRange(holdings);
        _ctx.Transactions.RemoveRange(txs);

        user.VirtualBalance = startBalance;
        user.StartingBalance = startBalance;

        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Msg"] = $"Zresetowano portfel: {user.Email}";
        return RedirectToAction(nameof(Users));
    }

    // === Coiny ===

    public async Task<IActionResult> Coins(int? editId)
    {
        var coins = await _ctx.Coins.OrderBy(c => c.Symbol).ToListAsync();
        Coin formCoin = new();
        if (editId.HasValue)
        {
            var found = coins.FirstOrDefault(c => c.Id == editId.Value);
            if (found is not null) formCoin = found;
        }
        ViewBag.Coins = coins;
        return View(formCoin);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCoin(Coin coin, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Coins = await _ctx.Coins.OrderBy(c => c.Symbol).ToListAsync();
            return View(nameof(Coins), coin);
        }

        // Upload pliku - jeśli załączony, zastępuje URL
        if (imageFile is not null && imageFile.Length > 0)
        {
            var uploadResult = await TrySaveUploadedImageAsync(imageFile);
            if (uploadResult.Url is null)
            {
                TempData["Err"] = uploadResult.Error ?? "Nie udało się zapisać pliku.";
                ViewBag.Coins = await _ctx.Coins.OrderBy(c => c.Symbol).ToListAsync();
                return View(nameof(Coins), coin);
            }
            coin.ImageUrl = uploadResult.Url;
        }

        coin.Symbol = coin.Symbol.Trim().ToUpperInvariant();
        coin.CoinGeckoId = coin.CoinGeckoId.Trim().ToLowerInvariant();

        if (coin.Id == 0)
        {
            _ctx.Coins.Add(coin);
            TempData["Msg"] = $"Dodano {coin.Symbol}.";
        }
        else
        {
            _ctx.Coins.Update(coin);
            TempData["Msg"] = $"Zaktualizowano {coin.Symbol}.";
        }
        await _ctx.SaveChangesAsync();
        return RedirectToAction(nameof(Coins));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCoin(int id)
    {
        var c = await _ctx.Coins.FindAsync(id);
        if (c is not null)
        {
            _ctx.Coins.Remove(c);
            await _ctx.SaveChangesAsync();
            TempData["Msg"] = $"Usunięto {c.Symbol}.";
        }
        return RedirectToAction(nameof(Coins));
    }

    private async Task<(string? Url, string? Error)> TrySaveUploadedImageAsync(IFormFile file)
    {
        if (file.Length > MaxImageSizeBytes)
            return (null, $"Plik za duży (max {MaxImageSizeBytes / 1024 / 1024} MB).");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return (null, "Niedozwolony format. Dozwolone: " + string.Join(", ", AllowedImageExtensions));

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"coin_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return ($"/uploads/{fileName}", null);
    }
}

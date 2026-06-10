using CryptoDashboard.Data;
using CryptoDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Controllers;

public class ChatController : Controller
{
    private readonly AppDbContext _ctx;
    private readonly UserManager<ApplicationUser> _userMgr;

    public ChatController(AppDbContext ctx, UserManager<ApplicationUser> userMgr)
    {
        _ctx = ctx;
        _userMgr = userMgr;
    }

    public async Task<IActionResult> Index()
    {
        var msgs = await _ctx.ChatMessages
            .Include(m => m.User)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync();
        msgs.Reverse(); // chronologicznie od najstarszej do najnowszej
        return View(msgs);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return RedirectToAction(nameof(Index));

        content = content.Trim();
        if (content.Length > 500) content = content[..500];

        var userId = _userMgr.GetUserId(User)!;
        _ctx.ChatMessages.Add(new ChatMessage
        {
            UserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        });
        await _ctx.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

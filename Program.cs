using CryptoDashboard.Data;
using CryptoDashboard.Models;
using CryptoDashboard.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DbContext (SQLite)
var conn = builder.Configuration.GetConnectionString("DefaultConnection")
           ?? "Data Source=crypto.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(conn));

// Identity z rolami
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.SignIn.RequireConfirmedAccount = false;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireDigit = false;
        opt.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Identity/Account/Login";
    opt.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// "Pusty" EmailSender - Identity UI tego wymaga; nic nie wysyłamy
builder.Services.AddTransient<IEmailSender, NoOpEmailSender>();

// Typed HttpClient dla CoinGecko
builder.Services.AddHttpClient<CoinGeckoService>((sp, c) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    c.BaseAddress = new Uri(cfg["CoinGecko:BaseUrl"] ?? "https://api.coingecko.com/api/v3/");
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoDashboard/1.0");
});

// Cache + serwisy domenowe
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TradingService>();
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddScoped<SiteSettingsService>();

// Automatyczne odświeżanie cen w tle
builder.Services.AddHostedService<PriceUpdateService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // dla Identity UI

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Seed bazy
using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbInitializer.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Błąd podczas seedowania bazy.");
    }
}

app.Run();

/// <summary>
/// Identity UI wymaga IEmailSender. Nic nie wysyłamy - to projekt edukacyjny.
/// </summary>
public class NoOpEmailSender : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
        => Task.CompletedTask;
}

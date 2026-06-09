using CryptoDashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Data;

public static class DbInitializer
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<AppDbContext>();
        await ctx.Database.MigrateAsync();

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { AdminRole, UserRole })
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));

        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        const string adminEmail = "admin@dashboard.local";
        const string adminPassword = "Admin123!";

        var admin = await userMgr.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                VirtualBalance = 10000m,
                StartingBalance = 10000m
            };
            var res = await userMgr.CreateAsync(admin, adminPassword);
            if (res.Succeeded)
                await userMgr.AddToRoleAsync(admin, AdminRole);
        }

        if (!await ctx.SiteSettings.AnyAsync())
        {
            ctx.SiteSettings.AddRange(
                new SiteSetting { Key = SiteSetting.KeyAppName, Value = "CryptoDashboard" },
                new SiteSetting { Key = SiteSetting.KeyTagline, Value = "Wirtualna gra inwestycyjna na prawdziwych cenach krypto" },
                new SiteSetting { Key = SiteSetting.KeyFooterText, Value = "Projekt zaliczeniowy. Dane cenowe: CoinGecko." }
            );
            await ctx.SaveChangesAsync();
        }

        if (!await ctx.Coins.AnyAsync())
        {
            ctx.Coins.AddRange(
                new Coin { Symbol = "BTC", Name = "Bitcoin", CoinGeckoId = "bitcoin",
                           ImageUrl = "https://assets.coingecko.com/coins/images/1/small/bitcoin.png" },
                new Coin { Symbol = "ETH", Name = "Ethereum", CoinGeckoId = "ethereum",
                           ImageUrl = "https://assets.coingecko.com/coins/images/279/small/ethereum.png" },
                new Coin { Symbol = "SOL", Name = "Solana", CoinGeckoId = "solana",
                           ImageUrl = "https://assets.coingecko.com/coins/images/4128/small/solana.png" },
                new Coin { Symbol = "ADA", Name = "Cardano", CoinGeckoId = "cardano",
                           ImageUrl = "https://assets.coingecko.com/coins/images/975/small/cardano.png" },
                new Coin { Symbol = "DOGE", Name = "Dogecoin", CoinGeckoId = "dogecoin",
                           ImageUrl = "https://assets.coingecko.com/coins/images/5/small/dogecoin.png" },
                new Coin { Symbol = "XRP", Name = "XRP", CoinGeckoId = "ripple",
                           ImageUrl = "https://assets.coingecko.com/coins/images/44/small/xrp-symbol-white-128.png" }
            );
            await ctx.SaveChangesAsync();
        }
    }
}

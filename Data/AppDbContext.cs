using CryptoDashboard.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CryptoDashboard.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Coin> Coins => Set<Coin>();
    public DbSet<PricePoint> PricePoints => Set<PricePoint>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.VirtualBalance).HasPrecision(20, 4);
            e.Property(u => u.StartingBalance).HasPrecision(20, 4);
        });

        b.Entity<Coin>(e =>
        {
            e.HasIndex(c => c.Symbol).IsUnique();
            e.HasIndex(c => c.CoinGeckoId).IsUnique();
        });

        b.Entity<PricePoint>(e =>
        {
            e.Property(p => p.Price).HasPrecision(20, 8);
            e.Property(p => p.Change24h).HasPrecision(10, 4);
            e.HasIndex(p => new { p.CoinId, p.Timestamp });
            e.HasOne(p => p.Coin)
             .WithMany(c => c.PricePoints)
             .HasForeignKey(p => p.CoinId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Holding>(e =>
        {
            e.Property(h => h.Quantity).HasPrecision(28, 12);
            e.Property(h => h.AvgBuyPrice).HasPrecision(20, 8);
            e.HasIndex(h => new { h.UserId, h.CoinId }).IsUnique();
            e.HasOne(h => h.User)
             .WithMany(u => u.Holdings)
             .HasForeignKey(h => h.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.Coin)
             .WithMany(c => c.Holdings)
             .HasForeignKey(h => h.CoinId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Transaction>(e =>
        {
            e.Property(t => t.Quantity).HasPrecision(28, 12);
            e.Property(t => t.PricePerUnit).HasPrecision(20, 8);
            e.Property(t => t.TotalUsd).HasPrecision(20, 4);
            e.HasIndex(t => new { t.UserId, t.Timestamp });
            e.HasOne(t => t.User)
             .WithMany(u => u.Transactions)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.HasIndex(m => m.CreatedAt);
            e.HasOne(m => m.User)
             .WithMany()
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SiteSetting>(e =>
        {
            e.HasIndex(s => s.Key).IsUnique();
        });
    }
}

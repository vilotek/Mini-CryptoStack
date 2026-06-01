namespace CryptoDashboard.Models;

public class PricePoint
{
    public long Id { get; set; }

    public int CoinId { get; set; }
    public Coin? Coin { get; set; }

    public decimal Price { get; set; }

    public decimal? Change24h { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

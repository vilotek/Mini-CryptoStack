using CryptoDashboard.Models.Enums;

namespace CryptoDashboard.Models;

public class Transaction
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int CoinId { get; set; }
    public Coin? Coin { get; set; }

    public TransactionType Type { get; set; }

    public decimal Quantity { get; set; }

    public decimal PricePerUnit { get; set; }

    public decimal TotalUsd { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

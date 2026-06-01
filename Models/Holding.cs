namespace CryptoDashboard.Models;

public class Holding
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int CoinId { get; set; }
    public Coin? Coin { get; set; }

    /// <summary>Ilość sztuk posiadanych (np. 0.5 BTC).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Średnia ważona cena zakupu — używana do liczenia P&amp;L.</summary>
    public decimal AvgBuyPrice { get; set; }
}

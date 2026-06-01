using System.ComponentModel.DataAnnotations;

namespace CryptoDashboard.Models;

public class Coin
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Nazwa")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "CoinGecko ID")]
    public string CoinGeckoId { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "URL logo")]
    public string? ImageUrl { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public List<PricePoint> PricePoints { get; set; } = new();
    public List<Holding> Holdings { get; set; } = new();
}

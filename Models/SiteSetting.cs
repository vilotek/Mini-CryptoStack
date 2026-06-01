using System.ComponentModel.DataAnnotations;

namespace CryptoDashboard.Models;

public class SiteSetting
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Wbudowane klucze - używaj tych stałych zamiast magicznych stringów
    public const string KeyAppName = "AppName";
    public const string KeyTagline = "Tagline";
    public const string KeyFooterText = "FooterText";
}

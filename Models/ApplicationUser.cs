using Microsoft.AspNetCore.Identity;

namespace CryptoDashboard.Models;

public class ApplicationUser : IdentityUser
{
    /// <summary>Aktualne saldo USD (wirtualne) — dostępne do wydania.</summary>
    public decimal VirtualBalance { get; set; } = 10000m;

    /// <summary>Saldo startowe — do liczenia P&amp;L.</summary>
    public decimal StartingBalance { get; set; } = 10000m;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public List<Holding> Holdings { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
}

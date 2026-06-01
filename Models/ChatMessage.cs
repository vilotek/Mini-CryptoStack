using System.ComponentModel.DataAnnotations;

namespace CryptoDashboard.Models;

public class ChatMessage
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(500)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }

    public int UserId { get; set; }

    [Required]
    public string Action { get; set; } = string.Empty;

    [Required]
    public string TableAffected { get; set; } = string.Empty;

    public int? RecordId { get; set; }

    public string? Details { get; set; }

    [Required]
    public string Timestamp { get; set; } = string.Empty;

    public User? User { get; set; }
}

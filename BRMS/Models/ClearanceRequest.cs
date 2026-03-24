using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class ClearanceRequest
{
    [Key]
    public int ClearanceId { get; set; }

    public int ResidentId { get; set; }

    [Required]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    public string RequestedAt { get; set; } = string.Empty;

    public string? ProcessedAt { get; set; }

    public int? ProcessedBy { get; set; }

    public string? Remarks { get; set; }

    public string? ValidUntil { get; set; }

    public Resident? Resident { get; set; }

    public User? ProcessedByUser { get; set; }
}

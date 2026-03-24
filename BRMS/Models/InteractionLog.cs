using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class InteractionLog
{
    [Key]
    public int InteractionLogId { get; set; }

    public int ResidentId { get; set; }

    [Required]
    public string InteractionType { get; set; } = string.Empty;

    [Required]
    public string Notes { get; set; } = string.Empty;

    [Required]
    public string InteractionDate { get; set; } = string.Empty;

    [Required]
    public string CreatedAt { get; set; } = string.Empty;

    public int CreatedBy { get; set; }

    public Resident? Resident { get; set; }

    public User? CreatedByUser { get; set; }
}

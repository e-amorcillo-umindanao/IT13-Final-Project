using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class BlotterEntry
{
    [Key]
    public int BlotterEntryId { get; set; }

    [Required]
    public string BlotterNumber { get; set; } = string.Empty;

    public int? ComplainantId { get; set; }

    [Required]
    public string ComplainantName { get; set; } = string.Empty;

    [Required]
    public string RespondentName { get; set; } = string.Empty;

    [Required]
    public string IncidentType { get; set; } = string.Empty;

    [Required]
    public string IncidentDate { get; set; } = string.Empty;

    [Required]
    public string IncidentDetails { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    public string? Resolution { get; set; }

    [Required]
    public string FiledAt { get; set; } = string.Empty;

    public int FiledBy { get; set; }

    public string? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public Resident? Complainant { get; set; }

    public User? FiledByUser { get; set; }

    public User? UpdatedByUser { get; set; }
}

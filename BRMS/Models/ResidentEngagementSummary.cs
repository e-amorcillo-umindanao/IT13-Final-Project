namespace BRMS.Models;

public class ResidentEngagementSummary
{
    public int ResidentId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? PurokName { get; set; }

    public int Score { get; set; }

    public string Label { get; set; } = string.Empty;

    public DateTime? LastInteractionDate { get; set; }
}

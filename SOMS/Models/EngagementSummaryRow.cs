namespace SOMS.Models;

public sealed class EngagementSummaryRow
{
    public int MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime? LastInteraction { get; set; }
    public int InteractionsThisSemester { get; set; }
}

namespace SOMS.Models;

public class InteractionLog
{
    public int InteractionLogId { get; set; }
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public string InteractionType { get; set; } = "Call";
    public string Notes { get; set; } = string.Empty;
    public DateTime InteractionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
}

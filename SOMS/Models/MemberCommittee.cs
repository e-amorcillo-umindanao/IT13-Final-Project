namespace SOMS.Models;

public class MemberCommittee
{
    public int MemberCommitteeId { get; set; }
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int CommitteeId { get; set; }
    public Committee Committee { get; set; } = null!;
    public string? CommitteeRole { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

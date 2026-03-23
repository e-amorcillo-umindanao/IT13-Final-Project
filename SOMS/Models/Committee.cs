namespace SOMS.Models;

public class Committee
{
    public int CommitteeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<MemberCommittee> MemberCommittees { get; set; } = new List<MemberCommittee>();
}

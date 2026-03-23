namespace SOMS.Models;

public sealed class MemberLookupItem
{
    public int MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;

    public string DisplayName => $"{FullName} ({StudentId})";
}

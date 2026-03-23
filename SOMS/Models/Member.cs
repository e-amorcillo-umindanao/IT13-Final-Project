using System.ComponentModel.DataAnnotations.Schema;

namespace SOMS.Models;

public class Member
{
    public int MemberId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Active";
    public string RecruitmentStage { get; set; } = "Member";
    public DateTime JoinDate { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<MemberCommittee> MemberCommittees { get; set; } = new List<MemberCommittee>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<InteractionLog> InteractionLogs { get; set; } = new List<InteractionLog>();

    [NotMapped]
    public int SemesterEventCount { get; set; }

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();
}

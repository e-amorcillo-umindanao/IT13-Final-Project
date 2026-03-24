using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class User
{
    [Key]
    public int UserId { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public int? ResidentId { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public string CreatedAt { get; set; } = string.Empty;

    public string? LastLoginAt { get; set; }

    public Role? Role { get; set; }

    public Resident? Resident { get; set; }

    public ICollection<Household> CreatedHouseholds { get; set; } = new List<Household>();

    public ICollection<Resident> CreatedResidents { get; set; } = new List<Resident>();

    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();

    public ICollection<Attendance> RecordedAttendances { get; set; } = new List<Attendance>();

    public ICollection<InteractionLog> CreatedInteractionLogs { get; set; } = new List<InteractionLog>();

    public ICollection<BlotterEntry> FiledBlotterEntries { get; set; } = new List<BlotterEntry>();

    public ICollection<BlotterEntry> UpdatedBlotterEntries { get; set; } = new List<BlotterEntry>();

    public ICollection<ClearanceRequest> ProcessedClearanceRequests { get; set; } = new List<ClearanceRequest>();

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public ICollection<BarangaySettings> UpdatedBarangaySettings { get; set; } = new List<BarangaySettings>();
}

using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class Attendance
{
    [Key]
    public int AttendanceId { get; set; }

    public int EventId { get; set; }

    public int ResidentId { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    public string RecordedAt { get; set; } = string.Empty;

    public int RecordedBy { get; set; }

    public Event? Event { get; set; }

    public Resident? Resident { get; set; }

    public User? RecordedByUser { get; set; }
}

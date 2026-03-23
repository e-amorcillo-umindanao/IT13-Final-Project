namespace SOMS.Models;

public class Attendance
{
    public int AttendanceId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public string Status { get; set; } = "Present";
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public int? RecordedBy { get; set; }
    public User? RecordedByUser { get; set; }
}

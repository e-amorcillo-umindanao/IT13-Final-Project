namespace SOMS.Models;

public class Event
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string? Venue { get; set; }
    public string EventType { get; set; } = "General Assembly";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

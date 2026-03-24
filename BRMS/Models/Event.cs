using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class Event
{
    [Key]
    public int EventId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string EventDate { get; set; } = string.Empty;

    public string? Venue { get; set; }

    [Required]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public string CreatedAt { get; set; } = string.Empty;

    public int CreatedBy { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

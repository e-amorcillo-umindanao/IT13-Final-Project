using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class Resident
{
    [Key]
    public int ResidentId { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string BirthDate { get; set; } = string.Empty;

    [Required]
    public string Gender { get; set; } = string.Empty;

    public string? CivilStatus { get; set; }

    public string? ContactNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public int? PurokId { get; set; }

    public int? HouseholdId { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    public string? Categories { get; set; }

    [Required]
    public string ResidencySince { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    [Required]
    public string CreatedAt { get; set; } = string.Empty;

    public int CreatedBy { get; set; }

    public Purok? Purok { get; set; }

    public Household? Household { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<Household> HeadedHouseholds { get; set; } = new List<Household>();

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public ICollection<InteractionLog> InteractionLogs { get; set; } = new List<InteractionLog>();

    public ICollection<BlotterEntry> BlotterEntries { get; set; } = new List<BlotterEntry>();

    public ICollection<ClearanceRequest> ClearanceRequests { get; set; } = new List<ClearanceRequest>();
}

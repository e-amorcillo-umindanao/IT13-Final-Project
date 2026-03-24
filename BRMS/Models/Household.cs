using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class Household
{
    [Key]
    public int HouseholdId { get; set; }

    [Required]
    public string HouseholdNumber { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;

    public int? PurokId { get; set; }

    public int? HeadResidentId { get; set; }

    [Required]
    public string CreatedAt { get; set; } = string.Empty;

    public int CreatedBy { get; set; }

    public Purok? Purok { get; set; }

    public Resident? HeadResident { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<Resident> Residents { get; set; } = new List<Resident>();
}

using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class Purok
{
    [Key]
    public int PurokId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string CreatedAt { get; set; } = string.Empty;

    public ICollection<Household> Households { get; set; } = new List<Household>();

    public ICollection<Resident> Residents { get; set; } = new List<Resident>();
}

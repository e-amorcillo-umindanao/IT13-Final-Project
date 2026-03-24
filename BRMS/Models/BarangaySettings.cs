using System.ComponentModel.DataAnnotations;

namespace BRMS.Models;

public class BarangaySettings
{
    [Key]
    public int SettingId { get; set; }

    [Required]
    public string BarangayName { get; set; } = string.Empty;

    [Required]
    public string Municipality { get; set; } = string.Empty;

    [Required]
    public string Province { get; set; } = string.Empty;

    public string? CaptainName { get; set; }

    public string? SecretaryName { get; set; }

    public string? ContactNumber { get; set; }

    public string? LogoPath { get; set; }

    [Required]
    public string UpdatedAt { get; set; } = string.Empty;

    public int UpdatedBy { get; set; }

    public User? UpdatedByUser { get; set; }
}

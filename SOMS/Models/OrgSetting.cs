namespace SOMS.Models;

public class OrgSetting
{
    public int SettingId { get; set; } = 1;
    public string OrgName { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string SemesterLabel { get; set; } = string.Empty;
    public DateTime? SemesterStart { get; set; }
    public string? AdviserName { get; set; }
    public string? PresidentName { get; set; }
    public string? LogoPath { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedBy { get; set; }
    public User? UpdatedByUser { get; set; }
}

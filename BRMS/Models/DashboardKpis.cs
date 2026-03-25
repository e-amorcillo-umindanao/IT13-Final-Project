namespace BRMS.Models;

public class DashboardKpis
{
    public int TotalResidents { get; set; }
    public int TotalHouseholds { get; set; }
    public int NewResidentsThisMonth { get; set; }
    public int TotalBlotterOpen { get; set; }
    public int TotalClearancePending { get; set; }
    public int HighEngagementCount { get; set; }
    public int MediumEngagementCount { get; set; }
    public int LowEngagementCount { get; set; }
    public int TotalEventsThisMonth { get; set; }
}

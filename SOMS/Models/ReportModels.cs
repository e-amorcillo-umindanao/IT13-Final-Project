namespace SOMS.Models;

public sealed record DashboardKpis(
    int TotalMembers,
    double ActivePercent,
    int UpcomingEventsCount,
    int LowEngagementCount,
    int HighEngagementCount,
    int MediumEngagementCount);

public sealed record AttendanceTrendPoint(
    string EventTitle,
    DateTime EventDate,
    double AttendanceRate);

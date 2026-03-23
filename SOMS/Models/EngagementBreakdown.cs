namespace SOMS.Models;

public sealed record EngagementBreakdown(
    double AttendancePoints,
    double InteractionPoints,
    double RecencyPoints,
    int TotalScore,
    string Label);

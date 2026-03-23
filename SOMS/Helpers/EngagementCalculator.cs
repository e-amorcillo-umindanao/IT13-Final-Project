using SOMS.Models;

namespace SOMS.Helpers;

public static class EngagementCalculator
{
    public static int Compute(Member member, DateTime semesterStart)
        => GetBreakdown(member, semesterStart).TotalScore;

    public static string GetLabel(int score)
        => score >= 75 ? "High" : score >= 40 ? "Medium" : "Low";

    public static EngagementBreakdown GetBreakdown(Member member, DateTime semesterStart)
    {
        var totalEvents = member.SemesterEventCount > 0
            ? member.SemesterEventCount
            : member.Attendances
                .Where(attendance => attendance.Event is not null && attendance.Event.EventDate >= semesterStart)
                .Select(attendance => attendance.EventId)
                .Distinct()
                .Count();

        var attended = member.Attendances.Count(attendance =>
            string.Equals(attendance.Status, "Present", StringComparison.OrdinalIgnoreCase) &&
            attendance.Event is not null &&
            attendance.Event.EventDate >= semesterStart);

        var attendancePoints = totalEvents == 0
            ? 50
            : (attended / (double)totalEvents) * 50;

        var interactions = member.InteractionLogs.Count(interaction => interaction.InteractionDate >= semesterStart);
        var interactionPoints = Math.Min(interactions / 10.0, 1.0) * 30;

        var lastInteraction = member.InteractionLogs
            .OrderByDescending(interaction => interaction.InteractionDate)
            .FirstOrDefault()
            ?.InteractionDate;

        var recencyPoints = 0d;
        if (lastInteraction.HasValue)
        {
            var normalizedLastInteraction = NormalizeUtc(lastInteraction.Value);
            var daysSince = (DateTime.UtcNow - normalizedLastInteraction).TotalDays;

            recencyPoints = daysSince <= 7
                ? 20
                : daysSince <= 14
                    ? 16
                    : daysSince <= 30
                        ? 10
                        : daysSince <= 60
                            ? 5
                            : 0;
        }

        var totalScore = (int)(attendancePoints + interactionPoints + recencyPoints);
        totalScore = Math.Clamp(totalScore, 0, 100);

        return new EngagementBreakdown(
            attendancePoints,
            interactionPoints,
            recencyPoints,
            totalScore,
            GetLabel(totalScore));
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

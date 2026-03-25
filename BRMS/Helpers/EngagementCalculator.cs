namespace BRMS.Helpers;

public static class EngagementCalculator
{
    public static int CalculateScore(int totalEvents, int eventsAttended, int interactionsLast6Months, DateTime? lastInteractionDate)
    {
        var safeTotalEvents = Math.Max(totalEvents, 0);
        var safeEventsAttended = Math.Max(eventsAttended, 0);
        var safeInteractions = Math.Max(interactionsLast6Months, 0);

        var attendanceComponent = safeTotalEvents == 0
            ? 0
            : (double)safeEventsAttended / safeTotalEvents * 50d;

        var interactionComponent = Math.Min(safeInteractions, 10) / 10d * 30d;

        var recencyComponent = 0d;
        if (lastInteractionDate.HasValue)
        {
            var daysSinceInteraction = Math.Max(0d, (DateTime.Today - lastInteractionDate.Value.Date).TotalDays);
            if (daysSinceInteraction <= 30d)
            {
                recencyComponent = 20d;
            }
            else if (daysSinceInteraction < 90d)
            {
                recencyComponent = (1d - ((daysSinceInteraction - 30d) / 60d)) * 20d;
            }
        }

        var totalScore = attendanceComponent + interactionComponent + recencyComponent;
        return (int)Math.Round(Math.Clamp(totalScore, 0d, 100d), MidpointRounding.AwayFromZero);
    }
}

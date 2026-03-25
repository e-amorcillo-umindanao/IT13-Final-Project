using BRMS.Data;
using BRMS.Helpers;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class EngagementService
{
    private readonly AppDbContext _dbContext;
    private readonly AttendanceService _attendanceService;
    private readonly InteractionService _interactionService;

    public EngagementService(
        AppDbContext dbContext,
        AttendanceService attendanceService,
        InteractionService interactionService)
    {
        _dbContext = dbContext;
        _attendanceService = attendanceService;
        _interactionService = interactionService;
    }

    public async Task<int> GetEngagementScoreAsync(int residentId)
    {
        var residentExists = await _dbContext.Residents
            .AsNoTracking()
            .AnyAsync(resident => resident.ResidentId == residentId && !resident.IsDeleted);

        if (!residentExists)
        {
            return 0;
        }

        var attendances = await _attendanceService.GetAttendancesByResidentAsync(residentId);
        var interactions = await _interactionService.GetInteractionsByResidentAsync(residentId);
        var totalEvents = await _attendanceService.GetTotalEventsAsync();

        var eventsAttended = attendances.Count(attendance => IsAttendedStatus(attendance.Status));
        var cutoffDate = DateTime.Today.AddMonths(-6);
        var interactionsLast6Months = interactions.Count(log =>
        {
            var interactionDate = ParseDate(log.InteractionDate);
            return interactionDate.HasValue && interactionDate.Value.Date >= cutoffDate;
        });

        var lastInteractionDate = interactions
            .Select(log => ParseDate(log.InteractionDate))
            .Where(date => date.HasValue)
            .Max();

        return EngagementCalculator.CalculateScore(totalEvents, eventsAttended, interactionsLast6Months, lastInteractionDate);
    }

    public async Task<List<ResidentEngagementSummary>> GetAllEngagementSummariesAsync()
    {
        var residents = await _dbContext.Residents
            .Include(resident => resident.Purok)
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted && resident.Status == "Active")
            .OrderBy(resident => resident.LastName)
            .ThenBy(resident => resident.FirstName)
            .ToListAsync();

        var summaries = new List<ResidentEngagementSummary>(residents.Count);

        foreach (var resident in residents)
        {
            var interactions = await _interactionService.GetInteractionsByResidentAsync(resident.ResidentId);
            var score = await GetEngagementScoreAsync(resident.ResidentId);
            var lastInteractionDate = interactions
                .Select(log => ParseDate(log.InteractionDate))
                .Where(date => date.HasValue)
                .Max();

            summaries.Add(new ResidentEngagementSummary
            {
                ResidentId = resident.ResidentId,
                FullName = GetResidentName(resident),
                PurokName = resident.Purok?.Name,
                Score = score,
                Label = GetLabel(score),
                LastInteractionDate = lastInteractionDate
            });
        }

        return summaries;
    }

    public async Task<EngagementDistribution> GetEngagementDistributionAsync()
    {
        var summaries = await GetAllEngagementSummariesAsync();

        return new EngagementDistribution
        {
            High = summaries.Count(summary => summary.Label == "High"),
            Medium = summaries.Count(summary => summary.Label == "Medium"),
            Low = summaries.Count(summary => summary.Label == "Low")
        };
    }

    private static bool IsAttendedStatus(string status)
    {
        return status.Equals("Attended", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Present", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? ParseDate(string? value)
    {
        return DateTime.TryParse(value, out var parsedDate) ? parsedDate : null;
    }

    private static string GetLabel(int score)
    {
        return score >= 75 ? "High" : score >= 40 ? "Medium" : "Low";
    }

    private static string GetResidentName(Resident resident)
    {
        return string.Join(" ", new[] { resident.FirstName, resident.MiddleName, resident.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}

using BRMS.Data;
using BRMS.Helpers;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BRMS.Services;

public class ReportService
{
    private readonly AppDbContext _dbContext;

    public ReportService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardKpis> GetDashboardKpisAsync()
    {
        var now = DateTime.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var residents = await _dbContext.Residents
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted)
            .ToListAsync();

        var activeResidents = residents
            .Where(resident => HasStatus(resident.Status, "Active"))
            .ToList();

        var currentScores = await CalculateCurrentEngagementScoresAsync(activeResidents);

        var eventDates = await _dbContext.Events
            .AsNoTracking()
            .Select(eventItem => eventItem.EventDate)
            .ToListAsync();

        return new DashboardKpis
        {
            TotalResidents = residents.Count,
            TotalHouseholds = await _dbContext.Households.AsNoTracking().CountAsync(),
            NewResidentsThisMonth = residents.Count(resident =>
            {
                var createdAt = ParseDate(resident.CreatedAt);
                return createdAt.HasValue && createdAt.Value.Date >= monthStart && createdAt.Value.Date < nextMonthStart;
            }),
            TotalBlotterOpen = await _dbContext.BlotterEntries
                .AsNoTracking()
                .CountAsync(entry => entry.Status == "Open"),
            TotalClearancePending = await _dbContext.ClearanceRequests
                .AsNoTracking()
                .CountAsync(request => request.Status == "Pending"),
            HighEngagementCount = currentScores.Count(score => score >= 75),
            MediumEngagementCount = currentScores.Count(score => score >= 40 && score <= 74),
            LowEngagementCount = currentScores.Count(score => score <= 39),
            TotalEventsThisMonth = eventDates.Count(eventDateValue =>
            {
                var eventDate = ParseDate(eventDateValue);
                return eventDate.HasValue &&
                       eventDate.Value.Date >= monthStart &&
                       eventDate.Value.Date < nextMonthStart;
            })
        };
    }

    public async Task<List<MonthlyAveragePoint>> GetEngagementTrendAsync(int months = 6)
    {
        months = NormalizeMonths(months);

        var activeResidents = await _dbContext.Residents
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted && resident.Status == "Active")
            .ToListAsync();

        var residentIds = activeResidents.Select(resident => resident.ResidentId).ToList();
        var events = await _dbContext.Events
            .AsNoTracking()
            .ToListAsync();
        var attendances = await _dbContext.Attendances
            .AsNoTracking()
            .Include(attendance => attendance.Event)
            .Where(attendance => residentIds.Contains(attendance.ResidentId))
            .ToListAsync();
        var interactions = await _dbContext.InteractionLogs
            .AsNoTracking()
            .Where(log => residentIds.Contains(log.ResidentId))
            .ToListAsync();

        var attendanceByResident = attendances
            .GroupBy(attendance => attendance.ResidentId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var interactionsByResident = interactions
            .GroupBy(log => log.ResidentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var monthStarts = GetMonthStarts(months);
        var points = new List<MonthlyAveragePoint>(monthStarts.Count);

        foreach (var monthStart in monthStarts)
        {
            var monthEnd = GetMonthEnd(monthStart);
            var totalEvents = events.Count(eventItem =>
            {
                var eventDate = ParseDate(eventItem.EventDate);
                return eventDate.HasValue && eventDate.Value.Date <= monthEnd;
            });

            var scores = new List<int>(activeResidents.Count);

            foreach (var resident in activeResidents)
            {
                attendanceByResident.TryGetValue(resident.ResidentId, out var residentAttendances);
                interactionsByResident.TryGetValue(resident.ResidentId, out var residentInteractions);

                var eventsAttended = residentAttendances?.Count(attendance =>
                {
                    var attendanceDate = ParseDate(attendance.Event?.EventDate ?? attendance.RecordedAt);
                    return IsAttendedStatus(attendance.Status) &&
                           attendanceDate.HasValue &&
                           attendanceDate.Value.Date <= monthEnd;
                }) ?? 0;

                var interactionWindowStart = monthEnd.AddMonths(-6);
                var filteredInteractionDates = (residentInteractions ?? [])
                    .Select(log => ParseDate(log.InteractionDate))
                    .Where(date => date.HasValue && date.Value.Date <= monthEnd)
                    .Select(date => date!.Value)
                    .ToList();

                var interactionsLastSixMonths = filteredInteractionDates
                    .Count(date => date.Date >= interactionWindowStart);
                var lastInteractionDate = filteredInteractionDates.Count == 0
                    ? (DateTime?)null
                    : filteredInteractionDates.Max();

                scores.Add(EngagementCalculator.CalculateScore(
                    totalEvents,
                    eventsAttended,
                    interactionsLastSixMonths,
                    lastInteractionDate));
            }

            points.Add(new MonthlyAveragePoint
            {
                Month = monthStart.ToString("MMM"),
                AvgScore = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 2)
            });
        }

        return points;
    }

    public async Task<List<MonthlyCountPoint>> GetAttendanceTrendAsync(int months = 6)
    {
        months = NormalizeMonths(months);

        var attendances = await _dbContext.Attendances
            .AsNoTracking()
            .Include(attendance => attendance.Event)
            .ToListAsync();

        var monthStarts = GetMonthStarts(months);
        var points = new List<MonthlyCountPoint>(monthStarts.Count);

        foreach (var monthStart in monthStarts)
        {
            var nextMonthStart = monthStart.AddMonths(1);
            var count = attendances.Count(attendance =>
            {
                if (!IsAttendedStatus(attendance.Status))
                {
                    return false;
                }

                var eventDate = ParseDate(attendance.Event?.EventDate);
                return eventDate.HasValue &&
                       eventDate.Value.Date >= monthStart &&
                       eventDate.Value.Date < nextMonthStart;
            });

            points.Add(new MonthlyCountPoint
            {
                Month = monthStart.ToString("MMM"),
                Count = count
            });
        }

        return points;
    }

    public async Task<ResidentStatusDistribution> GetResidentStatusDistributionAsync()
    {
        var residents = await _dbContext.Residents
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted)
            .ToListAsync();

        return new ResidentStatusDistribution
        {
            Active = residents.Count(resident => HasStatus(resident.Status, "Active")),
            Inactive = residents.Count(resident => HasStatus(resident.Status, "Inactive")),
            Transferred = residents.Count(resident => HasStatus(resident.Status, "Transferred")),
            Deceased = residents.Count(resident => HasStatus(resident.Status, "Deceased"))
        };
    }

    public async Task<byte[]> ExportResidentsToExcelAsync()
    {
        var residents = await _dbContext.Residents
            .Include(resident => resident.Purok)
            .Include(resident => resident.Household)
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted)
            .OrderBy(resident => resident.LastName)
            .ThenBy(resident => resident.FirstName)
            .ToListAsync();

        return await ExcelExportHelper.ExportResidentsToExcelAsync(residents);
    }

    public async Task<byte[]> ExportBlotterToExcelAsync()
    {
        var blotterEntries = await _dbContext.BlotterEntries
            .Include(entry => entry.Complainant)
            .Include(entry => entry.FiledByUser)
            .Include(entry => entry.UpdatedByUser)
            .AsNoTracking()
            .OrderByDescending(entry => entry.FiledAt)
            .ToListAsync();

        return await ExcelExportHelper.ExportBlotterToExcelAsync(blotterEntries);
    }

    public async Task<byte[]> ExportClearanceToExcelAsync()
    {
        var clearanceRequests = await _dbContext.ClearanceRequests
            .Include(request => request.Resident)
            .Include(request => request.ProcessedByUser)
            .AsNoTracking()
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync();

        return await ExcelExportHelper.ExportClearanceToExcelAsync(clearanceRequests);
    }

    public async Task<byte[]> ExportResidentsToPdfAsync()
    {
        var residents = await _dbContext.Residents
            .Include(resident => resident.Purok)
            .Include(resident => resident.Household)
            .AsNoTracking()
            .Where(resident => !resident.IsDeleted)
            .OrderBy(resident => resident.LastName)
            .ThenBy(resident => resident.FirstName)
            .ToListAsync();

        var settings = await _dbContext.BarangaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.SettingId == 1);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(text => text.FontSize(9));

                page.Header().Column(column =>
                {
                    if (settings is not null)
                    {
                        column.Item().AlignCenter().Text(settings.BarangayName).Bold().FontSize(15);
                        column.Item().AlignCenter().Text($"{settings.Municipality}, {settings.Province}").FontSize(10);
                    }

                    column.Item().PaddingTop(8).AlignCenter().Text("RESIDENT LIST REPORT").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Generated on {DateTime.Today:MMMM dd, yyyy}");
                    column.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingTop(14).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.2f);
                        columns.RelativeColumn(1f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.3f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.5f);
                    });

                    table.Header(header =>
                    {
                        AddHeaderCell(header, "Full Name");
                        AddHeaderCell(header, "Age");
                        AddHeaderCell(header, "Gender");
                        AddHeaderCell(header, "Status");
                        AddHeaderCell(header, "Purok");
                        AddHeaderCell(header, "Household");
                        AddHeaderCell(header, "Category");
                        AddHeaderCell(header, "Residency Since");
                    });

                    foreach (var resident in residents)
                    {
                        AddBodyCell(table, GetResidentFullName(resident));
                        AddBodyCell(table, CalculateAge(resident.BirthDate).ToString());
                        AddBodyCell(table, resident.Gender);
                        AddBodyCell(table, resident.Status);
                        AddBodyCell(table, resident.Purok?.Name ?? "-");
                        AddBodyCell(table, resident.Household?.HouseholdNumber ?? "-");
                        AddBodyCell(table, string.IsNullOrWhiteSpace(resident.Categories) ? "-" : resident.Categories!);
                        AddBodyCell(table, FormatDate(resident.ResidencySince));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private async Task<List<int>> CalculateCurrentEngagementScoresAsync(List<Resident> activeResidents)
    {
        if (activeResidents.Count == 0)
        {
            return [];
        }

        var residentIds = activeResidents.Select(resident => resident.ResidentId).ToList();
        var totalEvents = await _dbContext.Events.AsNoTracking().CountAsync();
        var attendances = await _dbContext.Attendances
            .AsNoTracking()
            .Where(attendance => residentIds.Contains(attendance.ResidentId))
            .ToListAsync();
        var interactions = await _dbContext.InteractionLogs
            .AsNoTracking()
            .Where(log => residentIds.Contains(log.ResidentId))
            .ToListAsync();

        var cutoffDate = DateTime.Today.AddMonths(-6);
        var scores = new List<int>(activeResidents.Count);

        foreach (var resident in activeResidents)
        {
            var eventsAttended = attendances.Count(attendance =>
                attendance.ResidentId == resident.ResidentId &&
                IsAttendedStatus(attendance.Status));

            var interactionDates = interactions
                .Where(log => log.ResidentId == resident.ResidentId)
                .Select(log => ParseDate(log.InteractionDate))
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .ToList();

            var interactionsLastSixMonths = interactionDates.Count(date => date.Date >= cutoffDate);
            var lastInteractionDate = interactionDates.Count == 0 ? (DateTime?)null : interactionDates.Max();

            scores.Add(EngagementCalculator.CalculateScore(
                totalEvents,
                eventsAttended,
                interactionsLastSixMonths,
                lastInteractionDate));
        }

        return scores;
    }

    private static List<DateTime> GetMonthStarts(int months)
    {
        var currentMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthStarts = new List<DateTime>(months);

        for (var offset = months - 1; offset >= 0; offset--)
        {
            monthStarts.Add(currentMonthStart.AddMonths(-offset));
        }

        return monthStarts;
    }

    private static DateTime GetMonthEnd(DateTime monthStart)
    {
        var endOfMonth = monthStart.AddMonths(1).AddDays(-1);
        return endOfMonth.Date > DateTime.Today ? DateTime.Today : endOfMonth.Date;
    }

    private static int NormalizeMonths(int months)
    {
        return months <= 0 ? 6 : months;
    }

    private static bool HasStatus(string? value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
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

    private static string FormatDate(string? value)
    {
        return ParseDate(value)?.ToString("MMMM dd, yyyy") ?? "-";
    }

    private static int CalculateAge(string? birthDate)
    {
        var parsedDate = ParseDate(birthDate);
        if (!parsedDate.HasValue)
        {
            return 0;
        }

        var age = DateTime.Today.Year - parsedDate.Value.Year;
        if (parsedDate.Value.Date > DateTime.Today.AddYears(-age))
        {
            age--;
        }

        return Math.Max(age, 0);
    }

    private static string GetResidentFullName(Resident resident)
    {
        return string.Join(" ", new[] { resident.FirstName, resident.MiddleName, resident.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void AddHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell()
            .Background("#3B5BDB")
            .Border(1)
            .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
            .Padding(6)
            .Text(text)
            .FontColor(QuestPDF.Helpers.Colors.White)
            .Bold();
    }

    private static void AddBodyCell(TableDescriptor table, string text)
    {
        table.Cell()
            .Border(1)
            .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3)
            .Padding(6)
            .Text(string.IsNullOrWhiteSpace(text) ? "-" : text);
    }
}

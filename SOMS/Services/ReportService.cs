using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SOMS.Data;
using SOMS.Models;
using PdfColors = QuestPDF.Helpers.Colors;
using QuestContainer = QuestPDF.Infrastructure.IContainer;

namespace SOMS.Services;

public class ReportService
{
    private readonly AppDbContext _db;
    private readonly EngagementService _engagementService;

    public ReportService(AppDbContext db, EngagementService engagementService)
    {
        _db = db;
        _engagementService = engagementService;
    }

    public async Task<DashboardKpis> GetDashboardKpisAsync()
    {
        var totalMembersTask = _db.Members
            .AsNoTracking()
            .CountAsync(member => !member.IsDeleted);

        var activeMembersTask = _db.Members
            .AsNoTracking()
            .CountAsync(member => !member.IsDeleted && member.Status == "Active");

        var upcomingEventsTask = _db.Events
            .AsNoTracking()
            .CountAsync(eventItem => eventItem.EventDate >= DateTime.Today);

        var distributionTask = GetEngagementDistributionAsync();

        await Task.WhenAll(totalMembersTask, activeMembersTask, upcomingEventsTask, distributionTask);

        var totalMembers = await totalMembersTask;
        var activeMembers = await activeMembersTask;
        var upcomingEventsCount = await upcomingEventsTask;
        var (high, medium, low) = await distributionTask;

        var activePercent = totalMembers == 0
            ? 0
            : Math.Round(activeMembers / (double)totalMembers * 100, 1);

        return new DashboardKpis(
            totalMembers,
            activePercent,
            upcomingEventsCount,
            low,
            high,
            medium);
    }

    public async Task<List<AttendanceTrendPoint>> GetAttendanceTrendAsync(int lastNEvents = 5)
    {
        if (lastNEvents <= 0)
        {
            return [];
        }

        var activeMembersCount = await GetActiveMemberCountAsync();

        var events = await _db.Events
            .AsNoTracking()
            .Include(eventItem => eventItem.Attendances)
            .Where(eventItem => eventItem.EventDate < DateTime.Today)
            .OrderByDescending(eventItem => eventItem.EventDate)
            .Take(lastNEvents)
            .ToListAsync();

        return events
            .OrderBy(eventItem => eventItem.EventDate)
            .Select(eventItem =>
            {
                var totalMembers = GetAttendanceBaseMemberCount(eventItem, activeMembersCount);
                var presentCount = eventItem.Attendances.Count(attendance => attendance.Status == "Present");
                var rate = totalMembers == 0 ? 0 : presentCount / (double)totalMembers;

                return new AttendanceTrendPoint(eventItem.Title, eventItem.EventDate, rate);
            })
            .ToList();
    }

    public async Task<(int High, int Medium, int Low)> GetEngagementDistributionAsync()
    {
        var summaryRows = await _engagementService.GetEngagementSummaryAsync();

        var high = summaryRows.Count(row => row.Label == "High");
        var medium = summaryRows.Count(row => row.Label == "Medium");
        var low = summaryRows.Count(row => row.Label == "Low");

        return (high, medium, low);
    }

    public Task<List<InteractionLog>> GetRecentInteractionsAsync(int count = 5)
    {
        return _db.InteractionLogs
            .AsNoTracking()
            .Include(log => log.Member)
            .Include(log => log.CreatedByUser)
            .OrderByDescending(log => log.CreatedAt)
            .Take(Math.Max(count, 0))
            .ToListAsync();
    }

    public async Task<byte[]> ExportMemberListToPdfAsync()
    {
        var rows = await GetActiveMemberReportRowsAsync();

        var pdfRows = rows
            .Select(row => new[]
            {
                row.Number.ToString(),
                row.FullName,
                row.StudentId,
                row.Status,
                row.Committees,
                $"{row.EngagementScore} ({row.EngagementLabel})"
            })
            .ToList();

        return GenerateTablePdf(
            title: "Member List Report",
            subtitle: $"Active members: {rows.Count}",
            headers: ["No.", "Full Name", "Student ID", "Status", "Committee(s)", "Engagement"],
            columnWidths: [0.8f, 2.2f, 1.5f, 1.2f, 2.4f, 1.5f],
            rows: pdfRows,
            emptyStateMessage: "No active members found.");
    }

    public async Task<byte[]> ExportMemberListToExcelAsync()
    {
        var rows = await GetActiveMemberReportRowsAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Members");

        var headers = new[]
        {
            "No.",
            "Full Name",
            "Student ID",
            "Email",
            "Phone",
            "Status",
            "Committee(s)",
            "Recruitment Stage",
            "Join Date",
            "Engagement Score"
        };

        WriteHeaderRow(worksheet, headers);

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 2;
            var row = rows[index];

            worksheet.Cell(rowNumber, 1).Value = row.Number;
            worksheet.Cell(rowNumber, 2).Value = row.FullName;
            worksheet.Cell(rowNumber, 3).Value = row.StudentId;
            worksheet.Cell(rowNumber, 4).Value = row.Email;
            worksheet.Cell(rowNumber, 5).Value = row.Phone;
            worksheet.Cell(rowNumber, 6).Value = row.Status;
            worksheet.Cell(rowNumber, 7).Value = row.Committees;
            worksheet.Cell(rowNumber, 8).Value = row.RecruitmentStage;
            worksheet.Cell(rowNumber, 9).Value = row.JoinDate;
            worksheet.Cell(rowNumber, 10).Value = row.EngagementScore;
        }

        worksheet.Column(9).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportAttendanceToPdfAsync(int? eventId = null)
    {
        if (eventId.HasValue)
        {
            var detail = await GetAttendanceDetailReportAsync(eventId.Value);

            var detailRows = detail.Rows
                .Select(row => new[]
                {
                    row.MemberName,
                    row.StudentId,
                    row.Status
                })
                .ToList();

            return GenerateTablePdf(
                title: "Attendance Detail Report",
                subtitle: $"{detail.EventTitle} | {detail.EventDate:MMMM dd, yyyy}",
                headers: ["Member Name", "Student ID", "Status"],
                columnWidths: [2.4f, 1.6f, 1.2f],
                rows: detailRows,
                emptyStateMessage: "No attendance data found for this event.");
        }

        var rows = await GetAttendanceSummaryReportRowsAsync();

        var summaryRows = rows
            .Select(row => new[]
            {
                row.EventTitle,
                row.EventDate.ToString("MMM dd, yyyy"),
                row.TotalMembers.ToString(),
                row.Present.ToString(),
                row.Absent.ToString(),
                row.Excused.ToString(),
                $"{row.AttendanceRate * 100:0.#}%"
            })
            .ToList();

        return GenerateTablePdf(
            title: "Attendance Summary Report",
            subtitle: $"Events included: {rows.Count}",
            headers: ["Event Title", "Date", "Total Members", "Present", "Absent", "Excused", "Rate%"],
            columnWidths: [2.6f, 1.4f, 1.2f, 1f, 1f, 1f, 1f],
            rows: summaryRows,
            emptyStateMessage: "No events found.");
    }

    public async Task<byte[]> ExportAttendanceToExcelAsync(int? eventId = null)
    {
        using var workbook = new XLWorkbook();

        if (eventId.HasValue)
        {
            var detail = await GetAttendanceDetailReportAsync(eventId.Value);
            var worksheet = workbook.Worksheets.Add("Attendance");
            var headers = new[] { "Member Name", "Student ID", "Status" };

            WriteHeaderRow(worksheet, headers);

            for (var index = 0; index < detail.Rows.Count; index++)
            {
                var rowNumber = index + 2;
                var row = detail.Rows[index];

                worksheet.Cell(rowNumber, 1).Value = row.MemberName;
                worksheet.Cell(rowNumber, 2).Value = row.StudentId;
                worksheet.Cell(rowNumber, 3).Value = row.Status;
            }

            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(1);
        }
        else
        {
            var rows = await GetAttendanceSummaryReportRowsAsync();
            var worksheet = workbook.Worksheets.Add("Attendance");
            var headers = new[] { "Event Title", "Date", "Total Members", "Present", "Absent", "Excused", "Rate%" };

            WriteHeaderRow(worksheet, headers);

            for (var index = 0; index < rows.Count; index++)
            {
                var rowNumber = index + 2;
                var row = rows[index];

                worksheet.Cell(rowNumber, 1).Value = row.EventTitle;
                worksheet.Cell(rowNumber, 2).Value = row.EventDate;
                worksheet.Cell(rowNumber, 3).Value = row.TotalMembers;
                worksheet.Cell(rowNumber, 4).Value = row.Present;
                worksheet.Cell(rowNumber, 5).Value = row.Absent;
                worksheet.Cell(rowNumber, 6).Value = row.Excused;
                worksheet.Cell(rowNumber, 7).Value = row.AttendanceRate;
            }

            worksheet.Column(2).Style.DateFormat.Format = "yyyy-mm-dd";
            worksheet.Column(7).Style.NumberFormat.Format = "0.0%";
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(1);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public Task<string> SavePdfToFileAsync(byte[] pdf, string fileName)
        => SaveReportFileAsync(pdf, fileName);

    public Task<string> SaveExcelToFileAsync(byte[] workbookBytes, string fileName)
        => SaveReportFileAsync(workbookBytes, fileName);

    private async Task<List<MemberReportRow>> GetActiveMemberReportRowsAsync()
    {
        var membersTask = _db.Members
            .AsNoTracking()
            .Include(member => member.MemberCommittees)
            .ThenInclude(memberCommittee => memberCommittee.Committee)
            .Where(member => !member.IsDeleted && member.Status == "Active")
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .ToListAsync();

        var engagementTask = _engagementService.GetEngagementSummaryAsync();

        await Task.WhenAll(membersTask, engagementTask);

        var members = await membersTask;
        var engagementLookup = (await engagementTask)
            .ToDictionary(row => row.MemberId);

        return members
            .Select((member, index) =>
            {
                var engagement = engagementLookup.GetValueOrDefault(member.MemberId);

                return new MemberReportRow
                {
                    Number = index + 1,
                    FullName = member.FullName,
                    StudentId = member.StudentId,
                    Email = member.Email ?? "-",
                    Phone = member.PhoneNumber ?? "-",
                    Status = member.Status,
                    Committees = GetCommitteeNames(member),
                    RecruitmentStage = member.RecruitmentStage,
                    JoinDate = member.JoinDate,
                    EngagementScore = engagement?.Score ?? 0,
                    EngagementLabel = engagement?.Label ?? "Low"
                };
            })
            .ToList();
    }

    private async Task<List<AttendanceSummaryReportRow>> GetAttendanceSummaryReportRowsAsync()
    {
        var activeMembersCount = await GetActiveMemberCountAsync();

        var events = await _db.Events
            .AsNoTracking()
            .Include(eventItem => eventItem.Attendances)
            .OrderByDescending(eventItem => eventItem.EventDate)
            .ThenByDescending(eventItem => eventItem.EventId)
            .ToListAsync();

        return events
            .Select(eventItem => BuildAttendanceSummaryRow(eventItem, activeMembersCount))
            .ToList();
    }

    private async Task<AttendanceDetailReport> GetAttendanceDetailReportAsync(int eventId)
    {
        var eventTask = _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(eventItem => eventItem.EventId == eventId);

        var activeMembersTask = _db.Members
            .AsNoTracking()
            .Where(member => !member.IsDeleted && member.Status == "Active")
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .ToListAsync();

        var attendanceTask = _db.Attendances
            .AsNoTracking()
            .Include(attendance => attendance.Member)
            .Where(attendance => attendance.EventId == eventId)
            .ToListAsync();

        await Task.WhenAll(eventTask, activeMembersTask, attendanceTask);

        var eventItem = await eventTask;
        if (eventItem is null)
        {
            throw new InvalidOperationException("Event not found.");
        }

        var members = new Dictionary<int, Member>();
        foreach (var member in await activeMembersTask)
        {
            members[member.MemberId] = member;
        }

        var attendances = await attendanceTask;
        foreach (var attendance in attendances.Where(attendance => attendance.Member is not null))
        {
            members[attendance.MemberId] = attendance.Member;
        }

        var attendanceLookup = attendances.ToDictionary(attendance => attendance.MemberId, attendance => attendance.Status);

        var rows = members.Values
            .OrderBy(member => member.LastName)
            .ThenBy(member => member.FirstName)
            .Select(member => new AttendanceDetailRow
            {
                MemberName = member.FullName,
                StudentId = member.StudentId,
                Status = attendanceLookup.TryGetValue(member.MemberId, out var status) ? status : "Absent"
            })
            .ToList();

        return new AttendanceDetailReport
        {
            EventTitle = eventItem.Title,
            EventDate = eventItem.EventDate,
            Rows = rows
        };
    }

    private static AttendanceSummaryReportRow BuildAttendanceSummaryRow(Event eventItem, int activeMembersCount)
    {
        var totalMembers = GetAttendanceBaseMemberCount(eventItem, activeMembersCount);
        var present = eventItem.Attendances.Count(attendance => attendance.Status == "Present");
        var excused = eventItem.Attendances.Count(attendance => attendance.Status == "Excused");
        var recordedAbsent = eventItem.Attendances.Count(attendance => attendance.Status == "Absent");
        var absent = Math.Max(totalMembers - present - excused, recordedAbsent);
        var rate = totalMembers == 0 ? 0 : present / (double)totalMembers;

        return new AttendanceSummaryReportRow
        {
            EventTitle = eventItem.Title,
            EventDate = eventItem.EventDate,
            TotalMembers = totalMembers,
            Present = present,
            Absent = absent,
            Excused = excused,
            AttendanceRate = rate
        };
    }

    private static int GetAttendanceBaseMemberCount(Event eventItem, int activeMembersCount)
    {
        var recordedMembersCount = eventItem.Attendances
            .Select(attendance => attendance.MemberId)
            .Distinct()
            .Count();

        return Math.Max(activeMembersCount, recordedMembersCount);
    }

    private Task<int> GetActiveMemberCountAsync()
    {
        return _db.Members
            .AsNoTracking()
            .CountAsync(member => !member.IsDeleted && member.Status == "Active");
    }

    private static byte[] GenerateTablePdf(
        string title,
        string subtitle,
        IReadOnlyList<string> headers,
        IReadOnlyList<float> columnWidths,
        IReadOnlyList<string[]> rows,
        string emptyStateMessage)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(PdfColors.White);
                page.DefaultTextStyle(text => text.FontFamily(Fonts.Calibri).FontSize(10).FontColor(PdfColors.Grey.Darken4));

                page.Header().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Text(title).FontSize(18).Bold().FontColor("#0F172A");
                    column.Item().Text(subtitle).FontSize(10).FontColor(PdfColors.Grey.Medium);
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var width in columnWidths)
                        {
                            columns.RelativeColumn(width);
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var headerText in headers)
                        {
                            header.Cell()
                                .Element(TableHeaderCellStyle)
                                .Text(headerText)
                                .SemiBold()
                                .FontColor(PdfColors.White);
                        }
                    });

                    if (rows.Count == 0)
                    {
                        table.Cell()
                            .ColumnSpan((uint)headers.Count)
                            .Element(TableBodyCellStyle)
                            .Text(emptyStateMessage)
                            .Italic();
                    }
                    else
                    {
                        foreach (var row in rows)
                        {
                            foreach (var value in row)
                            {
                                table.Cell()
                                    .Element(TableBodyCellStyle)
                                    .Text(value);
                            }
                        }
                    }
                });

                page.Footer()
                    .AlignRight()
                    .Text($"Generated {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                    .FontSize(9)
                    .FontColor(PdfColors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    private static void WriteHeaderRow(IXLWorksheet worksheet, IReadOnlyList<string> headers)
    {
        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = headers[columnIndex];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static string GetCommitteeNames(Member member)
    {
        var committeeNames = member.MemberCommittees
            .Select(memberCommittee => memberCommittee.Committee?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name)
            .ToList();

        return committeeNames.Count == 0 ? "-" : string.Join(", ", committeeNames!);
    }

    private static QuestContainer TableHeaderCellStyle(QuestContainer container)
        => container
            .Background("#1E293B")
            .Border(1)
            .BorderColor("#E2E8F0")
            .PaddingVertical(8)
            .PaddingHorizontal(6);

    private static QuestContainer TableBodyCellStyle(QuestContainer container)
        => container
            .Border(1)
            .BorderColor("#E2E8F0")
            .PaddingVertical(6)
            .PaddingHorizontal(6);

    private static async Task<string> SaveReportFileAsync(byte[] content, string fileName)
    {
        var reportsPath = Path.Combine(FileSystem.AppDataDirectory, "Reports");
        Directory.CreateDirectory(reportsPath);

        var fullPath = Path.Combine(reportsPath, fileName);
        await File.WriteAllBytesAsync(fullPath, content);
        return fullPath;
    }

    private sealed class MemberReportRow
    {
        public int Number { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string Email { get; set; } = "-";
        public string Phone { get; set; } = "-";
        public string Status { get; set; } = string.Empty;
        public string Committees { get; set; } = "-";
        public string RecruitmentStage { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
        public int EngagementScore { get; set; }
        public string EngagementLabel { get; set; } = "Low";
    }

    private sealed class AttendanceSummaryReportRow
    {
        public string EventTitle { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public int TotalMembers { get; set; }
        public int Present { get; set; }
        public int Absent { get; set; }
        public int Excused { get; set; }
        public double AttendanceRate { get; set; }
    }

    private sealed class AttendanceDetailRow
    {
        public string MemberName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string Status { get; set; } = "Absent";
    }

    private sealed class AttendanceDetailReport
    {
        public string EventTitle { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public List<AttendanceDetailRow> Rows { get; set; } = [];
    }
}

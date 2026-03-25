using BRMS.Models;
using ClosedXML.Excel;

namespace BRMS.Helpers;

public static class ExcelExportHelper
{
    public static Task<byte[]> ExportResidentsToExcelAsync(IEnumerable<Resident> residents)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Residents");

        var headers = new[]
        {
            "Resident ID",
            "Full Name",
            "Birth Date",
            "Gender",
            "Civil Status",
            "Contact Number",
            "Email",
            "Address",
            "Purok",
            "Household",
            "Status",
            "Categories",
            "Residency Since",
            "Created At"
        };

        WriteHeaderRow(worksheet, headers);

        var rowIndex = 2;
        foreach (var resident in residents)
        {
            worksheet.Cell(rowIndex, 1).Value = resident.ResidentId;
            worksheet.Cell(rowIndex, 2).Value = GetResidentFullName(resident);
            worksheet.Cell(rowIndex, 3).Value = FormatDate(resident.BirthDate);
            worksheet.Cell(rowIndex, 4).Value = resident.Gender;
            worksheet.Cell(rowIndex, 5).Value = resident.CivilStatus ?? "-";
            worksheet.Cell(rowIndex, 6).Value = resident.ContactNumber ?? "-";
            worksheet.Cell(rowIndex, 7).Value = resident.Email ?? "-";
            worksheet.Cell(rowIndex, 8).Value = resident.Address ?? "-";
            worksheet.Cell(rowIndex, 9).Value = resident.Purok?.Name ?? "-";
            worksheet.Cell(rowIndex, 10).Value = resident.Household?.HouseholdNumber ?? "-";
            worksheet.Cell(rowIndex, 11).Value = resident.Status;
            worksheet.Cell(rowIndex, 12).Value = resident.Categories ?? "-";
            worksheet.Cell(rowIndex, 13).Value = FormatDate(resident.ResidencySince);
            worksheet.Cell(rowIndex, 14).Value = FormatDateTime(resident.CreatedAt);
            rowIndex++;
        }

        FinalizeWorksheet(worksheet, headers.Length, Math.Max(2, rowIndex - 1));
        return Task.FromResult(SaveWorkbook(workbook));
    }

    public static Task<byte[]> ExportBlotterToExcelAsync(IEnumerable<BlotterEntry> blotterEntries)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Blotter");

        var headers = new[]
        {
            "Blotter Number",
            "Complainant",
            "Respondent",
            "Incident Type",
            "Incident Date",
            "Status",
            "Resolution",
            "Filed At",
            "Filed By",
            "Updated At",
            "Updated By",
            "Incident Details"
        };

        WriteHeaderRow(worksheet, headers);

        var rowIndex = 2;
        foreach (var entry in blotterEntries)
        {
            worksheet.Cell(rowIndex, 1).Value = entry.BlotterNumber;
            worksheet.Cell(rowIndex, 2).Value = entry.ComplainantName;
            worksheet.Cell(rowIndex, 3).Value = entry.RespondentName;
            worksheet.Cell(rowIndex, 4).Value = entry.IncidentType;
            worksheet.Cell(rowIndex, 5).Value = FormatDate(entry.IncidentDate);
            worksheet.Cell(rowIndex, 6).Value = entry.Status;
            worksheet.Cell(rowIndex, 7).Value = entry.Resolution ?? "-";
            worksheet.Cell(rowIndex, 8).Value = FormatDateTime(entry.FiledAt);
            worksheet.Cell(rowIndex, 9).Value = entry.FiledByUser?.Username ?? "-";
            worksheet.Cell(rowIndex, 10).Value = FormatDateTime(entry.UpdatedAt);
            worksheet.Cell(rowIndex, 11).Value = entry.UpdatedByUser?.Username ?? "-";
            worksheet.Cell(rowIndex, 12).Value = entry.IncidentDetails;
            rowIndex++;
        }

        FinalizeWorksheet(worksheet, headers.Length, Math.Max(2, rowIndex - 1));
        return Task.FromResult(SaveWorkbook(workbook));
    }

    public static Task<byte[]> ExportClearanceToExcelAsync(IEnumerable<ClearanceRequest> clearanceRequests)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Clearance");

        var headers = new[]
        {
            "Resident Name",
            "Purpose",
            "Requested At",
            "Status",
            "Valid Until",
            "Processed At",
            "Processed By",
            "Remarks"
        };

        WriteHeaderRow(worksheet, headers);

        var rowIndex = 2;
        foreach (var request in clearanceRequests)
        {
            worksheet.Cell(rowIndex, 1).Value = GetResidentFullName(request.Resident);
            worksheet.Cell(rowIndex, 2).Value = request.Purpose;
            worksheet.Cell(rowIndex, 3).Value = FormatDateTime(request.RequestedAt);
            worksheet.Cell(rowIndex, 4).Value = request.Status;
            worksheet.Cell(rowIndex, 5).Value = FormatDate(request.ValidUntil);
            worksheet.Cell(rowIndex, 6).Value = FormatDateTime(request.ProcessedAt);
            worksheet.Cell(rowIndex, 7).Value = request.ProcessedByUser?.Username ?? "-";
            worksheet.Cell(rowIndex, 8).Value = request.Remarks ?? "-";
            rowIndex++;
        }

        FinalizeWorksheet(worksheet, headers.Length, Math.Max(2, rowIndex - 1));
        return Task.FromResult(SaveWorkbook(workbook));
    }

    private static void WriteHeaderRow(IXLWorksheet worksheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#3B5BDB");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void FinalizeWorksheet(IXLWorksheet worksheet, int headerCount, int rowCount)
    {
        var usedRange = worksheet.Range(1, 1, rowCount, headerCount);
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        usedRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string GetResidentFullName(Resident? resident)
    {
        if (resident is null)
        {
            return "-";
        }

        return string.Join(" ", new[] { resident.FirstName, resident.MiddleName, resident.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FormatDate(string? value)
    {
        return DateTime.TryParse(value, out var parsedDate)
            ? parsedDate.ToString("yyyy-MM-dd")
            : "-";
    }

    private static string FormatDateTime(string? value)
    {
        return DateTime.TryParse(value, out var parsedDate)
            ? parsedDate.ToString("yyyy-MM-dd HH:mm")
            : "-";
    }
}

using BRMS.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BRMS.Helpers;

public static class PdfExportHelper
{
    public static Task<byte[]> GenerateBlotterPdfAsync(BlotterEntry entry, BarangaySettings settings)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().AlignCenter().Text(settings.BarangayName).Bold().FontSize(16);
                    column.Item().AlignCenter().Text($"{settings.Municipality}, {settings.Province}").FontSize(11);
                    column.Item().PaddingTop(10).AlignCenter().Text("BARANGAY BLOTTER REPORT").Bold().FontSize(15);
                    column.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Blotter Number: {entry.BlotterNumber}").SemiBold();
                        row.RelativeItem().AlignRight().Text($"Filed At: {FormatDate(entry.FiledAt)}");
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Incident Date: {FormatDate(entry.IncidentDate)}");
                        row.RelativeItem().AlignRight().Text($"Incident Type: {entry.IncidentType}");
                    });

                    column.Item().Element(container => CreateLabeledField(container, "Complainant Name", entry.ComplainantName));
                    column.Item().Element(container => CreateLabeledField(container, "Respondent Name", entry.RespondentName));
                    column.Item().Element(container => CreateLabeledBlock(container, "Incident Details", entry.IncidentDetails));
                    column.Item().Element(container => CreateLabeledField(container, "Status", entry.Status));

                    if (!string.IsNullOrWhiteSpace(entry.Resolution))
                    {
                        column.Item().Element(container => CreateLabeledBlock(container, "Resolution", entry.Resolution!));
                    }
                });

                page.Footer().Column(column =>
                {
                    column.Item().LineHorizontal(1);
                    column.Item().PaddingTop(8).AlignCenter().Text($"Issued by {settings.CaptainName ?? "Barangay Captain"}, Barangay Captain");
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    public static Task<byte[]> GenerateClearancePdfAsync(ClearanceRequest request, Resident resident, BarangaySettings settings)
    {
        var fullName = string.Join(" ", new[] { resident.FirstName, resident.MiddleName, resident.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontSize(11));

                page.Header().Column(column =>
                {
                    if (!string.IsNullOrWhiteSpace(settings.LogoPath) && File.Exists(settings.LogoPath))
                    {
                        column.Item().AlignCenter().Height(60).Image(File.ReadAllBytes(settings.LogoPath)).FitHeight();
                    }

                    column.Item().AlignCenter().Text(settings.BarangayName).Bold().FontSize(16);
                    column.Item().AlignCenter().Text($"{settings.Municipality}, {settings.Province}").FontSize(11);
                    column.Item().PaddingTop(10).AlignCenter().Text("BARANGAY CLEARANCE").Bold().FontSize(16);
                    column.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(24).Column(column =>
                {
                    column.Spacing(18);
                    column.Item().AlignCenter().Text("TO WHOM IT MAY CONCERN:").Bold();

                    column.Item().Text(text =>
                    {
                        text.Span("This is to certify that ");
                        text.Span(fullName).SemiBold();
                        text.Span($", {CalculateAge(resident.BirthDate)} years old, ");
                        text.Span(string.IsNullOrWhiteSpace(resident.CivilStatus) ? "of legal age" : resident.CivilStatus!);
                        text.Span($", is a bonafide resident of {settings.BarangayName}, {settings.Municipality}, {settings.Province}, and has been a resident since {FormatDate(resident.ResidencySince)}.");
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("This clearance is issued for the purpose of ");
                        text.Span(request.Purpose).SemiBold();
                        text.Span(".");
                    });

                    column.Item().Text($"Valid Until: {FormatDate(request.ValidUntil)}").SemiBold();

                    column.Item().PaddingTop(36).AlignRight().Column(signature =>
                    {
                        signature.Item().Text(settings.CaptainName ?? "Barangay Captain").Bold();
                        signature.Item().Text("Barangay Captain");
                    });
                });

                page.Footer().AlignCenter().Text($"Issued on {DateTime.Today:MMMM dd, yyyy}");
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    public static Task<byte[]> GenerateCertificateOfResidencyPdfAsync(Resident resident, BarangaySettings settings)
    {
        var fullName = GetResidentFullName(resident);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontSize(11));

                page.Header().Column(column =>
                {
                    BuildLetterhead(column, settings);
                    column.Item().PaddingTop(10).AlignCenter().Text("CERTIFICATE OF RESIDENCY").Bold().FontSize(16);
                    column.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(24).Column(column =>
                {
                    column.Spacing(18);
                    column.Item().AlignCenter().Text("TO WHOM IT MAY CONCERN:").Bold();

                    column.Item().Text(text =>
                    {
                        text.Span("This is to certify that ");
                        text.Span(fullName).SemiBold();
                        text.Span($", {CalculateAge(resident.BirthDate)} years old, {resident.Gender}, ");
                        text.Span(string.IsNullOrWhiteSpace(resident.CivilStatus) ? "single" : resident.CivilStatus!);
                        text.Span($", is a bonafide resident of {settings.BarangayName}, {settings.Municipality}, {settings.Province}.");
                    });

                    column.Item().Text(text =>
                    {
                        text.Span(GetPronoun(resident.Gender, true));
                        text.Span($" has been a resident of this barangay since {FormatDate(resident.ResidencySince)} and is known to be of good moral character and standing in the community.");
                    });

                    column.Item().Text("This certificate is issued upon the request of the above-named person for whatever legal purpose it may serve.");

                    BuildIssuedAndSignature(column, settings);
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    public static Task<byte[]> GenerateCertificateOfIndigencyPdfAsync(Resident resident, BarangaySettings settings)
    {
        var fullName = GetResidentFullName(resident);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontSize(11));

                page.Header().Column(column =>
                {
                    BuildLetterhead(column, settings);
                    column.Item().PaddingTop(10).AlignCenter().Text("CERTIFICATE OF INDIGENCY").Bold().FontSize(16);
                    column.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(24).Column(column =>
                {
                    column.Spacing(18);
                    column.Item().AlignCenter().Text("TO WHOM IT MAY CONCERN:").Bold();

                    column.Item().Text(text =>
                    {
                        text.Span("This is to certify that ");
                        text.Span(fullName).SemiBold();
                        text.Span($", {CalculateAge(resident.BirthDate)} years old, {resident.Gender}, ");
                        text.Span(string.IsNullOrWhiteSpace(resident.CivilStatus) ? "single" : resident.CivilStatus!);
                        text.Span($", is a bonafide resident of {settings.BarangayName}, {settings.Municipality}, {settings.Province}.");
                    });

                    column.Item().Text("This further certifies that the above-named resident belongs to an indigent family and is qualified for government assistance and support programs subject to proper verification.");

                    column.Item().Text("This certification is issued upon request for whatever lawful purpose it may serve.");

                    BuildIssuedAndSignature(column, settings);
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    public static Task<byte[]> GenerateOfficialLetterPdfAsync(
        Resident resident,
        BarangaySettings settings,
        string recipientName,
        string recipientAddress,
        string subject,
        string body)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontSize(11));

                page.Header().Column(column =>
                {
                    BuildLetterhead(column, settings);
                    column.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(24).Column(column =>
                {
                    column.Spacing(16);
                    column.Item().AlignRight().Text(DateTime.Today.ToString("MMMM dd, yyyy"));
                    column.Item().Text(recipientName).SemiBold();
                    column.Item().Text(recipientAddress);
                    column.Item().Text($"Subject: {subject}").Bold();

                    column.Item().Text(text =>
                    {
                        foreach (var line in NormalizeMultiline(body))
                        {
                            text.Line(line);
                        }
                    });

                    column.Item().PaddingTop(24).Text("Prepared relative to resident: " + GetResidentFullName(resident));

                    BuildSignatureBlock(column, settings);
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    private static void CreateLabeledField(QuestPDF.Infrastructure.IContainer container, string label, string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label).Bold();
            column.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(value) ? "-" : value);
        });
    }

    private static void CreateLabeledBlock(QuestPDF.Infrastructure.IContainer container, string label, string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label).Bold();
            column.Item()
                .PaddingTop(4)
                .Border(1)
                .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                .Padding(10)
                .Text(string.IsNullOrWhiteSpace(value) ? "-" : value);
        });
    }

    private static string FormatDate(string? value)
    {
        return DateTime.TryParse(value, out var parsedDate)
            ? parsedDate.ToString("MMMM dd, yyyy")
            : "-";
    }

    private static int CalculateAge(string? birthDate)
    {
        if (!DateTime.TryParse(birthDate, out var parsedDate))
        {
            return 0;
        }

        var age = DateTime.Today.Year - parsedDate.Year;
        if (parsedDate.Date > DateTime.Today.AddYears(-age))
        {
            age--;
        }

        return Math.Max(age, 0);
    }

    private static void BuildLetterhead(ColumnDescriptor column, BarangaySettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.LogoPath) && File.Exists(settings.LogoPath))
        {
            column.Item().AlignCenter().Height(60).Image(File.ReadAllBytes(settings.LogoPath)).FitHeight();
        }

        column.Item().AlignCenter().Text(settings.BarangayName).Bold().FontSize(16);
        column.Item().AlignCenter().Text($"{settings.Municipality}, {settings.Province}").FontSize(11);
    }

    private static void BuildIssuedAndSignature(ColumnDescriptor column, BarangaySettings settings)
    {
        column.Item().PaddingTop(10).Text($"Issued this {DateTime.Today:MMMM dd, yyyy}.");
        BuildSignatureBlock(column, settings);
    }

    private static void BuildSignatureBlock(ColumnDescriptor column, BarangaySettings settings)
    {
        column.Item().PaddingTop(36).AlignRight().Column(signature =>
        {
            signature.Item().Text(settings.CaptainName ?? "Barangay Captain").Bold();
            signature.Item().Text("Barangay Captain");
        });
    }

    private static IEnumerable<string> NormalizeMultiline(string body)
    {
        return body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim());
    }

    private static string GetResidentFullName(Resident resident)
    {
        return string.Join(" ", new[] { resident.FirstName, resident.MiddleName, resident.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string GetPronoun(string? gender, bool subjectCase)
    {
        var value = gender?.Trim().ToLowerInvariant();
        return value switch
        {
            "male" => subjectCase ? "He" : "him",
            "female" => subjectCase ? "She" : "her",
            _ => subjectCase ? "They" : "them"
        };
    }
}

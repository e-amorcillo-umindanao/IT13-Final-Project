using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SOMS.Models;
using PdfColors = QuestPDF.Helpers.Colors;

namespace SOMS.Helpers;

public static class PdfExportHelper
{
    public static byte[] GenerateMembershipCertificate(Member member, OrgSetting orgSettings, string? additionalNotes = null)
    {
        var issuedDate = DateTime.Now;
        var logoBytes = LoadLogoBytes(orgSettings.LogoPath);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureCertificatePage(page);

                page.Content().Element(content =>
                {
                    BuildCertificateShell(
                        content,
                        orgSettings,
                        issuedDate,
                        logoBytes,
                        "CERTIFICATE OF MEMBERSHIP",
                        body =>
                        {
                            body.Item().PaddingTop(16).AlignCenter().Text("This certifies that").FontSize(14);
                            body.Item().PaddingVertical(10).AlignCenter().Text(text =>
                            {
                                text.Span(member.FullName).FontSize(24).Bold().Underline();
                            });
                            body.Item().AlignCenter().Text($"is a recognized {member.RecruitmentStage} member of {orgSettings.OrgName}")
                                .FontSize(14);
                            body.Item().PaddingTop(8).AlignCenter().Text($"Academic Year: {orgSettings.AcademicYear} | {orgSettings.SemesterLabel}")
                                .FontSize(12);

                            if (!string.IsNullOrWhiteSpace(additionalNotes))
                            {
                                body.Item().PaddingTop(18).AlignCenter().Text(additionalNotes.Trim())
                                    .FontSize(11).Italic();
                            }
                        });
                });
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateParticipationCertificate(Member member, Event eventItem, OrgSetting orgSettings, string? additionalNotes = null)
    {
        var issuedDate = DateTime.Now;
        var logoBytes = LoadLogoBytes(orgSettings.LogoPath);
        var venueSegment = string.IsNullOrWhiteSpace(eventItem.Venue)
            ? string.Empty
            : $" at {eventItem.Venue}";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureCertificatePage(page);

                page.Content().Element(content =>
                {
                    BuildCertificateShell(
                        content,
                        orgSettings,
                        issuedDate,
                        logoBytes,
                        "CERTIFICATE OF PARTICIPATION",
                        body =>
                        {
                            body.Item().PaddingTop(16).AlignCenter().Text("This certifies that").FontSize(14);
                            body.Item().PaddingVertical(10).AlignCenter().Text(text =>
                            {
                                text.Span(member.FullName).FontSize(24).Bold().Underline();
                            });
                            body.Item().AlignCenter().Text($"participated in {eventItem.Title}")
                                .FontSize(14);
                            body.Item().PaddingTop(8).AlignCenter().Text($"held on {eventItem.EventDate:MMMM dd, yyyy}{venueSegment}")
                                .FontSize(12);

                            if (!string.IsNullOrWhiteSpace(additionalNotes))
                            {
                                body.Item().PaddingTop(18).AlignCenter().Text(additionalNotes.Trim())
                                    .FontSize(11).Italic();
                            }
                        });
                });
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateOfficialLetter(OrgSetting orgSettings, string recipientName, string body, string? additionalNotes)
    {
        var issuedDate = DateTime.Now;
        var logoBytes = LoadLogoBytes(orgSettings.LogoPath);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2f, Unit.Centimetre);
                page.DefaultTextStyle(text => text.FontFamily(Fonts.Calibri).FontSize(11).FontColor(PdfColors.Grey.Darken4));
                page.PageColor(PdfColors.White);

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(header =>
                        {
                            header.Spacing(4);
                            header.Item().Text(orgSettings.OrgName).FontSize(16).Bold();
                            header.Item().Text($"Academic Year {orgSettings.AcademicYear}").FontSize(11);
                        });

                        row.ConstantItem(150).AlignRight().Text($"{issuedDate:MMMM dd, yyyy}").FontSize(11);
                    });

                    if (logoBytes is not null)
                    {
                        column.Item().AlignLeft().Height(60).Image(logoBytes).FitHeight();
                    }

                    column.Item().Text($"To: {recipientName.Trim()}").FontSize(12).Bold();
                    column.Item().LineHorizontal(1).LineColor(PdfColors.Grey.Lighten1);
                    column.Item().PaddingTop(4).Text(body.Trim()).Justify().LineHeight(1.5f);

                    if (!string.IsNullOrWhiteSpace(additionalNotes))
                    {
                        column.Item().PaddingTop(8).Text($"Additional Notes: {additionalNotes.Trim()}").FontSize(10).Italic();
                    }

                    column.Item().ExtendVertical();

                    column.Item().AlignRight().Width(220).Column(footer =>
                    {
                        footer.Spacing(6);
                        footer.Item().PaddingTop(24).LineHorizontal(1).LineColor(PdfColors.Grey.Medium);
                        footer.Item().AlignCenter().Text(OrFallback(orgSettings.PresidentName, "Organization President")).Bold();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void ConfigureCertificatePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.5f, Unit.Centimetre);
        page.PageColor(PdfColors.White);
        page.DefaultTextStyle(text => text.FontFamily(Fonts.Calibri).FontColor(PdfColors.Grey.Darken4));
    }

    private static void BuildCertificateShell(
        QuestPDF.Infrastructure.IContainer container,
        OrgSetting orgSettings,
        DateTime issuedDate,
        byte[]? logoBytes,
        string title,
        Action<ColumnDescriptor> bodyBuilder)
    {
        container
            .Border(2)
            .BorderColor(PdfColors.Grey.Lighten1)
            .PaddingVertical(24)
            .PaddingHorizontal(28)
            .Column(column =>
            {
                column.Spacing(8);

                if (logoBytes is not null)
                {
                    column.Item().AlignCenter().Height(80).Image(logoBytes).FitHeight();
                }

                column.Item().AlignCenter().Text(orgSettings.OrgName).FontSize(20).Bold();
                column.Item().AlignCenter().Text(title)
                    .FontSize(14)
                    .SemiBold()
                    .LetterSpacing(2);

                bodyBuilder(column);

                column.Item().ExtendVertical();

                column.Item().PaddingTop(36).Row(row =>
                {
                    row.RelativeItem().Column(signature =>
                    {
                        signature.Spacing(4);
                        signature.Item().LineHorizontal(1).LineColor(PdfColors.Grey.Medium);
                        signature.Item().AlignCenter().Text(OrFallback(orgSettings.AdviserName, "Faculty Adviser")).Bold();
                        signature.Item().AlignCenter().Text("Faculty Adviser").FontSize(10);
                    });

                    row.ConstantItem(40);

                    row.RelativeItem().Column(signature =>
                    {
                        signature.Spacing(4);
                        signature.Item().LineHorizontal(1).LineColor(PdfColors.Grey.Medium);
                        signature.Item().AlignCenter().Text(OrFallback(orgSettings.PresidentName, "Organization President")).Bold();
                        signature.Item().AlignCenter().Text("Organization President").FontSize(10);
                    });
                });

                column.Item().PaddingTop(12).AlignRight().Text($"Issued: {issuedDate:MMMM dd, yyyy}").FontSize(10);
            });
    }

    private static byte[]? LoadLogoBytes(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
        {
            return null;
        }

        return File.ReadAllBytes(logoPath);
    }

    private static string OrFallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

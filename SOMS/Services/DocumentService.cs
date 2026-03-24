using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using SOMS.Data;
using SOMS.Helpers;
using SOMS.Models;

namespace SOMS.Services;

public class DocumentService
{
    private readonly AppDbContext _db;

    public DocumentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OrgSetting> GetOrgSettingsAsync()
    {
        var orgSettings = await _db.OrgSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.SettingId == 1);

        return orgSettings ?? throw new InvalidOperationException("Organization settings are not configured.");
    }

    public async Task<byte[]> GenerateCertificateOfMembershipAsync(int memberId, string? additionalNotes = null)
    {
        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.MemberId == memberId && !item.IsDeleted);

        if (member is null)
        {
            throw new InvalidOperationException("Member not found.");
        }

        var orgSettings = await GetOrgSettingsAsync();
        return PdfExportHelper.GenerateMembershipCertificate(member, orgSettings, additionalNotes);
    }

    public async Task<byte[]> GenerateCertificateOfParticipationAsync(int memberId, int eventId, string? additionalNotes = null)
    {
        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.MemberId == memberId && !item.IsDeleted);

        if (member is null)
        {
            throw new InvalidOperationException("Member not found.");
        }

        var eventItem = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.EventId == eventId);

        if (eventItem is null)
        {
            throw new InvalidOperationException("Event not found.");
        }

        var orgSettings = await GetOrgSettingsAsync();
        return PdfExportHelper.GenerateParticipationCertificate(member, eventItem, orgSettings, additionalNotes);
    }

    public async Task<byte[]> GenerateOfficialLetterAsync(string recipientName, string body, string? additionalNotes)
    {
        var orgSettings = await GetOrgSettingsAsync();
        return PdfExportHelper.GenerateOfficialLetter(orgSettings, recipientName, body, additionalNotes);
    }

    public async Task<string> SavePdfToFileAsync(byte[] pdf, string fileName)
    {
        var documentsPath = Path.Combine(FileSystem.AppDataDirectory, "Documents");
        Directory.CreateDirectory(documentsPath);

        var fullPath = Path.Combine(documentsPath, fileName);
        await File.WriteAllBytesAsync(fullPath, pdf);
        return fullPath;
    }
}

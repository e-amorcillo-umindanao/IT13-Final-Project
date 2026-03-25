using BRMS.Data;
using BRMS.Helpers;
using BRMS.Models;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class DocumentService
{
    private readonly AppDbContext _dbContext;

    public DocumentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<byte[]> GenerateCertificateOfResidencyAsync(int residentId)
    {
        var resident = await GetResidentAsync(residentId);
        var settings = await GetSettingsAsync();
        return await PdfExportHelper.GenerateCertificateOfResidencyPdfAsync(resident, settings);
    }

    public async Task<byte[]> GenerateCertificateOfIndigencyAsync(int residentId)
    {
        var resident = await GetResidentAsync(residentId);
        var settings = await GetSettingsAsync();
        return await PdfExportHelper.GenerateCertificateOfIndigencyPdfAsync(resident, settings);
    }

    public async Task<byte[]> GenerateOfficialLetterAsync(
        int residentId,
        string recipientName,
        string recipientAddress,
        string subject,
        string body)
    {
        var resident = await GetResidentAsync(residentId);
        var settings = await GetSettingsAsync();
        return await PdfExportHelper.GenerateOfficialLetterPdfAsync(
            resident,
            settings,
            recipientName,
            recipientAddress,
            subject,
            body);
    }

    private async Task<Resident> GetResidentAsync(int residentId)
    {
        var resident = await _dbContext.Residents
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ResidentId == residentId && !candidate.IsDeleted);

        return resident ?? throw new InvalidOperationException("Resident not found.");
    }

    private async Task<BarangaySettings> GetSettingsAsync()
    {
        var settings = await _dbContext.BarangaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.SettingId == 1);

        return settings ?? throw new InvalidOperationException("Barangay settings not found.");
    }
}

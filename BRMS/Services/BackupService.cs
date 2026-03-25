using BRMS.Data;
using Microsoft.EntityFrameworkCore;

namespace BRMS.Services;

public class BackupService
{
    private readonly AppDbContext _dbContext;

    public BackupService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task BackupOnShutdownAsync()
    {
        var databasePath = _dbContext.Database.GetDbConnection().DataSource;
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            return;
        }

        await _dbContext.Database.CloseConnectionAsync();

        var backupDirectory = Path.Combine(AppContext.BaseDirectory, "Backups");
        Directory.CreateDirectory(backupDirectory);

        var backupFileName = $"brms_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var backupPath = Path.Combine(backupDirectory, backupFileName);

        File.Copy(databasePath, backupPath, overwrite: false);
    }
}

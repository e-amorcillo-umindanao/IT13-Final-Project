using Microsoft.Maui.Storage;

namespace SOMS.Services;

public class BackupService
{
    private readonly SemaphoreSlim _backupLock = new(1, 1);

    public async Task BackupAsync()
    {
        await _backupLock.WaitAsync();

        try
        {
            var source = Path.Combine(FileSystem.AppDataDirectory, "soms.db");
            if (!File.Exists(source))
            {
                return;
            }

            var backupDirectory = Path.Combine(FileSystem.AppDataDirectory, "Backups");
            Directory.CreateDirectory(backupDirectory);

            var destination = GetUniqueBackupPath(backupDirectory);
            File.Copy(source, destination, overwrite: false);

            var backupFiles = new DirectoryInfo(backupDirectory)
                .GetFiles("soms_backup_*.db")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();

            foreach (var backupFile in backupFiles.Skip(5))
            {
                backupFile.Delete();
            }
        }
        finally
        {
            _backupLock.Release();
        }
    }

    private static string GetUniqueBackupPath(string backupDirectory)
    {
        var timestamp = DateTime.Now;
        var baseName = $"soms_backup_{timestamp:yyyyMMdd_HHmmss_fff}";
        var path = Path.Combine(backupDirectory, $"{baseName}.db");
        var sequence = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(backupDirectory, $"{baseName}_{sequence++}.db");
        }

        return path;
    }
}

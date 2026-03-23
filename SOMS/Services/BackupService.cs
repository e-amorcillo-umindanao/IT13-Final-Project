using Microsoft.Maui.Storage;

namespace SOMS.Services;

public class BackupService
{
    public Task BackupAsync()
    {
        var source = Path.Combine(FileSystem.AppDataDirectory, "soms.db");
        if (!File.Exists(source))
        {
            return Task.CompletedTask;
        }

        var backupDirectory = Path.Combine(FileSystem.AppDataDirectory, "Backups");
        Directory.CreateDirectory(backupDirectory);

        var destination = Path.Combine(
            backupDirectory,
            $"soms_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

        File.Copy(source, destination, overwrite: false);

        var backupFiles = new DirectoryInfo(backupDirectory)
            .GetFiles("soms_backup_*.db")
            .OrderByDescending(file => file.CreationTimeUtc)
            .ToList();

        foreach (var backupFile in backupFiles.Skip(5))
        {
            backupFile.Delete();
        }

        return Task.CompletedTask;
    }
}

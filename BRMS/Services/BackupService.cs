using BRMS.Data;

namespace BRMS.Services;

public class BackupService
{
    private readonly AppDbContext _dbContext;

    public BackupService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

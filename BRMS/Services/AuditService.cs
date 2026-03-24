using BRMS.Data;

namespace BRMS.Services;

public class AuditService
{
    private readonly AppDbContext _dbContext;

    public AuditService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

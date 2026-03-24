using BRMS.Data;

namespace BRMS.Services;

public class EngagementService
{
    private readonly AppDbContext _dbContext;

    public EngagementService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

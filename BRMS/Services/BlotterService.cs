using BRMS.Data;

namespace BRMS.Services;

public class BlotterService
{
    private readonly AppDbContext _dbContext;

    public BlotterService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

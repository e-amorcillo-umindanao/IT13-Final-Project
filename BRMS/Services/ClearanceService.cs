using BRMS.Data;

namespace BRMS.Services;

public class ClearanceService
{
    private readonly AppDbContext _dbContext;

    public ClearanceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

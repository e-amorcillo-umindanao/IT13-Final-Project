using BRMS.Data;

namespace BRMS.Services;

public class ResidentService
{
    private readonly AppDbContext _dbContext;

    public ResidentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

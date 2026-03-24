using BRMS.Data;

namespace BRMS.Services;

public class HouseholdService
{
    private readonly AppDbContext _dbContext;

    public HouseholdService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

using BRMS.Data;

namespace BRMS.Services;

public class InteractionService
{
    private readonly AppDbContext _dbContext;

    public InteractionService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

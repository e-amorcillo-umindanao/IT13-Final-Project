using BRMS.Data;

namespace BRMS.Services;

public class EventService
{
    private readonly AppDbContext _dbContext;

    public EventService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

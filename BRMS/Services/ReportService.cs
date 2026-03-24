using BRMS.Data;

namespace BRMS.Services;

public class ReportService
{
    private readonly AppDbContext _dbContext;

    public ReportService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

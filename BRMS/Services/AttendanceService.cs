using BRMS.Data;

namespace BRMS.Services;

public class AttendanceService
{
    private readonly AppDbContext _dbContext;

    public AttendanceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

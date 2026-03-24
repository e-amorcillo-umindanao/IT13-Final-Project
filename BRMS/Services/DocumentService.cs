using BRMS.Data;

namespace BRMS.Services;

public class DocumentService
{
    private readonly AppDbContext _dbContext;

    public DocumentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

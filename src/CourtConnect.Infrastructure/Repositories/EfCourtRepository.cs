using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Domain;
using Microsoft.EntityFrameworkCore;

namespace DeemSoft.CourtConnect.Infrastructure.Repositories;

public sealed class EfCourtRepository(CourtConnectDbContext context) : ICourtRepository
{
    public async Task AddAsync(Court court, CancellationToken cancellationToken)
    {
        await context.Courts.AddAsync(court, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public Task<Court?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return context.Courts.SingleOrDefaultAsync(court => court.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Court>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await context.Courts
            .OrderBy(court => court.Name)
            .ToListAsync(cancellationToken);
    }
}

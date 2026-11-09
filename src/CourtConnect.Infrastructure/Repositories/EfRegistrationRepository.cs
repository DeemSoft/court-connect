using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Domain;
using Microsoft.EntityFrameworkCore;

namespace DeemSoft.CourtConnect.Infrastructure.Repositories;

public sealed class EfRegistrationRepository(CourtConnectDbContext context) : IRegistrationRepository
{
    public async Task AddAsync(Registration registration, CancellationToken cancellationToken)
    {
        await context.Registrations.AddAsync(registration, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public Task<Registration?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return context.Registrations.SingleOrDefaultAsync(registration => registration.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken)
    {
        // El listado representa jugadores apuntados ahora; el historial cancelado se conserva en la base de datos.
        return await context.Registrations
            .AsNoTracking()
            .Where(registration =>
                registration.GameId == gameId
                && registration.Status == RegistrationStatus.Confirmed)
            .OrderBy(registration => registration.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

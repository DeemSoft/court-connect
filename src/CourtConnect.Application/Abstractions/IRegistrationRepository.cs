using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Abstractions;

public interface IRegistrationRepository
{
    Task AddAsync(Registration registration, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<Registration?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Registration>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Registration>> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken);
}

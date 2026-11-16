using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Abstractions;

public interface IGameRepository
{
    Task AddAsync(Game game, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Game>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<Game>> GetOpenUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<Game>> GetByOrganizerUserIdAsync(Guid organizerUserId, CancellationToken cancellationToken);
}

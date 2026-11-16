using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Abstractions;

public interface ICourtRepository
{
    Task AddAsync(Court court, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<Court?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasGamesAsync(Guid courtId, CancellationToken cancellationToken);

    void Remove(Court court);

    Task<IReadOnlyList<Court>> GetAllAsync(CancellationToken cancellationToken);
}

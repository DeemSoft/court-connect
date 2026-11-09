using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Abstractions;

public interface IPlayerRepository
{
    Task AddAsync(Player player, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Player?> GetByNicknameAsync(string nickname, CancellationToken cancellationToken);
}

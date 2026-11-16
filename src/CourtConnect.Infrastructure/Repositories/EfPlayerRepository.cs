using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Domain;
using Microsoft.EntityFrameworkCore;

namespace DeemSoft.CourtConnect.Infrastructure.Repositories;

public sealed class EfPlayerRepository(CourtConnectDbContext context) : IPlayerRepository
{
    public async Task AddAsync(Player player, CancellationToken cancellationToken)
    {
        await context.Players.AddAsync(player, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return context.Players.SingleOrDefaultAsync(player => player.Id == id, cancellationToken);
    }

    public Task<Player?> GetByNicknameAsync(string nickname, CancellationToken cancellationToken)
    {
        return context.Players.SingleOrDefaultAsync(player => player.Nickname == nickname, cancellationToken);
    }

    public async Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await context.Players
            .OrderBy(player => player.Nickname)
            .ToListAsync(cancellationToken);
    }
}

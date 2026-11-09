using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Domain;
using Microsoft.EntityFrameworkCore;

namespace DeemSoft.CourtConnect.Infrastructure.Repositories;

public sealed class EfGameRepository(CourtConnectDbContext context) : IGameRepository
{
    public async Task AddAsync(Game game, CancellationToken cancellationToken)
    {
        await context.Games.AddAsync(game, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Se cargan relaciones que necesitan las reglas del dominio y el mapeo de salida.
        return context.Games
            .Include(game => game.Court)
            .Include(game => game.Registrations)
            .SingleOrDefaultAsync(game => game.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Game>> GetOpenUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // El estado y el instante se filtran en SQL; las plazas se calculan con las inscripciones cargadas.
        return await context.Games
            .Include(game => game.Court)
            .Include(game => game.Registrations)
            .Where(game => game.Status == GameStatus.Open && game.StartAt > now)
            .OrderBy(game => game.StartAt)
            .ToListAsync(cancellationToken);
    }
}

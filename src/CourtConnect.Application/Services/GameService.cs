using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Exceptions;
using DeemSoft.CourtConnect.Application.Mapping;
using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Services;

public sealed class GameService(IGameRepository games, ICourtRepository courts, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<GameDto>> GetOpenUpcomingAsync(CancellationToken cancellationToken)
    {
        // Se inyecta el reloj para que el filtro temporal sea determinista en las pruebas.
        var now = timeProvider.GetUtcNow();
        var upcomingGames = await games.GetOpenUpcomingAsync(now, cancellationToken);

        return upcomingGames.Select(DtoMapper.ToDto).ToArray();
    }

    public async Task<IReadOnlyList<GameDto>> GetByOrganizerUserIdAsync(
        Guid organizerUserId,
        CancellationToken cancellationToken)
    {
        var organizerGames = await games.GetByOrganizerUserIdAsync(organizerUserId, cancellationToken);
        return organizerGames.Select(DtoMapper.ToDto).ToArray();
    }

    public async Task<GameDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Game", id);

        return DtoMapper.ToDto(game);
    }

    public async Task<GameDto> CreateAsync(
        CreateGameRequest request,
        Guid organizerUserId,
        CancellationToken cancellationToken)
    {
        var courtId = request.CourtId
            ?? throw new ArgumentException("Debes seleccionar una pista.", nameof(request.CourtId));
        var court = await courts.GetByIdAsync(courtId, cancellationToken)
            ?? throw new EntityNotFoundException("Court", courtId);

        // El dominio valida las reglas del partido; aquí se comprueba que la fecha aún sea futura.
        var now = timeProvider.GetUtcNow();
        if (request.StartAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(request.StartAt), "La fecha del partido debe estar en el futuro.");
        }

        var game = new Game(
            court,
            request.StartAt,
            TimeSpan.FromMinutes(request.DurationMinutes),
            request.Capacity,
            request.MinimumAge,
            request.PricePerPlayer,
            organizerUserId: organizerUserId);

        await games.AddAsync(game, cancellationToken);
        await games.SaveChangesAsync(cancellationToken);

        return DtoMapper.ToDto(game);
    }

    public async Task<GameDto> UpdateAsync(
        Guid id,
        Guid organizerUserId,
        UpdateGameRequest request,
        CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Game", id);

        EnsureOrganizer(game, organizerUserId);

        if (request.StartAt <= timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(request.StartAt), "La fecha del partido debe estar en el futuro.");
        }

        game.UpdateDetails(
            request.StartAt,
            TimeSpan.FromMinutes(request.DurationMinutes),
            request.Capacity,
            request.MinimumAge,
            request.PricePerPlayer);

        await games.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToDto(game);
    }

    public async Task<GameDto> SetStatusAsync(
        Guid id,
        Guid organizerUserId,
        GameStatus status,
        CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Game", id);

        EnsureOrganizer(game, organizerUserId);
        game.SetStatus(status);

        await games.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToDto(game);
    }

    private static void EnsureOrganizer(Game game, Guid organizerUserId)
    {
        if (!game.IsOrganizedBy(organizerUserId))
        {
            throw new ForbiddenException("Solo el organizador de este partido puede gestionarlo.");
        }
    }
}

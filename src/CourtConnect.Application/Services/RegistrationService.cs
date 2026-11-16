using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Exceptions;
using DeemSoft.CourtConnect.Application.Mapping;
using DeemSoft.CourtConnect.Domain;
using Microsoft.Extensions.Logging;

namespace DeemSoft.CourtConnect.Application.Services;

public sealed class RegistrationService(
    IGameRepository games,
    IPlayerRepository players,
    IRegistrationRepository registrations,
    TimeProvider timeProvider,
    ILogger<RegistrationService> logger)
{
    public async Task<RegistrationDto> RegisterAsync(Guid gameId, Guid identityUserId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null)
        {
            logger.LogWarning("Registration rejected: game {GameId} was not found.", gameId);
            throw new EntityNotFoundException("Game", gameId);
        }

        var player = await players.GetByIdentityUserIdAsync(identityUserId, cancellationToken);
        if (player is null)
        {
            logger.LogWarning(
                "Registration rejected: account {IdentityUserId} has no player profile for game {GameId}.",
                identityUserId,
                gameId);
            throw new EntityNotFoundException("Player profile", identityUserId);
        }

        Registration registration;
        try
        {
            // Solo se traducen los rechazos del dominio; los errores al persistir deben propagarse como fallos del servidor.
            registration = game.Register(player, timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Registration rejected for player {PlayerId} and game {GameId}.",
                player.Id,
                gameId);
            throw new ConflictException(exception.Message);
        }

        await registrations.AddAsync(registration, cancellationToken);
        await registrations.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Registration {RegistrationId} created for player {PlayerId} and game {GameId}.",
            registration.Id,
            player.Id,
            gameId);

        return DtoMapper.ToDto(registration);
    }

    public async Task<IReadOnlyList<RegistrationDto>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken)
    {
        if (await games.GetByIdAsync(gameId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Game", gameId);
        }

        var gameRegistrations = await registrations.GetByGameIdAsync(gameId, cancellationToken);
        return gameRegistrations.Select(DtoMapper.ToDto).ToArray();
    }

    public async Task<IReadOnlyList<GameParticipantDto>> GetPlayersByGameIdAsync(
        Guid gameId,
        Guid organizerUserId,
        CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken)
            ?? throw new EntityNotFoundException("Game", gameId);
        if (!game.IsOrganizedBy(organizerUserId))
        {
            throw new ForbiddenException("Solo el organizador de este partido puede ver las inscripciones.");
        }

        return await registrations.GetPlayersByGameIdAsync(gameId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerRegistrationDto>> GetByIdentityUserIdAsync(
        Guid identityUserId,
        CancellationToken cancellationToken)
    {
        // La identidad se resuelve a su ficha; nunca se acepta un identificador de jugador enviado por el cliente.
        var player = await players.GetByIdentityUserIdAsync(identityUserId, cancellationToken);
        if (player is null)
        {
            throw new EntityNotFoundException("Player profile", identityUserId);
        }

        var playerRegistrations = await registrations.GetByPlayerIdAsync(player.Id, cancellationToken);
        var playerGameIds = playerRegistrations.Select(registration => registration.GameId).Distinct().ToArray();
        var playerGames = await games.GetByIdsAsync(playerGameIds, cancellationToken);
        var gamesById = playerGames.ToDictionary(game => game.Id);

        return playerRegistrations
            .Select(registration => new PlayerRegistrationDto(
                DtoMapper.ToDto(registration),
                DtoMapper.ToDto(gamesById.TryGetValue(registration.GameId, out var game)
                    ? game
                    : throw new EntityNotFoundException("Game", registration.GameId))))
            .OrderBy(item => item.Game.StartAt)
            .ToArray();
    }

    public async Task CancelAsync(Guid registrationId, Guid identityUserId, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetByIdAsync(registrationId, cancellationToken);
        if (registration is null)
        {
            throw new EntityNotFoundException("Registration", registrationId);
        }

        var player = await players.GetByIdentityUserIdAsync(identityUserId, cancellationToken);
        if (player is null || player.Id != registration.PlayerId)
        {
            logger.LogWarning(
                "Registration cancellation rejected for account {IdentityUserId} and registration {RegistrationId}.",
                identityUserId,
                registrationId);
            // Devolver el mismo 404 que para una inscripción inexistente evita revelar inscripciones ajenas.
            throw new EntityNotFoundException("Registration", registrationId);
        }

        var game = await games.GetByIdAsync(registration.GameId, cancellationToken)
            ?? throw new EntityNotFoundException("Game", registration.GameId);

        try
        {
            game.CancelRegistration(registration, timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await registrations.SaveChangesAsync(cancellationToken);
    }
}

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
    public async Task<RegistrationDto> RegisterAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null)
        {
            logger.LogWarning("Registration rejected: game {GameId} was not found.", gameId);
            throw new EntityNotFoundException("Game", gameId);
        }

        var player = await players.GetByIdAsync(playerId, cancellationToken);
        if (player is null)
        {
            logger.LogWarning("Registration rejected: player {PlayerId} was not found for game {GameId}.", playerId, gameId);
            throw new EntityNotFoundException("Player", playerId);
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
                playerId,
                gameId);
            throw new ConflictException(exception.Message);
        }

        await registrations.AddAsync(registration, cancellationToken);
        await registrations.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Registration {RegistrationId} created for player {PlayerId} and game {GameId}.",
            registration.Id,
            playerId,
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

    public async Task CancelAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetByIdAsync(registrationId, cancellationToken);
        if (registration is null)
        {
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

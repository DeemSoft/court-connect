using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Exceptions;
using DeemSoft.CourtConnect.Application.Mapping;
using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Services;

public sealed class PlayerService(IPlayerRepository players, TimeProvider timeProvider)
{
    public async Task<PlayerDto> CreateForIdentityAsync(
        Guid identityUserId,
        string email,
        CreatePlayerProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (await players.GetByIdentityUserIdAsync(identityUserId, cancellationToken) is not null)
        {
            throw new ConflictException("Esta cuenta ya tiene un perfil de jugador.");
        }

        if (await players.GetByNicknameAsync(request.Nickname.Trim(), cancellationToken) is not null)
        {
            throw new ConflictException($"El nombre de usuario '{request.Nickname}' ya está en uso.");
        }

        var dateOfBirth = request.DateOfBirth
            ?? throw new ArgumentException("La fecha de nacimiento es obligatoria.", nameof(request.DateOfBirth));
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var player = new Player(request.Nickname, request.FullName, dateOfBirth, email, today, identityUserId);

        await players.AddAsync(player, cancellationToken);
        await players.SaveChangesAsync(cancellationToken);

        return DtoMapper.ToDto(player);
    }

    public async Task<PlayerDto> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken cancellationToken)
    {
        var player = await players.GetByIdentityUserIdAsync(identityUserId, cancellationToken)
            ?? throw new EntityNotFoundException("Player profile", identityUserId);

        return DtoMapper.ToDto(player);
    }

    public async Task<PlayerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var player = await players.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Player", id);

        return DtoMapper.ToDto(player);
    }

    public async Task<IReadOnlyList<PlayerOptionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var allPlayers = await players.GetAllAsync(cancellationToken);
        return allPlayers.Select(DtoMapper.ToOptionDto).ToArray();
    }
}

using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Exceptions;
using DeemSoft.CourtConnect.Application.Mapping;
using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.Application.Services;

public sealed class PlayerService(IPlayerRepository players, TimeProvider timeProvider)
{
    public async Task<PlayerDto> CreateAsync(CreatePlayerRequest request, CancellationToken cancellationToken)
    {
        // Esta comprobación da un error claro; el índice único de la base también protege ante altas simultáneas.
        if (await players.GetByNicknameAsync(request.Nickname.Trim(), cancellationToken) is not null)
        {
            throw new ConflictException($"The nickname '{request.Nickname}' is already in use.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var dateOfBirth = request.DateOfBirth
            ?? throw new ArgumentException("The player's date of birth is required.", nameof(request.DateOfBirth));
        var player = new Player(request.Nickname, request.FullName, dateOfBirth, request.Email, today);

        await players.AddAsync(player, cancellationToken);
        await players.SaveChangesAsync(cancellationToken);

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

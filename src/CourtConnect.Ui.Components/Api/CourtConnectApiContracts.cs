namespace DeemSoft.CourtConnect.Ui.Components.Api;

public sealed record GameDto(
    Guid Id,
    Guid CourtId,
    string CourtName,
    DateTimeOffset StartAt,
    int DurationMinutes,
    int Capacity,
    int AvailableSlots,
    int MinimumAge,
    decimal PricePerPlayer,
    string Status);

public sealed record PlayerOptionDto(Guid Id, string Nickname);

public sealed record RegistrationDto(
    Guid Id,
    Guid GameId,
    Guid PlayerId,
    DateTimeOffset CreatedAt,
    decimal Amount,
    string Status);

public interface ICourtConnectApiClient
{
    Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken);

    Task<GameDto> GetGameAsync(Guid gameId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerOptionDto>> GetPlayersAsync(CancellationToken cancellationToken);

    Task<RegistrationDto> RegisterAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken);
}

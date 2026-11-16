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

public sealed record CourtDto(Guid Id, string Name, string Location);

public sealed record CurrentAccountDto(string? Email, IReadOnlyList<string> Roles);

public sealed record CreateGameRequestDto(
    Guid? CourtId,
    DateTimeOffset StartAt,
    int DurationMinutes,
    int Capacity,
    int MinimumAge,
    decimal PricePerPlayer);

public sealed record UpdateGameRequestDto(
    DateTimeOffset StartAt,
    int DurationMinutes,
    int Capacity,
    int MinimumAge,
    decimal PricePerPlayer);

public sealed record CreatePlayerProfileRequest(string Nickname, string FullName, DateOnly DateOfBirth);

public sealed record IdentityTokenResponse(
    string TokenType,
    string AccessToken,
    long ExpiresIn,
    string RefreshToken);

public sealed record RegistrationDto(
    Guid Id,
    Guid GameId,
    Guid PlayerId,
    DateTimeOffset CreatedAt,
    decimal Amount,
    string Status);

public sealed record PlayerRegistrationDto(RegistrationDto Registration, GameDto Game);

public interface ICourtConnectApiClient
{
    Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken);

    Task<GameDto> GetGameAsync(Guid gameId, CancellationToken cancellationToken);

    Task RegisterAccountAsync(string email, string password, CancellationToken cancellationToken);

    Task<IdentityTokenResponse> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<IdentityTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task<CurrentAccountDto> GetCurrentAccountAsync(string accessToken, CancellationToken cancellationToken);

    Task<CurrentAccountDto> GetMyAccountAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CourtDto>> GetCourtsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<GameDto>> GetMyOrganizedGamesAsync(CancellationToken cancellationToken);

    Task<GameDto> CreateGameAsync(CreateGameRequestDto request, CancellationToken cancellationToken);

    Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto request, CancellationToken cancellationToken);

    Task<GameDto> SetGameStatusAsync(Guid gameId, string status, CancellationToken cancellationToken);

    Task<PlayerDto> GetMyProfileAsync(CancellationToken cancellationToken);

    Task<PlayerDto> CreateMyProfileAsync(
        CreatePlayerProfileRequest request,
        CancellationToken cancellationToken);

    Task<RegistrationDto> RegisterAsync(Guid gameId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerRegistrationDto>> GetMyRegistrationsAsync(CancellationToken cancellationToken);
}

public sealed record PlayerDto(Guid Id, string Nickname, string FullName, DateOnly DateOfBirth, string Email);

public interface IWebAuthenticationService
{
    Task SignInAsync(string email, string password, bool isPersistent, CancellationToken cancellationToken);

    Task RefreshAsync(CancellationToken cancellationToken);

    Task SignOutAsync();
}

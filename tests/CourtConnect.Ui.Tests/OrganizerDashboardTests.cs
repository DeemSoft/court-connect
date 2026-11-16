using Bunit;
using DeemSoft.CourtConnect.Ui.Components.Api;
using DeemSoft.CourtConnect.Ui.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CourtConnect.Ui.Tests;

public sealed class OrganizerDashboardTests : BunitContext
{
    [Fact]
    public void Dashboard_ShowsOnlyTheOrganizersGamesAndAvailableCourts()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var game = CreateGame(court);
        var api = new FakeCourtConnectApiClient([court], [game]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerDashboard>();

        Assert.Contains("Gestiona tus partidos", page.Markup);
        Assert.Contains("Pista Central", page.Markup);
        Assert.Contains("Estado: Abierto", page.Markup);
        Assert.Contains("Cerrar partido", page.Markup);
    }

    [Fact]
    public async Task Dashboard_CreatesAGameForTheSelectedCourt()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var api = new FakeCourtConnectApiClient([court]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerDashboard>();
        await page.Find("#create-court").ChangeAsync(court.Id.ToString());
        await page.Find("form").SubmitAsync();

        Assert.NotNull(api.LastCreatedRequest);
        Assert.Equal(court.Id, api.LastCreatedRequest.CourtId);
        Assert.Contains("El partido se ha creado correctamente", page.Markup);
    }

    [Fact]
    public void Dashboard_RendersEditAndStatusActionsAsSubmitButtons()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var game = CreateGame(court);
        var api = new FakeCourtConnectApiClient([court], [game]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerDashboard>();

        Assert.Contains($"value=\"edit:{game.Id}\"", page.Markup);
        Assert.Contains($"value=\"status:{game.Id}\"", page.Markup);
        Assert.Contains("type=\"submit\"", page.Markup);
    }

    private static GameDto CreateGame(CourtDto court)
    {
        return new GameDto(
            Guid.NewGuid(),
            court.Id,
            court.Name,
            DateTimeOffset.UtcNow.AddDays(2),
            90,
            4,
            4,
            16,
            10m,
            "Open");
    }

    private sealed class FakeCourtConnectApiClient(
        IReadOnlyList<CourtDto> courts,
        IReadOnlyList<GameDto>? games = null) : ICourtConnectApiClient
    {
        private readonly List<GameDto> _games = games?.ToList() ?? [];

        public CreateGameRequestDto? LastCreatedRequest { get; private set; }

        public string? LastUpdatedStatus { get; private set; }

        public UpdateGameRequestDto? LastUpdatedRequest { get; private set; }

        public Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GameDto>>(_games);
        }

        public Task<GameDto> GetGameAsync(Guid gameId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_games.Single(game => game.Id == gameId));
        }

        public Task RegisterAccountAsync(string email, string password, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IdentityTokenResponse> LoginAsync(string email, string password, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IdentityTokenResponse("Bearer", "access", 3600, "refresh"));
        }

        public Task<IdentityTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IdentityTokenResponse("Bearer", "access", 3600, "refresh"));
        }

        public Task<CurrentAccountDto> GetCurrentAccountAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CurrentAccountDto("organizer@example.com", ["Organizer"]));
        }

        public Task<CurrentAccountDto> GetMyAccountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new CurrentAccountDto("organizer@example.com", ["Organizer"]));
        }

        public Task<IReadOnlyList<CourtDto>> GetCourtsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(courts);
        }

        public Task<IReadOnlyList<GameDto>> GetMyOrganizedGamesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GameDto>>(_games);
        }

        public Task<GameDto> CreateGameAsync(CreateGameRequestDto request, CancellationToken cancellationToken)
        {
            LastCreatedRequest = request;
            var court = courts.Single(item => item.Id == request.CourtId);
            var game = new GameDto(
                Guid.NewGuid(),
                court.Id,
                court.Name,
                request.StartAt,
                request.DurationMinutes,
                request.Capacity,
                request.Capacity,
                request.MinimumAge,
                request.PricePerPlayer,
                "Open");
            _games.Add(game);
            return Task.FromResult(game);
        }

        public Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto request, CancellationToken cancellationToken)
        {
            LastUpdatedRequest = request;
            var game = _games.Single(item => item.Id == gameId) with
            {
                StartAt = request.StartAt,
                DurationMinutes = request.DurationMinutes,
                Capacity = request.Capacity,
                MinimumAge = request.MinimumAge,
                PricePerPlayer = request.PricePerPlayer
            };
            ReplaceGame(game);
            return Task.FromResult(game);
        }

        public Task<GameDto> SetGameStatusAsync(Guid gameId, string status, CancellationToken cancellationToken)
        {
            LastUpdatedStatus = status;
            var game = _games.Single(item => item.Id == gameId) with { Status = status };
            ReplaceGame(game);
            return Task.FromResult(game);
        }

        public Task<PlayerDto> GetMyProfileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new PlayerDto(Guid.NewGuid(), "player", "Test Player", new DateOnly(2000, 1, 1), "player@example.com"));
        }

        public Task<PlayerDto> CreateMyProfileAsync(CreatePlayerProfileRequest request, CancellationToken cancellationToken)
        {
            return GetMyProfileAsync(cancellationToken);
        }

        public Task<RegistrationDto> RegisterAsync(Guid gameId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new RegistrationDto(Guid.NewGuid(), gameId, Guid.NewGuid(), DateTimeOffset.UtcNow, 10m, "Confirmed"));
        }

        public Task<IReadOnlyList<PlayerRegistrationDto>> GetMyRegistrationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PlayerRegistrationDto>>([]);
        }

        private void ReplaceGame(GameDto game)
        {
            var index = _games.FindIndex(item => item.Id == game.Id);
            _games[index] = game;
        }
    }
}

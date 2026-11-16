using Bunit;
using DeemSoft.CourtConnect.Ui.Components.Api;
using DeemSoft.CourtConnect.Ui.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CourtConnect.Ui.Tests;

public sealed class GamePageTests : BunitContext
{
    [Fact]
    public void Games_ShowsTheCourtAndAvailableSlots()
    {
        var game = CreateGame();
        Services.AddSingleton<ICourtConnectApiClient>(new FakeCourtConnectApiClient([game]));

        var page = Render<Games>();

        Assert.Contains(game.CourtName, page.Markup);
        Assert.Contains("3 plazas libres", page.Markup);
    }

    [Fact]
    public void Games_WhenThereAreNoResults_ShowsAnEmptyState()
    {
        Services.AddSingleton<ICourtConnectApiClient>(new FakeCourtConnectApiClient());

        var page = Render<Games>();

        Assert.Contains("No hay partidos abiertos próximamente", page.Markup);
    }

    [Fact]
    public void Games_WhenApiFails_ShowsTheApiMessage()
    {
        var api = new FakeCourtConnectApiClient
        {
            GamesException = new CourtConnectApiException(
                System.Net.HttpStatusCode.ServiceUnavailable,
                "La API no está disponible.")
        };
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<Games>();

        Assert.Contains("La API no está disponible", page.Markup);
    }

    [Fact]
    public void GameDetails_WhenNoSlotsRemain_ShowsComplete()
    {
        var game = CreateGame() with { AvailableSlots = 0 };
        Services.AddSingleton<ICourtConnectApiClient>(new FakeCourtConnectApiClient([game]));

        var page = Render<GameDetails>(parameters => parameters.Add(component => component.Id, game.Id));

        Assert.Contains("Completo", page.Markup);
        Assert.DoesNotContain("Apuntarme", page.Markup);
    }

    [Fact]
    public void GameDetails_WhenApiFails_ShowsTheApiMessage()
    {
        var api = new FakeCourtConnectApiClient
        {
            GameException = new CourtConnectApiException(
                System.Net.HttpStatusCode.NotFound,
                "No se encontró el recurso solicitado.")
        };
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<GameDetails>(parameters => parameters.Add(component => component.Id, Guid.NewGuid()));

        Assert.Contains("No se encontró el recurso solicitado", page.Markup);
    }

    [Fact]
    public async Task Register_WhenApiReturnsConflict_ShowsTheProblemMessage()
    {
        var game = CreateGame();
        var api = new FakeCourtConnectApiClient([game])
        {
            RegistrationException = new CourtConnectApiException(
                System.Net.HttpStatusCode.Conflict,
                "No se pudo completar la acción: el partido está completo.")
        };
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<RegisterForGame>(parameters => parameters.Add(component => component.Id, game.Id));
        await page.Find("form").SubmitAsync();

        Assert.Contains("el partido está completo", page.Markup);
        Assert.Equal(game.Id, api.LastRegistrationGameId);
    }

    [Fact]
    public async Task Register_WhenApiSucceeds_ShowsConfirmation()
    {
        var game = CreateGame();
        var api = new FakeCourtConnectApiClient([game]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<RegisterForGame>(parameters => parameters.Add(component => component.Id, game.Id));
        await page.Find("form").SubmitAsync();

        Assert.Contains("Tu inscripción se ha confirmado", page.Markup);
        Assert.Equal(game.Id, api.LastRegistrationGameId);
    }

    [Fact]
    public void Registrations_ShowsTheCurrentPlayersEnrolledGame()
    {
        var game = CreateGame();
        var api = new FakeCourtConnectApiClient([game])
        {
            MyRegistrations =
            [
                new PlayerRegistrationDto(
                    new RegistrationDto(Guid.NewGuid(), game.Id, Guid.NewGuid(), DateTimeOffset.UtcNow, 10m, "Confirmed"),
                    game)
            ]
        };
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<Registrations>();

        Assert.Contains("Inscripciones", page.Markup);
        Assert.Contains("Pista Central", page.Markup);
        Assert.Contains("Importe de la inscripción", page.Markup);
    }

    [Fact]
    public async Task Register_DoesNotRenderAPlayerSelector()
    {
        var game = CreateGame();
        Services.AddSingleton<ICourtConnectApiClient>(new FakeCourtConnectApiClient([game]));

        var page = Render<RegisterForGame>(parameters => parameters.Add(component => component.Id, game.Id));

        Assert.Empty(page.FindAll("select"));
        Assert.DoesNotContain("Jugador", page.Markup);
    }

    [Fact]
    public void Register_WhenGameIsFull_DoesNotRenderTheForm()
    {
        var game = CreateGame() with { AvailableSlots = 0 };
        Services.AddSingleton<ICourtConnectApiClient>(new FakeCourtConnectApiClient([game]));

        var page = Render<RegisterForGame>(parameters => parameters.Add(component => component.Id, game.Id));

        Assert.Contains("ya no admite inscripciones", page.Markup);
        Assert.Empty(page.FindAll("form"));
    }

    private static GameDto CreateGame()
    {
        return new GameDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pista Central",
            DateTimeOffset.UtcNow.AddDays(1),
            90,
            4,
            3,
            16,
            10m,
            "Open");
    }

    private sealed class FakeCourtConnectApiClient(IReadOnlyList<GameDto>? games = null) : ICourtConnectApiClient
    {
        public IReadOnlyList<PlayerRegistrationDto> MyRegistrations { get; init; } = [];

        public CourtConnectApiException? RegistrationException { get; init; }

        public CourtConnectApiException? GamesException { get; init; }

        public CourtConnectApiException? GameException { get; init; }

        public Guid? LastRegistrationGameId { get; private set; }

        public Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken)
        {
            if (GamesException is not null)
            {
                return Task.FromException<IReadOnlyList<GameDto>>(GamesException);
            }

            return Task.FromResult(games ?? Array.Empty<GameDto>());
        }

        public Task<CurrentAccountDto> GetCurrentAccountAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CurrentAccountDto("player@example.com", ["Player"]));
        }

        public Task<CurrentAccountDto> GetMyAccountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new CurrentAccountDto("player@example.com", ["Player"]));
        }

        public Task<IReadOnlyList<CourtDto>> GetCourtsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CourtDto>>([]);
        }

        public Task<IReadOnlyList<GameDto>> GetMyOrganizedGamesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(games ?? Array.Empty<GameDto>());
        }

        public Task<GameDto> CreateGameAsync(CreateGameRequestDto request, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateGame());
        }

        public Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto request, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateGame());
        }

        public Task<GameDto> SetGameStatusAsync(Guid gameId, string status, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateGame() with { Status = status });
        }

        public Task<GameDto> GetGameAsync(Guid gameId, CancellationToken cancellationToken)
        {
            if (GameException is not null)
            {
                return Task.FromException<GameDto>(GameException);
            }

            var game = (games ?? Array.Empty<GameDto>()).SingleOrDefault(item => item.Id == gameId);
            return game is null
                ? Task.FromException<GameDto>(new InvalidOperationException("Test game is missing."))
                : Task.FromResult(game);
        }

        public Task RegisterAccountAsync(string email, string password, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IdentityTokenResponse> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new IdentityTokenResponse("Bearer", "access", 3600, "refresh"));
        }

        public Task<IdentityTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IdentityTokenResponse("Bearer", "access", 3600, "refresh"));
        }

        public Task<PlayerDto> GetMyProfileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new PlayerDto(Guid.NewGuid(), "player", "Test Player", new DateOnly(2000, 1, 1), "player@example.com"));
        }

        public Task<PlayerDto> CreateMyProfileAsync(
            CreatePlayerProfileRequest request,
            CancellationToken cancellationToken)
        {
            return GetMyProfileAsync(cancellationToken);
        }

        public Task<RegistrationDto> RegisterAsync(Guid gameId, CancellationToken cancellationToken)
        {
            LastRegistrationGameId = gameId;
            if (RegistrationException is not null)
            {
                return Task.FromException<RegistrationDto>(RegistrationException);
            }

            return Task.FromResult(new RegistrationDto(
                Guid.NewGuid(),
                gameId,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                10m,
                "Confirmed"));
        }

        public Task<IReadOnlyList<PlayerRegistrationDto>> GetMyRegistrationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(MyRegistrations);
        }
    }
}

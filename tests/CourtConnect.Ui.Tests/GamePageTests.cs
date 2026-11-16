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
        var player = new PlayerOptionDto(Guid.NewGuid(), "base-player");
        var api = new FakeCourtConnectApiClient([game], [player])
        {
            RegistrationException = new CourtConnectApiException(
                System.Net.HttpStatusCode.Conflict,
                "No se pudo completar la acción: el partido está completo.")
        };
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<RegisterForGame>(parameters => parameters.Add(component => component.Id, game.Id));
        page.Find("select").Change(player.Id.ToString());
        await page.Find("form").SubmitAsync();

        Assert.Contains("el partido está completo", page.Markup);
        Assert.Equal((game.Id, player.Id), api.LastRegistration);
    }

    [Fact]
    public async Task Register_WhenApiSucceeds_ShowsConfirmation()
    {
        var game = CreateGame();
        var player = new PlayerOptionDto(Guid.NewGuid(), "base-player");
        var api = new FakeCourtConnectApiClient([game], [player]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<RegisterForGame>(parameters => parameters.Add(component => component.Id, game.Id));
        page.Find("select").Change(player.Id.ToString());
        await page.Find("form").SubmitAsync();

        Assert.Contains("Tu inscripción se ha confirmado", page.Markup);
    }

    [Fact]
    public async Task Register_WithoutAPlayer_ShowsValidationMessage()
    {
        var game = CreateGame();
        Services.AddSingleton<ICourtConnectApiClient>(
            new FakeCourtConnectApiClient([game], [new PlayerOptionDto(Guid.NewGuid(), "base-player")]));

        var page = Render<RegisterForGame>(parameters => parameters.Add(component => component.Id, game.Id));
        await page.Find("form").SubmitAsync();

        Assert.Contains("Selecciona un jugador", page.Markup);
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

    private sealed class FakeCourtConnectApiClient(
        IReadOnlyList<GameDto>? games = null,
        IReadOnlyList<PlayerOptionDto>? players = null) : ICourtConnectApiClient
    {
        public CourtConnectApiException? RegistrationException { get; init; }

        public CourtConnectApiException? GamesException { get; init; }

        public CourtConnectApiException? GameException { get; init; }

        public (Guid GameId, Guid PlayerId)? LastRegistration { get; private set; }

        public Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken)
        {
            if (GamesException is not null)
            {
                return Task.FromException<IReadOnlyList<GameDto>>(GamesException);
            }

            return Task.FromResult(games ?? Array.Empty<GameDto>());
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

        public Task<IReadOnlyList<PlayerOptionDto>> GetPlayersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(players ?? Array.Empty<PlayerOptionDto>());
        }

        public Task<RegistrationDto> RegisterAsync(
            Guid gameId,
            Guid playerId,
            CancellationToken cancellationToken)
        {
            LastRegistration = (gameId, playerId);
            if (RegistrationException is not null)
            {
                return Task.FromException<RegistrationDto>(RegistrationException);
            }

            return Task.FromResult(new RegistrationDto(
                Guid.NewGuid(),
                gameId,
                playerId,
                DateTimeOffset.UtcNow,
                10m,
                "Confirmed"));
        }
    }
}

using System.Net;
using System.Text.RegularExpressions;
using DeemSoft.CourtConnect.Ui.Components.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CourtConnect.Ui.Tests;

public sealed class RegisterFormHttpTests
{
    [Fact]
    public async Task PostForm_WithSelectedPlayer_BindsThePlayerAndConfirmsRegistration()
    {
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var api = new FakeCourtConnectApiClient(gameId, playerId);
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICourtConnectApiClient>();
                services.AddSingleton<ICourtConnectApiClient>(api);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var route = $"/games/{gameId}/register";

        using var getResponse = await client.GetAsync(route);
        var html = await getResponse.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.True(tokenMatch.Success);
        Assert.Contains("name=\"FormModel.PlayerId\"", html);

        using var formContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "RegisterForGame"),
            new KeyValuePair<string, string>("__RequestVerificationToken", tokenMatch.Groups[1].Value),
            new KeyValuePair<string, string>("FormModel.PlayerId", playerId.ToString())
        ]);
        using var postResponse = await client.PostAsync(route, formContent);
        var postHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Tu inscripción se ha confirmado", postHtml);
        Assert.Equal((gameId, playerId), api.LastRegistration);
    }

    private sealed class FakeCourtConnectApiClient(Guid gameId, Guid playerId) : ICourtConnectApiClient
    {
        public (Guid GameId, Guid PlayerId)? LastRegistration { get; private set; }

        public Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GameDto>>([]);
        }

        public Task<GameDto> GetGameAsync(Guid requestedGameId, CancellationToken cancellationToken)
        {
            if (requestedGameId != gameId)
            {
                return Task.FromException<GameDto>(new InvalidOperationException("Unexpected game id."));
            }

            return Task.FromResult(new GameDto(
                gameId,
                Guid.NewGuid(),
                "Pista de prueba",
                DateTimeOffset.UtcNow.AddDays(1),
                90,
                4,
                4,
                16,
                10m,
                "Open"));
        }

        public Task<IReadOnlyList<PlayerOptionDto>> GetPlayersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PlayerOptionDto>>(
                [new PlayerOptionDto(playerId, "jugador-prueba")]);
        }

        public Task<RegistrationDto> RegisterAsync(
            Guid requestedGameId,
            Guid requestedPlayerId,
            CancellationToken cancellationToken)
        {
            LastRegistration = (requestedGameId, requestedPlayerId);
            return Task.FromResult(new RegistrationDto(
                Guid.NewGuid(),
                requestedGameId,
                requestedPlayerId,
                DateTimeOffset.UtcNow,
                10m,
                "Confirmed"));
        }
    }
}

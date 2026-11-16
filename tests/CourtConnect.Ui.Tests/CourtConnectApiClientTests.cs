using System.Net;
using System.Text;
using DeemSoft.CourtConnect.Ui.Components.Api;
using DeemSoft.CourtConnect.Ui.Web.Services;

namespace CourtConnect.Ui.Tests;

public sealed class CourtConnectApiClientTests
{
    [Fact]
    public async Task GetGamesAsync_DeserializesTheApiResponse()
    {
        var gameId = Guid.NewGuid();
        using var httpClient = CreateClient(HttpStatusCode.OK, $$"""
            [
              {
                "id": "{{gameId}}",
                "courtId": "{{Guid.NewGuid()}}",
                "courtName": "Pista Central",
                "startAt": "2027-01-01T18:00:00Z",
                "durationMinutes": 90,
                "capacity": 4,
                "availableSlots": 3,
                "minimumAge": 16,
                "pricePerPlayer": 10,
                "status": "Open"
              }
            ]
            """);
        var api = new CourtConnectApiClient(httpClient);

        var games = await api.GetGamesAsync(CancellationToken.None);

        var game = Assert.Single(games);
        Assert.Equal(gameId, game.Id);
        Assert.Equal("Pista Central", game.CourtName);
        Assert.Equal(3, game.AvailableSlots);
    }

    [Fact]
    public async Task RegisterAccountAsync_PostsTheIdentityApiPayload()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        await api.RegisterAccountAsync("player@example.com", "Strong-password-123!", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/auth/register", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains("\"email\":\"player@example.com\"", handler.RequestBody);
        Assert.Contains("\"password\":\"Strong-password-123!\"", handler.RequestBody);
    }

    [Fact]
    public async Task LoginAsync_UsesBearerTokenModeAndDeserializesTokens()
    {
        using var httpClient = CreateClient(
            HttpStatusCode.OK,
            """{"tokenType":"Bearer","accessToken":"access-token","expiresIn":3600,"refreshToken":"refresh-token"}""");
        var api = new CourtConnectApiClient(httpClient);

        var tokens = await api.LoginAsync("player@example.com", "Strong-password-123!", CancellationToken.None);

        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal("access-token", tokens.AccessToken);
        Assert.Equal(3600, tokens.ExpiresIn);
    }

    [Fact]
    public async Task RefreshAsync_SendsTheRefreshTokenToIdentityApi()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """{"tokenType":"Bearer","accessToken":"new-access","expiresIn":3600,"refreshToken":"new-refresh"}""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var tokens = await api.RefreshAsync("old-refresh", CancellationToken.None);

        Assert.Equal("new-access", tokens.AccessToken);
        Assert.Equal("/auth/refresh", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Contains("\"refreshToken\":\"old-refresh\"", handler.RequestBody);
    }

    [Fact]
    public async Task GetCurrentAccountAsync_SendsTheSuppliedAccessToken()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"email":"organizer@example.com","roles":["Organizer"]}""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var account = await api.GetCurrentAccountAsync("organizer-access", CancellationToken.None);

        Assert.Equal("/auth/me", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("organizer-access", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("Organizer", account.Roles);
    }

    [Fact]
    public async Task GetMyAccountAsync_UsesTheAuthenticatedAccountEndpoint()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"email":"player@example.com","roles":["Player"]}""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var account = await api.GetMyAccountAsync(CancellationToken.None);

        Assert.Equal("/auth/me", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("player@example.com", account.Email);
        Assert.Contains("Player", account.Roles);
    }

    [Fact]
    public async Task CreateCourtAsync_PostsTheCourtPayload()
    {
        var courtId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent($$"""{"id":"{{courtId}}","name":"Central","location":"Barcelona"}""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var court = await api.CreateCourtAsync(
            new CreateCourtRequestDto("Central", "Barcelona"),
            CancellationToken.None);

        Assert.Equal(courtId, court.Id);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/courts", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains("\"name\":\"Central\"", handler.RequestBody);
        Assert.Contains("\"location\":\"Barcelona\"", handler.RequestBody);
    }

    [Fact]
    public async Task UpdateCourtAsync_PutsTheCourtPayload()
    {
        var courtId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent($$"""{"id":"{{courtId}}","name":"Norte","location":"Badalona"}""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var court = await api.UpdateCourtAsync(
            courtId,
            new UpdateCourtRequestDto("Norte", "Badalona"),
            CancellationToken.None);

        Assert.Equal(courtId, court.Id);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal($"/courts/{courtId}", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains("\"name\":\"Norte\"", handler.RequestBody);
        Assert.Contains("\"location\":\"Badalona\"", handler.RequestBody);
    }

    [Fact]
    public async Task DeleteCourtAsync_UsesTheCourtResourceEndpoint()
    {
        var courtId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        await api.DeleteCourtAsync(courtId, CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal($"/courts/{courtId}", handler.Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateGameAsync_PostsTheOrganizerGamePayload()
    {
        var gameId = Guid.NewGuid();
        var courtId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent(
                $$"""{"id":"{{gameId}}","courtId":"{{courtId}}","courtName":"Central","startAt":"2027-01-01T18:00:00Z","durationMinutes":90,"capacity":4,"availableSlots":4,"minimumAge":16,"pricePerPlayer":10,"status":"Open"}""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var game = await api.CreateGameAsync(
            new CreateGameRequestDto(courtId, DateTimeOffset.UtcNow.AddDays(2), 90, 4, 16, 10m),
            CancellationToken.None);

        Assert.Equal(gameId, game.Id);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/games", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains($"\"courtId\":\"{courtId}\"", handler.RequestBody);
    }

    [Fact]
    public async Task GetGameParticipantsAsync_RequestsTheGameParticipantList()
    {
        var gameId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""[{"nickname":"driss","fullName":"Driss Example"}]""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var participants = await api.GetGameParticipantsAsync(gameId, CancellationToken.None);

        var participant = Assert.Single(participants);
        Assert.Equal("driss", participant.Nickname);
        Assert.Equal("Driss Example", participant.FullName);
        Assert.Equal($"/games/{gameId}/participants", handler.Request!.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(
        HttpStatusCode.NotFound,
        "No se encontró el recurso",
        "No se encontró el partido solicitado.",
        "No se encontró el recurso solicitado.")]
    [InlineData(
        HttpStatusCode.Conflict,
        "Conflicto",
        "El partido está completo.",
        "No se pudo completar la acción")]
    public async Task GetGameAsync_WhenApiReturnsProblemDetails_ThrowsReadableException(
        HttpStatusCode statusCode,
        string title,
        string detail,
        string expectedMessage)
    {
        using var httpClient = CreateClient(
            statusCode,
            $$"""{"title":"{{title}}","detail":"{{detail}}","status":{{(int)statusCode}}}""",
            "application/problem+json");
        var api = new CourtConnectApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<CourtConnectApiException>(
            () => api.GetGameAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Contains(expectedMessage, exception.Message);
        Assert.Contains(detail, exception.Message);
    }

    [Fact]
    public async Task GetGameAsync_WhenApiReturnsBadRequestWithoutProblemDetails_ThrowsReadableException()
    {
        using var httpClient = CreateClient(HttpStatusCode.BadRequest, "invalid request");
        var api = new CourtConnectApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<CourtConnectApiException>(
            () => api.GetGameAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("La solicitud contiene datos no válidos", exception.Message);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task GetGamesAsync_WhenApiCannotBeReached_ThrowsReadableException()
    {
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(
            _ => throw new HttpRequestException("Connection refused.")))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var api = new CourtConnectApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<CourtConnectApiException>(
            () => api.GetGamesAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Contains("Comprueba que esté en ejecución", exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task RegisterAsync_PostsWithoutAcceptingAPlayerId()
    {
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent(
                $$"""{"id":"{{registrationId}}","gameId":"{{gameId}}","playerId":"{{playerId}}","createdAt":"2027-01-01T12:00:00Z","amount":10,"status":"Confirmed"}""")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var registration = await api.RegisterAsync(gameId, CancellationToken.None);

        Assert.Equal(registrationId, registration.Id);
        Assert.Equal(gameId, registration.GameId);
        Assert.Equal(playerId, registration.PlayerId);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/games/{gameId}/registrations", handler.Request.RequestUri!.AbsolutePath);
        Assert.Null(handler.Request.Content);
    }

    [Fact]
    public async Task CancelRegistrationAsync_PostsToTheCancellationEndpoint()
    {
        var registrationId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        await api.CancelRegistrationAsync(registrationId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/registrations/{registrationId}/cancel", handler.Request.RequestUri!.AbsolutePath);
        Assert.Null(handler.Request.Content);
    }

    [Fact]
    public async Task GetMyRegistrationsAsync_RequestsTheAuthenticatedPlayersRegistrations()
    {
        var registrationId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var courtId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                $$"""
            [
              {
                "registration": {
                  "id": "{{registrationId}}",
                  "gameId": "{{gameId}}",
                  "playerId": "{{Guid.NewGuid()}}",
                  "createdAt": "2027-01-01T12:00:00Z",
                  "amount": 10,
                  "status": "Confirmed"
                },
                "game": {
                  "id": "{{gameId}}",
                  "courtId": "{{courtId}}",
                  "courtName": "Pista Central",
                  "startAt": "2027-01-02T18:00:00Z",
                  "durationMinutes": 90,
                  "capacity": 4,
                  "availableSlots": 3,
                  "minimumAge": 16,
                  "pricePerPlayer": 10,
                  "status": "Open"
                }
              }
            ]
            """)
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new CourtConnectApiClient(httpClient);

        var registrations = await api.GetMyRegistrationsAsync(CancellationToken.None);

        var result = Assert.Single(registrations);
        Assert.Equal("/registrations/mine", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(registrationId, result.Registration.Id);
        Assert.Equal("Pista Central", result.Game.CourtName);
    }

    private static HttpClient CreateClient(
        HttpStatusCode statusCode,
        string body,
        string contentType = "application/json")
    {
        return new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = JsonContent(body, contentType)
        }))
        {
            BaseAddress = new Uri("http://localhost/")
        };
    }

    private static StringContent JsonContent(string body, string contentType = "application/json")
    {
        return new StringContent(body, Encoding.UTF8, contentType);
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }
}

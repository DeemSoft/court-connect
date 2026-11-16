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
    public async Task GetPlayersAsync_DeserializesTheSelectionOptions()
    {
        var playerId = Guid.NewGuid();
        using var httpClient = CreateClient(HttpStatusCode.OK, $$"""[{"id":"{{playerId}}","nickname":"base-player"}]""");
        var api = new CourtConnectApiClient(httpClient);

        var players = await api.GetPlayersAsync(CancellationToken.None);

        var player = Assert.Single(players);
        Assert.Equal(playerId, player.Id);
        Assert.Equal("base-player", player.Nickname);
    }

    [Theory]
    [InlineData(
        HttpStatusCode.NotFound,
        "Resource not found",
        "The requested game does not exist.",
        "No se encontró el recurso solicitado.")]
    [InlineData(
        HttpStatusCode.Conflict,
        "Conflict",
        "The game is full.",
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
    public async Task RegisterAsync_PostsTheSelectedPlayerAndDeserializesTheRegistration()
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

        var registration = await api.RegisterAsync(gameId, playerId, CancellationToken.None);

        Assert.Equal(registrationId, registration.Id);
        Assert.Equal(gameId, registration.GameId);
        Assert.Equal(playerId, registration.PlayerId);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal($"/games/{gameId}/registrations", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains($"\"playerId\":\"{playerId}\"", handler.RequestBody);
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

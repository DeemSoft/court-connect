using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeemSoft.CourtConnect.Domain;
using DeemSoft.CourtConnect.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeemSoft.CourtConnect.IntegrationTests;

[Collection(SqlServerApiCollection.Name)]
public sealed class CourtConnectApiTests(SqlServerApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetGames_ReturnsOnlyOpenUpcomingGamesWithAvailableSlots()
    {
        var court = await CreateCourtAsync();
        var player = await CreatePlayerAsync();
        // Los tres casos comprueban que el filtro usa estado y fecha, y que las plazas cuentan solo inscripciones activas.
        var upcoming = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), GameStatus.Open, 3, [player]);
        var closed = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(11), GameStatus.Closed, 3);
        var past = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(-1), GameStatus.Open, 3);
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/games");
        var games = await response.Content.ReadFromJsonAsync<List<GameResponse>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(games);
        var result = Assert.Single(games, game => game.Id == upcoming.Id);
        Assert.Equal(2, result.AvailableSlots);
        Assert.DoesNotContain(games, game => game.Id == closed.Id);
        Assert.DoesNotContain(games, game => game.Id == past.Id);
    }

    [Fact]
    public async Task GetGame_WhenItExists_ReturnsDetailsAsDto()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10));
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/games/{game.Id}");
        var result = await response.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal(court.Name, result.CourtName);
    }

    [Fact]
    public async Task GetGame_WhenItDoesNotExist_Returns404()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Theory]
    [InlineData("/courts", "{\"name\":\"\",\"location\":\"Barcelona\"}")]
    [InlineData("/players", "{\"nickname\":\"new-player\",\"fullName\":\"Test Player\",\"dateOfBirth\":\"2000-01-01\",\"email\":\"not-an-email\"}")]
    [InlineData("/games", "{\"courtId\":\"00000000-0000-0000-0000-000000000001\",\"startAt\":\"2027-01-01T18:00:00Z\",\"durationMinutes\":90,\"capacity\":1,\"minimumAge\":16,\"pricePerPlayer\":10}")]
    public async Task Post_WhenRequestIsInvalid_Returns400ProblemDetails(string path, string body)
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync(path, JsonContent(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task CreateCourt_PersistsAndReturns201()
    {
        using var client = fixture.Factory.CreateClient();
        var request = new { Name = UniqueName("court"), Location = "Barcelona" };

        var response = await client.PostAsJsonAsync("/courts", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<CourtResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        Assert.True(await dbContext.Courts.AnyAsync(court => court.Id == result.Id));
    }

    [Fact]
    public async Task CreateGame_WithExistingCourt_PersistsAndReturns201()
    {
        var court = await CreateCourtAsync();
        using var client = fixture.Factory.CreateClient();
        var request = new
        {
            CourtId = court.Id,
            StartAt = DateTimeOffset.UtcNow.AddDays(10),
            DurationMinutes = 90,
            Capacity = 4,
            MinimumAge = 16,
            PricePerPlayer = 10m
        };

        var response = await client.PostAsJsonAsync("/games", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(court.Id, result.CourtId);
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        Assert.True(await dbContext.Games.AnyAsync(game => game.Id == result.Id));
    }

    [Fact]
    public async Task CreateGame_WhenCourtDoesNotExist_Returns404()
    {
        using var client = fixture.Factory.CreateClient();
        var request = new
        {
            CourtId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddDays(10),
            DurationMinutes = 90,
            Capacity = 4,
            MinimumAge = 16,
            PricePerPlayer = 10m
        };

        var response = await client.PostAsJsonAsync("/games", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task CreatePlayer_PersistsAndReturns201()
    {
        using var client = fixture.Factory.CreateClient();
        var nickname = UniqueName("player");
        var request = new
        {
            Nickname = nickname,
            FullName = "Integration Test Player",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Email = $"{nickname}@example.com"
        };

        var response = await client.PostAsJsonAsync("/players", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<PlayerResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(nickname, result.Nickname);
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        Assert.True(await dbContext.Players.AnyAsync(player => player.Id == result.Id));
    }

    [Fact]
    public async Task CreatePlayer_WhenNicknameAlreadyExists_Returns409()
    {
        var player = await CreatePlayerAsync();
        using var client = fixture.Factory.CreateClient();
        var request = new
        {
            player.Nickname,
            FullName = "Duplicate Player",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Email = $"{UniqueName("duplicate")}@example.com"
        };

        var response = await client.PostAsJsonAsync("/players", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task GetPlayer_WhenItExists_ReturnsDetails()
    {
        var player = await CreatePlayerAsync();
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/players/{player.Id}");
        var result = await response.Content.ReadFromJsonAsync<PlayerResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(player.Id, result.Id);
        Assert.Equal(player.Nickname, result.Nickname);
    }

    [Fact]
    public async Task GetPlayer_WhenItDoesNotExist_Returns404()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/players/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task RegisterPlayer_PersistsAndReturns201()
    {
        var court = await CreateCourtAsync();
        var player = await CreatePlayerAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), capacity: 4);
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/games/{game.Id}/registrations",
            new { PlayerId = player.Id },
            JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal(player.Id, result.PlayerId);
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        Assert.True(await dbContext.Registrations.AnyAsync(registration => registration.Id == result.Id));
    }

    [Theory]
    [InlineData(RegistrationFailure.GameFull)]
    [InlineData(RegistrationFailure.DuplicatePlayer)]
    [InlineData(RegistrationFailure.AgeTooLow)]
    [InlineData(RegistrationFailure.GameNotOpen)]
    [InlineData(RegistrationFailure.GameStarted)]
    public async Task RegisterPlayer_WhenDomainRuleFails_Returns409(RegistrationFailure failure)
    {
        var court = await CreateCourtAsync();
        var player = await CreatePlayerAsync();
        var gameStart = failure == RegistrationFailure.GameStarted
            ? DateTimeOffset.UtcNow.AddMinutes(-1)
            : DateTimeOffset.UtcNow.AddDays(10);
        var minimumAge = failure == RegistrationFailure.AgeTooLow ? 18 : 16;
        var status = failure == RegistrationFailure.GameNotOpen ? GameStatus.Closed : GameStatus.Open;
        var capacity = failure == RegistrationFailure.GameFull ? 2 : 4;
        // Preparar el partido según cada regla deja la misma ruta HTTP a prueba en todos los casos.
        IReadOnlyList<Player> existingPlayers = failure switch
        {
            RegistrationFailure.GameFull => new[] { player, await CreatePlayerAsync() },
            RegistrationFailure.DuplicatePlayer => new[] { player },
            _ => Array.Empty<Player>()
        };
        var game = await CreateGameAsync(court, gameStart, status, capacity, existingPlayers, minimumAge);
        if (failure == RegistrationFailure.AgeTooLow)
        {
            player = await CreatePlayerAsync(new DateOnly(2010, 1, 1));
        }

        using var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/games/{game.Id}/registrations",
            new { PlayerId = player.Id },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task RegisterPlayer_WhenGameDoesNotExist_Returns404()
    {
        var player = await CreatePlayerAsync();
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/games/{Guid.NewGuid()}/registrations",
            new { PlayerId = player.Id },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task RegisterPlayer_WhenPlayerDoesNotExist_Returns404()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10));
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/games/{game.Id}/registrations",
            new { PlayerId = Guid.NewGuid() },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task GetRegistrations_ReturnsConfirmedRegistrationsAndUnknownGameReturns404()
    {
        var court = await CreateCourtAsync();
        var player = await CreatePlayerAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), capacity: 4, players: [player]);
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/games/{game.Id}/registrations");
        var registrations = await response.Content.ReadFromJsonAsync<List<RegistrationResponse>>(JsonOptions);
        var missingGameResponse = await client.GetAsync($"/games/{Guid.NewGuid()}/registrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(registrations);
        var result = Assert.Single(registrations);
        Assert.Equal(player.Id, result.PlayerId);
        Assert.Equal(HttpStatusCode.NotFound, missingGameResponse.StatusCode);
    }

    [Fact]
    public async Task CancelRegistration_ReleasesSlotAndSecondCancellationReturns409()
    {
        var court = await CreateCourtAsync();
        var player = await CreatePlayerAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), capacity: 2);
        using var client = fixture.Factory.CreateClient();
        var registrationResponse = await client.PostAsJsonAsync(
            $"/games/{game.Id}/registrations",
            new { PlayerId = player.Id },
            JsonOptions);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        Assert.NotNull(registration);
        var cancelResponse = await client.PostAsync($"/registrations/{registration.Id}/cancel", content: null);
        var gameResponse = await client.GetAsync($"/games/{game.Id}");
        var gameResult = await gameResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);
        var secondCancelResponse = await client.PostAsync($"/registrations/{registration.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);
        Assert.NotNull(gameResult);
        Assert.Equal(2, gameResult.AvailableSlots);
        Assert.Equal(HttpStatusCode.Conflict, secondCancelResponse.StatusCode);
        await AssertProblemDetailsAsync(secondCancelResponse);
    }

    [Fact]
    public async Task CancelRegistration_WhenRegistrationDoesNotExist_Returns404()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync($"/registrations/{Guid.NewGuid()}/cancel", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    private async Task<Court> CreateCourtAsync()
    {
        var court = new Court(UniqueName("court"), "Barcelona");
        // Cada helper usa un ámbito EF independiente para no reutilizar entidades rastreadas entre preparaciones.
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        dbContext.Courts.Add(court);
        await dbContext.SaveChangesAsync();
        return court;
    }

    private async Task<Player> CreatePlayerAsync(DateOnly? dateOfBirth = null)
    {
        var nickname = UniqueName("player");
        var player = new Player(
            nickname,
            "Integration Test Player",
            dateOfBirth ?? new DateOnly(1990, 1, 1),
            $"{nickname}@example.com",
            DateOnly.FromDateTime(DateTime.UtcNow));
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        dbContext.Players.Add(player);
        await dbContext.SaveChangesAsync();
        return player;
    }

    private async Task<Game> CreateGameAsync(
        Court court,
        DateTimeOffset startAt,
        GameStatus status = GameStatus.Open,
        int capacity = 4,
        IReadOnlyList<Player>? players = null,
        int minimumAge = 16)
    {
        var game = new Game(court, startAt, TimeSpan.FromHours(1), capacity, minimumAge, 10m, status);
        // Se crean las inscripciones por el dominio para respetar sus reglas antes de guardar el agregado.
        foreach (var player in players ?? [])
        {
            game.Register(player, DateTimeOffset.UtcNow);
        }

        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        dbContext.Attach(court);
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();
        return game;
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    private static string UniqueName(string prefix)
    {
        // La base se comparte durante la colección, por eso cada prueba necesita datos con claves únicas.
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response)
    {
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(document.RootElement.TryGetProperty("status", out var status));
        Assert.Equal((int)response.StatusCode, status.GetInt32());
    }

    public enum RegistrationFailure
    {
        GameFull,
        DuplicatePlayer,
        AgeTooLow,
        GameNotOpen,
        GameStarted
    }

    private sealed record CourtResponse(Guid Id, string Name, string Location);

    private sealed record GameResponse(
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

    private sealed record PlayerResponse(Guid Id, string Nickname, string FullName, DateOnly DateOfBirth, string Email);

    private sealed record RegistrationResponse(
        Guid Id,
        Guid GameId,
        Guid PlayerId,
        DateTimeOffset CreatedAt,
        decimal Amount,
        string Status);
}

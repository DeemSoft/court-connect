using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeemSoft.CourtConnect.Domain;
using DeemSoft.CourtConnect.Infrastructure;
using DeemSoft.CourtConnect.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
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

    [Fact]
    public async Task Authorization_LeavesGameReadsPublicAndProtectsOtherOperations()
    {
        using var anonymousClient = fixture.Factory.CreateClient();

        var publicGames = await anonymousClient.GetAsync("/games");
        var protectedCreate = await anonymousClient.PostAsync(
            "/courts",
            JsonContent("{\"name\":\"New court\",\"location\":\"Barcelona\"}"));
        var protectedCourtList = await anonymousClient.GetAsync("/courts");
        var protectedCourtUpdate = await anonymousClient.PutAsync(
            $"/courts/{Guid.NewGuid()}",
            JsonContent("{\"name\":\"Updated court\",\"location\":\"Barcelona\"}"));
        var protectedCourtDelete = await anonymousClient.DeleteAsync($"/courts/{Guid.NewGuid()}");
        var protectedOwnedGames = await anonymousClient.GetAsync("/games/mine");

        Assert.Equal(HttpStatusCode.OK, publicGames.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedCourtList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedCourtUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedCourtDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedOwnedGames.StatusCode);

        var playerIdentity = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var playerClient = playerIdentity.Client;
        var forbiddenCreate = await playerClient.PostAsync(
            "/courts",
            JsonContent("{\"name\":\"New court\",\"location\":\"Barcelona\"}"));
        var forbiddenCourtList = await playerClient.GetAsync("/courts");
        var forbiddenCourtUpdate = await playerClient.PutAsync(
            $"/courts/{Guid.NewGuid()}",
            JsonContent("{\"name\":\"Updated court\",\"location\":\"Barcelona\"}"));
        var forbiddenCourtDelete = await playerClient.DeleteAsync($"/courts/{Guid.NewGuid()}");
        var forbiddenOwnedGames = await playerClient.GetAsync("/games/mine");

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCourtList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCourtUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCourtDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenOwnedGames.StatusCode);
    }

    [Fact]
    public async Task CurrentAccount_ReturnsTheAuthenticatedUsersRoles()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;

        var response = await client.GetAsync("/auth/me");
        var account = await response.Content.ReadFromJsonAsync<CurrentAccountResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(account);
        Assert.Contains(CourtConnectRoles.Organizer, account.Roles);
    }

    [Fact]
    public async Task OrganizerQueries_ReturnCourtsAndOnlyGamesOwnedByTheOrganizer()
    {
        var court = await CreateCourtAsync();
        var owner = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        var otherOrganizer = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        var ownedGame = await CreateGameAsync(
            court,
            DateTimeOffset.UtcNow.AddDays(10),
            organizerUserId: owner.UserId);
        await CreateGameAsync(
            court,
            DateTimeOffset.UtcNow.AddDays(11),
            organizerUserId: otherOrganizer.UserId);
        using var client = owner.Client;

        var gamesResponse = await client.GetAsync("/games/mine");
        var games = await gamesResponse.Content.ReadFromJsonAsync<List<GameResponse>>(JsonOptions);
        var courtsResponse = await client.GetAsync("/courts");
        var courts = await courtsResponse.Content.ReadFromJsonAsync<List<CourtResponse>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, gamesResponse.StatusCode);
        Assert.NotNull(games);
        var result = Assert.Single(games);
        Assert.Equal(ownedGame.Id, result.Id);
        Assert.Equal(HttpStatusCode.OK, courtsResponse.StatusCode);
        Assert.NotNull(courts);
        Assert.Contains(courts, item => item.Id == court.Id);
    }

    [Fact]
    public async Task OrganizerCanUpdateAndDeleteCourtWithoutGames()
    {
        var court = await CreateCourtAsync();
        var organizer = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = organizer.Client;

        var updateResponse = await client.PutAsJsonAsync(
            $"/courts/{court.Id}",
            new { Name = "Updated court", Location = "Badalona" },
            JsonOptions);
        var updatedCourt = await updateResponse.Content.ReadFromJsonAsync<CourtResponse>(JsonOptions);
        var deleteResponse = await client.DeleteAsync($"/courts/{court.Id}");

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedCourt);
        Assert.Equal("Updated court", updatedCourt.Name);
        Assert.Equal("Badalona", updatedCourt.Location);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        Assert.False(await dbContext.Courts.AnyAsync(item => item.Id == court.Id));
    }

    [Fact]
    public async Task DeleteCourt_WhenCourtHasGames_ReturnsConflictAndKeepsCourt()
    {
        var court = await CreateCourtAsync();
        await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10));
        var organizer = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = organizer.Client;

        var response = await client.DeleteAsync($"/courts/{court.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemDetailsAsync(response);

        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        Assert.True(await dbContext.Courts.AnyAsync(item => item.Id == court.Id));
    }

    [Fact]
    public async Task UpdateOrDeleteCourt_WhenCourtDoesNotExist_ReturnsNotFound()
    {
        var organizer = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = organizer.Client;
        var courtId = Guid.NewGuid();

        var updateResponse = await client.PutAsJsonAsync(
            $"/courts/{courtId}",
            new { Name = "Updated court", Location = "Badalona" },
            JsonOptions);
        var deleteResponse = await client.DeleteAsync($"/courts/{courtId}");

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GameParticipants_RequireTheOwningOrganizerAndReturnOnlyPlayerNames()
    {
        var court = await CreateCourtAsync();
        var owner = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        var otherOrganizer = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        var player = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        var cancelledPlayer = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        var game = await CreateGameAsync(
            court,
            DateTimeOffset.UtcNow.AddDays(10),
            organizerUserId: owner.UserId);
        using var ownerClient = owner.Client;
        using var otherClient = otherOrganizer.Client;
        using var playerClient = player.Client;
        using var cancelledPlayerClient = cancelledPlayer.Client;
        using var anonymousClient = fixture.Factory.CreateClient();
        var registrationResponse = await playerClient.PostAsync(
            $"/games/{game.Id}/registrations",
            content: null);
        var cancelledRegistrationResponse = await cancelledPlayerClient.PostAsync(
            $"/games/{game.Id}/registrations",
            content: null);
        var cancelledRegistration = await cancelledRegistrationResponse.Content
            .ReadFromJsonAsync<RegistrationResponse>(JsonOptions);
        Assert.NotNull(cancelledRegistration);
        var cancellationResponse = await cancelledPlayerClient.PostAsync(
            $"/registrations/{cancelledRegistration.Id}/cancel",
            content: null);
        var anonymousResponse = await anonymousClient.GetAsync($"/games/{game.Id}/participants");
        var playerResponse = await playerClient.GetAsync($"/games/{game.Id}/participants");
        var ownerResponse = await ownerClient.GetAsync($"/games/{game.Id}/participants");
        var otherResponse = await otherClient.GetAsync($"/games/{game.Id}/participants");
        var missingGameResponse = await ownerClient.GetAsync($"/games/{Guid.NewGuid()}/participants");
        var responseBody = await ownerResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, cancelledRegistrationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancellationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, playerResponse.StatusCode);
        Assert.True(ownerResponse.IsSuccessStatusCode, responseBody);
        Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingGameResponse.StatusCode);
        var participants = JsonSerializer.Deserialize<List<GameParticipantResponse>>(responseBody, JsonOptions);
        var participant = Assert.Single(participants!);
        Assert.Equal(player.Player!.Nickname, participant.Nickname);
        Assert.Equal(player.Player.FullName, participant.FullName);
        Assert.DoesNotContain(cancelledPlayer.Player!.Nickname, responseBody);
        Assert.DoesNotContain("email", responseBody.ToLowerInvariant());
        Assert.DoesNotContain("playerid", responseBody.ToLowerInvariant());
        Assert.DoesNotContain("dateofbirth", responseBody.ToLowerInvariant());
    }

    [Fact]
    public async Task Post_WhenRequestIsInvalid_Returns400ProblemDetails()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;

        var response = await client.PostAsync("/courts", JsonContent("{\"name\":\"\",\"location\":\"Barcelona\"}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task CreateCourt_PersistsAndReturns201()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;
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
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;
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
        var savedGame = await dbContext.Games.SingleAsync(game => game.Id == result.Id);
        Assert.Equal(identity.UserId, savedGame.OrganizerUserId);
    }

    [Fact]
    public async Task CreateGame_WhenCourtDoesNotExist_Returns404()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;
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
    public async Task GameManagement_OnlyTheOrganizerCanEditAndChangeStatus()
    {
        var court = await CreateCourtAsync();
        var owner = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        var otherOrganizer = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var ownerClient = owner.Client;
        using var otherClient = otherOrganizer.Client;
        var createResponse = await ownerClient.PostAsJsonAsync(
            "/games",
            new
            {
                CourtId = court.Id,
                StartAt = DateTimeOffset.UtcNow.AddDays(10),
                DurationMinutes = 90,
                Capacity = 4,
                MinimumAge = 16,
                PricePerPlayer = 10m
            },
            JsonOptions);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdGame);

        var updateRequest = new
        {
            StartAt = DateTimeOffset.UtcNow.AddDays(12),
            DurationMinutes = 120,
            Capacity = 6,
            MinimumAge = 18,
            PricePerPlayer = 12m
        };
        var otherUpdate = await otherClient.PutAsJsonAsync(
            $"/games/{createdGame.Id}",
            updateRequest,
            JsonOptions);
        var ownerUpdate = await ownerClient.PutAsJsonAsync(
            $"/games/{createdGame.Id}",
            updateRequest,
            JsonOptions);
        var otherStatus = await otherClient.PutAsJsonAsync(
            $"/games/{createdGame.Id}/status",
            new { Status = "Closed" },
            JsonOptions);
        var close = await ownerClient.PutAsJsonAsync(
            $"/games/{createdGame.Id}/status",
            new { Status = "Closed" },
            JsonOptions);
        var reopen = await ownerClient.PutAsJsonAsync(
            $"/games/{createdGame.Id}/status",
            new { Status = "Open" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, otherUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherStatus.StatusCode);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        var savedGame = await dbContext.Games.SingleAsync(game => game.Id == createdGame.Id);
        Assert.Equal(owner.UserId, savedGame.OrganizerUserId);
        Assert.Equal(GameStatus.Open, savedGame.Status);
    }

    [Fact]
    public async Task CreatePlayerProfile_LinksTheAuthenticatedAccountAndAssignsPlayerRole()
    {
        var identity = await CreateIdentityClientAsync();
        using var client = identity.Client;
        var nickname = UniqueName("player");
        var request = new
        {
            Nickname = nickname,
            FullName = "Integration Test Player",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };

        var response = await client.PostAsJsonAsync("/players/me", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<PlayerResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(nickname, result.Nickname);
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        var savedPlayer = await dbContext.Players.SingleAsync(player => player.Id == result.Id);
        Assert.Equal(identity.UserId, savedPlayer.IdentityUserId);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<CourtConnectUser>>();
        var user = await userManager.FindByIdAsync(identity.UserId.ToString());
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, CourtConnectRoles.Player));
    }

    [Fact]
    public async Task PlayerProfile_RefreshesTheTokenSoTheNewPlayerRoleCanBeUsed()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10));
        var identity = await CreateIdentityClientAsync(createProfile: true);
        using var client = identity.Client;

        var staleTokenResponse = await client.PostAsync(
            $"/games/{game.Id}/registrations",
            content: null);
        var refreshResponse = await client.PostAsJsonAsync(
            "/auth/refresh",
            new { RefreshToken = identity.RefreshToken },
            JsonOptions);
        var refreshedTokens = await refreshResponse.Content.ReadFromJsonAsync<IdentityTokenResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, staleTokenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshedTokens);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(refreshedTokens.TokenType, refreshedTokens.AccessToken);

        var registrationResponse = await client.PostAsync(
            $"/games/{game.Id}/registrations",
            content: null);

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
    }

    [Fact]
    public async Task GetPlayer_WhenItExists_ReturnsDetails()
    {
        var player = await CreatePlayerAsync();
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;

        var response = await client.GetAsync($"/players/{player.Id}");
        var result = await response.Content.ReadFromJsonAsync<PlayerResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(player.Id, result.Id);
        Assert.Equal(player.Nickname, result.Nickname);
    }

    [Fact]
    public async Task GetPlayers_ReturnsOnlySelectionFields()
    {
        var player = await CreatePlayerAsync();
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;

        var response = await client.GetAsync("/players");
        var body = await response.Content.ReadAsStringAsync();
        var players = JsonSerializer.Deserialize<List<PlayerOptionResponse>>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(players);
        var result = Assert.Single(players, item => item.Id == player.Id);
        Assert.Equal(player.Nickname, result.Nickname);
        using var document = JsonDocument.Parse(body);
        var playerJson = Assert.Single(
            document.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == player.Id);
        Assert.Equal(["id", "nickname"], playerJson.EnumerateObject().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task GetPlayer_WhenItDoesNotExist_Returns404()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;

        var response = await client.GetAsync($"/players/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task RegisterPlayer_PersistsAndReturns201()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), capacity: 4);
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var client = identity.Client;

        var response = await client.PostAsync($"/games/{game.Id}/registrations", content: null);
        var result = await response.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal(identity.Player!.Id, result.PlayerId);
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
        var identity = await CreateIdentityClientAsync(
            CourtConnectRoles.Player,
            createProfile: true,
            dateOfBirth: failure == RegistrationFailure.AgeTooLow
                ? new DateOnly(2009, 1, 1)
                : new DateOnly(1990, 1, 1));
        using var client = identity.Client;
        var player = identity.Player!;
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

        var response = await client.PostAsync($"/games/{game.Id}/registrations", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task RegisterPlayer_WhenGameDoesNotExist_Returns404()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var client = identity.Client;

        var response = await client.PostAsync($"/games/{Guid.NewGuid()}/registrations", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task RegisterPlayer_WhenPlayerDoesNotExist_Returns404()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10));
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Player);
        using var client = identity.Client;

        var response = await client.PostAsync($"/games/{game.Id}/registrations", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task GetRegistrations_ReturnsConfirmedRegistrationsAndUnknownGameReturns404()
    {
        var court = await CreateCourtAsync();
        var player = await CreatePlayerAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), capacity: 4, players: [player]);
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var client = identity.Client;

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
    public async Task GetMyRegistrations_ReturnsOnlyTheAuthenticatedPlayersRegistrations()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), capacity: 4);
        var firstPlayer = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        var secondPlayer = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var firstClient = firstPlayer.Client;
        using var secondClient = secondPlayer.Client;
        var firstCreation = await firstClient.PostAsync($"/games/{game.Id}/registrations", content: null);
        var firstRegistration = await firstCreation.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);
        var secondCreation = await secondClient.PostAsync($"/games/{game.Id}/registrations", content: null);
        var secondRegistration = await secondCreation.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);

        var firstResponse = await firstClient.GetAsync("/registrations/mine");
        var firstResults = await firstResponse.Content.ReadFromJsonAsync<List<PlayerRegistrationWithGameResponse>>(JsonOptions);
        var secondResponse = await secondClient.GetAsync("/registrations/mine");
        var secondResults = await secondResponse.Content.ReadFromJsonAsync<List<PlayerRegistrationWithGameResponse>>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, firstCreation.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondCreation.StatusCode);
        Assert.NotNull(firstRegistration);
        Assert.NotNull(secondRegistration);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var firstResult = Assert.Single(firstResults!);
        var secondResult = Assert.Single(secondResults!);
        Assert.Equal(firstRegistration.Id, firstResult.Registration.Id);
        Assert.Equal(secondRegistration.Id, secondResult.Registration.Id);
        Assert.Equal(game.Id, firstResult.Game.Id);
    }

    [Fact]
    public async Task GetMyRegistrations_RequiresThePlayerRole()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Organizer);
        using var client = identity.Client;

        var response = await client.GetAsync("/registrations/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelRegistration_ReleasesSlotAndSecondCancellationReturns409()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10), capacity: 2);
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var client = identity.Client;
        var registrationResponse = await client.PostAsync(
            $"/games/{game.Id}/registrations",
            content: null);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        Assert.NotNull(registration);
        var cancelResponse = await client.PostAsync($"/registrations/{registration.Id}/cancel", content: null);
        var myRegistrationsResponse = await client.GetAsync("/registrations/mine");
        var myRegistrations = await myRegistrationsResponse.Content.ReadFromJsonAsync<List<PlayerRegistrationWithGameResponse>>(JsonOptions);
        var gameResponse = await client.GetAsync($"/games/{game.Id}");
        var gameResult = await gameResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);
        var secondCancelResponse = await client.PostAsync($"/registrations/{registration.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, myRegistrationsResponse.StatusCode);
        Assert.Empty(myRegistrations!);
        Assert.NotNull(gameResult);
        Assert.Equal(2, gameResult.AvailableSlots);
        Assert.Equal(HttpStatusCode.Conflict, secondCancelResponse.StatusCode);
        await AssertProblemDetailsAsync(secondCancelResponse);
    }

    [Fact]
    public async Task CancelRegistration_WhenFewerThan72HoursRemain_Returns409AndKeepsRegistrationActive()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddHours(71));
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var client = identity.Client;
        var registrationResponse = await client.PostAsync($"/games/{game.Id}/registrations", content: null);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        Assert.NotNull(registration);
        var cancelResponse = await client.PostAsync($"/registrations/{registration.Id}/cancel", content: null);
        var myRegistrationsResponse = await client.GetAsync("/registrations/mine");
        var myRegistrations = await myRegistrationsResponse.Content
            .ReadFromJsonAsync<List<PlayerRegistrationWithGameResponse>>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
        await AssertProblemDetailsAsync(cancelResponse);
        Assert.NotNull(myRegistrations);
        Assert.Contains(myRegistrations, item => item.Registration.Id == registration.Id);
    }

    [Fact]
    public async Task CancelRegistration_WhenRegistrationDoesNotExist_Returns404()
    {
        var identity = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var client = identity.Client;

        var response = await client.PostAsync($"/registrations/{Guid.NewGuid()}/cancel", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response);
    }

    [Fact]
    public async Task CancelRegistration_WhenOwnedByAnotherPlayer_Returns404AndKeepsRegistrationActive()
    {
        var court = await CreateCourtAsync();
        var game = await CreateGameAsync(court, DateTimeOffset.UtcNow.AddDays(10));
        var firstPlayer = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        var otherPlayer = await CreateIdentityClientAsync(CourtConnectRoles.Player, createProfile: true);
        using var firstClient = firstPlayer.Client;
        using var otherClient = otherPlayer.Client;
        var createResponse = await firstClient.PostAsync($"/games/{game.Id}/registrations", content: null);
        var registration = await createResponse.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(registration);

        var otherCancellation = await otherClient.PostAsync(
            $"/registrations/{registration.Id}/cancel",
            content: null);
        using var publicClient = fixture.Factory.CreateClient();
        var gameResponse = await publicClient.GetAsync($"/games/{game.Id}");
        var gameResult = await gameResponse.Content.ReadFromJsonAsync<GameResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, otherCancellation.StatusCode);
        Assert.NotNull(gameResult);
        Assert.Equal(game.Capacity - 1, gameResult.AvailableSlots);
        await AssertProblemDetailsAsync(otherCancellation);
    }

    private async Task<TestIdentity> CreateIdentityClientAsync(
        string? role = null,
        bool createProfile = false,
        DateOnly? dateOfBirth = null)
    {
        var client = fixture.Factory.CreateClient();
        var email = $"{UniqueName("account")}@example.com";
        const string password = "Strong!Password123";
        var registerResponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { Email = email, Password = password },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        Guid userId;
        await using (var scope = fixture.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<CourtConnectUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            userId = user.Id;

            if (role is not null)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var createRoleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                    Assert.True(createRoleResult.Succeeded);
                }

                var addRoleResult = await userManager.AddToRoleAsync(user, role);
                Assert.True(addRoleResult.Succeeded);
            }
        }

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login?useCookies=false",
            new { Email = email, Password = password },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<IdentityTokenResponse>(JsonOptions);
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);

        PlayerResponse? profile = null;
        if (createProfile)
        {
            var profileResponse = await client.PostAsJsonAsync(
                "/players/me",
                new
                {
                    Nickname = UniqueName("player"),
                    FullName = "Integration Test Player",
                    DateOfBirth = dateOfBirth ?? new DateOnly(1990, 1, 1)
                },
                JsonOptions);
            Assert.Equal(HttpStatusCode.Created, profileResponse.StatusCode);
            profile = await profileResponse.Content.ReadFromJsonAsync<PlayerResponse>(JsonOptions);
            Assert.NotNull(profile);
        }

        Player? player = null;
        if (profile is not null)
        {
            await using var scope = fixture.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
            player = await dbContext.Players.SingleAsync(item => item.Id == profile.Id);
        }

        return new TestIdentity(client, userId, player, tokens.RefreshToken);
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
        int minimumAge = 16,
        Guid? organizerUserId = null)
    {
        var game = new Game(
            court,
            startAt,
            TimeSpan.FromHours(1),
            capacity,
            minimumAge,
            10m,
            status,
            organizerUserId);
        // Se crean las inscripciones por el dominio para respetar sus reglas antes de guardar el agregado.
        foreach (var player in players ?? [])
        {
            game.Register(player, DateTimeOffset.UtcNow);
        }

        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        foreach (var player in players ?? [])
        {
            dbContext.Attach(player);
        }

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

    private sealed record PlayerOptionResponse(Guid Id, string Nickname);

    private sealed record GameParticipantResponse(string Nickname, string FullName);

    private sealed record RegistrationResponse(
        Guid Id,
        Guid GameId,
        Guid PlayerId,
        DateTimeOffset CreatedAt,
        decimal Amount,
        string Status);

    private sealed record PlayerRegistrationWithGameResponse(RegistrationResponse Registration, GameResponse Game);

    private sealed record IdentityTokenResponse(
        string TokenType,
        string AccessToken,
        long ExpiresIn,
        string RefreshToken);

    private sealed record CurrentAccountResponse(string? Email, List<string> Roles);

    private sealed record TestIdentity(HttpClient Client, Guid UserId, Player? Player, string RefreshToken);
}

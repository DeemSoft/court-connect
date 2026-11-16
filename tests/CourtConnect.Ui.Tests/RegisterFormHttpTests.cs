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
    public async Task AccountRegistration_CompletesProfileAndLogoutEndsTheSession()
    {
        var gameId = Guid.NewGuid();
        var api = new FakeCourtConnectApiClient(gameId);
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

        using var registerResponse = await client.GetAsync("/register");
        var registerHtml = await registerResponse.Content.ReadAsStringAsync();
        using var registerContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "RegisterAccount"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiforgeryToken(registerHtml)),
            new KeyValuePair<string, string>("FormModel.Email", "new-player@example.com"),
            new KeyValuePair<string, string>("FormModel.Password", "Strong-password-123!"),
            new KeyValuePair<string, string>("FormModel.ConfirmPassword", "Strong-password-123!")
        ]);
        using var registerPost = await client.PostAsync("/register", registerContent);
        using var profileResponse = await client.GetAsync("/profile");
        var profileHtml = WebUtility.HtmlDecode(await profileResponse.Content.ReadAsStringAsync());

        Assert.True(registerPost.IsSuccessStatusCode);
        Assert.True(api.AccountRegistered);
        Assert.True(api.LoginCalled);
        Assert.Contains("Mi perfil", profileHtml);
        Assert.Contains("Datos de la cuenta", profileHtml);
        Assert.Contains("new-player@example.com", profileHtml);
        Assert.Contains("Todavía no tienes un perfil de jugador", profileHtml);
        var playerNavigation = Regex.Match(profileHtml, "<nav class=\"site-nav\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.Contains("href=\"/logout\"", playerNavigation);
        Assert.Contains("href=\"/inscripciones\"", playerNavigation);
        Assert.Contains("Inscripciones", playerNavigation);

        using var profileContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "PlayerProfile"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiforgeryToken(profileHtml)),
            new KeyValuePair<string, string>("FormModel.Nickname", "new-player"),
            new KeyValuePair<string, string>("FormModel.FullName", "New Player"),
            new KeyValuePair<string, string>("FormModel.DateOfBirth", "1990-01-01")
        ]);
        using var profilePost = await client.PostAsync("/profile", profileContent);
        var completedProfileHtml = WebUtility.HtmlDecode(await profilePost.Content.ReadAsStringAsync());

        Assert.True(profilePost.IsSuccessStatusCode);
        Assert.True(api.ProfileCreated);
        Assert.True(api.TokenRefreshed);
        Assert.Contains("Tu perfil de jugador ya está completo", completedProfileHtml);
        Assert.Contains("Datos de jugador", completedProfileHtml);
        Assert.Contains("new-player", completedProfileHtml);
        Assert.Contains("New Player", completedProfileHtml);
        Assert.Contains("01/01/1990", completedProfileHtml);
        Assert.Contains("new-player@example.com", completedProfileHtml);
        Assert.Contains("Jugador", completedProfileHtml);

        using var logoutResponse = await client.GetAsync("/logout");
        var logoutHtml = await logoutResponse.Content.ReadAsStringAsync();
        using var logoutContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "Logout"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiforgeryToken(logoutHtml))
        ]);
        using var logoutPost = await client.PostAsync("/logout", logoutContent);
        using var protectedPage = await client.GetAsync($"/games/{gameId}/register");
        var protectedPageHtml = WebUtility.HtmlDecode(await protectedPage.Content.ReadAsStringAsync());

        Assert.True(logoutPost.IsSuccessStatusCode);
        Assert.Contains("Iniciar sesión", protectedPageHtml);
        Assert.DoesNotContain("Confirmar inscripción", protectedPageHtml);
    }

    [Fact]
    public async Task PlayerCanCancelAnActiveRegistrationFromTheRegistrationsPage()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDto(
            gameId,
            Guid.NewGuid(),
            "Pista Central",
            DateTimeOffset.UtcNow.AddDays(5),
            90,
            4,
            3,
            16,
            10m,
            "Open");
        var registration = new RegistrationDto(
            Guid.NewGuid(),
            gameId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            10m,
            "Confirmed");
        var api = new FakeCourtConnectApiClient(gameId)
        {
            MyRegistrations = [new PlayerRegistrationDto(registration, game)]
        };
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

        using var loginResponse = await client.GetAsync("/login");
        var loginHtml = await loginResponse.Content.ReadAsStringAsync();
        using var loginContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "Login"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiforgeryToken(loginHtml)),
            new KeyValuePair<string, string>("FormModel.Email", "player@example.com"),
            new KeyValuePair<string, string>("FormModel.Password", "Strong-password-123!")
        ]);
        using var loginPost = await client.PostAsync("/login", loginContent);
        Assert.True(loginPost.IsSuccessStatusCode || loginPost.StatusCode == HttpStatusCode.Redirect);

        using var registrationsResponse = await client.GetAsync("/inscripciones");
        var registrationsHtml = await registrationsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Darme de baja", registrationsHtml);
        using var cancellationContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "CancelRegistration"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiforgeryToken(registrationsHtml)),
            new KeyValuePair<string, string>("FormModel.RegistrationId", registration.Id.ToString())
        ]);
        using var cancellationResponse = await client.PostAsync("/inscripciones", cancellationContent);
        var cancellationHtml = WebUtility.HtmlDecode(await cancellationResponse.Content.ReadAsStringAsync());

        Assert.True(cancellationResponse.IsSuccessStatusCode);
        Assert.Equal(registration.Id, api.LastCancelledRegistrationId);
        Assert.Contains("Te has dado de baja del partido.", cancellationHtml);
        Assert.Contains("Todavía no tienes inscripciones activas.", cancellationHtml);
    }

    [Fact]
    public async Task PostForm_UsesTheLoggedInPlayerAndDoesNotAcceptAPlayerId()
    {
        var gameId = Guid.NewGuid();
        var api = new FakeCourtConnectApiClient(gameId);
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

        using var anonymousResponse = await client.GetAsync(route);
        var anonymousHtml = WebUtility.HtmlDecode(await anonymousResponse.Content.ReadAsStringAsync());
        Assert.Contains("Iniciar sesión", anonymousHtml);
        Assert.DoesNotContain("Confirmar inscripción", anonymousHtml);

        using var loginResponse = await client.GetAsync("/login");
        var loginHtml = await loginResponse.Content.ReadAsStringAsync();
        var loginToken = ExtractAntiforgeryToken(loginHtml);
        using var loginContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "Login"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken),
            new KeyValuePair<string, string>("FormModel.Email", "player@example.com"),
            new KeyValuePair<string, string>("FormModel.Password", "Strong-password-123!")
        ]);
        using var loginPostResponse = await client.PostAsync("/login", loginContent);
        Assert.True(loginPostResponse.IsSuccessStatusCode || loginPostResponse.StatusCode == HttpStatusCode.Redirect);

        using var getResponse = await client.GetAsync(route);
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(html);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.DoesNotContain("name=\"FormModel.PlayerId\"", html);
        Assert.DoesNotContain("<select", html);

        using var formContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "RegisterForGame"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        ]);
        using var postResponse = await client.PostAsync(route, formContent);
        var postHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Tu inscripción se ha confirmado", postHtml);
        Assert.Equal(gameId, api.LastRegistrationGameId);
    }

    [Fact]
    public async Task OrganizerProfileHttp_ShowsTheAuthenticatedAccountDetailsInSpanish()
    {
        var api = new FakeCourtConnectApiClient(Guid.NewGuid(), "Organizer");
        await using var factory = CreateFactory(api);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await SignInAsOrganizerAsync(client);

        using var profileResponse = await client.GetAsync("/profile");
        var profileHtml = WebUtility.HtmlDecode(await profileResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.Contains("organizer@example.com", profileHtml);
        Assert.Contains("Organizador", profileHtml);
        Assert.Contains("Datos de la cuenta", profileHtml);
        var organizerNavigation = Regex.Match(profileHtml, "<nav class=\"site-nav\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.Contains("href=\"/organizer\"", organizerNavigation);
        Assert.DoesNotContain("href=\"/inscripciones\"", organizerNavigation);
    }

    [Fact]
    public async Task OrganizerNewGameHttp_CreatesGameFromTheSubmittedForm()
    {
        var api = new FakeCourtConnectApiClient(Guid.NewGuid(), "Organizer");
        await using var factory = CreateFactory(api);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await SignInAsOrganizerAsync(client);

        using var dashboardResponse = await client.GetAsync("/organizer/games/new");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Nuevo partido", dashboardHtml);
        using var gameContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "CreateOrganizerGame"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiforgeryToken(dashboardHtml)),
            new KeyValuePair<string, string>("Form.CourtId", api.TestCourt.Id.ToString()),
            new KeyValuePair<string, string>("Form.StartAt", DateTime.Now.AddDays(2).ToString("yyyy-MM-ddTHH:mm")),
            new KeyValuePair<string, string>("Form.DurationMinutes", "90"),
            new KeyValuePair<string, string>("Form.Capacity", "4"),
            new KeyValuePair<string, string>("Form.MinimumAge", "16"),
            new KeyValuePair<string, string>("Form.PricePerPlayer", "10")
        ]);

        using var createResponse = await client.PostAsync("/organizer/games/new", gameContent);
        var createdHtml = WebUtility.HtmlDecode(await createResponse.Content.ReadAsStringAsync());

        Assert.True(createResponse.IsSuccessStatusCode, createdHtml);
        Assert.NotNull(api.LastCreatedRequest);
        Assert.Equal(api.TestCourt.Id, api.LastCreatedRequest.CourtId);
        Assert.Contains("El partido se ha creado correctamente", createdHtml);
    }

    [Fact]
    public async Task OrganizerDashboardHttp_ShowsTheOwnersGamesAndEditAction()
    {
        var api = new FakeCourtConnectApiClient(Guid.NewGuid(), "Organizer", includeOwnedGame: true);
        await using var factory = CreateFactory(api);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await SignInAsOrganizerAsync(client);

        using var dashboardResponse = await client.GetAsync("/organizer");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Mis partidos", dashboardHtml);
        Assert.Contains($"href=\"/organizer/games/{api.TestGame.Id}\"", dashboardHtml);
        Assert.Contains("Editar", dashboardHtml);
    }

    [Fact]
    public async Task OrganizerGameDetailsHttp_ShowsTheStatusActionAndParticipantList()
    {
        var api = new FakeCourtConnectApiClient(Guid.NewGuid(), "Organizer", includeOwnedGame: true);
        await using var factory = CreateFactory(api);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await SignInAsOrganizerAsync(client);

        using var detailResponse = await client.GetAsync($"/organizer/games/{api.TestGame.Id}");
        var detailHtml = WebUtility.HtmlDecode(await detailResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Contains("Estado: Abierto", detailHtml);
        Assert.Contains("Cerrar partido", detailHtml);
        Assert.Contains("Jugadores inscritos", detailHtml);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeCourtConnectApiClient api)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICourtConnectApiClient>();
                services.AddSingleton<ICourtConnectApiClient>(api);
            }));
    }

    private static async Task SignInAsOrganizerAsync(HttpClient client)
    {
        using var loginResponse = await client.GetAsync("/login");
        var loginHtml = await loginResponse.Content.ReadAsStringAsync();
        using var loginContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "Login"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiforgeryToken(loginHtml)),
            new KeyValuePair<string, string>("FormModel.Email", "organizer@example.com"),
            new KeyValuePair<string, string>("FormModel.Password", "Strong-password-123!")
        ]);
        using var loginPost = await client.PostAsync("/login", loginContent);
        Assert.True(loginPost.IsSuccessStatusCode || loginPost.StatusCode == HttpStatusCode.Redirect);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var tokenMatch = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success);
        return tokenMatch.Groups[1].Value;
    }

    private sealed class FakeCourtConnectApiClient : ICourtConnectApiClient
    {
        private readonly Guid _gameId;
        private readonly string _accountRole;
        private readonly List<GameDto> _ownedGames = [];
        private PlayerDto? _playerProfile;
        private string? _accountEmail;

        public FakeCourtConnectApiClient(Guid gameId, string accountRole = "Player", bool includeOwnedGame = false)
        {
            _gameId = gameId;
            _accountRole = accountRole;
            TestCourt = new CourtDto(Guid.NewGuid(), "Pista de prueba", "Barcelona");
            if (includeOwnedGame)
            {
                _ownedGames.Add(CreateTestGame());
            }
        }

        public CourtDto TestCourt { get; }

        public GameDto TestGame => _ownedGames.Single(game => game.Id == _gameId);

        public Guid? LastRegistrationGameId { get; private set; }

        public CreateGameRequestDto? LastCreatedRequest { get; private set; }

        public UpdateGameRequestDto? LastUpdatedRequest { get; private set; }

        public string? LastUpdatedStatus { get; private set; }

        public bool AccountRegistered { get; private set; }

        public bool LoginCalled { get; private set; }

        public bool ProfileCreated { get; private set; }

        public bool TokenRefreshed { get; private set; }

        public IReadOnlyList<PlayerRegistrationDto> MyRegistrations { get; init; } = [];

        public Guid? LastCancelledRegistrationId { get; private set; }

        public Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GameDto>>([]);
        }

        public Task<GameDto> GetGameAsync(Guid requestedGameId, CancellationToken cancellationToken)
        {
            if (requestedGameId != _gameId)
            {
                return Task.FromException<GameDto>(new InvalidOperationException("Unexpected game id."));
            }

            return Task.FromResult(CreateTestGame());
        }

        public Task RegisterAccountAsync(string email, string password, CancellationToken cancellationToken)
        {
            AccountRegistered = true;
            _accountEmail = email;
            return Task.CompletedTask;
        }

        public Task<IdentityTokenResponse> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            LoginCalled = true;
            _accountEmail = email;
            return Task.FromResult(new IdentityTokenResponse("Bearer", "access", 3600, "refresh"));
        }

        public Task<IdentityTokenResponse> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            TokenRefreshed = true;
            return Task.FromResult(new IdentityTokenResponse("Bearer", "refreshed-access", 3600, "rotated-refresh"));
        }

        public Task<CurrentAccountDto> GetCurrentAccountAsync(string accessToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CurrentAccountDto(GetAccountEmail(), [_accountRole]));
        }

        public Task<CurrentAccountDto> GetMyAccountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new CurrentAccountDto(GetAccountEmail(), [_accountRole]));
        }

        public Task<IReadOnlyList<CourtDto>> GetCourtsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CourtDto>>([TestCourt]);
        }

        public Task<CourtDto> CreateCourtAsync(
            CreateCourtRequestDto request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CourtDto(Guid.NewGuid(), request.Name, request.Location));
        }

        public Task<CourtDto> UpdateCourtAsync(
            Guid courtId,
            UpdateCourtRequestDto request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CourtDto(courtId, request.Name, request.Location));
        }

        public Task DeleteCourtAsync(Guid courtId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GameDto>> GetMyOrganizedGamesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GameDto>>(_ownedGames.ToArray());
        }

        public Task<GameDto> CreateGameAsync(CreateGameRequestDto request, CancellationToken cancellationToken)
        {
            LastCreatedRequest = request;
            var game = new GameDto(
                Guid.NewGuid(),
                request.CourtId ?? TestCourt.Id,
                TestCourt.Name,
                request.StartAt,
                request.DurationMinutes,
                request.Capacity,
                request.Capacity,
                request.MinimumAge,
                request.PricePerPlayer,
                "Open");
            _ownedGames.Add(game);
            return Task.FromResult(game);
        }

        public Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto request, CancellationToken cancellationToken)
        {
            LastUpdatedRequest = request;
            var game = _ownedGames.Single(item => item.Id == gameId) with
            {
                StartAt = request.StartAt,
                DurationMinutes = request.DurationMinutes,
                Capacity = request.Capacity,
                MinimumAge = request.MinimumAge,
                PricePerPlayer = request.PricePerPlayer
            };
            ReplaceOwnedGame(game);
            return Task.FromResult(game);
        }

        public Task<GameDto> SetGameStatusAsync(Guid gameId, string status, CancellationToken cancellationToken)
        {
            LastUpdatedStatus = status;
            var game = _ownedGames.Single(item => item.Id == gameId) with { Status = status };
            ReplaceOwnedGame(game);
            return Task.FromResult(game);
        }

        public Task<IReadOnlyList<GameParticipantDto>> GetGameParticipantsAsync(
            Guid gameId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GameParticipantDto>>([]);
        }

        public Task<PlayerDto> GetMyProfileAsync(CancellationToken cancellationToken)
        {
            if (_playerProfile is null)
            {
                return Task.FromException<PlayerDto>(new CourtConnectApiException(
                    HttpStatusCode.NotFound,
                    "No existe un perfil de jugador."));
            }

            return Task.FromResult(_playerProfile);
        }

        public Task<PlayerDto> CreateMyProfileAsync(
            CreatePlayerProfileRequest request,
            CancellationToken cancellationToken)
        {
            ProfileCreated = true;
            _playerProfile = new PlayerDto(
                Guid.NewGuid(),
                request.Nickname,
                request.FullName,
                request.DateOfBirth,
                GetAccountEmail() ?? string.Empty);
            return GetMyProfileAsync(cancellationToken);
        }

        private string? GetAccountEmail() =>
            _accountEmail ?? (_accountRole == "Organizer" ? "organizer@example.com" : "player@example.com");

        public Task<RegistrationDto> RegisterAsync(Guid requestedGameId, CancellationToken cancellationToken)
        {
            LastRegistrationGameId = requestedGameId;
            return Task.FromResult(new RegistrationDto(
                Guid.NewGuid(),
                requestedGameId,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                10m,
                "Confirmed"));
        }

        public Task CancelRegistrationAsync(Guid registrationId, CancellationToken cancellationToken)
        {
            LastCancelledRegistrationId = registrationId;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlayerRegistrationDto>> GetMyRegistrationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(MyRegistrations);
        }

        private GameDto CreateTestGame()
        {
            return new GameDto(
                _gameId,
                TestCourt.Id,
                TestCourt.Name,
                DateTimeOffset.UtcNow.AddDays(10),
                90,
                4,
                4,
                16,
                10m,
                "Open");
        }

        private void ReplaceOwnedGame(GameDto game)
        {
            var index = _ownedGames.FindIndex(item => item.Id == game.Id);
            _ownedGames[index] = game;
        }
    }
}

using Bunit;
using DeemSoft.CourtConnect.Ui.Components.Api;
using DeemSoft.CourtConnect.Ui.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CourtConnect.Ui.Tests;

public sealed class OrganizerDashboardTests : BunitContext
{
    public OrganizerDashboardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Dashboard_ShowsTheOrganizersGamesAndLinksToManagementPages()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var game = CreateGame(court);
        var api = new FakeCourtConnectApiClient([court], [game]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerDashboard>();

        Assert.Contains("Mis partidos", page.Markup);
        Assert.Contains("Pista Central", page.Markup);
        Assert.Contains("Abierto", page.Markup);
        Assert.Contains("Plazas ocupadas", page.Markup);
        Assert.Contains($"/organizer/games/{game.Id}", page.Markup);
        Assert.Contains("/organizer/games/new", page.Markup);
        Assert.Contains("Gestionar pistas", page.Markup);
    }

    [Fact]
    public async Task NewGame_CreatesAGameForTheSelectedCourt()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var api = new FakeCourtConnectApiClient([court]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerNewGame>();
        await page.Find("#create-court").ChangeAsync(court.Id.ToString());
        await page.Find("form").SubmitAsync();

        Assert.NotNull(api.LastCreatedRequest);
        Assert.Equal(court.Id, api.LastCreatedRequest.CourtId);
        Assert.Contains("El partido se ha creado correctamente", page.Markup);
    }

    [Fact]
    public async Task Dashboard_SortsGamesBySortableColumnsAndLinksToDetails()
    {
        var zuluCourt = new CourtDto(Guid.NewGuid(), "Pista Zulu", "Barcelona");
        var alphaCourt = new CourtDto(Guid.NewGuid(), "Pista Alfa", "Barcelona");
        var games = new[] { CreateGame(zuluCourt), CreateGame(alphaCourt) };
        var api = new FakeCourtConnectApiClient([zuluCourt, alphaCourt], games);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerDashboard>();

        Assert.NotEmpty(page.FindAll("th button"));
        Assert.Contains($"href=\"/organizer/games/{games[0].Id}\"", page.Markup);
        Assert.Contains("Editar", page.Markup);
        Assert.Contains("Pista Zulu", page.Find("tbody tr").TextContent);
        await page.Find("th button").ClickAsync();
        Assert.Contains("Pista Alfa", page.Find("tbody tr").TextContent);
    }

    [Fact]
    public async Task Dashboard_EditsAnOwnedGame()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var game = CreateGame(court);
        var api = new FakeCourtConnectApiClient([court], [game]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerDashboard>();
        await page.Find("button.button-secondary").ClickAsync();
        await page.Find("#edit-duration").ChangeAsync("120");
        await page.Find("form").SubmitAsync();

        Assert.Equal(120, api.LastUpdatedRequest!.DurationMinutes);
        Assert.Contains("Los cambios se han guardado", page.Markup);
    }

    [Fact]
    public async Task NewGame_WhenFormIsInvalid_ShowsValidationErrors()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        Services.AddSingleton<ICourtConnectApiClient>(new FakeCourtConnectApiClient([court]));

        var page = Render<OrganizerNewGame>();
        await page.Find("form").SubmitAsync();

        Assert.Contains("Selecciona una pista", page.Markup);
    }

    [Fact]
    public async Task NewGame_WhenApiRejectsCreation_ShowsTheProblemMessage()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var api = new FakeCourtConnectApiClient([court])
        {
            GameCreationException = new CourtConnectApiException(
                System.Net.HttpStatusCode.BadRequest,
                "La fecha del partido debe estar en el futuro.")
        };
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerNewGame>();
        await page.Find("#create-court").ChangeAsync(court.Id.ToString());
        await page.Find("form").SubmitAsync();

        Assert.Contains("La fecha del partido debe estar en el futuro", page.Markup);
    }

    [Fact]
    public async Task GameDetails_ShowsOnlyPlayerNamesAndCanCloseTheGame()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var game = CreateGame(court);
        var api = new FakeCourtConnectApiClient([court], [game])
        {
            Participants = [new GameParticipantDto("driss", "Driss Example")]
        };
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerGameDetails>(parameters => parameters.Add(component => component.Id, game.Id));

        Assert.Contains("driss", page.Markup);
        Assert.Contains("Driss Example", page.Markup);
        Assert.DoesNotContain("email", page.Markup, StringComparison.OrdinalIgnoreCase);
        await page.Find("button").ClickAsync();

        Assert.Equal("Closed", api.LastUpdatedStatus);
        Assert.Contains("El partido se ha cerrado", page.Markup);
        Assert.Contains("Abrir partido", page.Markup);
    }

    [Fact]
    public async Task CourtsPage_CreatesAndListsACourt()
    {
        var api = new FakeCourtConnectApiClient([]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerCourts>();
        Assert.DoesNotContain("id=\"court-name\"", page.Markup);
        await page.FindAll("button").Single(button => button.TextContent == "Crear nueva pista").ClickAsync();
        await page.Find("#court-name").ChangeAsync("Pista Norte");
        await page.Find("#court-location").ChangeAsync("Badalona");
        await page.Find("form").SubmitAsync();

        Assert.Equal("Pista Norte", api.LastCreatedCourtRequest!.Name);
        Assert.Equal("Badalona", api.LastCreatedCourtRequest.Location);
        Assert.Contains("Pista Norte", page.Markup);
        Assert.Contains("Badalona", page.Markup);
    }

    [Fact]
    public async Task CourtsPage_WhenRequiredFieldsAreMissing_ShowsValidationErrors()
    {
        Services.AddSingleton<ICourtConnectApiClient>(new FakeCourtConnectApiClient([]));

        var page = Render<OrganizerCourts>();
        await page.FindAll("button").Single(button => button.TextContent == "Crear nueva pista").ClickAsync();
        await page.Find("form").SubmitAsync();

        Assert.Contains("Indica el nombre de la pista", page.Markup);
        Assert.Contains("Indica la ubicación de la pista", page.Markup);
    }

    [Fact]
    public async Task CourtsPage_CanEditAndDeleteACourtAfterConfirmation()
    {
        var court = new CourtDto(Guid.NewGuid(), "Pista Central", "Barcelona");
        var api = new FakeCourtConnectApiClient([court]);
        Services.AddSingleton<ICourtConnectApiClient>(api);

        var page = Render<OrganizerCourts>();
        await page.FindAll("button").Single(button => button.TextContent == "Editar").ClickAsync();
        await page.Find($"#court-name-{court.Id}").ChangeAsync("Pista Norte");
        await page.Find($"#court-location-{court.Id}").ChangeAsync("Badalona");
        await page.Find("form").SubmitAsync();

        Assert.Equal(court.Id, api.LastUpdatedCourtId);
        Assert.Equal("Pista Norte", api.LastUpdatedCourtRequest!.Name);
        Assert.Contains("Pista Norte", page.Markup);
        Assert.Contains("Badalona", page.Markup);

        await page.FindAll("button").Single(button => button.TextContent == "Eliminar").ClickAsync();
        Assert.Contains("¿Quieres eliminar esta pista?", page.Markup);
        await page.FindAll("button").Single(button => button.TextContent == "Confirmar eliminación").ClickAsync();

        Assert.Equal(court.Id, api.LastDeletedCourtId);
        Assert.DoesNotContain("Pista Norte", page.Markup);
        Assert.Contains("La pista se ha eliminado correctamente.", page.Markup);
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
        private readonly List<CourtDto> _courts = courts.ToList();
        private readonly List<GameDto> _games = games?.ToList() ?? [];

        public CreateGameRequestDto? LastCreatedRequest { get; private set; }

        public CreateCourtRequestDto? LastCreatedCourtRequest { get; private set; }

        public UpdateCourtRequestDto? LastUpdatedCourtRequest { get; private set; }

        public Guid? LastUpdatedCourtId { get; private set; }

        public Guid? LastDeletedCourtId { get; private set; }

        public string? LastUpdatedStatus { get; private set; }

        public UpdateGameRequestDto? LastUpdatedRequest { get; private set; }

        public IReadOnlyList<GameParticipantDto> Participants { get; init; } = [];

        public CourtConnectApiException? GameCreationException { get; init; }

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
            return Task.FromResult<IReadOnlyList<CourtDto>>(_courts);
        }

        public Task<CourtDto> CreateCourtAsync(
            CreateCourtRequestDto request,
            CancellationToken cancellationToken)
        {
            LastCreatedCourtRequest = request;
            var court = new CourtDto(Guid.NewGuid(), request.Name, request.Location);
            _courts.Add(court);
            return Task.FromResult(court);
        }

        public Task<CourtDto> UpdateCourtAsync(
            Guid courtId,
            UpdateCourtRequestDto request,
            CancellationToken cancellationToken)
        {
            LastUpdatedCourtId = courtId;
            LastUpdatedCourtRequest = request;
            var updatedCourt = new CourtDto(courtId, request.Name, request.Location);
            var index = _courts.FindIndex(court => court.Id == courtId);
            _courts[index] = updatedCourt;
            return Task.FromResult(updatedCourt);
        }

        public Task DeleteCourtAsync(Guid courtId, CancellationToken cancellationToken)
        {
            LastDeletedCourtId = courtId;
            _courts.RemoveAll(court => court.Id == courtId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GameDto>> GetMyOrganizedGamesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GameDto>>(_games);
        }

        public Task<GameDto> CreateGameAsync(CreateGameRequestDto request, CancellationToken cancellationToken)
        {
            if (GameCreationException is not null)
            {
                return Task.FromException<GameDto>(GameCreationException);
            }

            LastCreatedRequest = request;
            var court = _courts.Single(item => item.Id == request.CourtId);
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

        public Task<IReadOnlyList<GameParticipantDto>> GetGameParticipantsAsync(
            Guid gameId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Participants);
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

        public Task CancelRegistrationAsync(Guid registrationId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
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

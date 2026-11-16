using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Exceptions;
using DeemSoft.CourtConnect.Application.Services;
using DeemSoft.CourtConnect.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeemSoft.CourtConnect.UnitTests;

public sealed class RegistrationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    [Fact]
    public async Task RegisterAsync_WithValidGameAndPlayer_PersistsRegistration()
    {
        var fixture = new Fixture();

        var registration = await fixture.Service.RegisterAsync(fixture.Game.Id, fixture.UserId, CancellationToken.None);

        Assert.Equal(RegistrationStatus.Confirmed.ToString(), registration.Status);
        Assert.Equal(fixture.Game.PricePerPlayer, registration.Amount);
        Assert.Single(fixture.RegistrationRepository.Items);
        Assert.Equal(1, fixture.Game.CountActiveRegistrations());
    }

    [Theory]
    [InlineData(RegistrationFailure.GameFull)]
    [InlineData(RegistrationFailure.DuplicatePlayer)]
    [InlineData(RegistrationFailure.AgeTooLow)]
    [InlineData(RegistrationFailure.GameNotOpen)]
    [InlineData(RegistrationFailure.GameStarted)]
    public async Task RegisterAsync_WhenBusinessRuleFails_ThrowsConflict(RegistrationFailure failure)
    {
        var fixture = new Fixture(failure);

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.RegisterAsync(fixture.Game.Id, fixture.UserId, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_WhenGameDoesNotExist_ThrowsNotFound()
    {
        var fixture = new Fixture(RegistrationFailure.GameNotFound);
        var missingGameId = Guid.NewGuid();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            fixture.Service.RegisterAsync(missingGameId, fixture.UserId, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_WhenPlayerDoesNotExist_ThrowsNotFound()
    {
        var fixture = new Fixture(RegistrationFailure.PlayerNotFound);
        var missingUserId = Guid.NewGuid();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            fixture.Service.RegisterAsync(fixture.Game.Id, missingUserId, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdentityUserIdAsync_ReturnsOnlyTheCurrentPlayersActiveRegistrations()
    {
        var fixture = new Fixture();
        var ownRegistration = fixture.Game.Register(fixture.Player, Now);
        var otherPlayer = CreatePlayer("other", identityUserId: Guid.NewGuid());
        var otherRegistration = fixture.Game.Register(otherPlayer, Now.AddSeconds(1));
        fixture.RegistrationRepository.Seed(ownRegistration);
        fixture.RegistrationRepository.Seed(otherRegistration);

        var registrations = await fixture.Service.GetByIdentityUserIdAsync(fixture.UserId, CancellationToken.None);

        var result = Assert.Single(registrations);
        Assert.Equal(ownRegistration.Id, result.Registration.Id);
        Assert.Equal(fixture.Game.Id, result.Game.Id);
        Assert.Equal(fixture.Game.Court.Name, result.Game.CourtName);
    }

    [Fact]
    public async Task CancelAsync_WithConfirmedRegistration_CancelsAndPersists()
    {
        var fixture = new Fixture();
        var registration = fixture.Game.Register(fixture.Player, Now.AddMinutes(-1));
        fixture.RegistrationRepository.Seed(registration);

        await fixture.Service.CancelAsync(registration.Id, fixture.UserId, CancellationToken.None);

        Assert.Equal(RegistrationStatus.Cancelled, registration.Status);
        Assert.Equal(0, fixture.Game.CountActiveRegistrations());
    }

    [Fact]
    public async Task CancelAsync_WhenRegistrationBelongsToAnotherPlayer_ReturnsNotFoundWithoutCancellingIt()
    {
        var fixture = new Fixture();
        var registration = fixture.Game.Register(fixture.Player, Now.AddMinutes(-1));
        fixture.RegistrationRepository.Seed(registration);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            fixture.Service.CancelAsync(registration.Id, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
    }

    [Fact]
    public async Task CancelAsync_WhenRegistrationDoesNotExist_ThrowsNotFound()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            fixture.Service.CancelAsync(Guid.NewGuid(), fixture.UserId, CancellationToken.None));
    }

    [Fact]
    public async Task CancelAsync_WhenRegistrationIsAlreadyCancelled_ThrowsConflict()
    {
        var fixture = new Fixture();
        var registration = fixture.Game.Register(fixture.Player, Now.AddMinutes(-1));
        fixture.Game.CancelRegistration(registration, Now);
        fixture.RegistrationRepository.Seed(registration);

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.CancelAsync(registration.Id, fixture.UserId, CancellationToken.None));
    }

    public enum RegistrationFailure
    {
        GameNotFound,
        PlayerNotFound,
        GameFull,
        DuplicatePlayer,
        AgeTooLow,
        GameNotOpen,
        GameStarted
    }

    private sealed class Fixture
    {
        public Fixture(RegistrationFailure? failure = null)
        {
            var startAt = failure == RegistrationFailure.GameStarted ? Now : Now.AddDays(10);
            var minimumAge = failure == RegistrationFailure.AgeTooLow ? 18 : 16;
            var status = failure == RegistrationFailure.GameNotOpen ? GameStatus.Draft : GameStatus.Open;
            var capacity = failure == RegistrationFailure.GameFull ? 2 : 4;
            var court = new Court("Central", "Barcelona");

            Game = new Game(court, startAt, TimeSpan.FromHours(2), capacity, minimumAge, 10m, status);
            Player = CreatePlayer("player", identityUserId: UserId);

            if (failure != RegistrationFailure.GameNotFound)
            {
                GameRepository.Items.Add(Game.Id, Game);
            }

            if (failure != RegistrationFailure.PlayerNotFound)
            {
                PlayerRepository.Items.Add(Player.Id, Player);
            }

            switch (failure)
            {
                case RegistrationFailure.GameFull:
                    Game.Register(CreatePlayer("other-1"), Now);
                    Game.Register(CreatePlayer("other-2"), Now);
                    break;
                case RegistrationFailure.DuplicatePlayer:
                    Game.Register(Player, Now);
                    break;
                case RegistrationFailure.AgeTooLow:
                    Player = CreatePlayer("player", new DateOnly(2010, 1, 10), UserId);
                    PlayerRepository.Items.Clear();
                    PlayerRepository.Items.Add(Player.Id, Player);
                    break;
            }

            Service = new RegistrationService(
                GameRepository,
                PlayerRepository,
                RegistrationRepository,
                new FixedTimeProvider(Now),
                NullLogger<RegistrationService>.Instance);
        }

        public FakeGameRepository GameRepository { get; } = new();

        public FakePlayerRepository PlayerRepository { get; } = new();

        public FakeRegistrationRepository RegistrationRepository { get; } = new();

        public Game Game { get; }

        public Player Player { get; private set; }

        public Guid UserId { get; } = Guid.NewGuid();

        public RegistrationService Service { get; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }

    private sealed class FakeGameRepository : IGameRepository
    {
        public Dictionary<Guid, Game> Items { get; } = [];

        public Task AddAsync(Game game, CancellationToken cancellationToken)
        {
            Items.Add(game.Id, game);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Items.TryGetValue(id, out var game);
            return Task.FromResult(game);
        }

        public Task<IReadOnlyList<Game>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Game> games = Items.Values.Where(game => ids.Contains(game.Id)).ToArray();
            return Task.FromResult(games);
        }

        public Task<IReadOnlyList<Game>> GetOpenUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            IReadOnlyList<Game> games = Items.Values
                .Where(game => game.Status == GameStatus.Open && game.StartAt > now)
                .ToArray();
            return Task.FromResult(games);
        }

        public Task<IReadOnlyList<Game>> GetByOrganizerUserIdAsync(Guid organizerUserId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Game> games = Items.Values
                .Where(game => game.OrganizerUserId == organizerUserId)
                .OrderByDescending(game => game.StartAt)
                .ToArray();
            return Task.FromResult(games);
        }
    }

    private sealed class FakePlayerRepository : IPlayerRepository
    {
        public Dictionary<Guid, Player> Items { get; } = [];

        public Task AddAsync(Player player, CancellationToken cancellationToken)
        {
            Items.Add(player.Id, player);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Items.TryGetValue(id, out var player);
            return Task.FromResult(player);
        }

        public Task<Player?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.Values.SingleOrDefault(player => player.IdentityUserId == identityUserId));
        }

        public Task<Player?> GetByNicknameAsync(string nickname, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.Values.SingleOrDefault(player => player.Nickname == nickname));
        }

        public Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Player>>(Items.Values.OrderBy(player => player.Nickname).ToArray());
        }
    }

    private sealed class FakeRegistrationRepository : IRegistrationRepository
    {
        public Dictionary<Guid, Registration> Items { get; } = [];

        public void Seed(Registration registration)
        {
            Items.Add(registration.Id, registration);
        }

        public Task AddAsync(Registration registration, CancellationToken cancellationToken)
        {
            Items.Add(registration.Id, registration);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Registration?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Items.TryGetValue(id, out var registration);
            return Task.FromResult(registration);
        }

        public Task<IReadOnlyList<Registration>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Registration> registrations = Items.Values
                .Where(registration => registration.GameId == gameId)
                .ToArray();
            return Task.FromResult(registrations);
        }

        public Task<IReadOnlyList<Registration>> GetByPlayerIdAsync(
            Guid playerId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Registration> registrations = Items.Values
                .Where(registration =>
                    registration.PlayerId == playerId
                    && registration.Status == RegistrationStatus.Confirmed)
                .ToArray();
            return Task.FromResult(registrations);
        }
    }

    private static Player CreatePlayer(
        string nickname,
        DateOnly? dateOfBirth = null,
        Guid? identityUserId = null)
    {
        return new Player(
            nickname,
            "Test Player",
            dateOfBirth ?? new DateOnly(2000, 1, 1),
            $"{nickname}@example.com",
            Today,
            identityUserId);
    }
}

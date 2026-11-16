using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Exceptions;
using DeemSoft.CourtConnect.Application.Services;
using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.UnitTests;

public sealed class ApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    [Fact]
    public async Task CourtService_CreateAsync_PersistsAndReturnsDto()
    {
        var repository = new FakeCourtRepository();
        var service = new CourtService(repository);

        var result = await service.CreateAsync(new CreateCourtRequest("Central", "Barcelona"), CancellationToken.None);

        Assert.Equal("Central", result.Name);
        Assert.Single(repository.Items);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task GameService_GetOpenUpcomingAsync_UsesCurrentTimeAndMapsGames()
    {
        var repository = new FakeGameRepository();
        var court = new Court("Central", "Barcelona");
        var upcomingGame = CreateGame(court, Now.AddDays(1));
        var pastGame = CreateGame(court, Now.AddDays(-1));
        repository.Items.Add(upcomingGame.Id, upcomingGame);
        repository.Items.Add(pastGame.Id, pastGame);
        var service = new GameService(repository, new FakeCourtRepository(), new FixedTimeProvider(Now));

        var results = await service.GetOpenUpcomingAsync(CancellationToken.None);

        var gameDto = Assert.Single(results);
        Assert.Equal(upcomingGame.Id, gameDto.Id);
        Assert.Equal("Central", gameDto.CourtName);
    }

    [Fact]
    public async Task GameService_GetByIdAsync_WhenGameDoesNotExist_ThrowsNotFound()
    {
        var service = new GameService(new FakeGameRepository(), new FakeCourtRepository(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GameService_CreateAsync_WithExistingCourt_PersistsGame()
    {
        var courtRepository = new FakeCourtRepository();
        var court = new Court("Central", "Barcelona");
        courtRepository.Items.Add(court.Id, court);
        var gameRepository = new FakeGameRepository();
        var service = new GameService(gameRepository, courtRepository, new FixedTimeProvider(Now));
        var request = new CreateGameRequest(court.Id, Now.AddDays(2), 90, 4, 16, 10m);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(court.Id, result.CourtId);
        Assert.Single(gameRepository.Items);
        Assert.Equal(1, gameRepository.SaveCount);
    }

    [Fact]
    public async Task GameService_CreateAsync_WhenCourtDoesNotExist_ThrowsNotFound()
    {
        var service = new GameService(new FakeGameRepository(), new FakeCourtRepository(), new FixedTimeProvider(Now));
        var request = new CreateGameRequest(Guid.NewGuid(), Now.AddDays(2), 90, 4, 16, 10m);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GameService_CreateAsync_WhenStartTimeIsPast_ThrowsArgumentOutOfRange()
    {
        var courtRepository = new FakeCourtRepository();
        var court = new Court("Central", "Barcelona");
        courtRepository.Items.Add(court.Id, court);
        var service = new GameService(new FakeGameRepository(), courtRepository, new FixedTimeProvider(Now));
        var request = new CreateGameRequest(court.Id, Now, 90, 4, 16, 10m);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task PlayerService_CreateAsync_WithNewNickname_PersistsPlayer()
    {
        var repository = new FakePlayerRepository();
        var service = new PlayerService(repository, new FixedTimeProvider(Now));
        var request = new CreatePlayerRequest("player", "Test Player", new DateOnly(2000, 1, 1), "player@example.com");

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal("player", result.Nickname);
        Assert.Single(repository.Items);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task PlayerService_CreateAsync_WhenNicknameAlreadyExists_ThrowsConflict()
    {
        var repository = new FakePlayerRepository();
        var existingPlayer = CreatePlayer("player");
        repository.Items.Add(existingPlayer.Id, existingPlayer);
        var service = new PlayerService(repository, new FixedTimeProvider(Now));
        var request = new CreatePlayerRequest("player", "Test Player", new DateOnly(2000, 1, 1), "other@example.com");

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task PlayerService_CreateAsync_WhenPlayerIsUnderSixteen_ThrowsArgumentOutOfRange()
    {
        var service = new PlayerService(new FakePlayerRepository(), new FixedTimeProvider(Now));
        var request = new CreatePlayerRequest("player", "Test Player", Today.AddYears(-15), "player@example.com");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task PlayerService_CreateAsync_WhenDateOfBirthIsMissing_ThrowsArgument()
    {
        var service = new PlayerService(new FakePlayerRepository(), new FixedTimeProvider(Now));
        var request = new CreatePlayerRequest("player", "Test Player", null, "player@example.com");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task PlayerService_GetByIdAsync_WhenPlayerDoesNotExist_ThrowsNotFound()
    {
        var service = new PlayerService(new FakePlayerRepository(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private static Game CreateGame(Court court, DateTimeOffset startAt)
    {
        return new Game(court, startAt, TimeSpan.FromHours(1), 4, 16, 10m);
    }

    private static Player CreatePlayer(string nickname)
    {
        return new Player(nickname, "Test Player", new DateOnly(2000, 1, 1), $"{nickname}@example.com", Today);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }

    private sealed class FakeCourtRepository : ICourtRepository
    {
        public Dictionary<Guid, Court> Items { get; } = [];

        public int SaveCount { get; private set; }

        public Task AddAsync(Court court, CancellationToken cancellationToken)
        {
            Items.Add(court.Id, court);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<Court?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Items.TryGetValue(id, out var court);
            return Task.FromResult(court);
        }
    }

    private sealed class FakeGameRepository : IGameRepository
    {
        public Dictionary<Guid, Game> Items { get; } = [];

        public int SaveCount { get; private set; }

        public Task AddAsync(Game game, CancellationToken cancellationToken)
        {
            Items.Add(game.Id, game);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Items.TryGetValue(id, out var game);
            return Task.FromResult(game);
        }

        public Task<IReadOnlyList<Game>> GetOpenUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            IReadOnlyList<Game> results = Items.Values
                .Where(game => game.Status == GameStatus.Open && game.StartAt > now)
                .ToArray();
            return Task.FromResult(results);
        }
    }

    private sealed class FakePlayerRepository : IPlayerRepository
    {
        public Dictionary<Guid, Player> Items { get; } = [];

        public int SaveCount { get; private set; }

        public Task AddAsync(Player player, CancellationToken cancellationToken)
        {
            Items.Add(player.Id, player);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            Items.TryGetValue(id, out var player);
            return Task.FromResult(player);
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
}

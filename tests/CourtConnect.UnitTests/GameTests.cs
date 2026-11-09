using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.UnitTests;

public class GameTests
{
    // Las fechas fijas hacen que las pruebas no dependan del día en que se ejecutan.
    private static readonly DateTimeOffset GameStart = new(2026, 1, 10, 20, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateOnly PlayerRegistrationDate = new(2026, 1, 10);

    [Fact]
    public void Register_WithAvailableSlots_AddsConfirmedRegistration()
    {
        var game = CreateGame(capacity: 2);
        var player = CreatePlayer("player-1");

        var registration = game.Register(player, GameStart.AddHours(-1));

        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        Assert.Equal(1, game.CountActiveRegistrations());
        Assert.True(game.HasActiveRegistrationFor(player));
    }

    [Fact]
    public void Register_OnLastAvailableSlot_Succeeds()
    {
        var game = CreateGame(capacity: 2);

        game.Register(CreatePlayer("player-1"), GameStart.AddHours(-1));
        var lastRegistration = game.Register(CreatePlayer("player-2"), GameStart.AddMinutes(-30));

        Assert.Equal(2, game.CountActiveRegistrations());
        Assert.Equal(RegistrationStatus.Confirmed, lastRegistration.Status);
    }

    [Fact]
    public void Register_WhenGameIsFull_ThrowsException()
    {
        var game = CreateGame(capacity: 2);
        game.Register(CreatePlayer("player-1"), GameStart.AddHours(-1));
        game.Register(CreatePlayer("player-2"), GameStart.AddMinutes(-30));

        Assert.Throws<InvalidOperationException>(() =>
            game.Register(CreatePlayer("player-3"), GameStart.AddMinutes(-10)));
    }

    [Fact]
    public void Register_WhenPlayerAlreadyHasActiveRegistration_ThrowsException()
    {
        var game = CreateGame();
        var player = CreatePlayer("player-1");
        game.Register(player, GameStart.AddHours(-1));

        Assert.Throws<InvalidOperationException>(() =>
            game.Register(player, GameStart.AddMinutes(-30)));
    }

    [Theory]
    // El primer caso cumple 16 años el mismo día del partido; el segundo no alcanza la edad exigida.
    [InlineData(2010, 1, 10, 16, true)]
    [InlineData(2009, 1, 11, 18, false)]
    public void Register_ValidatesAgeOnGameDate(int year, int month, int day, int minimumAge, bool succeeds)
    {
        var game = CreateGame(minimumAge: minimumAge);
        var player = CreatePlayer("age-test", new DateOnly(year, month, day));

        if (succeeds)
        {
            var registration = game.Register(player, GameStart.AddHours(-1));
            Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
            return;
        }

        Assert.Throws<InvalidOperationException>(() =>
            game.Register(player, GameStart.AddHours(-1)));
    }

    [Theory]
    [InlineData(GameStatus.Draft)]
    [InlineData(GameStatus.Closed)]
    public void Register_WhenGameIsNotOpen_ThrowsException(GameStatus status)
    {
        var game = CreateGame(status: status);

        Assert.Throws<InvalidOperationException>(() =>
            game.Register(CreatePlayer("player-1"), GameStart.AddHours(-1)));
    }

    [Theory]
    // La inscripción es válida un minuto antes; a la hora de inicio y después ya no se admite.
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void Register_EnforcesStartTimeBoundary(int minutesFromStart, bool succeeds)
    {
        var game = CreateGame();
        var registrationDate = GameStart.AddMinutes(minutesFromStart);

        if (succeeds)
        {
            Assert.NotNull(game.Register(CreatePlayer("player-1"), registrationDate));
            return;
        }

        Assert.Throws<InvalidOperationException>(() =>
            game.Register(CreatePlayer("player-1"), registrationDate));
    }

    [Fact]
    public void CancelRegistration_ReleasesSlotAndAllowsPlayerToRegisterAgain()
    {
        var game = CreateGame(capacity: 2);
        var player = CreatePlayer("player-1");
        var registration = game.Register(player, GameStart.AddHours(-1));

        game.CancelRegistration(registration, GameStart.AddMinutes(-30));
        var replacement = game.Register(player, GameStart.AddMinutes(-20));

        Assert.Equal(RegistrationStatus.Cancelled, registration.Status);
        Assert.Equal(RegistrationStatus.Confirmed, replacement.Status);
        Assert.Equal(1, game.CountActiveRegistrations());
    }

    [Theory]
    // La cancelación debe rechazarse tanto en el instante de inicio como después.
    [InlineData(0)]
    [InlineData(1)]
    public void CancelRegistration_AtOrAfterGameStart_ThrowsException(int minutesFromStart)
    {
        var game = CreateGame();
        var registration = game.Register(CreatePlayer("player-1"), GameStart.AddHours(-1));

        Assert.Throws<InvalidOperationException>(() =>
            game.CancelRegistration(registration, GameStart.AddMinutes(minutesFromStart)));
    }

    [Fact]
    public void CancelRegistration_WhenAlreadyCancelled_ThrowsException()
    {
        var game = CreateGame();
        var registration = game.Register(CreatePlayer("player-1"), GameStart.AddHours(-1));
        game.CancelRegistration(registration, GameStart.AddMinutes(-30));

        Assert.Throws<InvalidOperationException>(() =>
            game.CancelRegistration(registration, GameStart.AddMinutes(-20)));
    }

    [Fact]
    public void CancelRegistration_WhenRegistrationBelongsToAnotherGame_ThrowsException()
    {
        var firstGame = CreateGame();
        var secondGame = CreateGame();
        var registration = firstGame.Register(CreatePlayer("player-1"), GameStart.AddHours(-1));

        Assert.Throws<InvalidOperationException>(() =>
            secondGame.CancelRegistration(registration, GameStart.AddMinutes(-30)));
    }

    [Fact]
    public void Register_SavesPriceAtTheTimeOfRegistration()
    {
        var game = CreateGame(pricePerPlayer: 12.50m);

        var registration = game.Register(CreatePlayer("player-1"), GameStart.AddHours(-1));

        Assert.Equal(12.50m, registration.Amount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsCapacityBelowTwo(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateGame(capacity: capacity));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(0)]
    public void Constructor_RejectsMinimumAgeBelowSixteen(int minimumAge)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateGame(minimumAge: minimumAge));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveDuration(int durationHours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Game(new Court("Central", "Barcelona"), GameStart, TimeSpan.FromHours(durationHours), 4, 16, 0m));
    }

    [Fact]
    public void Constructor_RejectsNegativePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateGame(pricePerPlayer: -0.01m));
    }

    [Fact]
    public void Constructor_RejectsInvalidStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateGame(status: (GameStatus)99));
    }

    [Fact]
    public void Constructor_RejectsNullCourt()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Game(null!, GameStart, TimeSpan.FromHours(2), 4, 16, 0m));
    }

    [Fact]
    public void Register_RejectsNullPlayer()
    {
        var game = CreateGame();

        Assert.Throws<ArgumentNullException>(() => game.Register(null!, GameStart.AddHours(-1)));
    }

    [Fact]
    public void CancelRegistration_RejectsNullRegistration()
    {
        var game = CreateGame();

        Assert.Throws<ArgumentNullException>(() =>
            game.CancelRegistration(null!, GameStart.AddHours(-1)));
    }

    [Fact]
    public void HasActiveRegistrationFor_RejectsNullPlayer()
    {
        var game = CreateGame();

        Assert.Throws<ArgumentNullException>(() => game.HasActiveRegistrationFor(null!));
    }

    private static Game CreateGame(
        int capacity = 4,
        int minimumAge = 16,
        decimal pricePerPlayer = 0m,
        GameStatus status = GameStatus.Open)
    {
        return new Game(
            new Court("Central", "Barcelona"),
            GameStart,
            TimeSpan.FromHours(2),
            capacity,
            minimumAge,
            pricePerPlayer,
            status);
    }

    private static Player CreatePlayer(string nickname, DateOnly? dateOfBirth = null)
    {
        return new Player(
            nickname,
            "Test Player",
            dateOfBirth ?? new DateOnly(2000, 1, 1),
            $"{nickname}@example.com",
            PlayerRegistrationDate);
    }

}

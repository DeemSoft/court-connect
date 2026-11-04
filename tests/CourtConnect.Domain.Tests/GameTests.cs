using DeemSoft.CourtConnect.Domain;

namespace CourtConnect.Domain.Tests;

public class GameTests
{
    [Fact]
    public void Register_WhenGameHasReachedCapacity_ThrowsException()
    {
        var game = CreateGame(capacity: 2, minimumAge: 16);
        var playerOne = CreatePlayer("player-1", new DateOnly(2000, 1, 1));
        var playerTwo = CreatePlayer("player-2", new DateOnly(2001, 1, 1));

        game.Register(playerOne, new DateTime(2026, 1, 10, 9, 0, 0));
        game.Register(playerTwo, new DateTime(2026, 1, 10, 9, 5, 0));

        var playerThree = CreatePlayer("player-3", new DateOnly(2002, 1, 1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            game.Register(playerThree, new DateTime(2026, 1, 10, 9, 10, 0)));

        Assert.Equal("The game has reached its maximum number of active registrations.", exception.Message);
    }

    [Fact]
    public void Register_WhenPlayerAlreadyHasActiveRegistration_ThrowsException()
    {
        var game = CreateGame(capacity: 4, minimumAge: 16);
        var player = CreatePlayer("player-1", new DateOnly(2000, 1, 1));

        game.Register(player, new DateTime(2026, 1, 10, 9, 0, 0));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            game.Register(player, new DateTime(2026, 1, 10, 9, 5, 0)));

        Assert.Equal("The player already has an active registration for this game.", exception.Message);
    }

    [Fact]
    public void Register_WhenPlayerIsTooYoungForGame_ThrowsException()
    {
        var game = CreateGame(capacity: 4, minimumAge: 18);
        var player = CreatePlayer("young-player", new DateOnly(2008, 12, 31));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            game.Register(player, new DateTime(2026, 1, 10, 9, 0, 0)));

        Assert.Equal("The player does not meet the minimum age for this game.", exception.Message);
    }

    [Fact]
    public void Register_WhenGameIsNotOpen_ThrowsException()
    {
        var court = new Court("Central", "Barcelona");
        var game = new Game(court, new DateTime(2026, 1, 15, 20, 0, 0), TimeSpan.FromHours(2), 4, 16, 0m, GameStatus.Closed);
        var player = CreatePlayer("player-1", new DateOnly(2000, 1, 1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            game.Register(player, new DateTime(2026, 1, 10, 9, 0, 0)));

        Assert.Equal("Registrations are only allowed in open games.", exception.Message);
    }

    [Fact]
    public void CancelRegistration_WhenCancelledAfterGameStarts_ThrowsException()
    {
        var game = CreateGame(capacity: 4, minimumAge: 16);
        var player = CreatePlayer("player-1", new DateOnly(2000, 1, 1));
        var registration = game.Register(player, new DateTime(2026, 1, 10, 9, 0, 0));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            game.CancelRegistration(registration, new DateTime(2026, 1, 15, 20, 0, 0)));

        Assert.Equal("Registrations can only be cancelled before the game starts.", exception.Message);
    }

    [Fact]
    public void Register_StoresAmountAtTimeOfRegistration()
    {
        var game = new Game(new Court("Central", "Barcelona"), new DateTime(2026, 1, 15, 20, 0, 0), TimeSpan.FromHours(2), 4, 16, 12.50m);
        var player = CreatePlayer("player-1", new DateOnly(2000, 1, 1));

        var registration = game.Register(player, new DateTime(2026, 1, 10, 9, 0, 0));

        Assert.Equal(12.50m, registration.Amount);
        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
    }

    [Fact]
    public void CancelRegistration_ReleasesTheSlot()
    {
        var game = CreateGame(capacity: 2, minimumAge: 16);
        var playerOne = CreatePlayer("player-1", new DateOnly(2000, 1, 1));
        var playerTwo = CreatePlayer("player-2", new DateOnly(2001, 1, 1));

        var firstRegistration = game.Register(playerOne, new DateTime(2026, 1, 10, 9, 0, 0));
        game.Register(playerTwo, new DateTime(2026, 1, 10, 9, 5, 0));

        game.CancelRegistration(firstRegistration, new DateTime(2026, 1, 11, 9, 0, 0));

        Assert.Equal(RegistrationStatus.Cancelled, firstRegistration.Status);
        Assert.Equal(1, game.CountActiveRegistrations());
    }

    private static Game CreateGame(int capacity, int minimumAge)
    {
        return new Game(
            new Court("Central", "Barcelona"),
            new DateTime(2026, 1, 15, 20, 0, 0),
            TimeSpan.FromHours(2),
            capacity,
            minimumAge,
            0m,
            GameStatus.Open);
    }

    private static Player CreatePlayer(string nickname, DateOnly dateOfBirth)
    {
        return new Player(nickname, "Test Player", dateOfBirth, $"{nickname}@example.com");
    }
}

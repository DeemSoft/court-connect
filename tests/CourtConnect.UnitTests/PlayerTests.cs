using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.UnitTests;

public class PlayerTests
{
    // Se fija el día de alta para probar la edad mínima sin depender del reloj del sistema.
    private static readonly DateOnly RegistrationDate = new(2026, 1, 10);

    [Fact]
    public void Constructor_TrimsPlayerDetails()
    {
        var player = new Player(" player ", " Test Player ", new DateOnly(2000, 1, 1), " player@example.com ", RegistrationDate);

        Assert.Equal("player", player.Nickname);
        Assert.Equal("Test Player", player.FullName);
        Assert.Equal("player@example.com", player.Email);
    }

    [Fact]
    public void Constructor_AllowsPlayerWhoTurnsSixteenOnRegistrationDate()
    {
        var player = new Player("player", "Test Player", new DateOnly(2010, 1, 10), "player@example.com", RegistrationDate);

        Assert.Equal(16, player.GetAgeOn(RegistrationDate));
    }

    [Fact]
    public void Constructor_RejectsPlayerYoungerThanSixteenOnRegistrationDate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Player("player", "Test Player", new DateOnly(2010, 1, 11), "player@example.com", RegistrationDate));
    }

    [Theory]
    [InlineData("", "Test Player", "player@example.com")]
    [InlineData("player", " ", "player@example.com")]
    [InlineData("player", "Test Player", " ")]
    public void Constructor_RejectsBlankRequiredDetails(string nickname, string fullName, string email)
    {
        Assert.Throws<ArgumentException>(() =>
            new Player(nickname, fullName, new DateOnly(2000, 1, 1), email, RegistrationDate));
    }

    [Fact]
    public void GetAgeOn_UsesTheBirthdayOnTheComparisonDate()
    {
        var player = new Player("player", "Test Player", new DateOnly(2010, 1, 10), "player@example.com", RegistrationDate);

        // Antes del cumpleaños tiene 15 años; el propio día del cumpleaños ya tiene 16.
        Assert.Equal(16, player.GetAgeOn(new DateOnly(2026, 1, 10)));
        Assert.Equal(15, player.GetAgeOn(new DateOnly(2026, 1, 9)));
    }
}

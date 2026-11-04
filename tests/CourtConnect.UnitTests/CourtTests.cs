using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.UnitTests;

public class CourtTests
{
    [Fact]
    public void Constructor_TrimsNameAndLocation()
    {
        var court = new Court(" Central ", " Barcelona ");

        Assert.Equal("Central", court.Name);
        Assert.Equal("Barcelona", court.Location);
    }

    [Theory]
    [InlineData("", "Barcelona")]
    [InlineData("Central", " ")]
    public void Constructor_RejectsBlankNameOrLocation(string name, string location)
    {
        Assert.Throws<ArgumentException>(() => new Court(name, location));
    }
}

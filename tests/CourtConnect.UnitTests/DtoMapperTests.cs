using DeemSoft.CourtConnect.Application.Mapping;
using DeemSoft.CourtConnect.Domain;

namespace DeemSoft.CourtConnect.UnitTests;

public sealed class DtoMapperTests
{
    [Fact]
    public void ToDto_Court_MapsDomainProperties()
    {
        var court = new Court("Central", "Barcelona");

        var dto = DtoMapper.ToDto(court);

        Assert.Equal(court.Id, dto.Id);
        Assert.Equal(court.Name, dto.Name);
        Assert.Equal(court.Location, dto.Location);
    }

    [Fact]
    public void ToDto_Game_MapsAvailableSlotsAndCourtDetails()
    {
        var court = new Court("Central", "Barcelona");
        var game = new Game(
            court,
            new DateTimeOffset(2027, 1, 1, 18, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(90),
            4,
            16,
            12.50m);

        game.Register(new Player("player", "Test Player", new DateOnly(2000, 1, 1), "player@example.com", new DateOnly(2026, 1, 1)), new DateTimeOffset(2026, 12, 31, 12, 0, 0, TimeSpan.Zero));

        var dto = DtoMapper.ToDto(game);

        Assert.Equal(game.Id, dto.Id);
        Assert.Equal(court.Id, dto.CourtId);
        Assert.Equal(court.Name, dto.CourtName);
        Assert.Equal(90, dto.DurationMinutes);
        Assert.Equal(3, dto.AvailableSlots);
        Assert.Equal(GameStatus.Open.ToString(), dto.Status);
    }

    [Fact]
    public void ToDto_Player_MapsDomainProperties()
    {
        var player = new Player("player", "Test Player", new DateOnly(2000, 1, 1), "player@example.com", new DateOnly(2026, 1, 1));

        var dto = DtoMapper.ToDto(player);

        Assert.Equal(player.Id, dto.Id);
        Assert.Equal(player.Nickname, dto.Nickname);
        Assert.Equal(player.FullName, dto.FullName);
        Assert.Equal(player.DateOfBirth, dto.DateOfBirth);
        Assert.Equal(player.Email, dto.Email);
    }

    [Fact]
    public void ToDto_Registration_MapsDomainProperties()
    {
        var court = new Court("Central", "Barcelona");
        var game = new Game(court, new DateTimeOffset(2027, 1, 1, 18, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), 4, 16, 10m);
        var player = new Player("player", "Test Player", new DateOnly(2000, 1, 1), "player@example.com", new DateOnly(2026, 1, 1));

        var registration = game.Register(player, new DateTimeOffset(2026, 12, 31, 12, 0, 0, TimeSpan.Zero));
        var dto = DtoMapper.ToDto(registration);

        Assert.Equal(registration.Id, dto.Id);
        Assert.Equal(game.Id, dto.GameId);
        Assert.Equal(player.Id, dto.PlayerId);
        Assert.Equal(registration.Amount, dto.Amount);
        Assert.Equal(RegistrationStatus.Confirmed.ToString(), dto.Status);
    }
}

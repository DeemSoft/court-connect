using DeemSoft.CourtConnect.Domain;
using DeemSoft.CourtConnect.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DeemSoft.CourtConnect.UnitTests;

public sealed class InfrastructureModelTests
{
    [Fact]
    public void CourtModel_UsesExpectedLengths()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Court))!;

        Assert.Equal(100, entity.FindProperty(nameof(Court.Name))!.GetMaxLength());
        Assert.Equal(200, entity.FindProperty(nameof(Court.Location))!.GetMaxLength());
    }

    [Fact]
    public void PlayerModel_HasUniqueNicknameIndex()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Player))!;

        var index = entity.GetIndexes().Single(i => i.Properties.Select(property => property.Name).SequenceEqual([nameof(Player.Nickname)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void GameModel_UsesPricePrecisionAndCourtForeignKey()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Game))!;
        var priceProperty = entity.FindProperty(nameof(Game.PricePerPlayer))!;

        Assert.Equal(18, priceProperty.GetPrecision());
        Assert.Equal(2, priceProperty.GetScale());

        var courtForeignKey = entity.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(Court));
        Assert.Equal("CourtId", courtForeignKey.Properties.Single().Name);
    }

    [Fact]
    public void RegistrationModel_UsesUniqueConfirmedIndex()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Registration))!;

        var index = entity.GetIndexes().Single(i => i.Properties.Select(property => property.Name).SequenceEqual([nameof(Registration.GameId), nameof(Registration.PlayerId)]));

        Assert.True(index.IsUnique);
        Assert.Equal("[Status] = 'Confirmed'", index.GetFilter());
    }

    private static CourtConnectDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CourtConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CourtConnectDbContext(options);
    }
}

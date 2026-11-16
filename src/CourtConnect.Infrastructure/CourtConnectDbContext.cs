using DeemSoft.CourtConnect.Domain;
using DeemSoft.CourtConnect.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DeemSoft.CourtConnect.Infrastructure;

public sealed class CourtConnectDbContext : IdentityDbContext<CourtConnectUser, IdentityRole<Guid>, Guid>
{
    public CourtConnectDbContext(DbContextOptions<CourtConnectDbContext> options)
        : base(options)
    {
    }

    public DbSet<Court> Courts => Set<Court>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Se centraliza aquí la configuración para mantener las entidades de dominio libres de atributos de EF.
        modelBuilder.ApplyConfiguration(new CourtConfiguration());
        modelBuilder.ApplyConfiguration(new GameConfiguration());
        modelBuilder.ApplyConfiguration(new PlayerConfiguration());
        modelBuilder.ApplyConfiguration(new RegistrationConfiguration());

        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000001"),
                Name = CourtConnectRoles.Organizer,
                NormalizedName = "ORGANIZER",
                ConcurrencyStamp = "60000000-0000-0000-0000-000000000001"
            },
            new IdentityRole<Guid>
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000002"),
                Name = CourtConnectRoles.Player,
                NormalizedName = "PLAYER",
                ConcurrencyStamp = "60000000-0000-0000-0000-000000000002"
            });

        // HasData requiere identificadores estables para que EF pueda comparar los datos semilla entre migraciones.
        var courtCentralId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var courtNorthId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var gameOpenId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var gameFullId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var gameClosedId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var playerMariaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var playerPauId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var playerIreneId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var playerLeoId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var playerSaraId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        modelBuilder.Entity<Court>().HasData(
            new { Id = courtCentralId, Name = "Central", Location = "Barcelona" },
            new { Id = courtNorthId, Name = "North", Location = "Girona" });

        modelBuilder.Entity<Player>().HasData(
            new { Id = playerMariaId, Nickname = "maria", FullName = "Maria López", DateOfBirth = new DateOnly(2002, 4, 18), Email = "maria@example.com" },
            new { Id = playerPauId, Nickname = "pau", FullName = "Pau García", DateOfBirth = new DateOnly(1999, 8, 3), Email = "pau@example.com" },
            new { Id = playerIreneId, Nickname = "irene", FullName = "Irene Soler", DateOfBirth = new DateOnly(2005, 11, 28), Email = "irene@example.com" },
            new { Id = playerLeoId, Nickname = "leo", FullName = "Leo Torres", DateOfBirth = new DateOnly(2001, 6, 10), Email = "leo@example.com" },
            new { Id = playerSaraId, Nickname = "sara", FullName = "Sara Valls", DateOfBirth = new DateOnly(2003, 2, 14), Email = "sara@example.com" });

        modelBuilder.Entity<Game>().HasData(
            new { Id = gameOpenId, CourtId = courtCentralId, StartAt = new DateTimeOffset(2027, 1, 12, 18, 30, 0, TimeSpan.FromHours(1)), Duration = TimeSpan.FromMinutes(90), Capacity = 4, MinimumAge = 16, PricePerPlayer = 12.50m, Status = GameStatus.Open },
            new { Id = gameFullId, CourtId = courtNorthId, StartAt = new DateTimeOffset(2027, 1, 15, 20, 0, 0, TimeSpan.FromHours(1)), Duration = TimeSpan.FromMinutes(120), Capacity = 2, MinimumAge = 18, PricePerPlayer = 15m, Status = GameStatus.Open },
            new { Id = gameClosedId, CourtId = courtCentralId, StartAt = new DateTimeOffset(2027, 1, 10, 20, 0, 0, TimeSpan.FromHours(1)), Duration = TimeSpan.FromMinutes(60), Capacity = 4, MinimumAge = 16, PricePerPlayer = 10m, Status = GameStatus.Closed });

        var registrationOpenAId = Guid.Parse("11111111-0001-0000-0000-000000000001");
        var registrationOpenBId = Guid.Parse("11111111-0001-0000-0000-000000000002");
        var registrationFullAId = Guid.Parse("11111111-0001-0000-0000-000000000003");
        var registrationFullBId = Guid.Parse("11111111-0001-0000-0000-000000000004");

        modelBuilder.Entity<Registration>().HasData(
            new { Id = registrationOpenAId, GameId = gameOpenId, PlayerId = playerMariaId, CreatedAt = new DateTimeOffset(2026, 12, 3, 10, 0, 0, TimeSpan.FromHours(1)), Amount = 12.50m, Status = RegistrationStatus.Confirmed },
            new { Id = registrationOpenBId, GameId = gameOpenId, PlayerId = playerPauId, CreatedAt = new DateTimeOffset(2026, 12, 4, 12, 0, 0, TimeSpan.FromHours(1)), Amount = 12.50m, Status = RegistrationStatus.Confirmed },
            new { Id = registrationFullAId, GameId = gameFullId, PlayerId = playerIreneId, CreatedAt = new DateTimeOffset(2026, 12, 5, 18, 0, 0, TimeSpan.FromHours(1)), Amount = 15m, Status = RegistrationStatus.Confirmed },
            new { Id = registrationFullBId, GameId = gameFullId, PlayerId = playerLeoId, CreatedAt = new DateTimeOffset(2026, 12, 5, 19, 0, 0, TimeSpan.FromHours(1)), Amount = 15m, Status = RegistrationStatus.Confirmed });
    }
}

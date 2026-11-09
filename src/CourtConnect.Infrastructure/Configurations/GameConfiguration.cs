using DeemSoft.CourtConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeemSoft.CourtConnect.Infrastructure;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");

        builder.HasKey(game => game.Id);
        builder.Property(game => game.Id).ValueGeneratedNever();

        builder.Property(game => game.StartAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(game => game.Duration)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(game => game.Capacity)
            .IsRequired();

        builder.Property(game => game.MinimumAge)
            .IsRequired();

        builder.Property(game => game.PricePerPlayer)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(game => game.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(game => game.Court)
            .WithMany()
            .HasForeignKey("CourtId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(game => game.Registrations)
            .WithOne()
            .HasForeignKey(registration => registration.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(game => game.Registrations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex("CourtId");
    }
}

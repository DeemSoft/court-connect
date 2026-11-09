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

        // La colección se gestiona dentro de Game; la relación se configura una sola vez en RegistrationConfiguration.
        builder.Ignore(game => game.Registrations);

        builder.HasOne(game => game.Court)
            .WithMany()
            .HasForeignKey("CourtId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex("CourtId");
    }
}

using DeemSoft.CourtConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeemSoft.CourtConnect.Infrastructure;

public sealed class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.ToTable("Registrations");

        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.Id).ValueGeneratedNever();

        builder.Property(registration => registration.CreatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(registration => registration.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(registration => registration.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // La restricción solo afecta a inscripciones activas; las canceladas no impiden volver a apuntarse.
        builder.HasIndex(registration => new { registration.GameId, registration.PlayerId })
            .IsUnique()
            .HasFilter("[Status] = 'Confirmed'");

        // Se conservan jugadores con historial; al borrar un partido se eliminan sus inscripciones.
        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(registration => registration.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(registration => registration.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

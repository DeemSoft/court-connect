using DeemSoft.CourtConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeemSoft.CourtConnect.Infrastructure;

public sealed class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.ToTable("Courts");

        builder.HasKey(court => court.Id);
        builder.Property(court => court.Id).ValueGeneratedNever();

        builder.Property(court => court.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(court => court.Location)
            .HasMaxLength(200)
            .IsRequired();
    }
}

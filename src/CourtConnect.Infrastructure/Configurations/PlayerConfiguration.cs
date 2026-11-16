using DeemSoft.CourtConnect.Domain;
using DeemSoft.CourtConnect.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeemSoft.CourtConnect.Infrastructure;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players");

        builder.HasKey(player => player.Id);
        builder.Property(player => player.Id).ValueGeneratedNever();

        builder.HasIndex(player => player.Nickname)
            .IsUnique();

        builder.HasIndex(player => player.IdentityUserId)
            .IsUnique()
            .HasFilter("[IdentityUserId] IS NOT NULL");

        builder.HasOne<CourtConnectUser>()
            .WithMany()
            .HasForeignKey(player => player.IdentityUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(player => player.Nickname)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(player => player.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(player => player.DateOfBirth)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(player => player.Email)
            .HasMaxLength(200)
            .IsRequired();
    }
}

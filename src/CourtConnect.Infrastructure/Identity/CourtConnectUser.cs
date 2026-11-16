using Microsoft.AspNetCore.Identity;

namespace DeemSoft.CourtConnect.Infrastructure.Identity;

public sealed class CourtConnectUser : IdentityUser<Guid>
{
}

public static class CourtConnectRoles
{
    public const string Organizer = "Organizer";
    public const string Player = "Player";
}

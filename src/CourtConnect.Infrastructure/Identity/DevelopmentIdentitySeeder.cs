using DeemSoft.CourtConnect.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeemSoft.CourtConnect.Infrastructure.Identity;

public static class DevelopmentIdentitySeeder
{
    public static async Task SeedAsync(
        CourtConnectDbContext context,
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<CourtConnectUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(roleManager, CourtConnectRoles.Organizer);
        await EnsureRoleAsync(roleManager, CourtConnectRoles.Player);

        var password = configuration["Identity:DevelopmentPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Development accounts were not seeded. Set Identity:DevelopmentPassword in User Secrets to enable them.");
            return;
        }

        var organizerEmail = configuration["Identity:DevelopmentOrganizerEmail"] ?? "organizer@example.com";
        var organizer = await FindOrCreateUserAsync(userManager, organizerEmail, password);
        await EnsureUserRoleAsync(userManager, organizer, CourtConnectRoles.Organizer);

        var players = await context.Players.ToListAsync(cancellationToken);
        foreach (var player in players)
        {
            var user = await FindOrCreateUserAsync(userManager, player.Email, password);
            await EnsureUserRoleAsync(userManager, user, CourtConnectRoles.Player);
            player.LinkAccount(user.Id);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        EnsureSucceeded(result, $"Could not create the {roleName} role.");
    }

    private static async Task<CourtConnectUser> FindOrCreateUserAsync(
        UserManager<CourtConnectUser> userManager,
        string email,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            return user;
        }

        user = new CourtConnectUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        EnsureSucceeded(result, "Could not create a development account.");
        return user;
    }

    private static async Task EnsureUserRoleAsync(
        UserManager<CourtConnectUser> userManager,
        CourtConnectUser user,
        string roleName)
    {
        if (await userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var result = await userManager.AddToRoleAsync(user, roleName);
        EnsureSucceeded(result, $"Could not assign the {roleName} role to a development account.");
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            var errorCodes = string.Join(", ", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"{message} Identity errors: {errorCodes}");
        }
    }
}

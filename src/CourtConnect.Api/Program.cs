using System.Security.Claims;
using System.Text.Json.Serialization;
using DeemSoft.CourtConnect.Domain;
using DeemSoft.CourtConnect.Infrastructure;
using DeemSoft.CourtConnect.Infrastructure.Identity;
using DeemSoft.CourtConnect.Infrastructure.Repositories;
using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// .NET 10 valida automáticamente los atributos de los DTOs en Minimal APIs.
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<CourtConnectDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("COURTCONNECT_CONNECTION_STRING")
        ?? throw new InvalidOperationException(
            "Configure ConnectionStrings:DefaultConnection with User Secrets or COURTCONNECT_CONNECTION_STRING.");

    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<ICourtRepository, EfCourtRepository>();
builder.Services.AddScoped<IGameRepository, EfGameRepository>();
builder.Services.AddScoped<IPlayerRepository, EfPlayerRepository>();
builder.Services.AddScoped<IRegistrationRepository, EfRegistrationRepository>();
builder.Services.AddScoped<CourtService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<RegistrationService>();

builder.Services.AddIdentityApiEndpoints<CourtConnectUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<CourtConnectDbContext>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CourtConnectRoles.Organizer, policy => policy.RequireRole(CourtConnectRoles.Organizer));
    options.AddPolicy(CourtConnectRoles.Player, policy => policy.RequireRole(CourtConnectRoles.Player));
});
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CourtConnect.Api.ApiExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
    await dbContext.Database.MigrateAsync();
    await DevelopmentIdentitySeeder.SeedAsync(
        dbContext,
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<CourtConnectUser>>(),
        app.Configuration,
        app.Logger);

    app.MapOpenApi();
}

app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { message = "CourtConnect API ready." }))
    .WithSummary("Check API availability");
app.MapGroup("/auth")
    .WithTags("Identity")
    .MapIdentityApi<CourtConnectUser>();
app.MapGet("/auth/me", async (
    ClaimsPrincipal user,
    UserManager<CourtConnectUser> userManager) =>
{
    var identityUser = await userManager.GetUserAsync(user)
        ?? throw new InvalidOperationException("The authenticated account could not be loaded.");
    var roles = await userManager.GetRolesAsync(identityUser);
    return TypedResults.Ok(new { email = identityUser.Email, roles });
})
    .WithTags("Identity")
    .WithSummary("Get the authenticated account and its roles")
    .RequireAuthorization();

// Cada grupo comparte el prefijo y la etiqueta que aparecerán en OpenAPI.
var courts = app.MapGroup("/courts").WithTags("Courts");
courts.MapGet("", async (CourtService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetAllAsync(cancellationToken)))
    .WithName("GetCourts")
    .WithSummary("List courts for organizers")
    .RequireAuthorization(CourtConnectRoles.Organizer);
courts.MapPost("", async (CreateCourtRequest request, CourtService service, CancellationToken cancellationToken) =>
{
    var court = await service.CreateAsync(request, cancellationToken);
    return TypedResults.Created($"/courts/{court.Id}", court);
})
    .WithName("CreateCourt")
    .WithSummary("Create a court")
    .RequireAuthorization(CourtConnectRoles.Organizer);
courts.MapPut("/{id:guid}", async (
    Guid id,
    UpdateCourtRequest request,
    CourtService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.UpdateAsync(id, request, cancellationToken)))
    .WithName("UpdateCourt")
    .WithSummary("Update a court")
    .RequireAuthorization(CourtConnectRoles.Organizer);
courts.MapDelete("/{id:guid}", async (
    Guid id,
    CourtService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteAsync(id, cancellationToken);
    return TypedResults.NoContent();
})
    .WithName("DeleteCourt")
    .WithSummary("Delete a court when it has no associated games")
    .RequireAuthorization(CourtConnectRoles.Organizer);

var games = app.MapGroup("/games").WithTags("Games");
games.MapGet("/mine", async (
    ClaimsPrincipal user,
    GameService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByOrganizerUserIdAsync(GetIdentityUserId(user), cancellationToken)))
    .WithName("GetMyOrganizedGames")
    .WithSummary("List games owned by the authenticated organizer")
    .RequireAuthorization(CourtConnectRoles.Organizer);
games.MapGet("", async (GameService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetOpenUpcomingAsync(cancellationToken)))
    .WithName("GetOpenUpcomingGames")
    .WithSummary("List open upcoming games");
games.MapGet("/{id:guid}", async (Guid id, GameService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByIdAsync(id, cancellationToken)))
    .WithName("GetGameById")
    .WithSummary("Get a game by ID");
games.MapPost("", async (
    CreateGameRequest request,
    ClaimsPrincipal user,
    GameService service,
    CancellationToken cancellationToken) =>
{
    var game = await service.CreateAsync(request, GetIdentityUserId(user), cancellationToken);
    return TypedResults.Created($"/games/{game.Id}", game);
})
    .WithName("CreateGame")
    .WithSummary("Create a game owned by the authenticated organizer")
    .RequireAuthorization(CourtConnectRoles.Organizer);
games.MapPut("/{id:guid}", async (
    Guid id,
    UpdateGameRequest request,
    ClaimsPrincipal user,
    GameService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.UpdateAsync(id, GetIdentityUserId(user), request, cancellationToken)))
    .WithName("UpdateGame")
    .WithSummary("Update a game owned by the authenticated organizer")
    .RequireAuthorization(CourtConnectRoles.Organizer);
games.MapPut("/{id:guid}/status", async (
    Guid id,
    UpdateGameStatusRequest request,
    ClaimsPrincipal user,
    GameService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.SetStatusAsync(
        id,
        GetIdentityUserId(user),
        request.Status,
        cancellationToken)))
    .WithName("SetGameStatus")
    .WithSummary("Open or close a game owned by the authenticated organizer")
    .RequireAuthorization(CourtConnectRoles.Organizer);
games.MapGet("/{id:guid}/participants", async (
    Guid id,
    ClaimsPrincipal user,
    RegistrationService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetPlayersByGameIdAsync(
        id,
        GetIdentityUserId(user),
        cancellationToken)))
    .WithName("GetGameParticipants")
    .WithSummary("List only participant nicknames and names for the authenticated organizer's game")
    .RequireAuthorization(CourtConnectRoles.Organizer);

var players = app.MapGroup("/players").WithTags("Players");
players.MapPost("/me", async (
    CreatePlayerProfileRequest request,
    ClaimsPrincipal user,
    UserManager<CourtConnectUser> userManager,
    PlayerService service,
    CancellationToken cancellationToken) =>
{
    var identityUser = await userManager.GetUserAsync(user)
        ?? throw new InvalidOperationException("The authenticated account could not be loaded.");
    if (!await userManager.IsInRoleAsync(identityUser, CourtConnectRoles.Player))
    {
        var roleResult = await userManager.AddToRoleAsync(identityUser, CourtConnectRoles.Player);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException("The Player role could not be assigned to the account.");
        }
    }

    var player = await service.CreateForIdentityAsync(
        GetIdentityUserId(user),
        identityUser.Email ?? throw new InvalidOperationException("The authenticated account has no email address."),
        request,
        cancellationToken);
    return TypedResults.Created("/players/me", player);
})
    .WithName("CreateMyPlayerProfile")
    .WithSummary("Create a player profile for the authenticated account")
    .RequireAuthorization();
players.MapGet("/me", async (
    ClaimsPrincipal user,
    PlayerService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByIdentityUserIdAsync(GetIdentityUserId(user), cancellationToken)))
    .WithName("GetMyPlayerProfile")
    .WithSummary("Get the player profile linked to the authenticated account")
    .RequireAuthorization();
players.MapGet("", async (PlayerService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetAllAsync(cancellationToken)))
    .RequireAuthorization(CourtConnectRoles.Organizer);
players.MapGet("/{id:guid}", async (Guid id, PlayerService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByIdAsync(id, cancellationToken)))
    .RequireAuthorization(CourtConnectRoles.Organizer);

var gameRegistrations = app.MapGroup("/games/{gameId:guid}/registrations").WithTags("Registrations");
gameRegistrations.MapPost("", async (
    Guid gameId,
    ClaimsPrincipal user,
    RegistrationService service,
    CancellationToken cancellationToken) =>
{
    var registration = await service.RegisterAsync(gameId, GetIdentityUserId(user), cancellationToken);
    return TypedResults.Created($"/registrations/{registration.Id}", registration);
})
    .WithName("RegisterForGame")
    .WithSummary("Register the authenticated player for a game")
    .RequireAuthorization(CourtConnectRoles.Player);
gameRegistrations.MapGet("", async (
    Guid gameId,
    RegistrationService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByGameIdAsync(gameId, cancellationToken)))
    .RequireAuthorization();

var registrations = app.MapGroup("/registrations").WithTags("Registrations");
registrations.MapGet("/mine", async (
    ClaimsPrincipal user,
    RegistrationService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByIdentityUserIdAsync(GetIdentityUserId(user), cancellationToken)))
    .WithName("GetMyRegistrations")
    .WithSummary("List the authenticated player's active game registrations")
    .RequireAuthorization(CourtConnectRoles.Player);
registrations.MapPost("/{registrationId:guid}/cancel", async (
    Guid registrationId,
    ClaimsPrincipal user,
    RegistrationService service,
    CancellationToken cancellationToken) =>
{
    await service.CancelAsync(registrationId, GetIdentityUserId(user), cancellationToken);
    return TypedResults.NoContent();
})
    .WithName("CancelMyRegistration")
    .WithSummary("Cancel a registration owned by the authenticated player")
    .RequireAuthorization(CourtConnectRoles.Player);

app.Run();

static Guid GetIdentityUserId(ClaimsPrincipal user)
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(userId, out var identityUserId)
        ? identityUserId
        : throw new InvalidOperationException("The authenticated account has no valid identifier.");
}

public partial class Program;

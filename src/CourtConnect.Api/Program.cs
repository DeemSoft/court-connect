using DeemSoft.CourtConnect.Infrastructure;
using DeemSoft.CourtConnect.Infrastructure.Repositories;
using DeemSoft.CourtConnect.Application.Abstractions;
using DeemSoft.CourtConnect.Application.Dtos;
using DeemSoft.CourtConnect.Application.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// .NET 10 valida automáticamente los atributos de los DTOs en Minimal APIs.
builder.Services.AddValidation();
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

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CourtConnect.Api.ApiExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { message = "CourtConnect API ready." }));

// Cada grupo comparte el prefijo y la etiqueta que aparecerán en OpenAPI.
var courts = app.MapGroup("/courts").WithTags("Courts");
courts.MapPost("", async (CreateCourtRequest request, CourtService service, CancellationToken cancellationToken) =>
{
    var court = await service.CreateAsync(request, cancellationToken);
    return TypedResults.Created($"/courts/{court.Id}", court);
});

var games = app.MapGroup("/games").WithTags("Games");
games.MapGet("", async (GameService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetOpenUpcomingAsync(cancellationToken)));
games.MapGet("/{id:guid}", async (Guid id, GameService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByIdAsync(id, cancellationToken)));
games.MapPost("", async (CreateGameRequest request, GameService service, CancellationToken cancellationToken) =>
{
    var game = await service.CreateAsync(request, cancellationToken);
    return TypedResults.Created($"/games/{game.Id}", game);
});

var players = app.MapGroup("/players").WithTags("Players");
players.MapPost("", async (CreatePlayerRequest request, PlayerService service, CancellationToken cancellationToken) =>
{
    var player = await service.CreateAsync(request, cancellationToken);
    return TypedResults.Created($"/players/{player.Id}", player);
});
players.MapGet("", async (PlayerService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetAllAsync(cancellationToken)));
players.MapGet("/{id:guid}", async (Guid id, PlayerService service, CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByIdAsync(id, cancellationToken)));

var gameRegistrations = app.MapGroup("/games/{gameId:guid}/registrations").WithTags("Registrations");
gameRegistrations.MapPost("", async (
    Guid gameId,
    CreateRegistrationRequest request,
    RegistrationService service,
    CancellationToken cancellationToken) =>
{
    // El identificador nullable permite que la validación del DTO responda 400 si falta en el JSON.
    var playerId = request.PlayerId
        ?? throw new ArgumentException("A player id is required.", nameof(request.PlayerId));
    var registration = await service.RegisterAsync(gameId, playerId, cancellationToken);
    return TypedResults.Created($"/registrations/{registration.Id}", registration);
});
gameRegistrations.MapGet("", async (
    Guid gameId,
    RegistrationService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.GetByGameIdAsync(gameId, cancellationToken)));

var registrations = app.MapGroup("/registrations").WithTags("Registrations");
registrations.MapPost("/{registrationId:guid}/cancel", async (
    Guid registrationId,
    RegistrationService service,
    CancellationToken cancellationToken) =>
{
    await service.CancelAsync(registrationId, cancellationToken);
    return TypedResults.NoContent();
});

app.Run();

public partial class Program;

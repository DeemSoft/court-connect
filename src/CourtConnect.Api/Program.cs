using DeemSoft.CourtConnect.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CourtConnectDbContext>(options =>
{
    // User Secrets y la variable de entorno permiten configurar cada equipo sin guardar credenciales en el repositorio.
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("COURTCONNECT_CONNECTION_STRING")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=CourtConnect;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    options.UseSqlServer(connectionString);
});

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { message = "CourtConnect API ready." }));

app.Run();

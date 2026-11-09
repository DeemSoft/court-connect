using DeemSoft.CourtConnect.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace DeemSoft.CourtConnect.IntegrationTests;

public sealed class SqlServerApiFixture : IAsyncLifetime
{
    // Un único contenedor por colección evita arrancar SQL Server para cada prueba.
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Integration!Test123")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // La API de pruebas apunta al contenedor, no a los secretos ni a la base local del desarrollador.
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _sqlServer.GetConnectionString()
                    });
                });
            });

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CourtConnectDbContext>();
        // Aplicar migraciones asegura que las pruebas ejerciten el mismo esquema que usa la aplicación.
        await dbContext.Database.MigrateAsync();
    }

    public AsyncServiceScope CreateScope()
    {
        return Factory.Services.CreateAsyncScope();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _sqlServer.DisposeAsync();
    }
}

// Las pruebas comparten la base temporal; se ejecutan en serie para evitar interferencias entre escrituras.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerApiCollection : ICollectionFixture<SqlServerApiFixture>
{
    public const string Name = "SQL Server API";
}

using System.Data.Common;
using System.Net;
using System.Text.Json;
using CourtConnect.Api;
using DeemSoft.CourtConnect.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeemSoft.CourtConnect.IntegrationTests;

public sealed class ApiExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData(404, "No se encontró el recurso")]
    [InlineData(409, "Conflicto")]
    [InlineData(400, "Solicitud no válida")]
    public async Task InvokeAsync_WhenKnownErrorOccurs_WritesProblemDetails(int status, string expectedTitle)
    {
        Exception exception = status switch
        {
            404 => new EntityNotFoundException("Game", Guid.NewGuid()),
            409 => new ConflictException("La inscripción entra en conflicto con el estado del partido."),
            _ => new ArgumentException("La solicitud no es válida.")
        };
        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ApiExceptionHandlingMiddleware>.Instance);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        AssertProblemDetails(context.Response.Body, status, expectedTitle);
    }

    [Fact]
    public async Task InvokeAsync_WhenUniqueDatabaseConstraintFails_Returns409()
    {
        // Simula el código de error de SQL Server sin necesitar otra conexión para esta prueba unitaria del middleware.
        var exception = new DbUpdateException("Unique index violation.", new UniqueConstraintException());
        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ApiExceptionHandlingMiddleware>.Instance);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        AssertProblemDetails(context.Response.Body, (int)HttpStatusCode.Conflict, "Conflicto");
    }

    [Fact]
    public async Task InvokeAsync_WhenAnotherDatabaseErrorOccurs_PropagatesException()
    {
        var exception = new DbUpdateException("Database unavailable.");
        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ApiExceptionHandlingMiddleware>.Instance);
        var context = CreateContext();

        var actual = await Assert.ThrowsAsync<DbUpdateException>(() => middleware.InvokeAsync(context));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseAlreadyStarted_ThrowsInsteadOfWritingAnotherResponse()
    {
        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw new ArgumentException("Entrada no válida."),
            NullLogger<ApiExceptionHandlingMiddleware>.Instance);
        var context = CreateContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/test";
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        return context;
    }

    private static void AssertProblemDetails(Stream responseBody, int expectedStatus, string expectedTitle)
    {
        responseBody.Position = 0;
        using var document = JsonDocument.Parse(responseBody);
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, document.RootElement.GetProperty("title").GetString());
        Assert.Equal("/test", document.RootElement.GetProperty("instance").GetString());
    }

    private sealed class UniqueConstraintException : DbException
    {
        // SQL Server devuelve 2601 cuando se viola un índice único.
        public override int ErrorCode => 2601;
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}

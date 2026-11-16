using System.Data.Common;
using System.Diagnostics;
using DeemSoft.CourtConnect.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CourtConnect.Api;

public sealed class ApiExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        // Se convierten solo errores conocidos a respuestas HTTP; los fallos inesperados deben seguir siendo errores 500.
        catch (EntityNotFoundException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "No se encontró el recurso", exception.Message);
        }
        catch (ConflictException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflicto", exception.Message);
        }
        catch (ForbiddenException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Acceso denegado", exception.Message);
        }
        catch (ArgumentException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Solicitud no válida", exception.Message);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            logger.LogWarning(exception, "A request conflicted with a unique database constraint.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Conflicto",
                "La solicitud entra en conflicto con un recurso existente.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        // SQL Server usa estos códigos para índices únicos y claves primarias duplicadas.
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 }
            || exception.GetBaseException() is DbException { ErrorCode: 2601 or 2627 };
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("The response has already started.");
        }

        context.Response.StatusCode = status;
        // El traceId permite relacionar el ProblemDetails con los registros del servidor.
        await Results.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier })
            .ExecuteAsync(context);
    }
}

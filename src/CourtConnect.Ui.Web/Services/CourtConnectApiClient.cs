using System.Net;
using System.Net.Http.Json;
using DeemSoft.CourtConnect.Ui.Components.Api;

namespace DeemSoft.CourtConnect.Ui.Web.Services;

public sealed class CourtConnectApiClient(HttpClient httpClient) : ICourtConnectApiClient
{
    public async Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "games");
        using var response = await SendAsync(request, cancellationToken);
        var games = await ReadJsonAsync<List<GameDto>>(response, cancellationToken);
        return games;
    }

    public async Task<GameDto> GetGameAsync(Guid gameId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<GameDto>(response, cancellationToken);
    }

    public async Task RegisterAccountAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/register")
        {
            Content = JsonContent.Create(new { Email = email, Password = password })
        };
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IdentityTokenResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/login?useCookies=false")
        {
            Content = JsonContent.Create(new { Email = email, Password = password })
        };
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<IdentityTokenResponse>(response, cancellationToken);
    }

    public async Task<IdentityTokenResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/refresh")
        {
            Content = JsonContent.Create(new { RefreshToken = refreshToken })
        };
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<IdentityTokenResponse>(response, cancellationToken);
    }

    public async Task<CurrentAccountDto> GetCurrentAccountAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<CurrentAccountDto>(response, cancellationToken);
    }

    public async Task<CurrentAccountDto> GetMyAccountAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/me");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<CurrentAccountDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<CourtDto>> GetCourtsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "courts");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<List<CourtDto>>(response, cancellationToken);
    }

    public async Task<CourtDto> CreateCourtAsync(
        CreateCourtRequestDto request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "courts")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await SendAsync(message, cancellationToken);
        return await ReadJsonAsync<CourtDto>(response, cancellationToken);
    }

    public async Task<CourtDto> UpdateCourtAsync(
        Guid courtId,
        UpdateCourtRequestDto request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"courts/{courtId}")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await SendAsync(message, cancellationToken);
        return await ReadJsonAsync<CourtDto>(response, cancellationToken);
    }

    public async Task DeleteCourtAsync(Guid courtId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"courts/{courtId}");
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<GameDto>> GetMyOrganizedGamesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "games/mine");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<List<GameDto>>(response, cancellationToken);
    }

    public async Task<GameDto> CreateGameAsync(
        CreateGameRequestDto request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "games")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await SendAsync(message, cancellationToken);
        return await ReadJsonAsync<GameDto>(response, cancellationToken);
    }

    public async Task<GameDto> UpdateGameAsync(
        Guid gameId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"games/{gameId}")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await SendAsync(message, cancellationToken);
        return await ReadJsonAsync<GameDto>(response, cancellationToken);
    }

    public async Task<GameDto> SetGameStatusAsync(
        Guid gameId,
        string status,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"games/{gameId}/status")
        {
            Content = JsonContent.Create(new { Status = status })
        };
        using var response = await SendAsync(message, cancellationToken);
        return await ReadJsonAsync<GameDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<GameParticipantDto>> GetGameParticipantsAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/participants");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<List<GameParticipantDto>>(response, cancellationToken);
    }

    public async Task<PlayerDto> GetMyProfileAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "players/me");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<PlayerDto>(response, cancellationToken);
    }

    public async Task<PlayerDto> CreateMyProfileAsync(
        CreatePlayerProfileRequest profile,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "players/me")
        {
            Content = JsonContent.Create(profile)
        };
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<PlayerDto>(response, cancellationToken);
    }

    public async Task<RegistrationDto> RegisterAsync(Guid gameId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/registrations");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<RegistrationDto>(response, cancellationToken);
    }

    public async Task CancelRegistrationAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"registrations/{registrationId}/cancel");
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerRegistrationDto>> GetMyRegistrationsAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "registrations/mine");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<List<PlayerRegistrationDto>>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new CourtConnectApiException(
                HttpStatusCode.ServiceUnavailable,
                "No se pudo conectar con la API. Comprueba que esté en ejecución e inténtalo de nuevo.",
                exception);
        }
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta JSON vacía.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiProblemDetails? problem = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(
                cancellationToken: cancellationToken);
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.NotFound => "No se encontró el recurso solicitado.",
            HttpStatusCode.Conflict => "No se pudo completar la acción porque el recurso ha cambiado o ya no está disponible.",
            HttpStatusCode.BadRequest => "La solicitud contiene datos no válidos.",
            HttpStatusCode.Unauthorized => "Inicia sesión para continuar o comprueba tus credenciales.",
            HttpStatusCode.Forbidden => "Tu cuenta no tiene permisos para realizar esta acción.",
            _ => $"La API no pudo completar la solicitud (HTTP {(int)response.StatusCode})."
        };

        if (!string.IsNullOrWhiteSpace(problem?.Detail))
        {
            message = $"{message} Detalle: {problem.Detail}";
        }
        else if (problem?.Errors is not null)
        {
            var validationErrors = problem.Errors
                .SelectMany(error => error.Value.Select(message => $"{error.Key}: {message}"));
            message = $"{message} {string.Join(" ", validationErrors)}";
        }

        throw new CourtConnectApiException(response.StatusCode, message);
    }

    private sealed record ApiProblemDetails(
        string? Title,
        string? Detail,
        Dictionary<string, string[]>? Errors);
}

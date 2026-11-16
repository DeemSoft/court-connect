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

    public async Task<IReadOnlyList<PlayerOptionDto>> GetPlayersAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "players");
        using var response = await SendAsync(request, cancellationToken);
        var players = await ReadJsonAsync<List<PlayerOptionDto>>(response, cancellationToken);
        return players;
    }

    public async Task<RegistrationDto> RegisterAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/registrations")
        {
            Content = JsonContent.Create(new { PlayerId = playerId })
        };
        using var response = await SendAsync(request, cancellationToken);
        return await ReadJsonAsync<RegistrationDto>(response, cancellationToken);
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
            _ => $"La API no pudo completar la solicitud (HTTP {(int)response.StatusCode})."
        };

        if (!string.IsNullOrWhiteSpace(problem?.Detail))
        {
            message = $"{message} Detalle: {problem.Detail}";
        }

        throw new CourtConnectApiException(response.StatusCode, message);
    }

    private sealed record ApiProblemDetails(string? Title, string? Detail);
}

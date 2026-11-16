using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace DeemSoft.CourtConnect.Ui.Web.Services;

public sealed class AccessTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            string? accessToken;
            if (httpContextAccessor.HttpContext is { } context)
            {
                accessToken = await context.GetTokenAsync("access_token");
            }
            else
            {
                var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
                accessToken = authenticationState.User.FindFirstValue(CourtConnectClaimTypes.AccessToken);
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

internal static class CourtConnectClaimTypes
{
    public const string AccessToken = "courtconnect:access_token";
}

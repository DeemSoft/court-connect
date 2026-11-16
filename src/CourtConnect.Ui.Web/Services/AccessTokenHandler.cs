using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace DeemSoft.CourtConnect.Ui.Web.Services;

public sealed class AccessTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null && httpContextAccessor.HttpContext is { } context)
        {
            var accessToken = await context.GetTokenAsync("access_token");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

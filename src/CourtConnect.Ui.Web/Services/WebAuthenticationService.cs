using System.Security.Claims;
using DeemSoft.CourtConnect.Ui.Components.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DeemSoft.CourtConnect.Ui.Web.Services;

public sealed class WebAuthenticationService(
    ICourtConnectApiClient apiClient,
    IHttpContextAccessor httpContextAccessor) : IWebAuthenticationService
{
    public async Task SignInAsync(
        string email,
        string password,
        bool isPersistent,
        CancellationToken cancellationToken)
    {
        var tokens = await apiClient.LoginAsync(email, password, cancellationToken);
        var account = await apiClient.GetCurrentAccountAsync(tokens.AccessToken, cancellationToken);
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The HTTP context is not available during sign-in.");
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.Email ?? email),
            new(ClaimTypes.Email, account.Email ?? email),
            new(CourtConnectClaimTypes.AccessToken, tokens.AccessToken)
        };
        claims.AddRange(account.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn)
        };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken }
        ]);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The HTTP context is not available during token refresh.");
        var refreshToken = await context.GetTokenAsync("refresh_token")
            ?? throw new InvalidOperationException("The authentication session has no refresh token.");
        var tokens = await apiClient.RefreshAsync(refreshToken, cancellationToken);
        var account = await apiClient.GetCurrentAccountAsync(tokens.AccessToken, cancellationToken);
        var authentication = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            throw new InvalidOperationException("The web authentication session could not be restored.");
        }

        if (authentication.Principal.Identity is not ClaimsIdentity identity)
        {
            throw new InvalidOperationException("The web authentication identity could not be updated.");
        }

        foreach (var roleClaim in identity.FindAll(ClaimTypes.Role).ToArray())
        {
            identity.RemoveClaim(roleClaim);
        }

        foreach (var role in account.Roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var accessTokenClaim in identity.FindAll(CourtConnectClaimTypes.AccessToken).ToArray())
        {
            identity.RemoveClaim(accessTokenClaim);
        }

        identity.AddClaim(new Claim(CourtConnectClaimTypes.AccessToken, tokens.AccessToken));

        var properties = authentication.Properties ?? new AuthenticationProperties();
        properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken }
        ]);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authentication.Principal,
            properties);
    }

    public async Task SignOutAsync()
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The HTTP context is not available during sign-out.");
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}

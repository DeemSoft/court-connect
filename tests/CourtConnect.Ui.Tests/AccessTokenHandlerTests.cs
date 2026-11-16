using System.Security.Claims;
using DeemSoft.CourtConnect.Ui.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CourtConnect.Ui.Tests;

public sealed class AccessTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsTheAccessTokenStoredInTheWebAuthenticationTicket()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "access-token" }]);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            properties,
            "test");
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuthenticationService(ticket))
            .BuildServiceProvider();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };
        var innerHandler = new CapturingHandler();
        using var handler = new AccessTokenHandler(accessor) { InnerHandler = innerHandler };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        using var response = await client.GetAsync("players/me");

        Assert.Equal("Bearer", innerHandler.Authorization?.Scheme);
        Assert.Equal("access-token", innerHandler.Authorization?.Parameter);
    }

    private sealed class FakeAuthenticationService(AuthenticationTicket ticket) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

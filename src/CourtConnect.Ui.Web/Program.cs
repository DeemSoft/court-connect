using DeemSoft.CourtConnect.Ui.Components.Api;
using DeemSoft.CourtConnect.Ui.Components.Pages;
using DeemSoft.CourtConnect.Ui.Web.Components;
using DeemSoft.CourtConnect.Ui.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CourtConnect.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddTransient<AccessTokenHandler>();
builder.Services.AddScoped<IWebAuthenticationService, WebAuthenticationService>();

var apiBaseUrl = builder.Configuration["CourtConnectApi:BaseUrl"]
    ?? throw new InvalidOperationException("Configure CourtConnectApi:BaseUrl.");
builder.Services.AddHttpClient<ICourtConnectApiClient, CourtConnectApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
}).AddHttpMessageHandler<AccessTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(Games).Assembly);

app.Run();

public partial class Program;

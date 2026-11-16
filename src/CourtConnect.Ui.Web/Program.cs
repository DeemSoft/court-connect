using DeemSoft.CourtConnect.Ui.Components.Api;
using DeemSoft.CourtConnect.Ui.Components.Pages;
using DeemSoft.CourtConnect.Ui.Web.Components;
using DeemSoft.CourtConnect.Ui.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var apiBaseUrl = builder.Configuration["CourtConnectApi:BaseUrl"]
    ?? throw new InvalidOperationException("Configure CourtConnectApi:BaseUrl.");
builder.Services.AddHttpClient<ICourtConnectApiClient, CourtConnectApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(Games).Assembly);

app.Run();

public partial class Program;

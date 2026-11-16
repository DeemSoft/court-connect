using Bunit;
using Bunit.TestDoubles;
using DeemSoft.CourtConnect.Ui.Web.Components.Layout;

namespace CourtConnect.Ui.Tests;

public sealed class OrganizerNavigationTests : BunitContext
{
    [Fact]
    public void PlayerDoesNotSeeOrganizerNavigation()
    {
        var authorization = AddAuthorization();
        authorization.SetAuthorized("player@example.com");
        authorization.SetRoles("Player");

        var navigation = Render<MainLayout>();

        Assert.DoesNotContain("Panel organizador", navigation.Markup);
        Assert.DoesNotContain(">Pistas<", navigation.Markup);
    }

    [Fact]
    public void OrganizerSeesOrganizerNavigation()
    {
        var authorization = AddAuthorization();
        authorization.SetAuthorized("organizer@example.com");
        authorization.SetRoles("Organizer");

        var navigation = Render<MainLayout>();

        Assert.Contains("Panel organizador", navigation.Markup);
        Assert.Contains(">Pistas<", navigation.Markup);
    }

}

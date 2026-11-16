using Bunit;
using DeemSoft.CourtConnect.Ui.Web.Components.Pages;

namespace CourtConnect.Ui.Tests;

public sealed class ErrorPageTests : BunitContext
{
    [Fact]
    public void Error_ShowsSafeMessageAndReturnLink()
    {
        var page = Render<Error>();

        Assert.Contains("Ha ocurrido un error", page.Markup);
        Assert.Contains("Volver a los partidos", page.Markup);
        Assert.DoesNotContain("Exception", page.Markup);
    }

    [Fact]
    public void NotFound_ShowsSpanishMessageAndReturnLink()
    {
        var page = Render<NotFound>();

        Assert.Contains("No se encontró la página", page.Markup);
        Assert.Contains("Ver partidos abiertos", page.Markup);
    }
}

using FluentAssertions;
using MudBlazor.Utilities;
using StatusTracker.Infrastructure;

namespace StatusTracker.Tests.Unit;

/// <summary>
/// Tests for ThemeFactory.Build and the internal DarkenColor logic (observable via the
/// PrimaryDarken property of the returned theme).
/// </summary>
[Trait("Category", "Unit")]
public class ThemeFactoryTests
{
    // ── Build ────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_DefaultColor_ReturnsMudThemeWithDefaultBlue()
    {
        var theme = ThemeFactory.Build();

        // Primary is MudColor — compare via hex format
        theme.PaletteLight.Primary.ToString(MudColorOutputFormats.Hex).Should().Be("#3d6ce7");
    }

    [Fact]
    public void Build_CustomAccentColor_SetsLightPalettePrimary()
    {
        var theme = ThemeFactory.Build("#ff5733");

        theme.PaletteLight.Primary.ToString(MudColorOutputFormats.Hex).Should().Be("#ff5733");
    }

    [Fact]
    public void Build_CustomAccentColor_SetsDarkPalettePrimary()
    {
        var theme = ThemeFactory.Build("#ff5733");

        theme.PaletteDark.Primary.ToString(MudColorOutputFormats.Hex).Should().Be("#ff5733");
    }

    [Fact]
    public void Build_CustomAccentColor_SetsPrimaryDarkenToDarkenedVersion()
    {
        // #ff5733: R=255*0.8=204, G=87*0.8=69, B=51*0.8=40
        // MudColor.ToString() uses rgb() format: "rgb(204,69,40)"
        var theme = ThemeFactory.Build("#ff5733");

        theme.PaletteLight.PrimaryDarken.Should().Be("rgb(204,69,40)");
    }

    [Fact]
    public void Build_DefaultColor_PrimaryDarkenIsNotEqualToPrimary()
    {
        var theme = ThemeFactory.Build();

        // The darkened version must differ from the primary (both in rgb() notation)
        theme.PaletteLight.PrimaryDarken.Should().NotBe(theme.PaletteLight.Primary.ToString());
    }

    [Fact]
    public void Build_ReturnsThemeWithExpectedTypographyFontFamilies()
    {
        var theme = ThemeFactory.Build();

        theme.Typography.Default.FontFamily.Should().Contain("DM Sans");
        theme.Typography.H1.FontFamily.Should().Contain("Bricolage Grotesque");
    }

    [Fact]
    public void Build_ReturnsThemeWithExpectedBorderRadius()
    {
        var theme = ThemeFactory.Build();

        theme.LayoutProperties.DefaultBorderRadius.Should().Be("10px");
    }

    // ── DarkenColor — #RGB expansion ─────────────────────────────────────────

    [Fact]
    public void Build_ThreeCharHexColor_ExpandsAndDarkens()
    {
        // #fff expands to #ffffff → R=255*0.8=204, G=204, B=204 → rgb(204,204,204)
        var theme = ThemeFactory.Build("#fff");

        theme.PaletteLight.PrimaryDarken.Should().Be("rgb(204,204,204)");
    }

    [Fact]
    public void Build_ThreeCharBlackHexColor_ExpandsAndDarkens()
    {
        // #000 expands to #000000 → R=0, G=0, B=0 → rgb(0,0,0)
        var theme = ThemeFactory.Build("#000");

        theme.PaletteLight.PrimaryDarken.Should().Be("rgb(0,0,0)");
    }

    // ── DarkenColor — #RRGGBB ────────────────────────────────────────────────

    [Fact]
    public void Build_SixCharHexColor_DarkensBy20Percent()
    {
        // #64c864: R=100*0.8=80, G=200*0.8=160, B=100*0.8=80 → rgb(80,160,80)
        var theme = ThemeFactory.Build("#64c864");

        theme.PaletteLight.PrimaryDarken.Should().Be("rgb(80,160,80)");
    }

    [Fact]
    public void Build_BlackColor_DarkenedColorRemainsBlack()
    {
        // #000000 * 0.8 = (0,0,0) → rgb(0,0,0)
        var theme = ThemeFactory.Build("#000000");

        theme.PaletteLight.PrimaryDarken.Should().Be("rgb(0,0,0)");
    }

    [Fact]
    public void Build_WhiteColor_DarkensToGray()
    {
        // #ffffff: 255*0.8=204 → rgb(204,204,204)
        var theme = ThemeFactory.Build("#ffffff");

        theme.PaletteLight.PrimaryDarken.Should().Be("rgb(204,204,204)");
    }
}

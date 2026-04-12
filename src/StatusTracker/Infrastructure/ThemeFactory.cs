using MudBlazor;

namespace StatusTracker.Infrastructure;

public static class ThemeFactory
{
    public static MudTheme Build(string accentColor = "#3d6ce7")
    {
        var accentHover = DarkenColor(accentColor);

        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = accentColor,
                PrimaryDarken = accentHover,

                Success = "#1a9a45",
                Warning = "#d4910a",
                Error = "#d63031",
                Info = "#8b92a0",

                Background = "#f4f6f9",
                Surface = "#ffffff",
                DrawerBackground = "#ffffff",
                AppbarBackground = "#ffffff",
                AppbarText = "#1a1d23",

                TextPrimary = "#1a1d23",
                TextSecondary = "#4b5060",
                TextDisabled = "#8b92a0",

                ActionDefault = "#1a1d23",
                ActionDisabled = "#8b92a0",

                Divider = "#e0e4eb",
                DividerLight = "#f0f2f6",

                TableLines = "#e8ecf1",
                TableHover = "#f8f9fb",

                DrawerText = "#1a1d23",
                DrawerIcon = "#4b5060",
            },
            PaletteDark = new PaletteDark
            {
                Primary = accentColor,
                Success = "#1a9a45",
                Warning = "#d4910a",
                Error = "#d63031",
                Background = "#0f1117",
                Surface = "#1a1d23",
                AppbarBackground = "#1a1d23",
                DrawerBackground = "#1a1d23",
                TextPrimary = "#f0f2f6",
                TextSecondary = "#9ba3b2",
                TextDisabled = "#5c6370",
                Divider = "#2a2e38",
                TableHover = "#21242c",
            },
            Typography = new Typography
            {
                Default = new DefaultTypography
                {
                    FontFamily = new[] { "DM Sans", "sans-serif" },
                    FontSize = "1rem",
                    LineHeight = "1.5",
                },
                H1 = new H1Typography
                {
                    FontFamily = new[] { "Bricolage Grotesque", "sans-serif" },
                    FontSize = "clamp(2rem, 1.5rem + 2.5vw, 3.5rem)",
                    FontWeight = "800",
                    LetterSpacing = "-0.02em",
                },
                H2 = new H2Typography
                {
                    FontFamily = new[] { "Bricolage Grotesque", "sans-serif" },
                    FontSize = "clamp(1.5rem, 1.1rem + 1.8vw, 2.5rem)",
                    FontWeight = "800",
                    LetterSpacing = "-0.02em",
                },
                H3 = new H3Typography
                {
                    FontFamily = new[] { "Bricolage Grotesque", "sans-serif" },
                    FontSize = "clamp(1.25rem, 1rem + 1vw, 1.75rem)",
                    FontWeight = "700",
                    LetterSpacing = "-0.01em",
                },
                H6 = new H6Typography
                {
                    FontFamily = new[] { "Bricolage Grotesque", "sans-serif" },
                    FontSize = "clamp(1rem, 0.9rem + 0.5vw, 1.25rem)",
                    FontWeight = "700",
                    LetterSpacing = "-0.01em",
                },
                Caption = new CaptionTypography
                {
                    FontFamily = new[] { "JetBrains Mono", "monospace" },
                    FontSize = "clamp(0.7rem, 0.65rem + 0.25vw, 0.8rem)",
                    LineHeight = "1.2",
                },
                Overline = new OverlineTypography
                {
                    FontFamily = new[] { "JetBrains Mono", "monospace" },
                    FontSize = "clamp(0.7rem, 0.65rem + 0.25vw, 0.8rem)",
                    LetterSpacing = "0.12em",
                },
                Body2 = new Body2Typography
                {
                    FontFamily = new[] { "JetBrains Mono", "monospace" },
                    FontSize = "clamp(0.8rem, 0.75rem + 0.25vw, 0.875rem)",
                },
            },
            Shadows = new Shadow
            {
                Elevation = new[]
                {
                    "none",
                    "0 1px 2px rgba(0,0,0,0.04), 0 1px 4px rgba(0,0,0,0.03)",
                    "0 1px 3px rgba(0,0,0,0.04), 0 4px 12px rgba(0,0,0,0.06)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",
                }
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "10px",
                DrawerWidthLeft = "260px",
                AppbarHeight = "64px",
            }
        };
    }

    private static string DarkenColor(string hexColor)
    {
        var hex = hexColor.StartsWith('#') ? hexColor[1..] : hexColor;

        // Expand #RGB → #RRGGBB
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

        if (hex.Length == 6)
        {
            var r = (int)(Convert.ToInt32(hex[..2], 16) * 0.8);
            var g = (int)(Convert.ToInt32(hex[2..4], 16) * 0.8);
            var b = (int)(Convert.ToInt32(hex[4..6], 16) * 0.8);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        return "#2d55c4"; // fallback
    }
}

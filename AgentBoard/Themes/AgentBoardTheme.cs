using MudBlazor;

namespace AgentBoard.Themes;

public static class AgentBoardTheme
{
    public static MudTheme Theme => new()
    {
        PaletteLight = new PaletteLight
        {
            // Minimal — app always runs in dark mode
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#00E5FF",          // neon cyan
            Info = "#40C4FF",             // electric blue
            Success = "#00E676",          // neon green
            Warning = "#FFD600",          // neon yellow
            Error = "#FF1744",            // neon red
            Background = "#0A0E1A",       // near-black navy
            BackgroundGray = "#0D1220",
            Surface = "#111827",          // dark surface
            DrawerBackground = "#0D1220",
            AppbarBackground = "#0D1220",
            AppbarText = "#00E5FF",
            TextPrimary = "#E0E6F0",
            TextSecondary = "#7B8EA8",
            ActionDefault = "#00E5FF",
            ActionDisabled = "#2A3450",
            ActionDisabledBackground = "#1A2035",
            Divider = "#1E2D45",
            DividerLight = "#162236",
            TableLines = "#1E2D45",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "system-ui", "sans-serif" },
            },
            H4 = new H4Typography
            {
                FontFamily = new[] { "Orbitron", "Inter", "sans-serif" },
                FontWeight = "700",
                LetterSpacing = "0.05em",
            },
            H5 = new H5Typography
            {
                FontFamily = new[] { "Orbitron", "Inter", "sans-serif" },
                FontWeight = "600",
            },
        },
    };
}

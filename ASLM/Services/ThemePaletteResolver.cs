// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using Microsoft.Maui.Graphics;

namespace ASLM.Services
{
    // Palette resolution

    /// <summary>
    /// Builds the full resolved color palette for a given theme mode.
    /// <para>
    /// Resolution order for custom themes:
    /// 1. Full base palette (Dark or Light according to <see cref="CustomTheme.BaseAppearance"/>).
    /// 2. Any colors explicitly defined in the custom theme's <c>Colors</c> dictionary
    ///    override the base values — absent keys fall back to the base palette.
    /// </para>
    /// </summary>
    public static class ThemePaletteResolver
    {
        // Dark palette

        /// <summary>
        /// Returns the complete dark-mode palette.
        /// Key names mirror the resource keys defined in <c>Resources/Styles/Colors.xaml</c>.
        /// </summary>
        public static Dictionary<string, Color> BuildDarkPalette() => new(StringComparer.OrdinalIgnoreCase)
        {
            // System palette
            ["SystemBlue"]   = C("#0A84FF"),
            ["SystemGreen"]  = C("#32D74B"),
            ["SystemIndigo"] = C("#5E5CE6"),
            ["SystemOrange"] = C("#FF9F0A"),
            ["SystemPink"]   = C("#FF375F"),
            ["SystemPurple"] = C("#BF5AF2"),
            ["SystemRed"]    = C("#FF453A"),
            ["SystemTeal"]   = C("#64D2FF"),
            ["SystemYellow"] = C("#FFD60A"),
            ["SystemMint"]   = C("#66D4CF"),
            ["SystemCyan"]   = C("#64D2FF"),

            ["SystemGray"]  = C("#8E8E93"),
            ["SystemGray2"] = C("#636366"),
            ["SystemGray3"] = C("#48484A"),
            ["SystemGray4"] = C("#3A3A3C"),
            ["SystemGray5"] = C("#2C2C2E"),
            ["SystemGray6"] = C("#1C1C1E"),

            // Backgrounds
            ["BackgroundPrimary"]   = C("#000000"),
            ["BackgroundSecondary"] = C("#1C1C1E"),
            ["BackgroundTertiary"]  = C("#2C2C2E"),

            // Labels
            ["LabelPrimary"]    = C("#FFFFFF"),
            ["LabelSecondary"]  = C("#99EBEBF5"),
            ["LabelTertiary"]   = C("#4DEBEBF5"),
            ["LabelQuaternary"] = C("#2EEBEBF5"),

            // Utility
            ["Separator"]         = C("#38383A"),
            ["OpaqueSeparator"]   = C("#38383A"),
            ["PlaceholderText"]   = C("#4DEBEBF5"),
            ["LinkColor"]         = C("#0984FF"),
            ["SystemBlueOverlay"] = C("#200A84FF"),

            // Legacy aliases
            ["Primary"]          = C("#0A84FF"),
            ["PrimaryDark"]      = C("#5E5CE6"),
            ["PrimaryDarkText"]  = C("#FFFFFF"),
            ["Secondary"]        = C("#2C2C2E"),
            ["SecondaryDarkText"] = C("#99EBEBF5"),
            ["Tertiary"]         = C("#3A3A3C"),

            ["DarkBackground"]          = C("#000000"),
            ["LightOnDarkBackground"]   = C("#FFFFFF"),
            ["LightBackground"]         = C("#000000"),
            ["DarkOnLightBackground"]   = C("#FFFFFF"),
            ["LightSecondaryBackground"] = C("#1C1C1E"),
            ["DarkSecondaryBackground"]  = C("#1C1C1E"),

            ["White"]       = C("#FFFFFF"),
            ["Black"]       = C("#000000"),
            ["Magenta"]     = C("#FF375F"),
            ["MidnightBlue"] = C("#FFFFFF"),
            ["OffBlack"]    = C("#1C1C1E"),

            // Action colors
            ["ActionRed"]   = C("#FF453A"),
            ["ActionBlue"]  = C("#0A84FF"),
            ["ActionGreen"] = C("#32D74B"),

            // Neutral scale
            ["Gray100"] = C("#EBEBF5"),
            ["Gray200"] = C("#8E8E93"),
            ["Gray300"] = C("#636366"),
            ["Gray400"] = C("#48484A"),
            ["Gray500"] = C("#3A3A3C"),
            ["Gray600"] = C("#2C2C2E"),
            ["Gray900"] = C("#1C1C1E"),
            ["Gray950"] = C("#000000"),

            // Surface variants
            ["OverlayBackground"]  = C("#40000000"),
            ["BackgroundSidebar"]  = C("#18181A"),
            ["FieldBackground"]    = C("#19191C"),
            ["BackgroundDeep"]     = C("#161618"),
            ["BackgroundElevated"] = C("#252526"),

            // Popover / toast
            ["PopoverBackground"]          = C("#28282A"),
            ["PopoverBorderColor"]         = C("#454548"),
            ["PopoverButtonSecondary"]     = C("#333334"),
            ["PopoverButtonSecondaryText"] = C("#D8D8DC"),
            ["PopoverTextSecondary"]       = C("#9A9AA0"),

            // Downloads dialog
            ["DownloadActiveSurface"]      = C("#202733"),
            ["DownloadActiveBorder"]       = C("#2C7CF6"),
            ["DownloadPassiveSurface"]     = C("#1F1F22"),
            ["DownloadPassiveListSurface"] = C("#1B1B1E"),
            ["DownloadPassiveBorder"]      = C("#343438"),
            ["DownloadActiveSubtitle"]     = C("#E7F1FF"),

            // Error overlay
            ["BackgroundErrorOverlay"] = C("#4C1F24"),
        };


        // Light palette

        /// <summary>
        /// Returns the complete light-mode palette.
        /// </summary>
        public static Dictionary<string, Color> BuildLightPalette() => new(StringComparer.OrdinalIgnoreCase)
        {
            // System palette
            ["SystemBlue"]   = C("#007AFF"),
            ["SystemGreen"]  = C("#34C759"),
            ["SystemIndigo"] = C("#5856D6"),
            ["SystemOrange"] = C("#FF9500"),
            ["SystemPink"]   = C("#FF2D55"),
            ["SystemPurple"] = C("#AF52DE"),
            ["SystemRed"]    = C("#FF3B30"),
            ["SystemTeal"]   = C("#5AC8FA"),
            ["SystemYellow"] = C("#FFCC00"),
            ["SystemMint"]   = C("#00C7BE"),
            ["SystemCyan"]   = C("#55BEF0"),

            ["SystemGray"]  = C("#8E8E93"),
            ["SystemGray2"] = C("#AEAEB2"),
            ["SystemGray3"] = C("#C7C7CC"),
            ["SystemGray4"] = C("#D1D1D6"),
            ["SystemGray5"] = C("#E5E5EA"),
            ["SystemGray6"] = C("#F2F2F7"),

            // Backgrounds
            ["BackgroundPrimary"]   = C("#F2F2F7"),
            ["BackgroundSecondary"] = C("#FFFFFF"),
            ["BackgroundTertiary"]  = C("#E5E5EA"),

            // Labels
            ["LabelPrimary"]    = C("#000000"),
            ["LabelSecondary"]  = C("#636366"),
            ["LabelTertiary"]   = C("#8E8E93"),
            ["LabelQuaternary"] = C("#AEAEB2"),

            // Utility
            ["Separator"]          = C("#B0B0B6"),
            ["OpaqueSeparator"]   = C("#B0B0B6"),
            ["PlaceholderText"]   = C("#8E8E93"),
            ["LinkColor"]         = C("#007AFF"),
            ["SystemBlueOverlay"] = C("#20007AFF"),

            // Legacy aliases
            ["Primary"]          = C("#007AFF"),
            ["PrimaryDark"]      = C("#5856D6"),
            ["PrimaryDarkText"]  = C("#FFFFFF"),
            ["Secondary"]        = C("#E5E5EA"),
            ["SecondaryDarkText"] = C("#3A3A3C"),
            ["Tertiary"]         = C("#D1D1D6"),

            ["DarkBackground"]           = C("#1C1C1E"),
            ["LightOnDarkBackground"]    = C("#FFFFFF"),
            ["LightBackground"]          = C("#FFFFFF"),
            ["DarkOnLightBackground"]    = C("#1C1C1E"),
            ["LightSecondaryBackground"] = C("#E5E5EA"),
            ["DarkSecondaryBackground"]  = C("#3A3A3C"),

            ["White"]        = C("#FFFFFF"),
            ["Black"]        = C("#000000"),
            ["Magenta"]      = C("#FF2D55"),
            ["MidnightBlue"] = C("#1C1C1E"),
            ["OffBlack"]     = C("#3A3A3C"),

            // Action colors
            ["ActionRed"]   = C("#FF3B30"),
            ["ActionBlue"]  = C("#007AFF"),
            ["ActionGreen"] = C("#34C759"),

            // Neutral scale
            ["Gray100"] = C("#F2F2F7"),
            ["Gray200"] = C("#E5E5EA"),
            ["Gray300"] = C("#D1D1D6"),
            ["Gray400"] = C("#C7C7CC"),
            ["Gray500"] = C("#AEAEB2"),
            ["Gray600"] = C("#8E8E93"),
            ["Gray900"] = C("#636366"),
            ["Gray950"] = C("#3A3A3C"),

            // Surface variants
            ["OverlayBackground"]  = C("#59000000"),
            ["BackgroundSidebar"]  = C("#EDEDF0"),
            ["FieldBackground"]    = C("#FFFFFF"),
            ["BackgroundDeep"]     = C("#EBEBEF"),
            ["BackgroundElevated"] = C("#FFFFFF"),

            // Popover / toast
            ["PopoverBackground"]          = C("#FFFFFF"),
            ["PopoverBorderColor"]         = C("#D8D8DC"),
            ["PopoverButtonSecondary"]     = C("#EFEFF4"),
            ["PopoverButtonSecondaryText"] = C("#1C1C1E"),
            ["PopoverTextSecondary"]       = C("#636366"),

            // Downloads dialog
            ["DownloadActiveSurface"]      = C("#E8F1FF"),
            ["DownloadActiveBorder"]       = C("#2C7CF6"),
            ["DownloadPassiveSurface"]     = C("#F5F5F7"),
            ["DownloadPassiveListSurface"] = C("#FAFAFC"),
            ["DownloadPassiveBorder"]      = C("#DDDDDF"),
            ["DownloadActiveSubtitle"]     = C("#194D99"),

            // Error overlay
            ["BackgroundErrorOverlay"] = C("#FFEBEC"),
        };


        /// <summary>
        /// Fills <paramref name="theme"/>.Colors with every palette key from the chosen built-in base.
        /// </summary>
        public static void PrefillCustomThemeFromBuiltIn(CustomTheme theme)
        {
            theme.Normalize();
            var isDark = string.Equals(theme.BaseAppearance, "dark", StringComparison.OrdinalIgnoreCase);
            var palette = isDark ? BuildDarkPalette() : BuildLightPalette();

            theme.Colors.Clear();
            foreach (var (key, color) in palette)
            {
                theme.Colors[key] = ToHex(color);
            }
        }

        /// <summary>
        /// Builds the resolved palette for a custom theme. Missing or invalid JSON keys fall back to
        /// the built-in palette for <see cref="CustomTheme.BaseAppearance"/>, then explicit theme overrides.
        /// </summary>
        public static Dictionary<string, Color> BuildCustomPalette(CustomTheme theme)
        {
            var palette = string.Equals(theme.BaseAppearance, "light", StringComparison.OrdinalIgnoreCase)
                ? BuildLightPalette()
                : BuildDarkPalette();

            foreach (var (key, hex) in theme.Colors)
            {
                if (string.IsNullOrWhiteSpace(key) || !CustomTheme.IsValidHex(hex))
                {
                    continue;
                }

                if (TryParseHex(hex, out var color))
                {
                    palette[key] = color;
                }
            }

            return palette;
        }


        // All known palette keys

        /// <summary>
        /// Returns the ordered list of every palette key known to the theme system.
        /// Used by the custom theme editor to display all editable slots.
        /// </summary>
        public static IReadOnlyList<string> AllKeys { get; } = BuildDarkPalette().Keys.ToList().AsReadOnly();

        /// <summary>
        /// Stroke for small color swatches: subtle ring that stays visible on both light and dark fills.
        /// </summary>
        public static Color SwatchContrastStroke(Color fill)
        {
            static double SrgbChannel(double c) =>
                c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

            var r = SrgbChannel(fill.Red);
            var g = SrgbChannel(fill.Green);
            var b = SrgbChannel(fill.Blue);
            var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            return luminance > 0.52
                ? Color.FromArgb("#D9FFFFFF")
                : Color.FromArgb("#FF9A9A9E");
        }


        // Helpers

        /// <summary>
        /// Parses a 6- or 8-digit hex string to a MAUI Color.
        /// </summary>
        public static bool TryParseHex(string hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            try
            {
                color = Color.FromArgb(hex.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Converts a MAUI Color to a canonical 8-digit hex string (#AARRGGBB).
        /// </summary>
        public static string ToHex(Color color) =>
            $"#{(int)(color.Alpha * 255):X2}{(int)(color.Red * 255):X2}{(int)(color.Green * 255):X2}{(int)(color.Blue * 255):X2}";

        // Inline constructor shorthand
        private static Color C(string hex) => Color.FromArgb(hex);
    }
}

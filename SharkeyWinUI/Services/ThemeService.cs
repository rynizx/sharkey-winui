using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SharkeyWinUI.Services;

/// <summary>
/// Manages app theme (Light/Dark/System) and accent color preferences.
/// Settings are persisted via <see cref="LocalSettingsService"/> and applied
/// to the WinUI resource dictionaries so every control picks up the accent.
/// </summary>
internal static class ThemeService
{
    private static readonly LocalSettingsService s_settings = new();

    private const string ThemeKey = "app_theme";
    private const string AccentKey = "app_accent";

    /// <summary>Pre-defined accent color presets.</summary>
    public static readonly AccentPreset[] Presets =
    [
        new("Pink",   "#FF4081", ColorFrom(0xFF, 0x40, 0x81)),
        new("Purple", "#7C4DFF", ColorFrom(0x7C, 0x4D, 0xFF)),
        new("Blue",   "#2979FF", ColorFrom(0x29, 0x79, 0xFF)),
        new("Teal",   "#00BFA5", ColorFrom(0x00, 0xBF, 0xA5)),
        new("Orange", "#FF6D00", ColorFrom(0xFF, 0x6D, 0x00)),
        new("Red",    "#F44336", ColorFrom(0xF4, 0x43, 0x36)),
    ];

    public record AccentPreset(string Name, string Hex, Color Color);

    // ── Read helpers ──────────────────────────────────────────────────────────

    public static string GetSavedThemeValue() =>
        s_settings.Get<string>(ThemeKey) ?? "default";

    public static string GetSavedAccentHex() =>
        s_settings.Get<string>(AccentKey) ?? "#FF4081";

    // ── Theme ─────────────────────────────────────────────────────────────────

    /// <summary>Applies the saved theme to the root element (call after window content exists).</summary>
    public static void ApplySavedTheme()
    {
        var theme = ParseTheme(GetSavedThemeValue());
        SetRootTheme(theme);
    }

    /// <summary>Saves and immediately applies the given theme.</summary>
    public static void SaveAndApplyTheme(string value)
    {
        s_settings.Set(ThemeKey, value);
        SetRootTheme(ParseTheme(value));
    }

    // ── Accent color ──────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the saved accent color to all relevant resource dictionary entries.
    /// Call this BEFORE creating the main window so XAML resolves the right colours.
    /// </summary>
    public static void ApplySavedAccent()
    {
        ApplyAccentToResources(GetSavedAccentHex());
    }

    /// <summary>Saves the accent hex and updates resources. A theme refresh is attempted.</summary>
    public static void SaveAndApplyAccent(string hex)
    {
        s_settings.Set(AccentKey, hex);
        ApplyAccentToResources(hex);
        ForceThemeRefresh();
    }

    // ── Resource application ──────────────────────────────────────────────────

    private static void ApplyAccentToResources(string hex)
    {
        if (!TryParseHex(hex, out var baseColor)) return;

        var light1 = Lighten(baseColor, 0.20);
        var light2 = Lighten(baseColor, 0.35);
        var light3 = Lighten(baseColor, 0.55);
        var dark1  = Darken(baseColor, 0.12);
        var dark2  = Darken(baseColor, 0.20);
        var dark3  = Darken(baseColor, 0.35);

        var res = Application.Current.Resources;

        // ── Flat (non-themed) resources ──
        res["SystemAccentColor"]       = baseColor;
        res["SystemAccentColorLight1"] = light1;
        res["SystemAccentColorLight2"] = light2;
        res["SystemAccentColorLight3"] = light3;
        res["SystemAccentColorDark1"]  = dark1;
        res["SystemAccentColorDark2"]  = dark2;
        res["SystemAccentColorDark3"]  = dark3;

        // AccentButton overrides
        res["AccentButtonBackground"]            = new SolidColorBrush(baseColor);
        res["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(dark1);
        res["AccentButtonBackgroundPressed"]     = new SolidColorBrush(dark2);
        res["AccentButtonBackgroundDisabled"]    = new SolidColorBrush(WithAlpha(baseColor, 0x88));

        // Named app brushes
        res["AppAccentBrush"] = new SolidColorBrush(baseColor);

        // ── Theme-dictionary resources ──
        // Light theme: accent controls use darker variants for contrast on light backgrounds.
        // Dark theme: accent controls use lighter variants for contrast on dark backgrounds.
        if (res.ThemeDictionaries.TryGetValue("Light", out var lightObj) &&
            lightObj is ResourceDictionary lightDict)
        {
            ApplyAccentToThemeDict(lightDict, baseColor, dark1, dark2, light1, light2, light3, dark1, dark2, dark3);
        }

        if (res.ThemeDictionaries.TryGetValue("Dark", out var darkObj) &&
            darkObj is ResourceDictionary darkDict)
        {
            ApplyAccentToThemeDict(darkDict, baseColor, light2, light1, light1, light2, light3, dark1, dark2, dark3);
        }
    }

    private static void ApplyAccentToThemeDict(
        ResourceDictionary dict,
        Color baseColor, Color fillDefault, Color fillSecondary,
        Color light1, Color light2, Color light3,
        Color dark1, Color dark2, Color dark3)
    {
        dict["SystemAccentColor"]       = baseColor;
        dict["SystemAccentColorLight1"] = light1;
        dict["SystemAccentColorLight2"] = light2;
        dict["SystemAccentColorLight3"] = light3;
        dict["SystemAccentColorDark1"]  = dark1;
        dict["SystemAccentColorDark2"]  = dark2;
        dict["SystemAccentColorDark3"]  = dark3;

        dict["AccentFillColorDefaultBrush"]   = new SolidColorBrush(fillDefault);
        dict["AccentFillColorSecondaryBrush"] = new SolidColorBrush(WithAlpha(fillSecondary, 0xE6));
        dict["AccentFillColorTertiaryBrush"]  = new SolidColorBrush(WithAlpha(fillSecondary, 0xCC));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void SetRootTheme(ElementTheme theme)
    {
        if (App.MainWindow?.Content is FrameworkElement root)
            root.RequestedTheme = theme;
    }

    /// <summary>
    /// Briefly toggles the root theme to force all {ThemeResource} bindings to
    /// re-resolve against updated resource dictionaries.
    /// </summary>
    private static void ForceThemeRefresh()
    {
        if (App.MainWindow?.Content is not FrameworkElement root) return;

        var current = root.RequestedTheme;
        root.RequestedTheme = current == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;
        root.RequestedTheme = current;
    }

    private static ElementTheme ParseTheme(string value) => value switch
    {
        "light" => ElementTheme.Light,
        "dark"  => ElementTheme.Dark,
        _       => ElementTheme.Default,
    };

    private static Color ColorFrom(byte r, byte g, byte b) =>
        Color.FromArgb(0xFF, r, g, b);

    private static Color Lighten(Color c, double amount) =>
        Color.FromArgb(0xFF,
            (byte)Math.Min(255, c.R + (255 - c.R) * amount),
            (byte)Math.Min(255, c.G + (255 - c.G) * amount),
            (byte)Math.Min(255, c.B + (255 - c.B) * amount));

    private static Color Darken(Color c, double amount) =>
        Color.FromArgb(0xFF,
            (byte)(c.R * (1 - amount)),
            (byte)(c.G * (1 - amount)),
            (byte)(c.B * (1 - amount)));

    private static Color WithAlpha(Color c, byte alpha) =>
        Color.FromArgb(alpha, c.R, c.G, c.B);

    private static bool TryParseHex(string hex, out Color color)
    {
        color = default;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return false;
        if (!byte.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)) return false;
        if (!byte.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)) return false;
        if (!byte.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)) return false;
        color = Color.FromArgb(0xFF, r, g, b);
        return true;
    }
}

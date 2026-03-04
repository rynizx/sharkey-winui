using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SharkeyWinUI.Helpers;

/// <summary>
/// Utility for rendering text that may contain custom emoji shortcodes
/// (e.g. <c>:neodog_flag_gay:</c>) as a mix of inline text runs and
/// inline images inside a horizontal <see cref="StackPanel"/>.
/// </summary>
/// <remarks>
/// WinUI 3's <see cref="TextBlock"/> does not support <c>InlineUIContainer</c>,
/// so a horizontal <see cref="StackPanel"/> is used as the container instead.
/// </remarks>
internal static class EmojiTextHelper
{
    /// <summary>
    /// Populates a horizontal <see cref="StackPanel"/> with text that may contain custom emoji
    /// shortcodes. Shortcodes present in <paramref name="emojis"/> are replaced
    /// with 20×20 inline images; everything else is rendered as plain <see cref="TextBlock"/> segments.
    /// </summary>
    /// <param name="container">Target horizontal StackPanel to populate.</param>
    /// <param name="text">The raw text, possibly containing <c>:shortcode:</c> patterns.</param>
    /// <param name="emojis">Map of shortcode → image URL. May be null or empty.</param>
    /// <param name="textStyle">Optional style applied to each text-segment TextBlock.</param>
    /// <param name="foreground">Optional foreground brush applied to each text-segment TextBlock.</param>
    public static void SetTextWithEmojis(
        StackPanel container,
        string text,
        Dictionary<string, string>? emojis,
        Style? textStyle = null,
        Brush? foreground = null)
    {
        container.Children.Clear();

        if (emojis is null || emojis.Count == 0)
        {
            container.Children.Add(MakeTextBlock(text, textStyle, foreground));
            return;
        }

        var matches = Regex.Matches(text, @":([\w+-]+):");
        var lastIndex = 0;

        foreach (Match match in matches)
        {
            var shortcode = match.Groups[1].Value;
            if (!emojis.TryGetValue(shortcode, out var url))
                continue;

            if (match.Index > lastIndex)
                container.Children.Add(MakeTextBlock(text[lastIndex..match.Index], textStyle, foreground));

            var img = new Image
            {
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform,
            };
            try { img.Source = new BitmapImage(new Uri(url)); }
            catch { /* skip broken URLs */ }

            container.Children.Add(img);
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            container.Children.Add(MakeTextBlock(text[lastIndex..], textStyle, foreground));

        // No shortcodes were resolved — add the full text as a single block
        if (container.Children.Count == 0)
            container.Children.Add(MakeTextBlock(text, textStyle, foreground));
    }

    private static TextBlock MakeTextBlock(string text, Style? style, Brush? foreground)
    {
        var tb = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        if (style != null) tb.Style = style;
        if (foreground != null) tb.Foreground = foreground;
        return tb;
    }
}

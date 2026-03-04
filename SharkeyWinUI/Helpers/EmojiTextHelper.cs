using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SharkeyWinUI.Helpers;

/// <summary>
/// Utility for rendering text that may contain custom emoji shortcodes
/// (e.g. <c>:neodog_flag_gay:</c>) as a mix of inline text runs and
/// inline images inside a <see cref="TextBlock"/>.
/// </summary>
internal static class EmojiTextHelper
{
    /// <summary>
    /// Populates a <see cref="TextBlock"/> with text that may contain custom emoji
    /// shortcodes. Shortcodes present in <paramref name="emojis"/> are replaced
    /// with 20×20 inline images; everything else is rendered as plain text.
    /// </summary>
    /// <param name="textBlock">The target TextBlock to populate.</param>
    /// <param name="text">The raw text, possibly containing <c>:shortcode:</c> patterns.</param>
    /// <param name="emojis">Map of shortcode → image URL. May be null or empty.</param>
    public static void SetTextWithEmojis(
        TextBlock textBlock,
        string text,
        Dictionary<string, string>? emojis)
    {
        textBlock.Inlines.Clear();

        if (emojis is null || emojis.Count == 0)
        {
            textBlock.Text = text;
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
                textBlock.Inlines.Add(new Run { Text = text[lastIndex..match.Index] });

            var img = new Image
            {
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            };
            try { img.Source = new BitmapImage(new Uri(url)); }
            catch { /* skip broken URLs */ }

            textBlock.Inlines.Add(new InlineUIContainer { Child = img });
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            textBlock.Inlines.Add(new Run { Text = text[lastIndex..] });

        // No shortcodes were resolved — fall back to plain text assignment
        if (textBlock.Inlines.Count == 0)
            textBlock.Text = text;
    }
}

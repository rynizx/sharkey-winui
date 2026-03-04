using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using SharkeyWinUI.Models;
using SharkeyWinUI.Pages;

namespace SharkeyWinUI.Controls;

public sealed partial class NoteCard : UserControl
{
    // ── Dependency property ───────────────────────────────────────────────────

    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(nameof(Note), typeof(Note), typeof(NoteCard),
            new PropertyMetadata(null, OnNoteChanged));

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    private static void OnNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NoteCard)d).Populate(e.NewValue as Note);

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<Note>? ReplyRequested;
    public event Action<Note>? RenoteRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public NoteCard()
    {
        InitializeComponent();
    }

    // ── Population ────────────────────────────────────────────────────────────

    private void Populate(Note? note)
    {
        if (note == null) return;

        // If this is a pure renote, show the renote header and display the target
        Note displayNote = note;
        if (note.IsPureRenote && note.Renote != null)
        {
            RenoteHeader.Visibility = Visibility.Visible;
            RenoteByText.Text = $"{note.User?.EffectiveName ?? note.User?.Username} renoted";
            displayNote = note.Renote;
        }
        else
        {
            RenoteHeader.Visibility = Visibility.Collapsed;
        }

        // Avatar
        if (!string.IsNullOrEmpty(displayNote.User?.AvatarUrl))
        {
            try { AvatarBrush.ImageSource = new BitmapImage(new Uri(displayNote.User.AvatarUrl)); }
            catch { /* leave blank */ }
        }

        // User names
        DisplayNameText.Text = displayNote.User?.EffectiveName ?? displayNote.User?.Username ?? "Unknown";
        UsernameText.Text = displayNote.User?.FullUsername ?? string.Empty;

        // Remote instance badge
        if (displayNote.User?.IsRemote == true && displayNote.User.Instance?.Name != null)
        {
            InstanceBadge.Visibility = Visibility.Visible;
            InstanceBadgeText.Text = displayNote.User.Instance.Name;
        }
        else
        {
            InstanceBadge.Visibility = Visibility.Collapsed;
        }

        // Timestamp (relative)
        TimestampText.Text = RelativeTime(displayNote.CreatedAt);

        // Content warning
        if (displayNote.HasContentWarning)
        {
            CwPanel.Visibility = Visibility.Visible;
            CwText.Text = displayNote.ContentWarning!;
            BodyText.Visibility = Visibility.Collapsed;
        }
        else
        {
            CwPanel.Visibility = Visibility.Collapsed;
            BodyText.Visibility = Visibility.Visible;
        }

        // Body text (plain — full MFM rendering would require a parser)
        BodyText.Text = displayNote.DisplayText;

        // Media
        PopulateMedia(displayNote);

        // Poll
        PopulatePoll(displayNote);

        // Reactions
        PopulateReactions(displayNote);

        // Action bar counts
        RepliesCountText.Text = displayNote.RepliesCount.ToString();
        RenoteCountText.Text  = displayNote.RenoteCount.ToString();

        // Favourite icon state (filled if note has a current reaction that's a like)
        FavouriteIcon.Glyph = displayNote.MyReaction != null ? "\uE735" : "\uE734";
    }

    private void PopulateMedia(Note note)
    {
        if (!note.HasMedia)
        {
            MediaGrid.Visibility = Visibility.Collapsed;
            return;
        }

        MediaGrid.Visibility = Visibility.Visible;
        // Clear previous children
        foreach (var child in GetGridChildren(MediaGrid).ToList())
            MediaGrid.Children.Remove(child);

        var images = note.Files.Where(f => f.IsImage).Take(4).ToList();

        // Collapse the second row when there are 1-2 images
        MediaGrid.RowDefinitions[1].Height = images.Count > 2
            ? new GridLength(160)
            : new GridLength(0);

        for (int i = 0; i < images.Count; i++)
        {
            var file = images[i];
            var img = new Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
            };
            if (!string.IsNullOrEmpty(file.ThumbnailUrl ?? file.Url))
            {
                try { img.Source = new BitmapImage(new Uri(file.ThumbnailUrl ?? file.Url!)); }
                catch { /* skip */ }
            }

            // Wrap each image in a border for rounded corners
            var wrapper = new Border
            {
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(6),
                Child = img,
            };

            // If single image, span both columns
            if (images.Count == 1)
            {
                Grid.SetColumnSpan(wrapper, 2);
            }

            Grid.SetRow(wrapper, i / 2);
            Grid.SetColumn(wrapper, i % 2);
            MediaGrid.Children.Add(wrapper);
        }
    }

    private void PopulatePoll(Note note)
    {
        PollPanel.Children.Clear();
        if (note.Poll == null)
        {
            PollPanel.Visibility = Visibility.Collapsed;
            return;
        }

        PollPanel.Visibility = Visibility.Visible;
        var total = note.Poll.TotalVotes;

        foreach (var choice in note.Poll.Choices)
        {
            var pct = total > 0 ? (double)choice.Votes / total : 0;
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var voted = new FontIcon
            {
                Glyph = choice.IsVoted ? "\uE73E" : "\uECCA",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var bar = new ProgressBar
            {
                Value = pct * 100,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = $"{choice.Text}  {choice.Votes}",
                VerticalAlignment = VerticalAlignment.Center,
                Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
            };

            Grid.SetColumn(voted, 0);
            Grid.SetColumn(bar, 1);
            Grid.SetColumn(label, 2);
            row.Children.Add(voted);
            row.Children.Add(bar);
            row.Children.Add(label);
            PollPanel.Children.Add(row);
        }

        if (note.Poll.ExpiresAt.HasValue)
        {
            PollPanel.Children.Add(new TextBlock
            {
                Text = note.Poll.IsExpired
                    ? "Poll ended"
                    : $"Ends {note.Poll.ExpiresAt.Value:g}",
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
            });
        }
    }

    private void PopulateReactions(Note note)
    {
        ReactionsPanel.Items.Clear();
        foreach (var kv in note.Reactions)
        {
            var btn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = kv.Key, FontSize = 16 },
                        new TextBlock
                        {
                            Text = kv.Value.ToString(),
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    }
                },
                Margin = new Thickness(0, 0, 4, 4),
                Tag = kv.Key,
            };
            if (note.MyReaction == kv.Key)
                btn.Style = Application.Current.Resources["AccentButtonStyle"] as Style;
            btn.Click += ReactionButton_Click;
            ReactionsPanel.Items.Add(btn);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void CwToggle_Checked(object sender, RoutedEventArgs e)
    {
        BodyText.Visibility = Visibility.Visible;
        ((ToggleButton)sender).Content = "Hide content";
    }

    private void CwToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        BodyText.Visibility = Visibility.Collapsed;
        ((ToggleButton)sender).Content = "Show content";
    }

    private void ReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        ReplyRequested?.Invoke(Note);
        FindParentFrame(this)?.Navigate(typeof(ComposePage), $"reply:{Note.Id}");
    }

    private void RenoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        RenoteRequested?.Invoke(Note);
        FindParentFrame(this)?.Navigate(typeof(ComposePage), $"renote:{Note.Id}");
    }

    private async void ReactButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        // Simple picker: show common reactions in a content dialog
        var dlg = new ContentDialog
        {
            Title = "Choose a reaction",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        var panel = new WrapGrid { Orientation = Orientation.Horizontal };
        foreach (var r in new[] { "👍", "❤️", "😂", "😮", "😢", "😡", "🎉", "🔥", "✨", "👀" })
        {
            var btn = new Button { Content = r, FontSize = 20, Margin = new Thickness(4), Tag = r };
            btn.Click += (_, _) =>
            {
                dlg.Hide();
                _ = SendReactionAsync(Note.Id, (string)btn.Tag);
            };
            panel.Children.Add(btn);
        }
        dlg.Content = panel;
        await dlg.ShowAsync();
    }

    private async Task SendReactionAsync(string noteId, string reaction)
    {
        try
        {
            if (Note?.MyReaction == reaction)
                await App.ApiClient.RemoveReactionAsync(noteId);
            else
                await App.ApiClient.AddReactionAsync(noteId, reaction);
        }
        catch { /* show error in production */ }
    }

    private async void FavouriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        try
        {
            await App.ApiClient.FavouriteNoteAsync(Note.Id);
            FavouriteIcon.Glyph = "\uE735";
        }
        catch { /* already favourited or other error */ }
    }

    private void CopyLinkItem_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var url = Note.Url ?? Note.Uri ?? $"{App.ApiClient.ServerUrl}/notes/{Note.Id}";
        var dp = new DataPackage();
        dp.SetText(url);
        Clipboard.SetContent(dp);
    }

    private async void OpenInBrowserItem_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var url = Note.Url ?? Note.Uri ?? $"{App.ApiClient.ServerUrl}/notes/{Note.Id}";
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    private async void DeleteNoteItem_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var dlg = new ContentDialog
        {
            Title = "Delete note",
            Content = "Are you sure you want to delete this note? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            try { await App.ApiClient.DeleteNoteAsync(Note.Id); }
            catch { /* surface error */ }
        }
    }

    private void ReactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string reaction && Note != null)
            _ = SendReactionAsync(Note.Id, reaction);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string RelativeTime(DateTimeOffset dt)
    {
        var diff = DateTimeOffset.UtcNow - dt;
        if (diff.TotalSeconds < 60)  return $"{(int)diff.TotalSeconds}s";
        if (diff.TotalMinutes < 60)  return $"{(int)diff.TotalMinutes}m";
        if (diff.TotalHours < 24)    return $"{(int)diff.TotalHours}h";
        if (diff.TotalDays < 7)      return $"{(int)diff.TotalDays}d";
        return dt.LocalDateTime.ToString("MMM d");
    }

    private static IEnumerable<UIElement> GetGridChildren(Grid grid)
        => grid.Children.OfType<UIElement>();

    private static Frame? FindParentFrame(DependencyObject d)
    {
        var current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(d);
        while (current != null)
        {
            if (current is Frame f) return f;
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

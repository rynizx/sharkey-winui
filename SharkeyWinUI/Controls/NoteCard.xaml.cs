using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using SharkeyWinUI.Helpers;
using SharkeyWinUI.Models;
using SharkeyWinUI.Pages;
using SharkeyWinUI.Services;
using System.Diagnostics;

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
    {
        try
        {
            ((NoteCard)d).Populate(e.NewValue as Note);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Error populating note: {ex.Message}");
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<Note>? ReplyRequested;
    public event Action<Note>? RenoteRequested;

    // ── State ─────────────────────────────────────────────────────────────────

    // Tracks whether the displayed note has been favourited by the current user.
    // Misskey/Sharkey note objects returned by timelines and search do NOT include
    // an isFavorited field, so we maintain this as local UI state initialised to
    // false on every Populate call.  The Favourite button toggles it optimistically.
    private bool _isFavourited;

    // Track dynamically created event handlers for proper cleanup
    private readonly List<(Button button, RoutedEventHandler handler)> _dynamicHandlers = new();

    // Track poll choice button handlers separately so they can be cleaned up
    // when the poll is re-rendered after a vote, without waiting for the next Populate call.
    private readonly List<(Button button, RoutedEventHandler handler)> _pollHandlers = new();

    // Tracks the last avatar URL set so we skip re-creating BitmapImage when unchanged.
    private string? _lastAvatarUrl;

    // Static caches for app-global style and brush resources — looked up once per key.
    private static readonly Dictionary<string, Style?> _styleCache = new();
    private static readonly Dictionary<string, Microsoft.UI.Xaml.Media.Brush> _brushCache = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public NoteCard()
    {
        InitializeComponent();
        Unloaded += NoteCard_Unloaded;
    }

    // ── Population ────────────────────────────────────────────────────────────

    private void Populate(Note? note)
    {
        try
        {
            if (note == null)
            {
                Debug.WriteLine("NoteCard: Populate called with null note");
                return;
            }

            // Clean up any previous dynamic handlers before populating new content
            CleanupDynamicHandlers();

            // If this is a pure renote, show the renote header and display the target
            Note displayNote = note;
            if (note.IsPureRenote && note.Renote != null)
            {
                RenoteHeader.Visibility = Visibility.Visible;
                EmojiTextHelper.SetTextWithEmojis(
                        RenoteByText,
                        $"{note.User?.EffectiveName ?? note.User?.Username} renoted",
                        note.User?.Emojis,
                        GetStyleResource("CaptionTextBlockStyle"),
                        GetBrushResource("TextFillColorSecondaryBrush"));
                displayNote = note.Renote;
            }
            else
            {
                RenoteHeader.Visibility = Visibility.Collapsed;
            }

            // Avatar — skip re-creating BitmapImage when the URL hasn't changed
            var avatarUrl = displayNote.User?.AvatarUrl;
            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl != _lastAvatarUrl)
            {
                _lastAvatarUrl = avatarUrl;
                try
                {
                    if (Uri.TryCreate(avatarUrl, UriKind.Absolute, out var avatarUri))
                    {
                        // Decode at 2× the 42 px render size so it looks sharp on HiDPI.
                        AvatarBrush.ImageSource = new BitmapImage(avatarUri)
                        {
                            DecodePixelWidth  = 84,
                            DecodePixelHeight = 84,
                        };
                    }
                    else
                    {
                        Debug.WriteLine($"NoteCard: Invalid avatar URL: {avatarUrl}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NoteCard: Failed to load avatar: {ex.Message}");
                }
            }
            else if (string.IsNullOrEmpty(avatarUrl))
            {
                _lastAvatarUrl = null;
                AvatarBrush.ImageSource = null;
            }

            // User names
            EmojiTextHelper.SetTextWithEmojis(
                DisplayNameText,
                displayNote.User?.EffectiveName ?? displayNote.User?.Username ?? "Unknown",
                displayNote.User?.Emojis,
                GetStyleResource("BodyStrongTextBlockStyle"));
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
            BodyText.Text = displayNote.DisplayText ?? string.Empty;

            // Media
            PopulateMedia(displayNote);

            // Poll
            PopulatePoll(displayNote);

            // Reactions
            PopulateReactions(displayNote);

            // Action bar counts
            RepliesCountText.Text = displayNote.RepliesCount.ToString();
            RenoteCountText.Text  = displayNote.RenoteCount.ToString();

            // Favourite icon state — reset local tracking so the icon reflects the
            // current note, not a stale state from a previously displayed note.
            // MyReaction tracks emoji reactions, NOT favourites; they are separate
            // Misskey API concepts. We have no inline favourite-status field from the
            // API so we initialise to false and let the user toggle from here.
            _isFavourited = false;
            FavouriteIcon.Glyph = "\uE734"; // unfilled star
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Critical error in Populate: {ex.Message}");
            Debug.WriteLine($"NoteCard: Stack trace: {ex.StackTrace}");
            // Try to show error state to user
            try
            {
                BodyText.Text = $"Error loading note: {ex.Message}";
                BodyText.Visibility = Visibility.Visible;
            }
            catch { /* Can't even show error */ }
        }
    }

    private void PopulateMedia(Note note)
    {
        try
        {
            if (!note.HasMedia)
            {
                MediaGrid.Visibility = Visibility.Collapsed;
                return;
            }

            MediaGrid.Children.Clear();

            var images = note.Files.Where(f => f.IsImage).Take(4).ToList();
            if (images.Count == 0)
            {
                MediaGrid.Visibility = Visibility.Collapsed;
                return;
            }

            MediaGrid.Visibility = Visibility.Visible;

            // Collapse the second row when there are 1-2 images
            MediaGrid.RowDefinitions[1].Height = images.Count > 2
                ? new GridLength(160)
                : new GridLength(0);

        var hasSensitive = images.Any(f => f.IsSensitive);

        for (int i = 0; i < images.Count; i++)
        {
            var file = images[i];
            var img = new Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
            };

            var imageUrl = file.ThumbnailUrl ?? file.Url;
            if (!string.IsNullOrEmpty(imageUrl))
            {
                try
                {
                    if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
                    {
                        // Constrain decode width to avoid decoding full-resolution images into memory.
                        img.Source = new BitmapImage(imageUri) { DecodePixelWidth = 400 };
                    }
                    else
                    {
                        Debug.WriteLine($"NoteCard: Invalid media URL: {imageUrl}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NoteCard: Failed to load media file: {ex.Message}");
                }
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

        // If any file is sensitive, add a full-grid overlay that can be dismissed
        if (hasSensitive)
        {
            var overlayBackground = GetBrushResource(
                "LayerFillColorDefaultBrush",
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0x00, 0x00, 0x00)));

            var overlay = new Border
            {
                Background = overlayBackground,
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(6),
            };

            var revealBtn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE7B3", FontSize = 24 },
                        new TextBlock
                        {
                            Text = "Sensitive media",
                            Style = GetStyleResource("CaptionTextBlockStyle"),
                        },
                        new TextBlock
                        {
                            Text = "Click to show",
                            Style = GetStyleResource("CaptionTextBlockStyle"),
                            Foreground = GetBrushResource("TextFillColorSecondaryBrush"),
                        },
                    },
                },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            revealBtn.Click += (_, _) => overlay.Visibility = Visibility.Collapsed;

            overlay.Child = revealBtn;
            Grid.SetRowSpan(overlay, 2);
            Grid.SetColumnSpan(overlay, 2);
            MediaGrid.Children.Add(overlay);
        }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Error in PopulateMedia: {ex.Message}");
            MediaGrid.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Cleans up dynamically created event handlers to prevent memory leaks.
    /// </summary>
    private void CleanupDynamicHandlers()
    {
        foreach (var (button, handler) in _dynamicHandlers)
        {
            button.Click -= handler;
        }
        _dynamicHandlers.Clear();

        CleanupPollHandlers();
    }

    private void CleanupPollHandlers()
    {
        foreach (var (button, handler) in _pollHandlers)
            button.Click -= handler;
        _pollHandlers.Clear();
    }

    private void PopulatePoll(Note note)
    {
        try
        {
            // Clean up poll choice button handlers before rebuilding the panel.
            // This is necessary when PopulatePoll is called directly (e.g. after a vote)
            // rather than via Populate, so stale handlers don't accumulate.
            CleanupPollHandlers();
            PollPanel.Children.Clear();
            if (note.Poll == null)
            {
                PollPanel.Visibility = Visibility.Collapsed;
                return;
            }

            PollPanel.Visibility = Visibility.Visible;
        var total = note.Poll.TotalVotes;
        var canVote = !note.Poll.IsExpired &&
                      (note.Poll.Multiple || note.Poll.Choices.All(c => !c.IsVoted));

        for (int i = 0; i < note.Poll.Choices.Count; i++)
        {
            var choice = note.Poll.Choices[i];
            var choiceIndex = i;
            var pct = total > 0 ? (double)choice.Votes / total : 0;

            var row = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, 0, 2) };
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
                Style = GetStyleResource("CaptionTextBlockStyle"),
            };

            Grid.SetColumn(voted, 0);
            Grid.SetColumn(bar, 1);
            Grid.SetColumn(label, 2);
            row.Children.Add(voted);
            row.Children.Add(bar);
            row.Children.Add(label);

            if (canVote && !choice.IsVoted)
            {
                // Wrap in a button so the user can tap to vote
                var btn = new Button
                {
                    Content = row,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(0),
                    Tag = choiceIndex,
                };
                RoutedEventHandler handler = PollChoiceButton_Click;
                btn.Click += handler;
                _pollHandlers.Add((btn, handler));
                PollPanel.Children.Add(btn);
            }
            else
            {
                PollPanel.Children.Add(row);
            }
        }

        // Footer: total votes + expiry
        var footerText = $"{total} vote{(total != 1 ? "s" : "")}";
        if (note.Poll.ExpiresAt.HasValue)
            footerText += note.Poll.IsExpired
                ? " · Poll ended"
                : $" · Ends {note.Poll.ExpiresAt.Value:g}";

        PollPanel.Children.Add(new TextBlock
        {
            Text = footerText,
            Foreground = GetBrushResource("TextFillColorSecondaryBrush"),
            Style = GetStyleResource("CaptionTextBlockStyle"),
            Margin = new Thickness(0, 4, 0, 0),
        });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Error in PopulatePoll: {ex.Message}");
            PollPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void PollChoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null || sender is not Button btn || btn.Tag is not int choiceIndex) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        // Disable all poll choice buttons to prevent double-taps while the request is in-flight.
        var pollButtons = PollPanel.Children.OfType<Button>().ToList();
        foreach (var b in pollButtons) b.IsEnabled = false;

        try
        {
            await App.ApiClient.VotePollAsync(displayNote.Id, choiceIndex);
            // Refresh the note to pick up updated vote counts, then re-render the poll
            var updated = await App.ApiClient.GetNoteAsync(displayNote.Id);
            PopulatePoll(updated);
        }
        catch (MisskeyApiException ex)
        {
            Debug.WriteLine($"NoteCard: Poll vote failed with API error: {ex.ResponseBody}");
            var dlg = new ContentDialog
            {
                Title = "Could not vote",
                Content = $"The server returned an error: {ex.ResponseBody}",
                CloseButtonText = "OK",
            };
            await ShowDialogAsync(dlg);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Poll vote failed: {ex.Message}");
            var dlg = new ContentDialog
            {
                Title = "Could not vote",
                Content = ex.Message,
                CloseButtonText = "OK",
            };
            await ShowDialogAsync(dlg);
        }
        finally
        {
            // Re-enable current poll buttons (may be different if PopulatePoll was called)
            foreach (var b in PollPanel.Children.OfType<Button>()) b.IsEnabled = true;
        }
    }

    private void PopulateReactions(Note note)
    {
        try
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
                    btn.Style = GetStyleResource("AccentButtonStyle");
                RoutedEventHandler handler = ReactionButton_Click;
                btn.Click += handler;
                _dynamicHandlers.Add((btn, handler));
                ReactionsPanel.Items.Add(btn);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Error in PopulateReactions: {ex.Message}");
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void NoteCard_Unloaded(object sender, RoutedEventArgs e)
    {
        CleanupDynamicHandlers();
    }

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
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        ReplyRequested?.Invoke(displayNote);
        var frame = FindParentFrame(this);
        if (frame != null)
        {
            frame.Navigate(typeof(ComposePage), $"reply:{displayNote.Id}");
        }
        else
        {
            Debug.WriteLine("NoteCard: Could not find parent Frame for navigation");
        }
    }

    private void RenoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        RenoteRequested?.Invoke(displayNote);
        var frame = FindParentFrame(this);
        if (frame != null)
        {
            frame.Navigate(typeof(ComposePage), $"renote:{displayNote.Id}");
        }
        else
        {
            Debug.WriteLine("NoteCard: Could not find parent Frame for navigation");
        }
    }

    private async void ReactButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        // Simple picker: show common reactions in a content dialog
        var dlg = new ContentDialog
        {
            Title = "Choose a reaction",
            CloseButtonText = "Cancel",
        };
        // WrapGrid without MaximumRowsOrColumns puts all items on one row;
        // use 5 columns so the 10 emoji buttons wrap into 2 tidy rows.
        var panel = new WrapGrid { Orientation = Orientation.Horizontal, MaximumRowsOrColumns = 5 };
        foreach (var r in new[] { "👍", "❤️", "😂", "😮", "😢", "😡", "🎉", "🔥", "✨", "👀" })
        {
            var btn = new Button { Content = r, FontSize = 20, Margin = new Thickness(4), Tag = r };
            btn.Click += (_, _) =>
            {
                dlg.Hide();
                _ = SendReactionAsync(displayNote.Id, (string)btn.Tag);
            };
            panel.Children.Add(btn);
        }
        dlg.Content = panel;
        await ShowDialogAsync(dlg);
    }

    private async Task SendReactionAsync(string noteId, string reaction)
    {
        try
        {
            // Check the displayed note's reaction, not the outer renote wrapper's
            var displayNote = GetDisplayNote();
            if (displayNote == null) return;

            if (displayNote.MyReaction == reaction)
                await App.ApiClient.RemoveReactionAsync(noteId);
            else
                await App.ApiClient.AddReactionAsync(noteId, reaction);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to send reaction: {ex.Message}");
        }
    }

    private async void FavouriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        try
        {
            if (_isFavourited)
            {
                await App.ApiClient.UnfavouriteNoteAsync(displayNote.Id);
                _isFavourited = false;
                FavouriteIcon.Glyph = "\uE734";
            }
            else
            {
                await App.ApiClient.FavouriteNoteAsync(displayNote.Id);
                _isFavourited = true;
                FavouriteIcon.Glyph = "\uE735";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to toggle favourite: {ex.Message}");
        }
    }

    private void CopyLinkItem_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        try
        {
            var url = displayNote.Url ?? displayNote.Uri ?? $"{App.ApiClient.ServerUrl}/notes/{displayNote.Id}";
            var dp = new DataPackage();
            dp.SetText(url);
            Clipboard.SetContent(dp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to copy link: {ex.Message}");
        }
    }

    private async void OpenInBrowserItem_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        try
        {
            var url = displayNote.Url ?? displayNote.Uri ?? $"{App.ApiClient.ServerUrl}/notes/{displayNote.Id}";
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                await Launcher.LaunchUriAsync(uri);
            }
            else
            {
                Debug.WriteLine($"NoteCard: Invalid URI for opening in browser: {url}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to open in browser: {ex.Message}");
        }
    }

    private async void MuteUserItem_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        var userId = displayNote.User?.Id;
        if (userId == null) return;

        var dlg = new ContentDialog
        {
            Title = "Mute user",
            Content = $"Mute @{displayNote.User?.Username}? Their notes will no longer appear in your timelines.",
            PrimaryButtonText = "Mute",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            if (await ShowDialogAsync(dlg) == ContentDialogResult.Primary)
            {
                await App.ApiClient.MuteUserAsync(userId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to mute user: {ex.Message}");
        }
    }

    private async void BlockUserItem_Click(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var displayNote = GetDisplayNote();
        if (displayNote == null) return;

        var userId = displayNote.User?.Id;
        if (userId == null) return;

        var dlg = new ContentDialog
        {
            Title = "Block user",
            Content = $"Block @{displayNote.User?.Username}? They will not be able to follow you or see your notes.",
            PrimaryButtonText = "Block",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            if (await ShowDialogAsync(dlg) == ContentDialogResult.Primary)
            {
                await App.ApiClient.BlockUserAsync(userId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to block user: {ex.Message}");
        }
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
        };

        try
        {
            if (await ShowDialogAsync(dlg) == ContentDialogResult.Primary)
            {
                await App.ApiClient.DeleteNoteAsync(Note.Id);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to delete note: {ex.Message}");
        }
    }

    private void ReactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string reaction && Note != null)
        {
            var displayNote = GetDisplayNote();
            if (displayNote != null)
            {
                _ = SendReactionAsync(displayNote.Id, reaction);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Safely retrieves a style resource, caching successful lookups.
    /// </summary>
    private static Style? GetStyleResource(string key)
    {
        if (_styleCache.TryGetValue(key, out var cached)) return cached;
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Style style)
        {
            _styleCache[key] = style;
            return style;
        }
        Debug.WriteLine($"NoteCard: Style resource '{key}' not found");
        _styleCache[key] = null;
        return null;
    }

    /// <summary>
    /// Safely retrieves a brush resource, caching successful lookups.
    /// </summary>
    private static Microsoft.UI.Xaml.Media.Brush GetBrushResource(string key, Microsoft.UI.Xaml.Media.Brush? fallback = null)
    {
        if (_brushCache.TryGetValue(key, out var cached)) return cached;
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Microsoft.UI.Xaml.Media.Brush brush)
        {
            _brushCache[key] = brush;
            return brush;
        }
        Debug.WriteLine($"NoteCard: Brush resource '{key}' not found, using fallback");
        return fallback ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x80, 0x80, 0x80));
    }

    /// <summary>
    /// Safely shows a ContentDialog only if XamlRoot is available and control is loaded.
    /// Returns true if dialog was shown, false otherwise.
    /// </summary>
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (XamlRoot == null)
        {
            Debug.WriteLine("NoteCard: Cannot show dialog - XamlRoot is null");
            return ContentDialogResult.None;
        }

        dialog.XamlRoot = XamlRoot;
        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NoteCard: Failed to show dialog: {ex.Message}");
            return ContentDialogResult.None;
        }
    }

    /// <summary>
    /// Returns the note to display — the inner note when this is a pure renote,
    /// or the note itself otherwise.
    /// </summary>
    private Note? GetDisplayNote()
    {
        if (Note == null)
        {
            Debug.WriteLine("NoteCard: GetDisplayNote called with null Note");
            return null;
        }
        return Note.IsPureRenote && Note.Renote != null ? Note.Renote : Note;
    }

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

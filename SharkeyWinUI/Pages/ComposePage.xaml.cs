using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class ComposePage : Page
{
    private readonly ObservableCollection<DriveFile> _attachedFiles = new();

    // "reply:{noteId}" or "renote:{noteId}" or null
    private string? _contextKind;
    private string? _contextNoteId;

    // Max note length — updated from instance meta if available
    private int _maxLength = 3000;

    public ComposePage()
    {
        InitializeComponent();
        AttachedFilesList.ItemsSource = _attachedFiles;
        TextBox.TextChanged += (_, _) => UpdateCharCounter();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var param = e.Parameter as string;
        if (param?.StartsWith("reply:") == true)
        {
            _contextKind   = "reply";
            _contextNoteId = param["reply:".Length..];
            HeaderText.Text = "Reply";
            await ShowContextNoteAsync(_contextNoteId);
        }
        else if (param?.StartsWith("renote:") == true)
        {
            _contextKind   = "renote";
            _contextNoteId = param["renote:".Length..];
            HeaderText.Text = "Renote";
            await ShowContextNoteAsync(_contextNoteId);
        }

        // Load max note length from cached meta
        try
        {
            var meta = await App.ApiClient.GetMetaAsync();
            _maxLength = meta.MaxNoteTextLength > 0 ? meta.MaxNoteTextLength : 3000;
            TextBox.MaxLength = _maxLength;
        }
        catch { /* use default */ }

        UpdateCharCounter();
    }

    private async Task ShowContextNoteAsync(string noteId)
    {
        try
        {
            var note = await App.ApiClient.GetNoteAsync(noteId);
            ContextBorder.Visibility = Visibility.Visible;
            ContextText.Text = $"@{note.User?.Username}: {note.DisplayText.Truncate(120)}";
        }
        catch
        {
            ContextBorder.Visibility = Visibility.Collapsed;
        }
    }

    // ── UI event handlers ─────────────────────────────────────────────────────

    private void CwToggle_Checked(object sender, RoutedEventArgs e)
    {
        CwBox.Visibility = Visibility.Visible;
        ((ToggleButton)sender).Content = "Remove content warning";
    }

    private void CwToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        CwBox.Visibility = Visibility.Collapsed;
        CwBox.Text = string.Empty;
        ((ToggleButton)sender).Content = "Add content warning";
    }

    private void PollToggle_Checked(object sender, RoutedEventArgs e)
    {
        PollPanel.Visibility = Visibility.Visible;
        // Start with 2 choices
        if (PollChoicesPanel.Children.Count == 0)
        {
            AddPollChoiceBox();
            AddPollChoiceBox();
        }
        ((ToggleButton)sender).Content = "Remove poll";
    }

    private void PollToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        PollPanel.Visibility = Visibility.Collapsed;
        ((ToggleButton)sender).Content = "Add poll";
    }

    private void AddPollChoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (PollChoicesPanel.Children.Count >= 10) return;
        AddPollChoiceBox();
    }

    private void AddPollChoiceBox()
    {
        var n = PollChoicesPanel.Children.Count + 1;
        PollChoicesPanel.Children.Add(new TextBox
        {
            PlaceholderText = $"Choice {n}",
            MaxLength = 50,
            Margin = new Thickness(0, 0, 0, 4),
        });
    }

    private void VisibilityBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VisibleUsersBox is null) return; // Guard: may fire before XAML fields are fully assigned
        var tag = (VisibilityBox.SelectedItem as ComboBoxItem)?.Tag as string;
        VisibleUsersBox.Visibility = tag == "specified" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AttachDriveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_attachedFiles.Count >= 16)
        {
            ShowError("You can attach at most 16 files.");
            return;
        }

        try
        {
            // Show a drive file picker dialog
            var dlg = new DrivePickerDialog { XamlRoot = XamlRoot };
            if (await dlg.ShowAsync() == ContentDialogResult.Primary && dlg.SelectedFile != null)
            {
                if (_attachedFiles.All(f => f.Id != dlg.SelectedFile.Id))
                    _attachedFiles.Add(dlg.SelectedFile);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string fileId)
        {
            var file = _attachedFiles.FirstOrDefault(f => f.Id == fileId);
            if (file != null) _attachedFiles.Remove(file);
        }
    }

    private async void PostButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        var text = TextBox.Text.Trim();
        var hasCw   = CwToggle.IsChecked == true;
        var hasPoll = PollToggle.IsChecked == true;

        // Validation
        if (string.IsNullOrWhiteSpace(text) && _attachedFiles.Count == 0 &&
            _contextKind != "renote" && !hasPoll)
        {
            ShowError("Please write something, attach a file, or add a poll.");
            return;
        }
        if (text.Length > _maxLength)
        {
            ShowError($"Note is too long ({text.Length} / {_maxLength} characters).");
            return;
        }
        if (hasCw && string.IsNullOrWhiteSpace(CwBox.Text))
        {
            ShowError("Please fill in the content warning, or remove it.");
            return;
        }

        // Build poll if enabled
        PollCreate? poll = null;
        if (hasPoll)
        {
            var choices = PollChoicesPanel.Children
                .OfType<TextBox>()
                .Select(tb => tb.Text.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (choices.Count < 2)
            {
                ShowError("A poll needs at least 2 non-empty choices.");
                return;
            }

            poll = new PollCreate
            {
                Choices = choices,
                Multiple = PollMultipleBox.IsChecked == true,
            };

            // Expiry date (if set)
            if (PollExpiry.SelectedDate.HasValue)
            {
                var expires = new DateTimeOffset(PollExpiry.SelectedDate.Value.DateTime,
                    TimeZoneInfo.Local.GetUtcOffset(PollExpiry.SelectedDate.Value.DateTime));
                if (expires <= DateTimeOffset.Now)
                {
                    ShowError("Poll expiry must be in the future.");
                    return;
                }
                poll.ExpiresAt = expires.ToUnixTimeMilliseconds();
            }
        }

        // Parse visible user IDs from text input when visibility = specified
        List<string>? visibleUserIds = null;
        var visTag = (VisibilityBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "public";
        if (visTag == "specified")
        {
            // This would resolve @user@host to IDs — simplified: pass raw strings,
            // server will reject invalid ones with a clear error message
            visibleUserIds = VisibleUsersBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (visibleUserIds.Count == 0)
            {
                ShowError("Specify at least one recipient for a 'specified' note.");
                return;
            }
        }

        SetPosting(true);
        try
        {
            var reactionAcceptance = (ReactionAcceptanceBox.SelectedItem as ComboBoxItem)?.Tag as string;
            if (string.IsNullOrEmpty(reactionAcceptance)) reactionAcceptance = null;

            await App.ApiClient.CreateNoteAsync(
                text: string.IsNullOrWhiteSpace(text) ? null : text,
                visibility: visTag,
                visibleUserIds: visibleUserIds,
                cw: hasCw ? CwBox.Text.Trim() : null,
                localOnly: LocalOnlyBox.IsChecked == true,
                reactionAcceptance: reactionAcceptance,
                replyId: _contextKind == "reply" ? _contextNoteId : null,
                renoteId: _contextKind == "renote" ? _contextNoteId : null,
                fileIds: _attachedFiles.Select(f => f.Id).ToList(),
                poll: poll
            );

            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(TimelinePage), "home");
        }
        catch (MisskeyApiException ex)
        {
            ShowError($"Could not post ({(int)ex.StatusCode}): {ex.ResponseBody}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetPosting(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateCharCounter()
    {
        var len = TextBox.Text.Length;
        CharCounter.Text = $"{len} / {_maxLength}";
        CharCounter.Foreground = len > _maxLength * 0.9
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private void SetPosting(bool posting)
    {
        PostingBar.Visibility = posting ? Visibility.Visible : Visibility.Collapsed;
        PostButton.IsEnabled  = !posting;
    }

    private void ShowError(string msg)
    {
        ErrorBar.Message = msg;
        ErrorBar.IsOpen  = true;
    }
}

// ── Drive picker dialog ───────────────────────────────────────────────────────

/// <summary>Simple dialog for picking a file from the user's Misskey/Sharkey drive.</summary>
internal sealed class DrivePickerDialog : ContentDialog
{
    public DriveFile? SelectedFile { get; private set; }

    private readonly ListView _list = new() { Height = 300 };
    private string? _untilId;

    public DrivePickerDialog()
    {
        Title = "Pick a drive file";
        PrimaryButtonText = "Select";
        CloseButtonText = "Cancel";
        IsPrimaryButtonEnabled = false;
        Content = _list;

        _list.SelectionChanged += (_, _) =>
        {
            SelectedFile = _list.SelectedItem as DriveFile;
            IsPrimaryButtonEnabled = SelectedFile != null;
        };

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var files = await App.ApiClient.GetDriveFilesAsync(limit: 50, untilId: _untilId);
            foreach (var f in files) _list.Items.Add(f);
            if (files.Count > 0) _untilId = files[^1].Id;
        }
        catch { /* surface silently */ }
    }
}

// ── String extension ──────────────────────────────────────────────────────────
internal static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

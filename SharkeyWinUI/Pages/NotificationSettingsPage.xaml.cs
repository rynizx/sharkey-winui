using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class NotificationSettingsPage : Page
{
    // ── Static config ─────────────────────────────────────────────────────────

    private static readonly (string ApiKey, string Label)[] NotificationTypeLabels =
    [
        ("follow",                "New follower"),
        ("followRequestAccepted", "Follow request accepted"),
        ("receiveFollowRequest",  "Received follow request"),
        ("mention",               "Mention"),
        ("reply",                 "Reply"),
        ("renote",                "Renote"),
        ("quote",                 "Quote"),
        ("reaction",              "Reaction"),
        ("pollEnded",             "Poll ended"),
        ("scheduledNotePosted",   "Scheduled note posted"),
        ("scheduledNotePostFailed", "Scheduled note failed"),
        ("roleAssigned",          "Role assigned"),
        ("chatRoomInvitationReceived", "Chat room invitation"),
        ("achievementEarned",     "Achievement earned"),
        ("exportCompleted",       "Export completed"),
        ("login",                 "Login (security alert)"),
        ("createToken",           "Token created (security alert)"),
        ("app",                   "App notification"),
    ];

    private static readonly (string ApiKey, string Label)[] EmailTypeLabels =
    [
        ("mention",              "Mention"),
        ("reply",                "Reply"),
        ("quote",                "Quote"),
        ("reaction",             "Reaction"),
        ("follow",               "New follower"),
        ("receiveFollowRequest", "Follow request"),
    ];

    private static readonly string[] ReceiveOptions =
        ["all", "following", "follower", "mutualFollow", "followingOrFollower", "never"];

    private static readonly Dictionary<string, string> ReceiveOptionLabels = new()
    {
        ["all"]                  = "Everyone",
        ["following"]            = "Users I follow",
        ["follower"]             = "My followers",
        ["mutualFollow"]         = "Mutual follows",
        ["followingOrFollower"]  = "Following or follower",
        ["never"]                = "Never",
    };

    // ── Page state ────────────────────────────────────────────────────────────

    private List<ReceiveConfigRow> _rows = new();
    private readonly List<CheckBox> _emailCheckBoxes = new();
    private User? _me;
    private CancellationTokenSource _cts = new();

    public NotificationSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = LoadAsync(_cts.Token);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        SetLoading(true);
        StatusBar.IsOpen = false;
        try
        {
            _me = await App.ApiClient.GetMeAsync(ct);
            BuildReceiveConfigList();
            BuildEmailTypesList();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
        finally { SetLoading(false); }
    }

    private void BuildReceiveConfigList()
    {
        var existingConfig = _me?.NotificationReceiveConfig;
        _rows = NotificationTypeLabels.Select(t =>
        {
            // Read existing config value for this type via reflection
            var currentType = GetReceiveConfigForType(existingConfig, t.ApiKey)?.Type ?? "all";
            return new ReceiveConfigRow
            {
                ApiKey = t.ApiKey,
                Label = t.Label,
                Options = ReceiveOptions.Select(o => ReceiveOptionLabels[o]).ToList(),
                SelectedOption = ReceiveOptionLabels.GetValueOrDefault(currentType, "Everyone"),
            };
        }).ToList();
        ReceiveConfigList.ItemsSource = _rows;
    }

    private void BuildEmailTypesList()
    {
        EmailTypesPanel.Children.Clear();
        _emailCheckBoxes.Clear();

        var existingEmailTypes = _me?.EmailNotificationTypes ?? new List<string>();

        foreach (var (apiKey, label) in EmailTypeLabels)
        {
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = existingEmailTypes.Contains(apiKey),
                Tag = apiKey,
            };
            _emailCheckBoxes.Add(cb);
            EmailTypesPanel.Children.Add(cb);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        SetLoading(true);
        StatusBar.IsOpen = false;
        try
        {
            var configMap = BuildReceiveConfigMap();
            var emailTypes = _emailCheckBoxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (string)cb.Tag)
                .ToList();

            await App.ApiClient.UpdateAccountAsync(new AccountUpdateRequest
            {
                NotificationReceiveConfig = configMap,
                EmailNotificationTypes    = emailTypes,
            });

            ShowStatus("Notification settings saved.", InfoBarSeverity.Success);
        }
        catch (MisskeyApiException ex)
        {
            ShowStatus($"Could not save ({(int)ex.StatusCode}): {ex.ResponseBody}", InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SetLoading(false);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the NotificationReceiveConfigMap from the UI rows, converting
    /// display labels back to API keys.
    /// </summary>
    private NotificationReceiveConfigMap BuildReceiveConfigMap()
    {
        var labelToKey = ReceiveOptionLabels.ToDictionary(kv => kv.Value, kv => kv.Key);
        var map = new NotificationReceiveConfigMap();

        foreach (var row in _rows)
        {
            if (!labelToKey.TryGetValue(row.SelectedOption ?? "Everyone", out var typeKey))
                typeKey = "all";

            var config = new NotificationReceiveConfig { Type = typeKey };
            SetReceiveConfigForType(map, row.ApiKey, config);
        }
        return map;
    }

    /// <summary>Gets the per-type config from the map using the API key string.</summary>
    private static NotificationReceiveConfig? GetReceiveConfigForType(
        NotificationReceiveConfigMap? map, string apiKey) =>
        apiKey switch
        {
            "follow"                     => map?.Follow,
            "followRequestAccepted"      => map?.FollowRequestAccepted,
            "receiveFollowRequest"       => map?.ReceiveFollowRequest,
            "mention"                    => map?.Mention,
            "reply"                      => map?.Reply,
            "renote"                     => map?.Renote,
            "quote"                      => map?.Quote,
            "reaction"                   => map?.Reaction,
            "pollEnded"                  => map?.PollEnded,
            "scheduledNotePosted"        => map?.ScheduledNotePosted,
            "scheduledNotePostFailed"    => map?.ScheduledNotePostFailed,
            "roleAssigned"               => map?.RoleAssigned,
            "chatRoomInvitationReceived" => map?.ChatRoomInvitationReceived,
            "achievementEarned"          => map?.AchievementEarned,
            "exportCompleted"            => map?.ExportCompleted,
            "login"                      => map?.Login,
            "createToken"                => map?.CreateToken,
            "app"                        => map?.App,
            _                            => null,
        };

    private static void SetReceiveConfigForType(
        NotificationReceiveConfigMap map, string apiKey, NotificationReceiveConfig cfg)
    {
        switch (apiKey)
        {
            case "follow":                     map.Follow                      = cfg; break;
            case "followRequestAccepted":      map.FollowRequestAccepted       = cfg; break;
            case "receiveFollowRequest":       map.ReceiveFollowRequest        = cfg; break;
            case "mention":                    map.Mention                     = cfg; break;
            case "reply":                      map.Reply                       = cfg; break;
            case "renote":                     map.Renote                      = cfg; break;
            case "quote":                      map.Quote                       = cfg; break;
            case "reaction":                   map.Reaction                    = cfg; break;
            case "pollEnded":                  map.PollEnded                   = cfg; break;
            case "scheduledNotePosted":        map.ScheduledNotePosted         = cfg; break;
            case "scheduledNotePostFailed":    map.ScheduledNotePostFailed     = cfg; break;
            case "roleAssigned":               map.RoleAssigned                = cfg; break;
            case "chatRoomInvitationReceived": map.ChatRoomInvitationReceived  = cfg; break;
            case "achievementEarned":          map.AchievementEarned           = cfg; break;
            case "exportCompleted":            map.ExportCompleted             = cfg; break;
            case "login":                      map.Login                       = cfg; break;
            case "createToken":                map.CreateToken                 = cfg; break;
            case "app":                        map.App                         = cfg; break;
        }
    }

    private void SetLoading(bool loading)
        => LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    private void ShowStatus(string msg, InfoBarSeverity severity)
    {
        StatusBar.Message  = msg;
        StatusBar.Severity = severity;
        StatusBar.IsOpen   = true;
    }
}

// ── View model for a single receive-config row ────────────────────────────────

internal sealed class ReceiveConfigRow : INotifyPropertyChanged
{
    public string ApiKey { get; set; } = string.Empty;
    public string Label  { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();

    private string? _selected;
    public string? SelectedOption
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

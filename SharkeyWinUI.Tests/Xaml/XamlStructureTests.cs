using System.Xml.Linq;

namespace SharkeyWinUI.Tests.Xaml;

/// <summary>
/// Validate XAML file structure without running the app.
/// These tests give agents (and developers) a machine-readable description of
/// the UI: which controls exist, what accessibility names they carry, and that
/// the XAML files are well-formed XML.
///
/// When a test in this class fails it typically means a control was renamed,
/// removed, or had its accessibility annotation stripped — useful for catching
/// regressions that screenshots would normally reveal.
/// </summary>
[TestClass]
public class XamlStructureTests
{
    // ── XAML namespaces present in every WinUI 3 file ────────────────────────

    private static readonly XNamespace Ns  = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X   = "http://schemas.microsoft.com/winfx/2006/xaml";

    // ── Path helpers ─────────────────────────────────────────────────────────

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.sln").Any())
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new DirectoryNotFoundException(
                "Could not locate solution root from: " + AppContext.BaseDirectory);
    }

    private static XDocument LoadXaml(string relativePath)
    {
        var fullPath = Path.Combine(SolutionRoot(), relativePath);
        Assert.IsTrue(File.Exists(fullPath), $"XAML file not found: {fullPath}");
        return XDocument.Load(fullPath);
    }

    // ── Helper: find by x:Name attribute ─────────────────────────────────────

    private static XElement? FindByName(XDocument doc, string name)
        => doc.Descendants()
              .FirstOrDefault(e => e.Attribute(X + "Name")?.Value == name);

    // ── All XAML files must be well-formed ────────────────────────────────────

    [TestMethod]
    public void AllXamlFiles_AreWellFormedXml()
    {
        var root = SolutionRoot();
        var xamlFiles = Directory.GetFiles(
            Path.Combine(root, "SharkeyWinUI"),
            "*.xaml",
            SearchOption.AllDirectories);

        Assert.IsTrue(xamlFiles.Length > 0, "No XAML files found in the project.");

        var malformed = new List<string>();
        foreach (var file in xamlFiles)
        {
            try { XDocument.Load(file); }
            catch (Exception ex)
            {
                malformed.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Assert.AreEqual(0, malformed.Count,
            "Malformed XAML files:\n" + string.Join("\n", malformed));
    }

    // ── NoteCard control ──────────────────────────────────────────────────────

    [TestMethod]
    public void NoteCard_HasReplyButton_WithAccessibilityName()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var btn = FindByName(doc, "ReplyButton");

        Assert.IsNotNull(btn, "ReplyButton not found in NoteCard.xaml");
        var autoName = btn.Attribute("AutomationProperties.Name")?.Value;
        Assert.AreEqual("Reply", autoName,
            "ReplyButton should have AutomationProperties.Name=\"Reply\" for screen readers.");
    }

    [TestMethod]
    public void NoteCard_HasRenoteButton_WithAccessibilityName()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var btn = FindByName(doc, "RenoteButton");

        Assert.IsNotNull(btn, "RenoteButton not found in NoteCard.xaml");
        Assert.AreEqual("Renote", btn.Attribute("AutomationProperties.Name")?.Value);
    }

    [TestMethod]
    public void NoteCard_HasReactButton()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var btn = FindByName(doc, "ReactButton");

        Assert.IsNotNull(btn, "ReactButton not found in NoteCard.xaml");
    }

    [TestMethod]
    public void NoteCard_HasAvatarButton_WithAccessibilityName()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var avatarBtn = doc.Descendants(Ns + "Button")
            .FirstOrDefault(e => e.Attribute("ToolTipService.ToolTip")?.Value == "View profile");

        Assert.IsNotNull(avatarBtn, "Avatar button with ToolTip='View profile' not found in NoteCard.xaml");
        Assert.AreEqual("View profile", avatarBtn.Attribute("AutomationProperties.Name")?.Value,
            "Avatar button must have AutomationProperties.Name for screen reader users.");
    }

    [TestMethod]
    public void NoteCard_HasBodyTextBlock_ForNoteText()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var body = FindByName(doc, "BodyText");

        Assert.IsNotNull(body, "BodyText TextBlock not found in NoteCard.xaml");
        Assert.AreEqual(Ns + "TextBlock", body.Name,
            "BodyText should be a TextBlock element.");
    }

    [TestMethod]
    public void NoteCard_HasTimestampTextBlock()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var ts = FindByName(doc, "TimestampText");

        Assert.IsNotNull(ts, "TimestampText not found in NoteCard.xaml");
    }

    [TestMethod]
    public void NoteCard_HasUsernameText()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var username = FindByName(doc, "UsernameText");

        Assert.IsNotNull(username, "UsernameText not found in NoteCard.xaml");
    }

    [TestMethod]
    public void NoteCard_HasDisplayNamePanel()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var displayName = FindByName(doc, "DisplayNameText");

        Assert.IsNotNull(displayName, "DisplayNameText panel not found in NoteCard.xaml");
    }

    [TestMethod]
    public void NoteCard_HasRenoteHeader_ForBoostIndicator()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var header = FindByName(doc, "RenoteHeader");

        Assert.IsNotNull(header, "RenoteHeader not found in NoteCard.xaml — " +
            "this StackPanel shows the boost attribution line above a renoted note.");
    }

    [TestMethod]
    public void NoteCard_HasContentWarningPanel_WithToggle()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var cwPanel = FindByName(doc, "CwPanel");
        var cwToggle = FindByName(doc, "CwToggle");
        var cwText = FindByName(doc, "CwText");

        Assert.IsNotNull(cwPanel,  "CwPanel not found — required for content-warning display.");
        Assert.IsNotNull(cwToggle, "CwToggle not found — required for expanding CW content.");
        Assert.IsNotNull(cwText,   "CwText not found — required for showing the CW label.");
    }

    [TestMethod]
    public void NoteCard_HasMediaGrid_ForAttachments()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var grid = FindByName(doc, "MediaGrid");

        Assert.IsNotNull(grid, "MediaGrid not found in NoteCard.xaml — " +
            "required to display image/video attachments.");
    }

    [TestMethod]
    public void NoteCard_HasPollPanel()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var poll = FindByName(doc, "PollPanel");

        Assert.IsNotNull(poll, "PollPanel not found in NoteCard.xaml — required for poll display.");
    }

    [TestMethod]
    public void NoteCard_HasReactionsPanel()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var reactions = FindByName(doc, "ReactionsPanel");

        Assert.IsNotNull(reactions, "ReactionsPanel (ItemsControl) not found in NoteCard.xaml.");
    }

    [TestMethod]
    public void NoteCard_HasInstanceBadge_ForFederatedNotes()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");
        var badge = FindByName(doc, "InstanceBadge");

        Assert.IsNotNull(badge, "InstanceBadge not found in NoteCard.xaml — " +
            "required to show the federated instance name alongside the username.");
    }

    [TestMethod]
    public void NoteCard_ReplyRenoteReact_AllHaveTooltips()
    {
        var doc = LoadXaml("SharkeyWinUI/Controls/NoteCard.xaml");

        foreach (var (name, expectedTip) in new[]
        {
            ("ReplyButton",  "Reply"),
            ("RenoteButton", "Renote"),
            ("ReactButton",  "React"),
        })
        {
            var btn = FindByName(doc, name);
            Assert.IsNotNull(btn, $"{name} not found.");
            Assert.AreEqual(
                expectedTip,
                btn.Attribute("ToolTipService.ToolTip")?.Value,
                $"{name} should have ToolTipService.ToolTip=\"{expectedTip}\".");
        }
    }

    [TestMethod]
    public void NoteCard_CodeBehind_SetsAccessibilityName_ForDynamicPollButtons()
    {
        var fullPath = Path.Combine(SolutionRoot(), "SharkeyWinUI/Controls/NoteCard.xaml.cs");
        Assert.IsTrue(File.Exists(fullPath), $"Code-behind file not found: {fullPath}");

        var code = File.ReadAllText(fullPath);
        StringAssert.Contains(
            code,
            "AutomationProperties.SetName(btn, $\"Vote for {choice.Text}\")",
            "Poll choice buttons should set an AutomationProperties.Name for screen readers.");
    }

    [TestMethod]
    public void NoteCard_CodeBehind_SetsAccessibilityName_ForDynamicReactionButtons()
    {
        var fullPath = Path.Combine(SolutionRoot(), "SharkeyWinUI/Controls/NoteCard.xaml.cs");
        Assert.IsTrue(File.Exists(fullPath), $"Code-behind file not found: {fullPath}");

        var code = File.ReadAllText(fullPath);
        StringAssert.Contains(
            code,
            "AutomationProperties.SetName(btn, $\"React with {kv.Key}, {reactionCountText}\")",
            "Reaction buttons should set an AutomationProperties.Name for screen readers.");
    }

    // ── Page-level structure ──────────────────────────────────────────────────

    [TestMethod]
    public void TimelinePage_XamlIsWellFormed()
    {
        var doc = LoadXaml("SharkeyWinUI/Pages/TimelinePage.xaml");
        // If LoadXaml succeeds the file parsed without error
        Assert.IsNotNull(doc.Root);
    }

    [TestMethod]
    public void ComposePage_XamlIsWellFormed()
    {
        var doc = LoadXaml("SharkeyWinUI/Pages/ComposePage.xaml");
        Assert.IsNotNull(doc.Root);
    }

    [TestMethod]
    public void ComposePage_PostButton_HasAccessibilityMetadata()
    {
        var doc = LoadXaml("SharkeyWinUI/Pages/ComposePage.xaml");
        var postButton = FindByName(doc, "PostButton");

        Assert.IsNotNull(postButton, "PostButton not found in ComposePage.xaml");
        Assert.AreEqual("Post note", postButton.Attribute("ToolTipService.ToolTip")?.Value,
            "PostButton should have a tooltip for consistency with other action buttons.");
        Assert.AreEqual("Post note", postButton.Attribute("AutomationProperties.Name")?.Value,
            "PostButton should expose an accessibility name for screen readers.");
    }

    [TestMethod]
    public void NotificationsPage_XamlIsWellFormed()
    {
        var doc = LoadXaml("SharkeyWinUI/Pages/NotificationsPage.xaml");
        Assert.IsNotNull(doc.Root);
    }

    [TestMethod]
    public void NotificationsPage_LoadMoreButton_StartsCollapsed()
    {
        var doc = LoadXaml("SharkeyWinUI/Pages/NotificationsPage.xaml");
        var loadMoreButton = FindByName(doc, "LoadMoreButton");

        Assert.IsNotNull(loadMoreButton, "LoadMoreButton not found in NotificationsPage.xaml");
        Assert.AreEqual("Collapsed", loadMoreButton.Attribute("Visibility")?.Value,
            "LoadMoreButton should start collapsed to avoid initial flash before data loads.");
    }

    [TestMethod]
    public void TimelinePage_CodeBehind_ShowsLoadMoreOnlyWhenPageIsFull()
    {
        var fullPath = Path.Combine(SolutionRoot(), "SharkeyWinUI/Pages/TimelinePage.xaml.cs");
        Assert.IsTrue(File.Exists(fullPath), $"Code-behind file not found: {fullPath}");

        var code = File.ReadAllText(fullPath);
        StringAssert.Contains(
            code,
            "private const int TimelinePageSize = 30;",
            "Timeline page size constant should remain explicit for pagination consistency.");
        StringAssert.Contains(
            code,
            "LoadMoreButton.Visibility = batch.Count == TimelinePageSize ? Visibility.Visible : Visibility.Collapsed;",
            "Timeline should only show LoadMore when the API returns a full page.");
    }

    [TestMethod]
    public void ProfilePage_XamlIsWellFormed()
    {
        var doc = LoadXaml("SharkeyWinUI/Pages/ProfilePage.xaml");
        Assert.IsNotNull(doc.Root);
    }

    [TestMethod]
    public void LoginPage_XamlIsWellFormed()
    {
        var doc = LoadXaml("SharkeyWinUI/Pages/LoginPage.xaml");
        Assert.IsNotNull(doc.Root);
    }
}

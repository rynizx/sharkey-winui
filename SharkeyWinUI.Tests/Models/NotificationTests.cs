using System.Text.Json;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Tests.Models;

[TestClass]
public class NotificationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── JSON deserialization ─────────────────────────────────────────────────

    [TestMethod]
    public void Deserialize_FollowNotification_ParsesTypeAndUser()
    {
        const string json = """
            {
                "id": "notif1",
                "createdAt": "2024-06-01T10:00:00Z",
                "isRead": false,
                "type": "follow",
                "userId": "u2",
                "user": {
                    "id": "u2",
                    "username": "bob",
                    "name": "Bob"
                }
            }
            """;

        var notif = JsonSerializer.Deserialize<Notification>(json, JsonOpts)!;

        Assert.AreEqual("notif1", notif.Id);
        Assert.AreEqual("follow", notif.Type);
        Assert.IsFalse(notif.IsRead);
        Assert.IsNotNull(notif.User);
        Assert.AreEqual("bob", notif.User!.Username);
    }

    [TestMethod]
    public void Deserialize_MentionNotification_HasNoteAttached()
    {
        const string json = """
            {
                "id": "notif2",
                "createdAt": "2024-06-01T10:00:00Z",
                "isRead": true,
                "type": "mention",
                "userId": "u3",
                "user": { "id": "u3", "username": "carol", "name": "Carol" },
                "note": {
                    "id": "note99",
                    "userId": "u3",
                    "createdAt": "2024-06-01T10:00:00Z",
                    "text": "Hey @alice, check this out!"
                }
            }
            """;

        var notif = JsonSerializer.Deserialize<Notification>(json, JsonOpts)!;

        Assert.AreEqual("mention", notif.Type);
        Assert.IsTrue(notif.IsRead);
        Assert.IsNotNull(notif.Note);
        Assert.AreEqual("note99", notif.Note!.Id);
    }

    [TestMethod]
    public void Deserialize_ReactionNotification_HasReactionString()
    {
        const string json = """
            {
                "id": "notif3",
                "createdAt": "2024-06-01T10:00:00Z",
                "isRead": false,
                "type": "reaction",
                "userId": "u4",
                "user": { "id": "u4", "username": "diana", "name": "Diana" },
                "reaction": "❤️",
                "note": {
                    "id": "mynote1",
                    "userId": "me",
                    "createdAt": "2024-06-01T08:00:00Z",
                    "text": "My original post"
                }
            }
            """;

        var notif = JsonSerializer.Deserialize<Notification>(json, JsonOpts)!;

        Assert.AreEqual("reaction", notif.Type);
        Assert.AreEqual("❤️", notif.Reaction);
        Assert.IsNotNull(notif.Note);
    }

    [TestMethod]
    public void Deserialize_AchievementNotification_ParsesAchievementField()
    {
        const string json = """
            {
                "id": "notif4",
                "createdAt": "2024-06-01T10:00:00Z",
                "isRead": false,
                "type": "achievementEarned",
                "achievement": "my_first_note"
            }
            """;

        var notif = JsonSerializer.Deserialize<Notification>(json, JsonOpts)!;

        Assert.AreEqual("achievementEarned", notif.Type);
        Assert.AreEqual("my_first_note", notif.Achievement);
        Assert.IsNull(notif.User);
    }

    [TestMethod]
    public void Deserialize_AppNotification_ParsesBodyAndHeader()
    {
        const string json = """
            {
                "id": "notif5",
                "createdAt": "2024-06-01T10:00:00Z",
                "isRead": false,
                "type": "app",
                "header": "App update",
                "body": "A new version is available",
                "icon": "https://example.social/icon.png"
            }
            """;

        var notif = JsonSerializer.Deserialize<Notification>(json, JsonOpts)!;

        Assert.AreEqual("app", notif.Type);
        Assert.AreEqual("App update", notif.Header);
        Assert.AreEqual("A new version is available", notif.Body);
        Assert.IsNotNull(notif.Icon);
    }

    // ── Summary computed property ────────────────────────────────────────────

    [TestMethod]
    public void Summary_FollowType_ContainsUsername()
    {
        var notif = new Notification
        {
            Type = "follow",
            User = new User { Username = "bob", DisplayName = "Bob" },
        };

        StringAssert.Contains(notif.Summary, "Bob");
    }

    [TestMethod]
    public void Summary_ReactionType_ContainsReactionEmoji()
    {
        var notif = new Notification
        {
            Type = "reaction",
            Reaction = "🎉",
            User = new User { Username = "carol", DisplayName = "Carol" },
        };

        StringAssert.Contains(notif.Summary, "🎉");
    }

    [TestMethod]
    public void Summary_UnknownType_DoesNotThrow()
    {
        var notif = new Notification { Type = "some_future_notification_type" };

        // Should return a non-null fallback string without throwing
        Assert.IsNotNull(notif.Summary);
    }

    [TestMethod]
    public void Summary_WithNullUser_ReturnsGracefulFallback()
    {
        var notif = new Notification { Type = "follow", User = null };

        // Should not throw; user name falls back to "Someone"
        StringAssert.Contains(notif.Summary, "Someone");
    }
}

using System.Text.Json;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Tests.Models;

[TestClass]
public class UserTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── JSON deserialization ─────────────────────────────────────────────────

    [TestMethod]
    public void Deserialize_LocalUser_HasNullHost()
    {
        const string json = """
            {
                "id": "localuser1",
                "username": "alice",
                "name": "Alice",
                "avatarUrl": "https://example.social/avatars/alice.png"
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.AreEqual("localuser1", user.Id);
        Assert.AreEqual("alice", user.Username);
        Assert.AreEqual("Alice", user.DisplayName);
        Assert.IsNull(user.Host);
    }

    [TestMethod]
    public void Deserialize_FederatedUser_HasHost()
    {
        const string json = """
            {
                "id": "feduser1",
                "username": "bob",
                "name": "Bob",
                "host": "mastodon.social",
                "avatarUrl": "https://mastodon.social/avatars/bob.png"
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.AreEqual("mastodon.social", user.Host);
        Assert.AreEqual("bob", user.Username);
    }

    [TestMethod]
    public void Deserialize_User_ParsesAvatarUrl()
    {
        const string json = """
            {
                "id": "u1",
                "username": "charlie",
                "avatarUrl": "https://example.social/files/avatar.webp",
                "avatarBlurhash": "L4ADc900fQ00_3j[j[j[~pj[Rjj["
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.AreEqual("https://example.social/files/avatar.webp", user.AvatarUrl);
        Assert.IsNotNull(user.AvatarBlurhash);
    }

    [TestMethod]
    public void Deserialize_BotUser_IsBotIsTrue()
    {
        const string json = """
            {
                "id": "bot1",
                "username": "rss_bot",
                "isBot": true
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.IsTrue(user.IsBot);
    }

    [TestMethod]
    public void Deserialize_UserWithCustomEmojis_ParsesEmojiDictionary()
    {
        const string json = """
            {
                "id": "u1",
                "username": "dana",
                "emojis": {
                    "neodog": "https://example.social/emoji/neodog.png",
                    "blobhaj": "https://example.social/emoji/blobhaj.webp"
                }
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.AreEqual(2, user.Emojis.Count);
        Assert.AreEqual("https://example.social/emoji/neodog.png", user.Emojis["neodog"]);
    }

    [TestMethod]
    public void Deserialize_DetailedUser_ParsesFollowerCounts()
    {
        const string json = """
            {
                "id": "u1",
                "username": "evan",
                "followersCount": 1200,
                "followingCount": 340,
                "notesCount": 8823
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.AreEqual(1200, user.FollowersCount);
        Assert.AreEqual(340, user.FollowingCount);
        Assert.AreEqual(8823, user.NotesCount);
    }

    [TestMethod]
    public void Deserialize_UserWithInstance_ParsesInstanceBadge()
    {
        const string json = """
            {
                "id": "u1",
                "username": "frank",
                "host": "fosstodon.org",
                "instance": {
                    "name": "Fosstodon",
                    "softwareName": "mastodon",
                    "softwareVersion": "4.2.0",
                    "iconUrl": "https://fosstodon.org/favicon.ico",
                    "faviconUrl": "https://fosstodon.org/favicon.ico",
                    "themeColor": "#191b22"
                }
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.IsNotNull(user.Instance);
        Assert.AreEqual("Fosstodon", user.Instance!.Name);
    }

    [TestMethod]
    public void Deserialize_UserWithDescription_ParsesBio()
    {
        const string json = """
            {
                "id": "u1",
                "username": "grace",
                "description": "Just a person on the internet :blobhaj:"
            }
            """;

        var user = JsonSerializer.Deserialize<User>(json, JsonOpts)!;

        Assert.AreEqual("Just a person on the internet :blobhaj:", user.Description);
    }

    // ── Default values ───────────────────────────────────────────────────────

    [TestMethod]
    public void User_DefaultEmojis_IsEmptyDictionary()
    {
        var user = new User();

        Assert.IsNotNull(user.Emojis);
        Assert.AreEqual(0, user.Emojis.Count);
    }

    [TestMethod]
    public void User_DefaultAvatarDecorations_IsEmptyList()
    {
        var user = new User();

        Assert.IsNotNull(user.AvatarDecorations);
        Assert.AreEqual(0, user.AvatarDecorations.Count);
    }
}

using System.Text.Json;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Tests.Models;

[TestClass]
public class NoteTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── JSON deserialization ─────────────────────────────────────────────────

    [TestMethod]
    public void Deserialize_BasicNote_ParsesCoreFields()
    {
        const string json = """
            {
                "id": "abc123",
                "userId": "user1",
                "createdAt": "2024-06-01T12:00:00Z",
                "text": "Hello Sharkey!",
                "visibility": "public",
                "renoteCount": 5,
                "repliesCount": 3,
                "reactions": {}
            }
            """;

        var note = JsonSerializer.Deserialize<Note>(json, JsonOpts)!;

        Assert.AreEqual("abc123", note.Id);
        Assert.AreEqual("user1", note.UserId);
        Assert.AreEqual("Hello Sharkey!", note.Text);
        Assert.AreEqual("public", note.Visibility);
        Assert.AreEqual(5, note.RenoteCount);
        Assert.AreEqual(3, note.RepliesCount);
    }

    [TestMethod]
    public void Deserialize_NoteWithReactions_ParsesDictionary()
    {
        const string json = """
            {
                "id": "r1",
                "userId": "u1",
                "createdAt": "2024-01-01T00:00:00Z",
                "reactions": { "👍": 3, "❤️": 7, ":neodog_flag_gay:": 1 }
            }
            """;

        var note = JsonSerializer.Deserialize<Note>(json, JsonOpts)!;

        Assert.AreEqual(3, note.Reactions["👍"]);
        Assert.AreEqual(7, note.Reactions["❤️"]);
        Assert.AreEqual(1, note.Reactions[":neodog_flag_gay:"]);
    }

    [TestMethod]
    public void Deserialize_PureRenote_HasNullTextAndNestedRenote()
    {
        const string json = """
            {
                "id": "renote1",
                "userId": "u1",
                "createdAt": "2024-01-01T00:00:00Z",
                "renoteId": "original1",
                "renote": {
                    "id": "original1",
                    "userId": "u2",
                    "createdAt": "2024-01-01T00:00:00Z",
                    "text": "Original post"
                }
            }
            """;

        var note = JsonSerializer.Deserialize<Note>(json, JsonOpts)!;

        Assert.IsNull(note.Text);
        Assert.AreEqual("original1", note.RenoteId);
        Assert.IsNotNull(note.Renote);
        Assert.AreEqual("Original post", note.Renote!.Text);
    }

    [TestMethod]
    public void Deserialize_NoteWithContentWarning_ParsesCw()
    {
        const string json = """
            {
                "id": "cw1",
                "userId": "u1",
                "createdAt": "2024-01-01T00:00:00Z",
                "text": "Spoiler body",
                "cw": "Spoiler warning"
            }
            """;

        var note = JsonSerializer.Deserialize<Note>(json, JsonOpts)!;

        Assert.AreEqual("Spoiler warning", note.ContentWarning);
        Assert.AreEqual("Spoiler body", note.Text);
    }

    [TestMethod]
    public void Deserialize_NoteWithPoll_ParsesPollAndChoices()
    {
        const string json = """
            {
                "id": "p1",
                "userId": "u1",
                "createdAt": "2024-01-01T00:00:00Z",
                "text": "What do you prefer?",
                "poll": {
                    "choices": [
                        { "text": "Dogs", "votes": 10, "isVoted": false },
                        { "text": "Cats", "votes": 15, "isVoted": true }
                    ],
                    "multiple": false
                }
            }
            """;

        var note = JsonSerializer.Deserialize<Note>(json, JsonOpts)!;

        Assert.IsNotNull(note.Poll);
        Assert.AreEqual(2, note.Poll!.Choices.Count);
        Assert.AreEqual("Dogs", note.Poll.Choices[0].Text);
        Assert.AreEqual(10, note.Poll.Choices[0].Votes);
        Assert.IsFalse(note.Poll.Choices[0].IsVoted);
        Assert.IsTrue(note.Poll.Choices[1].IsVoted);
    }

    [TestMethod]
    public void Deserialize_NoteWithFiles_ParsesDriveFiles()
    {
        const string json = """
            {
                "id": "media1",
                "userId": "u1",
                "createdAt": "2024-01-01T00:00:00Z",
                "files": [
                    {
                        "id": "file1",
                        "createdAt": "2024-01-01T00:00:00Z",
                        "name": "photo.jpg",
                        "type": "image/jpeg",
                        "md5": "abc",
                        "size": 102400,
                        "url": "https://example.com/photo.jpg"
                    }
                ]
            }
            """;

        var note = JsonSerializer.Deserialize<Note>(json, JsonOpts)!;

        Assert.AreEqual(1, note.Files.Count);
        Assert.AreEqual("file1", note.Files[0].Id);
        Assert.AreEqual("image/jpeg", note.Files[0].Type);
    }

    [TestMethod]
    public void Deserialize_NoteWithFederatedUser_ParsesUri()
    {
        const string json = """
            {
                "id": "fed1",
                "userId": "u1",
                "createdAt": "2024-01-01T00:00:00Z",
                "uri": "https://mastodon.social/users/foo/statuses/1",
                "user": {
                    "id": "u1",
                    "username": "foo",
                    "host": "mastodon.social"
                }
            }
            """;

        var note = JsonSerializer.Deserialize<Note>(json, JsonOpts)!;

        Assert.IsNotNull(note.Uri);
        Assert.IsNotNull(note.User?.Host);
    }

    // ── Computed properties ──────────────────────────────────────────────────

    [TestMethod]
    public void IsPureRenote_WhenTextNullAndRenoteIdPresent_ReturnsTrue()
    {
        var note = new Note { Text = null, RenoteId = "xyz" };

        Assert.IsTrue(note.IsPureRenote);
    }

    [TestMethod]
    public void IsPureRenote_WhenTextPresentAndRenoteIdPresent_ReturnsFalse()
    {
        // Quote-renote: has both its own text and a renoteId
        var note = new Note { Text = "My take on this", RenoteId = "xyz" };

        Assert.IsFalse(note.IsPureRenote);
    }

    [TestMethod]
    public void IsPureRenote_WhenNoRenoteId_ReturnsFalse()
    {
        var note = new Note { Text = null, RenoteId = null };

        Assert.IsFalse(note.IsPureRenote);
    }

    [TestMethod]
    public void HasContentWarning_WhenCwPresent_ReturnsTrue()
    {
        var note = new Note { ContentWarning = "Spoilers ahead" };

        Assert.IsTrue(note.HasContentWarning);
    }

    [TestMethod]
    public void HasContentWarning_WhenCwNull_ReturnsFalse()
    {
        var note = new Note { ContentWarning = null };

        Assert.IsFalse(note.HasContentWarning);
    }

    [TestMethod]
    public void HasContentWarning_WhenCwEmpty_ReturnsFalse()
    {
        var note = new Note { ContentWarning = "" };

        Assert.IsFalse(note.HasContentWarning);
    }

    [TestMethod]
    public void DisplayText_WhenTextPresent_ReturnsText()
    {
        var note = new Note { Text = "Hello world" };

        Assert.AreEqual("Hello world", note.DisplayText);
    }

    [TestMethod]
    public void DisplayText_WhenTextNull_ReturnsEmptyString()
    {
        var note = new Note { Text = null };

        Assert.AreEqual(string.Empty, note.DisplayText);
    }

    [TestMethod]
    public void HasMedia_WhenFilesPresent_ReturnsTrue()
    {
        var note = new Note();
        note.Files.Add(new DriveFile { Id = "f1" });

        Assert.IsTrue(note.HasMedia);
    }

    [TestMethod]
    public void HasMedia_WhenNoFiles_ReturnsFalse()
    {
        var note = new Note();

        Assert.IsFalse(note.HasMedia);
    }

    [TestMethod]
    public void IsReply_WhenReplyIdPresent_ReturnsTrue()
    {
        var note = new Note { ReplyId = "parent1" };

        Assert.IsTrue(note.IsReply);
    }

    [TestMethod]
    public void IsReply_WhenNoReplyId_ReturnsFalse()
    {
        var note = new Note { ReplyId = null };

        Assert.IsFalse(note.IsReply);
    }

    [TestMethod]
    public void IsRenote_WhenRenoteIdPresent_ReturnsTrue()
    {
        var note = new Note { RenoteId = "other1" };

        Assert.IsTrue(note.IsRenote);
    }

    [TestMethod]
    public void IsFederated_WhenUriAndHostBothPresent_ReturnsTrue()
    {
        var note = new Note
        {
            Uri = "https://mastodon.social/users/foo/statuses/1",
            User = new User { Host = "mastodon.social" },
        };

        Assert.IsTrue(note.IsFederated);
    }

    [TestMethod]
    public void IsFederated_WhenUriMissing_ReturnsFalse()
    {
        var note = new Note { User = new User { Host = "mastodon.social" } };

        Assert.IsFalse(note.IsFederated);
    }

    [TestMethod]
    public void IsFederated_WhenHostMissing_ReturnsFalse()
    {
        var note = new Note
        {
            Uri = "https://remote.example/notes/1",
            User = new User { Host = null },
        };

        Assert.IsFalse(note.IsFederated);
    }

    // ── Poll helpers ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Poll_TotalVotes_SumsAllChoiceVotes()
    {
        var poll = new Poll();
        poll.Choices.Add(new PollChoice { Text = "Yes", Votes = 10 });
        poll.Choices.Add(new PollChoice { Text = "No", Votes = 7 });
        poll.Choices.Add(new PollChoice { Text = "Maybe", Votes = 3 });

        Assert.AreEqual(20, poll.TotalVotes);
    }

    [TestMethod]
    public void Poll_IsExpired_WhenExpiresAtInThePast_ReturnsTrue()
    {
        var poll = new Poll { ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) };

        Assert.IsTrue(poll.IsExpired);
    }

    [TestMethod]
    public void Poll_IsExpired_WhenExpiresAtInTheFuture_ReturnsFalse()
    {
        var poll = new Poll { ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };

        Assert.IsFalse(poll.IsExpired);
    }

    [TestMethod]
    public void Poll_IsExpired_WhenNoExpiresAt_ReturnsFalse()
    {
        var poll = new Poll { ExpiresAt = null };

        Assert.IsFalse(poll.IsExpired);
    }

    // ── DriveFile helpers ────────────────────────────────────────────────────

    [TestMethod]
    public void DriveFile_IsImage_WhenMimeTypeIsImageJpeg_ReturnsTrue()
    {
        var file = new DriveFile { Type = "image/jpeg" };

        Assert.IsTrue(file.IsImage);
    }

    [TestMethod]
    public void DriveFile_IsVideo_WhenMimeTypeIsMp4_ReturnsTrue()
    {
        var file = new DriveFile { Type = "video/mp4" };

        Assert.IsTrue(file.IsVideo);
    }

    [TestMethod]
    public void DriveFile_IsAudio_WhenMimeTypeIsMp3_ReturnsTrue()
    {
        var file = new DriveFile { Type = "audio/mpeg" };

        Assert.IsTrue(file.IsAudio);
    }
}

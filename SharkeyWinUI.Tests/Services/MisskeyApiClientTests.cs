using SharkeyWinUI.Services;

namespace SharkeyWinUI.Tests.Services;

[TestClass]
public class MisskeyApiClientTests
{
    // ── Configure ────────────────────────────────────────────────────────────

    [TestMethod]
    public void Configure_SetsServerUrl_WithTrailingSlashStripped()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social/", null);

        Assert.AreEqual("https://example.social", client.ServerUrl);
    }

    [TestMethod]
    public void Configure_SetsServerUrl_WhenNoTrailingSlash()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", "mytoken");

        Assert.AreEqual("https://example.social", client.ServerUrl);
        Assert.AreEqual("mytoken", client.Token);
    }

    [TestMethod]
    public void Configure_SetsToken_ToNull_WhenNoAuth()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        Assert.IsNull(client.Token);
    }

    // ── GenerateMiAuthSession ────────────────────────────────────────────────

    [TestMethod]
    public void GenerateMiAuthSession_CheckUrl_ContainsApiMiAuthPath()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        var (checkUrl, _) = client.GenerateMiAuthSession("SharkeyWinUI", ["read:account"]);

        StringAssert.StartsWith(checkUrl, "https://example.social/api/miauth/");
        StringAssert.EndsWith(checkUrl, "/check");
    }

    [TestMethod]
    public void GenerateMiAuthSession_BrowserUrl_ContainsEncodedAppName()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        var (_, browserUrl) = client.GenerateMiAuthSession("Sharkey WinUI", ["read:account"]);

        StringAssert.Contains(browserUrl, "name=Sharkey%20WinUI");
    }

    [TestMethod]
    public void GenerateMiAuthSession_BrowserUrl_ContainsEncodedPermissions()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        var (_, browserUrl) = client.GenerateMiAuthSession(
            "SharkeyWinUI",
            ["read:account", "write:notes"]);

        // Commas in the joined permission string are URL-encoded
        StringAssert.Contains(browserUrl, "permission=");
        StringAssert.Contains(browserUrl, "read%3Aaccount");
        StringAssert.Contains(browserUrl, "write%3Anotes");
    }

    [TestMethod]
    public void GenerateMiAuthSession_BothUrls_ContainSameSessionId()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        var (checkUrl, browserUrl) = client.GenerateMiAuthSession("App", ["read:account"]);

        // Extract session ID from checkUrl: /api/miauth/<id>/check
        var checkParts = checkUrl.Split('/');
        var sessionId = checkParts[^2]; // second-to-last segment

        StringAssert.Contains(browserUrl, sessionId);
    }

    [TestMethod]
    public void GenerateMiAuthSession_SessionId_IsValidGuid()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        var (checkUrl, _) = client.GenerateMiAuthSession("App", ["read:account"]);

        var parts = checkUrl.Split('/');
        var sessionId = parts[^2];

        Assert.IsTrue(Guid.TryParse(sessionId, out _),
            $"Expected a GUID session ID but got: {sessionId}");
    }

    [TestMethod]
    public void GenerateMiAuthSession_EachCall_ProducesUniqueSessionId()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        var (check1, _) = client.GenerateMiAuthSession("App", ["read:account"]);
        var (check2, _) = client.GenerateMiAuthSession("App", ["read:account"]);

        Assert.AreNotEqual(check1, check2,
            "Each MiAuth session call should produce a unique session ID.");
    }

    [TestMethod]
    public void GenerateMiAuthSession_WithCallback_BrowserUrlContainsCallback()
    {
        var client = new MisskeyApiClient();
        client.Configure("https://example.social", null);

        var (_, browserUrl) = client.GenerateMiAuthSession(
            "SharkeyWinUI",
            ["read:account"],
            callbackUrl: "sharkeywinui://auth");

        StringAssert.Contains(browserUrl, "callback=");
        StringAssert.Contains(browserUrl, "sharkeywinui%3A%2F%2Fauth");
    }
}

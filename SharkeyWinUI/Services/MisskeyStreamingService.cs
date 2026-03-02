using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Services;

/// <summary>
/// Connects to the Misskey/Sharkey Streaming API over WebSocket.
///
/// Protocol (from misskey-js/src/streaming.ts):
///   Connect to: wss://{host}/streaming?i={token}
///   Subscribe:  { "type": "connect", "body": { "channel": "homeTimeline", "id": "1" } }
///   Messages:   { "type": "channel", "body": { "id": "1", "type": "note", "body": {...} } }
///   Broadcast:  { "type": "noteUpdated", "body": { "id": "noteId", "type": "reacted", "body": {...} } }
/// </summary>
public class MisskeyStreamingService : IDisposable
{
    // ── Public events ────────────────────────────────────────────────────────

    public event Action<Note>? NoteReceived;
    public event Action<NoteUpdatedEvent>? NoteUpdated;
    public event Action<Notification>? NotificationReceived;
    public event Action? Connected;
    public event Action? Disconnected;

    // ── State ────────────────────────────────────────────────────────────────

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, string> _channelIds = new(); // channelName -> id
    private int _idCounter;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Connection ────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to the streaming endpoint and begins receiving messages.
    /// Call <see cref="SubscribeChannelAsync"/> after connecting.
    /// </summary>
    public async Task ConnectAsync(string serverUrl, string token, CancellationToken ct = default)
    {
        Disconnect();

        var wsUrl = serverUrl.TrimEnd('/')
            .Replace("https://", "wss://")
            .Replace("http://", "ws://");
        wsUrl += $"/streaming?i={Uri.EscapeDataString(token)}&_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("User-Agent", "SharkeyWinUI/1.0");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
        Connected?.Invoke();

        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    /// <summary>Disconnects from the streaming endpoint.</summary>
    public void Disconnect()
    {
        _cts?.Cancel();
        _channelIds.Clear();
        if (_ws?.State == WebSocketState.Open)
        {
            try { _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(1000); }
            catch { /* ignore */ }
        }
        _ws?.Dispose();
        _ws = null;
        _cts?.Dispose();
        _cts = null;
    }

    // ── Channel subscriptions ────────────────────────────────────────────────

    /// <summary>
    /// Subscribes to a named channel. Idempotent: calling twice for the same
    /// channel name re-uses the existing subscription ID.
    /// </summary>
    public async Task SubscribeChannelAsync(string channelName, object? @params = null, CancellationToken ct = default)
    {
        // Re-use existing id if already subscribed (prevents duplicate subscriptions on reconnect)
        if (!_channelIds.TryGetValue(channelName, out var id))
        {
            id = (++_idCounter).ToString();
            _channelIds[channelName] = id;
        }

        var msg = new
        {
            type = "connect",
            body = new { channel = channelName, id, @params }
        };
        await SendJsonAsync(msg, ct);
    }

    /// <summary>Unsubscribes from a previously subscribed channel.</summary>
    public async Task UnsubscribeChannelAsync(string channelName, CancellationToken ct = default)
    {
        if (!_channelIds.TryGetValue(channelName, out var id)) return;
        _channelIds.Remove(channelName);
        await SendJsonAsync(new { type = "disconnect", body = new { id } }, ct);
    }

    /// <summary>Subscribes to real-time updates for a specific note (reactions, deletions).</summary>
    public async Task SubscribeNoteAsync(string noteId, CancellationToken ct = default)
    {
        await SendJsonAsync(new { type = "subNote", body = new { id = noteId } }, ct);
    }

    /// <summary>Unsubscribes from real-time updates for a specific note.</summary>
    public async Task UnsubscribeNoteAsync(string noteId, CancellationToken ct = default)
    {
        await SendJsonAsync(new { type = "unsubNote", body = new { id = noteId } }, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SendJsonAsync(object payload, CancellationToken ct)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts));
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var sb = new StringBuilder();

        try
        {
            while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Disconnected?.Invoke();
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                HandleMessage(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    private void HandleMessage(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp)) return;
            var type = typeProp.GetString();

            switch (type)
            {
                case "channel" when root.TryGetProperty("body", out var channelBody):
                    HandleChannelMessage(channelBody);
                    break;

                case "noteUpdated" when root.TryGetProperty("body", out var nuBody):
                    var nue = nuBody.Deserialize<NoteUpdatedEvent>(JsonOpts);
                    if (nue != null) NoteUpdated?.Invoke(nue);
                    break;
            }
        }
        catch (JsonException) { /* malformed message — ignore */ }
    }

    private void HandleChannelMessage(JsonElement body)
    {
        if (!body.TryGetProperty("type", out var typeProp)) return;
        var type = typeProp.GetString();

        if (!body.TryGetProperty("body", out var payloadProp)) return;

        switch (type)
        {
            case "note":
                var note = payloadProp.Deserialize<Note>(JsonOpts);
                if (note != null) NoteReceived?.Invoke(note);
                break;

            case "notification":
                var notif = payloadProp.Deserialize<Notification>(JsonOpts);
                if (notif != null) NotificationReceived?.Invoke(notif);
                break;
        }
    }

    public void Dispose() => Disconnect();
}

// ── Streaming event types ────────────────────────────────────────────────────

/// <summary>
/// Broadcast event received when a note is reacted to, unreacted, deleted,
/// or a poll is voted on. Mirrors NoteUpdatedEvent in streaming.types.ts.
/// </summary>
public class NoteUpdatedEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>"reacted" | "unreacted" | "deleted" | "pollVoted"</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public JsonElement Body { get; set; }
}

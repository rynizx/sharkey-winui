using Microsoft.UI.Xaml.Media.Imaging;

namespace SharkeyWinUI.Services;

public sealed class ImageCacheService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, BitmapImage> _cache = new();
    private readonly LinkedList<string> _lru = new();
    private readonly int _capacity;

    public ImageCacheService(int capacity = 120)
    {
        _capacity = Math.Max(10, capacity);
    }

    public BitmapImage? GetBitmapImage(string? url, int? decodePixelWidth = null, int? decodePixelHeight = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var key = BuildKey(url, decodePixelWidth, decodePixelHeight);

        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                TouchKey(key);
                return cached;
            }

            var image = new BitmapImage(uri);
            if (decodePixelWidth.HasValue)
                image.DecodePixelWidth = decodePixelWidth.Value;
            if (decodePixelHeight.HasValue)
                image.DecodePixelHeight = decodePixelHeight.Value;

            _cache[key] = image;
            _lru.AddFirst(key);
            TrimIfNeeded();
            return image;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _cache.Clear();
            _lru.Clear();
        }
    }

    private static string BuildKey(string url, int? decodePixelWidth, int? decodePixelHeight)
        => $"{url}|w:{decodePixelWidth?.ToString() ?? "-"}|h:{decodePixelHeight?.ToString() ?? "-"}";

    private void TouchKey(string key)
    {
        var node = _lru.Find(key);
        if (node == null)
            return;

        _lru.Remove(node);
        _lru.AddFirst(node);
    }

    private void TrimIfNeeded()
    {
        while (_cache.Count > _capacity)
        {
            var keyToRemove = _lru.Last?.Value;
            if (keyToRemove == null)
                break;

            _lru.RemoveLast();
            _cache.Remove(keyToRemove);
        }
    }
}

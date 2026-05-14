using FOOD_MAP.Shared.Utilities;

namespace FOOD_MAP.Services;

public sealed class DeepLinkRoutingService
{
    private readonly object _syncRoot = new();
    private string? _pendingPayload;

    public event EventHandler<DeepLinkPayloadReceivedEventArgs>? DeepLinkPayloadReceived;

    public void RegisterIncomingUri(string incomingUri)
    {
        if (string.IsNullOrWhiteSpace(incomingUri))
        {
            return;
        }

        if (!Uri.TryCreate(incomingUri, UriKind.Absolute, out var deepLinkUri))
        {
            return;
        }

        var normalizedPayload = NormalizePayload(deepLinkUri);
        if (string.IsNullOrWhiteSpace(normalizedPayload))
        {
            return;
        }

        lock (_syncRoot)
        {
            _pendingPayload = normalizedPayload;
        }

        DeepLinkPayloadReceived?.Invoke(this, new DeepLinkPayloadReceivedEventArgs(normalizedPayload));
    }

    public string? TryDequeuePendingPayload()
    {
        lock (_syncRoot)
        {
            var pendingPayload = _pendingPayload;
            _pendingPayload = null;
            return pendingPayload;
        }
    }

    private static string? NormalizePayload(Uri deepLinkUri)
    {
        var parsedPoiId = QrPayloadParser.ExtractPoiIdFromPayload(deepLinkUri.ToString());
        if (!string.IsNullOrWhiteSpace(parsedPoiId))
        {
            return parsedPoiId;
        }

        var absolutePath = deepLinkUri.AbsolutePath;
        if (absolutePath.StartsWith("/poi-scanner", StringComparison.OrdinalIgnoreCase)
            || absolutePath.StartsWith("/poiscanner", StringComparison.OrdinalIgnoreCase)
            || absolutePath.StartsWith("/poi/", StringComparison.OrdinalIgnoreCase))
        {
            return deepLinkUri.ToString();
        }

        return null;
    }
}

public sealed class DeepLinkPayloadReceivedEventArgs : EventArgs
{
    public DeepLinkPayloadReceivedEventArgs(string payload)
    {
        Payload = payload;
    }

    public string Payload { get; }
}

using System.Text.RegularExpressions;

namespace FOOD_MAP.Shared.Utilities;

public static class QrPayloadParser
{
    private static readonly Regex PoiIdPattern = new("[A-Za-z]{2}-\\d{1,6}", RegexOptions.Compiled);

    public static string? ExtractPoiIdFromPayload(string? qrPayload)
    {
        if (string.IsNullOrWhiteSpace(qrPayload))
        {
            return null;
        }

        var normalizedPayload = qrPayload.Trim();

        if (Uri.TryCreate(normalizedPayload, UriKind.Absolute, out var absoluteUri))
        {
            var poiIdFromQuery = TryGetPoiIdFromQuery(absoluteUri.Query);
            if (!string.IsNullOrWhiteSpace(poiIdFromQuery))
            {
                normalizedPayload = poiIdFromQuery;
            }
            else
            {
                var lastSegment = absoluteUri.Segments.LastOrDefault();
                if (!string.IsNullOrWhiteSpace(lastSegment))
                {
                    normalizedPayload = Uri.UnescapeDataString(lastSegment).Trim('/');
                }
            }
        }

        var matchedPoiId = PoiIdPattern.Match(normalizedPayload);
        if (!matchedPoiId.Success)
        {
            return null;
        }

        return matchedPoiId.Value.ToUpperInvariant();
    }

    private static string? TryGetPoiIdFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var segments = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var keyValue = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length != 2)
            {
                continue;
            }

            if (!string.Equals(keyValue[0], "poiId", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(keyValue[0], "poi", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(keyValue[0], "id", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(keyValue[0], "qr", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(keyValue[1]);
        }

        return null;
    }
}

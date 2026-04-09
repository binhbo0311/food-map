namespace FOOD_MAP.Shared.Models;

public class MediaAsset
{
    public int Id { get; set; }

    public int PoiTranslationId { get; set; }

    public string AssetType { get; set; } = string.Empty;

    public string RemoteUrl { get; set; } = string.Empty;

    public string LocalPath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string? ContentHash { get; set; }

    public bool IsDownloaded { get; set; }

    public DateTimeOffset? LastDownloadedUtc { get; set; }

    // Lưu relation để truy xuất media theo bản dịch POI.
    public POITranslation? PoiTranslation { get; set; }
}
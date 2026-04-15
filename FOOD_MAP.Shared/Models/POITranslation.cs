using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Shared.Models;

// Đảm bảo mỗi POI chỉ có tối đa 1 bản dịch cho mỗi ngôn ngữ.
[Index(nameof(PoiId), nameof(LanguageId), IsUnique = true)]
public class POITranslation
{
    public int Id { get; set; }

    public string PoiId { get; set; } = string.Empty;

    public int LanguageId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public string AudioFileUrl { get; set; } = string.Empty;

    public string TtsScript { get; set; } = string.Empty;

    // Nội dung mô tả mở rộng (HTML/text) để hiển thị chi tiết POI trên app.
    public string RichContentHtml { get; set; } = string.Empty;

    public POI? Poi { get; set; }

    public Language? Language { get; set; }

    // Lưu danh sách file media đã cache cho bản dịch này.
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
}

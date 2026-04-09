namespace FOOD_MAP.Shared.Models;

public class Language
{
    public int Id { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    public string LanguageName { get; set; } = string.Empty;

    // Quan hệ 1-n: một ngôn ngữ có nhiều bản dịch điểm đến.
    public ICollection<POITranslation> PoiTranslations { get; set; } = new List<POITranslation>();
}

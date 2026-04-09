namespace FOOD_MAP.Shared.Models;

public class POI
{
    public int Id { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int ActivationRadius { get; set; }

    public int Priority { get; set; }

    public string? QRCodeId { get; set; }

    // Quan hệ 1-n: một POI có nhiều nội dung theo ngôn ngữ.
    public ICollection<POITranslation> PoiTranslations { get; set; } = new List<POITranslation>();
}

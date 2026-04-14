namespace FOOD_MAP.Shared.Models;

public class POI
{
    // ID dạng business key: FD-001, VS-002, ST-003.
    public string Id { get; set; } = string.Empty;

    public PoiType Type { get; set; } = PoiType.Visit;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int ActivationRadius { get; set; }

    public int Priority { get; set; }

    public PoiApprovalStatus ApprovalStatus { get; set; } = PoiApprovalStatus.Pending;

    public DateTimeOffset SubmittedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedUtc { get; set; }

    public int? OwnerId { get; set; }

    public int? ReviewedByAdminUserId { get; set; }

    public string? QRCodeId { get; set; }

    public User? Owner { get; set; }

    public User? ReviewedByAdminUser { get; set; }

    // Quan hệ 1-n: một POI có nhiều nội dung theo ngôn ngữ.
    public ICollection<POITranslation> PoiTranslations { get; set; } = new List<POITranslation>();

    public ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();
}

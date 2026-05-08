namespace FOOD_MAP.Shared.Models;

public class TourList
{
    public int Id { get; set; }

    public string TourCode { get; set; } = string.Empty;

    public string? TourName { get; set; }

    public int? OwnerUserId { get; set; }

    public bool IsPublic { get; set; }

    public string PoiId { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public POI? Poi { get; set; }

    public User? OwnerUser { get; set; }
}
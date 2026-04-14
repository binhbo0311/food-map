namespace FOOD_MAP.Shared.Models;

public class FoodItem
{
    public int Id { get; set; }

    public string PoiId { get; set; } = string.Empty;

    public int? OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "VND";

    public bool IsAvailable { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public POI? Poi { get; set; }

    public User? Owner { get; set; }
}
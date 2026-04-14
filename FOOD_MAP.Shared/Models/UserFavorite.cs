namespace FOOD_MAP.Shared.Models;

public class UserFavorite
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string PoiId { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public User? User { get; set; }

    public POI? Poi { get; set; }
}
